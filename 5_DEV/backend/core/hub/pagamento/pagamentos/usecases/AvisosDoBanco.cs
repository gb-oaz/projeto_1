using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.In;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// POST /pagamento/notificacoes (RN-305): só o aviso do banco (ou a consulta
/// do agendamento) confirma um pagamento; o pagador nunca confirma. A
/// assinatura errada responde 401 e a tentativa fica registrada; o aviso
/// repetido (mesmo id) e o de pagamento desconhecido respondem 200 sem efeito.
/// A execução tem traceId novo, ligado ao id externo em avisosDoBanco; o
/// traceId da criação, se o banco o devolve, fica como traceIdOrigem (RNF-244).
/// Auditado como AVISO_DO_BANCO, com quem = "banco" (RN-309).
/// </summary>
public sealed class ReceberAvisoDoBanco(
    IIntermediador intermediador, IAvisosDoBanco avisos, IPagamentos pagamentos, IEstornos estornos, IFaturas faturas, ICanalDoPagamento canal,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IReceberAvisoDoBanco
{
    /// <summary>Quem fez a mudança, no histórico e na auditoria.</summary>
    public const string Banco = "banco";

    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public Task Executar(AvisoRecebido recebido, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.AvisoDoBanco, null, Banco, null), async andamento =>
        {
            var agora = relogio.GetUtcNow();
            var leitura = await intermediador.LerAviso(recebido, ct);
            if (!leitura.AssinaturaValida)
            {
                await avisos.Registrar(new RegistroDoAviso($"recusado:{traceId.Valor}", null, null, agora, false, "RECUSADO", null, traceId.Valor,
                    null), ct);
                throw FalhaDoPagamento.AssinaturaInvalida();
            }
            if (leitura.Aviso is not { } aviso)
            {
                await auditada.Concluir(andamento.Atual, ct);
                return true;
            }

            var origem = TraceId.Ler(aviso.TraceIdOrigem)?.Valor;
            if (!await avisos.Registrar(new RegistroDoAviso(aviso.Id, aviso.IdExterno, Texto(aviso.Tipo), agora, true, "RECEBIDO", null, traceId.Valor,
                origem), ct))
            {
                await auditada.Concluir(andamento.Atual, ct);
                return true;
            }
            try
            {
                var (resultado, pagamentoId) = await Aplicar(aviso, traceId.Valor, ct);
                await avisos.Concluir(aviso.Id, resultado, pagamentoId, ct);
                if (pagamentoId is not null) andamento.Alvejar(pagamentoId);
            }
            catch
            {
                await avisos.Remover(aviso.Id, CancellationToken.None);
                throw;
            }
            await auditada.Concluir(andamento.Atual, ct);
            return true;
        }, ct);

    /// <summary>Aplica o aviso; devolve APLICADO, REPETIDO ou DESCONHECIDO e o pagamento ligado.</summary>
    async Task<(string Resultado, string? PagamentoId)> Aplicar(AvisoDoBanco aviso, string traceId, CancellationToken ct)
    {
        if (aviso.Tipo is TipoDoAviso.EstornoConcluido or TipoDoAviso.EstornoRecusado) return await AplicarEstorno(aviso, traceId, ct);

        var pagamento = await pagamentos.PorIdExterno(aviso.IdExterno, ct);
        if (pagamento is null) return ("DESCONHECIDO", null);
        var mudou = aviso.Tipo == TipoDoAviso.PagamentoConfirmado
            ? await baixas.Confirmar(pagamento, aviso.Em, traceId, Banco, ct)
            : await baixas.Encerrar(pagamento, p => p.Recusar(Pagamento.RecusadoPeloEmissor, aviso.Em, traceId, Banco), ct);
        return (mudou ? "APLICADO" : "REPETIDO", pagamento.Id);
    }

    /// <summary>
    /// O estorno concluído soma no devolvido do pagamento e da fatura (o do
    /// pagamento em duplicidade não muda a fatura); o recusado volta o
    /// pagamento ao status de antes (RF-192).
    /// </summary>
    async Task<(string Resultado, string? PagamentoId)> AplicarEstorno(AvisoDoBanco aviso, string traceId, CancellationToken ct)
    {
        var estorno = await estornos.PorIdExterno(aviso.IdExterno, ct);
        if (estorno is null) return ("DESCONHECIDO", null);
        var pagamento = await pagamentos.Obter(estorno.PagamentoId, ct);
        if (pagamento is null) return ("DESCONHECIDO", null);
        var concluido = aviso.Tipo == TipoDoAviso.EstornoConcluido;
        if (!(concluido ? estorno.Concluir(aviso.Em, traceId) : estorno.Recusar(aviso.Em, traceId))) return ("REPETIDO", pagamento.Id);
        if (!await estornos.Salvar(estorno, ct)) throw Baixas.Concorrencia(estorno.Id);
        await AplicarNoPagamento(estorno, pagamento, concluido, aviso.Em, traceId, ct);
        return ("APLICADO", pagamento.Id);
    }

    /// <summary>O estorno no pagamento e na fatura, ao vivo (RNF-238).</summary>
    public static async Task AplicarNoPagamento(Estorno estorno, Pagamento pagamento, bool concluido, DateTimeOffset em, string traceId,
        IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal, CancellationToken ct)
    {
        if (concluido) pagamento.ConcluirEstorno(estorno.Valor, em, traceId);
        else pagamento.DesfazerEstorno(em, traceId, Banco);
        if (!await pagamentos.Salvar(pagamento, ct)) throw Baixas.Concorrencia(pagamento.Id);
        if (concluido && !pagamento.Duplicidade && await faturas.Obter(pagamento.FaturaId, ct) is { } fatura && fatura.PagaPor == pagamento.Id)
        {
            fatura.Devolver(estorno.Valor);
            if (!await faturas.Salvar(fatura, ct)) throw Baixas.Concorrencia(fatura.Id);
        }
        await canal.Publicar(pagamento.Id, ct);
    }

    Task AplicarNoPagamento(Estorno estorno, Pagamento pagamento, bool concluido, DateTimeOffset em, string traceId, CancellationToken ct) =>
        AplicarNoPagamento(estorno, pagamento, concluido, em, traceId, pagamentos, faturas, canal, ct);

    static string Texto(TipoDoAviso tipo) => tipo switch
    {
        TipoDoAviso.PagamentoConfirmado => "PAGAMENTO_CONFIRMADO",
        TipoDoAviso.PagamentoRecusado => "PAGAMENTO_RECUSADO",
        TipoDoAviso.EstornoConcluido => "ESTORNO_CONCLUIDO",
        _ => "ESTORNO_RECUSADO",
    };
}

