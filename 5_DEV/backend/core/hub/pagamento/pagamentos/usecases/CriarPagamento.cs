using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.UseCases;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.In;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// POST /pagamento/faturas/{id}/pagamentos (RF-184 a RF-188). Ordem: o corpo
/// (RNF-239: só o token do cartão), a fatura da conta de quem pede, o status
/// dela (RN-301), a forma ligada na Configuração (RF-193), um só pagamento em
/// aberto por fatura, ocupada numa só operação (RN-303), e a cobrança ao banco
/// com o traceId da execução (RNF-244). O valor é a fatura e os juros do dia,
/// fixo depois de gerado (RN-310). Sem resposta do banco em 15 s, nada fica
/// "AGUARDANDO" (RNF-241). Auditado como PAGAMENTO_CRIADO (RN-309).
/// </summary>
public sealed class CriarPagamento(
    IFaturas faturas, IPagamentos pagamentos, INumeracaoDoPagamento numeracao, IIntermediador intermediador, ICanalDoPagamento canal,
    IConfiguracoesDoPagamento configuracoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : ICriarPagamento
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <summary>A partir de quantos dias do vencimento da fatura o boleto vence com ela (RF-188).</summary>
    public const int DiasParaOBoletoVencerComAFatura = 3;

    /// <inheritdoc />
    public Task<PagamentoDto> Executar(string faturaId, PedidoDePagamento pedido, QuemPede quem, TraceId traceId, TraceId? traceIdOrigem,
        CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.PagamentoCriado, quem.ContaId, quem.UsuarioId, faturaId), async andamento =>
        {
            var (forma, token) = pedido.Validar();
            var fatura = await faturas.Obter(faturaId, ct);
            if (fatura is null || fatura.Conta.Id != quem.ContaId) throw FalhaDoPagamento.FaturaInexistente();
            if (fatura.Status == StatusDaFatura.Paga) throw FalhaDoPagamento.FaturaPaga();
            if (fatura.Status == StatusDaFatura.Cancelada) throw FalhaDoPagamento.FaturaCancelada();
            var configuracao = await ConfiguracaoAtual.Ler(configuracoes, ct);
            if (!configuracao.Aceita(forma)) throw FalhaDoPagamento.FormaIndisponivel();

            var agora = relogio.GetUtcNow();
            var hoje = Calendario.Dia(agora);
            var juros = await new TaxaDoVencimento(configuracoes).Juros(fatura, hoje, ct);
            var id = Numeros.De(Numeros.Pagamentos, await numeracao.Proximo(Numeros.Pagamentos, ct));
            await Ocupar(fatura.Id, id, ct);
            var pagamento = Pagamento.Novo(id, fatura, forma, juros?.Valor ?? Dinheiro.Zero, agora, quem.UsuarioId, traceId.Valor, traceIdOrigem?.Valor);
            var (expiraEm, vencimentoDoBoleto) = Prazos(forma, fatura, configuracao.Prazos, agora, hoje);

            ResultadoDaCobranca resultado;
            try
            {
                resultado = await intermediador.Cobrar(
                    new PedidoDeCobranca(id, fatura.Id, forma, pagamento.Valor, token, expiraEm, vencimentoDoBoleto, traceId.Valor), ct);
            }
            catch
            {
                await faturas.Liberar(fatura.Id, id, CancellationToken.None);
                throw;
            }
            if (resultado.Situacao is SituacaoDaCobranca.Indisponivel or SituacaoDaCobranca.TokenInvalido)
            {
                await faturas.Liberar(fatura.Id, id, ct);
                throw resultado.Situacao == SituacaoDaCobranca.Indisponivel
                    ? FalhaDoPagamento.BancoIndisponivel()
                    : FalhaDoPagamento.DadosInvalidos("cartao.token");
            }

            Nascer(pagamento, resultado, expiraEm, vencimentoDoBoleto, agora);
            await pagamentos.Incluir(pagamento, ct);
            if (pagamento.Status == StatusDoPagamento.Pago) await baixas.Quitar(fatura, pagamento, traceId.Valor, quem.UsuarioId, ct);
            if (!pagamento.EmAberto) await baixas.Liberar(pagamento, ct);
            andamento.Alvejar(id);
            await auditada.Concluir(andamento.Atual, ct);
            return PagamentoDto.De(pagamento);
        }, ct);

    /// <summary>
    /// Ocupa a fatura (RN-303). Se quem a ocupa já terminou (expirou,
    /// cancelou, pagou), a ocupação antiga é liberada e vale a nova.
    /// </summary>
    /// <exception cref="FalhaDoPagamento">PAGAMENTO_EM_ABERTO.</exception>
    async Task Ocupar(string faturaId, string pagamentoId, CancellationToken ct)
    {
        var outro = await faturas.Ocupar(faturaId, pagamentoId, ct);
        if (outro is null) return;
        var ocupante = await pagamentos.Obter(outro, ct);
        if (ocupante is null || ocupante.EmAberto) throw FalhaDoPagamento.PagamentoEmAberto();
        await faturas.Liberar(faturaId, outro, ct);
        if (await faturas.Ocupar(faturaId, pagamentoId, ct) is not null) throw FalhaDoPagamento.PagamentoEmAberto();
    }

    /// <summary>
    /// A validade (RN-302): PIX e débito em minutos da Configuração; o boleto
    /// vence com a fatura se ela está a 3 dias ou mais, senão em dias úteis, e
    /// expira depois dos dias úteis de compensação.
    /// </summary>
    static (DateTimeOffset? ExpiraEm, DateOnly? VencimentoDoBoleto) Prazos(FormaDePagamento forma, Fatura fatura, PrazosDoPagamento prazos,
        DateTimeOffset agora, DateOnly hoje)
    {
        switch (forma)
        {
            case FormaDePagamento.Pix:
                return (agora.AddMinutes(prazos.PixMinutos), null);
            case FormaDePagamento.CartaoDebito:
                return (agora.AddMinutes(prazos.DebitoMinutos), null);
            case FormaDePagamento.Boleto:
                var vencimento = fatura.Vencimento.DayNumber - hoje.DayNumber >= DiasParaOBoletoVencerComAFatura
                    ? fatura.Vencimento
                    : Calendario.SomarDiasUteis(hoje, prazos.BoletoDiasUteis);
                return (Calendario.InicioDoDia(Calendario.SomarDiasUteis(vencimento, prazos.BoletoDiasUteis).AddDays(1)), vencimento);
            default:
                return (null, null);
        }
    }

    /// <summary>O status que o banco deu à cobrança (RF-185 a RF-188).</summary>
    static void Nascer(Pagamento pagamento, ResultadoDaCobranca r, DateTimeOffset? expiraEm, DateOnly? vencimentoDoBoleto, DateTimeOffset agora)
    {
        var status = r.Situacao switch
        {
            SituacaoDaCobranca.Aprovada => StatusDoPagamento.Pago,
            SituacaoDaCobranca.Recusada => StatusDoPagamento.Recusado,
            SituacaoDaCobranca.EmAnalise => StatusDoPagamento.Processando,
            _ => StatusDoPagamento.Aguardando,
        };
        var boleto = vencimentoDoBoleto is { } v ? new DadosDoBoleto(v, r.LinhaDigitavel ?? "", r.CodigoBarras ?? "") : null;
        pagamento.Nascer(status, r.IdExterno, status == StatusDoPagamento.Aguardando ? expiraEm : null,
            status == StatusDoPagamento.Pago ? r.PagoEm ?? agora : null, r.Pix, boleto, r.ConfirmacaoUrl, r.Cartao,
            status == StatusDoPagamento.Recusado ? Pagamento.RecusadoPeloEmissor : null);
    }
}