/// <summary>
/// Parte do agendamento (RN-302): cada pagamento em aberto que passou do prazo,
/// ou que está em análise, é consultado no banco antes de mudar. Pago no
/// banco: confirma; recusado: recusa; ainda à espera e vencido: expira, e a
/// fatura fica livre para um novo pagamento. Cada mudança leva o traceId da
/// execução do agendamento (RNF-244).
/// </summary>
public sealed class ExpirarPagamentos(IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal, IIntermediador intermediador,
    TimeProvider relogio) : IExpirarPagamentos
{
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public async Task Executar(TraceId traceId, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        foreach (var pagamento in await pagamentos.EmAberto(ct))
        {
            var vencido = pagamento.ExpiraEm is { } expira && expira <= agora;
            if (!vencido && pagamento.Status != StatusDoPagamento.Processando) continue;
            var consulta = pagamento.IdExterno is { } externo ? await intermediador.Consultar(externo, ct) : new ConsultaNoBanco(SituacaoNoBanco.Aguardando);
            switch (consulta.Situacao)
            {
                case SituacaoNoBanco.Paga:
                    await baixas.Confirmar(pagamento, consulta.Em ?? agora, traceId.Valor, ReceberAvisoDoBanco.Banco, ct);
                    break;
                case SituacaoNoBanco.Recusada:
                    await baixas.Encerrar(pagamento, p => p.Recusar(Pagamento.RecusadoPeloEmissor, consulta.Em ?? agora, traceId.Valor,
                        ReceberAvisoDoBanco.Banco), ct);
                    break;
                default:
                    if (vencido) await baixas.Encerrar(pagamento, p => p.Expirar(agora, traceId.Valor), ct);
                    break;
            }
        }
    }
}
