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
/// do agendamento) confirma um pagamento; o pagador nunca confirma. O dono do
/// aviso sai do cabeçalho de assinatura e o pagamento é achado pelo id externo
/// e pelo intermediador que o gerou (RNF-242). Sem cabeçalho conhecido ou com a
/// assinatura errada, responde 401 e a tentativa fica registrada; o aviso
/// repetido (mesmo id) e o de pagamento desconhecido respondem 200 sem efeito.
/// A execução tem traceId novo, ligado ao id externo em avisosDoBanco; o
/// traceId da criação, se o banco o devolve, fica como traceIdOrigem (RNF-244).
/// Auditado como AVISO_DO_BANCO, com quem = "banco" (RN-309).
/// </summary>
public sealed class ReceberAvisoDoBanco(
    IIntermediadores intermediadores, IAvisosDoBanco avisos, IPagamentos pagamentos, IEstornos estornos, IFaturas faturas, ICanalDoPagamento canal,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IReceberAvisoDoBanco
{
    /// <summary>Quem fez a mudança, no histórico e na auditoria.</summary>
    public const string Banco = "banco";

    /// <summary>O resultado do aviso sem dono ou com a assinatura errada (RN-305-api).</summary>
    public const string Recusado = "RECUSADO";

    /// <summary>O resultado do aviso que já chegou antes, ou que não muda mais nada (RN-305-api).</summary>
    public const string Repetido = "REPETIDO";

    /// <summary>O resultado do aviso de pagamento que o intermediador não gerou aqui (RN-305-api).</summary>
    public const string Desconhecido = "DESCONHECIDO";

    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public Task Executar(AvisoRecebido recebido, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.AvisoDoBanco, null, Banco, null), async andamento =>
        {
            var agora = relogio.GetUtcNow();
            var dono = intermediadores.DoAviso(recebido);
            var conferencia = dono?.Conferir(recebido) ?? new ConferenciaDoAviso(false, null);
            if (dono is null || !conferencia.AssinaturaValida)
            {
                await avisos.Registrar(new RegistroDoAviso($"recusado:{traceId.Valor}", dono?.Nome, null, null, agora, false, Recusado, null,
                    traceId.Valor, null), ct);
                throw FalhaDoPagamento.AssinaturaInvalida();
            }
            // o pagamento que o intermediador não gerou aqui: sem efeito, e sem consultar o serviço dele
            if (conferencia.IdDoPagamento is { } idDoPagamento && await pagamentos.PorIdExterno(dono.Nome, idDoPagamento, ct) is null)
            {
                await avisos.Registrar(new RegistroDoAviso($"desconhecido:{traceId.Valor}", dono.Nome, idDoPagamento, null, agora, true, Desconhecido,
                    null, traceId.Valor, null), ct);
                await auditada.Concluir(andamento.Atual, ct);
                return true;
            }
            var leitura = await dono.LerAviso(recebido, ct);
            if (!leitura.AssinaturaValida)
            {
                await avisos.Registrar(new RegistroDoAviso($"recusado:{traceId.Valor}", dono.Nome, null, null, agora, false, Recusado, null,
                    traceId.Valor, null), ct);
                throw FalhaDoPagamento.AssinaturaInvalida();
            }
            if (leitura.Aviso is not { } aviso)
            {
                await auditada.Concluir(andamento.Atual, ct);
                return true;
            }

            var origem = TraceId.Ler(aviso.TraceIdOrigem)?.Valor;
            if (!await avisos.Registrar(new RegistroDoAviso(aviso.Id, dono.Nome, aviso.IdExterno, Texto(aviso.Tipo), agora, true, "RECEBIDO", null,
                traceId.Valor, origem), ct))
            {
                // o mesmo aviso de novo: sem efeito, e a chegada fica registrada com o traceId dela (RN-305-api)
                var doAviso = aviso.Tipo is TipoDoAviso.PagamentoConfirmado or TipoDoAviso.PagamentoRecusado
                    ? (await pagamentos.PorIdExterno(dono.Nome, aviso.IdExterno, ct))?.Id
                    : null;
                await avisos.Registrar(new RegistroDoAviso($"{Repetido.ToLowerInvariant()}:{traceId.Valor}", dono.Nome, aviso.IdExterno,
                    Texto(aviso.Tipo), agora, true, Repetido, doAviso, traceId.Valor, origem), ct);
                await auditada.Concluir(andamento.Atual, ct);
                return true;
            }
            try
            {
                var (resultado, pagamentoId) = await Aplicar(dono.Nome, aviso, traceId.Valor, ct);
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

    /// <summary>Aplica o aviso do intermediador; devolve APLICADO, REPETIDO ou DESCONHECIDO e o pagamento ligado.</summary>
    async Task<(string Resultado, string? PagamentoId)> Aplicar(string intermediador, AvisoDoBanco aviso, string traceId, CancellationToken ct)
    {
        if (aviso.Tipo is TipoDoAviso.EstornoConcluido or TipoDoAviso.EstornoRecusado) return await AplicarEstorno(intermediador, aviso, traceId, ct);

        var pagamento = await pagamentos.PorIdExterno(intermediador, aviso.IdExterno, ct);
        if (pagamento is null) return (Desconhecido, null);
        var mudou = aviso.Tipo == TipoDoAviso.PagamentoConfirmado
            ? await baixas.Confirmar(pagamento, aviso.Em, traceId, Banco, ct)
            : await baixas.Encerrar(pagamento, p => p.Recusar(Pagamento.RecusadoPeloEmissor, aviso.Em, traceId, Banco), ct);
        return (mudou ? "APLICADO" : Repetido, pagamento.Id);
    }

    /// <summary>
    /// O estorno concluído soma no devolvido do pagamento e da fatura (o do
    /// pagamento em duplicidade não muda a fatura); o recusado volta o
    /// pagamento ao status de antes (RF-192).
    /// </summary>
    async Task<(string Resultado, string? PagamentoId)> AplicarEstorno(string intermediador, AvisoDoBanco aviso, string traceId, CancellationToken ct)
    {
        var estorno = await estornos.PorIdExterno(intermediador, aviso.IdExterno, ct);
        if (estorno is null) return (Desconhecido, null);
        var pagamento = await pagamentos.Obter(estorno.PagamentoId, ct);
        if (pagamento is null) return (Desconhecido, null);
        var concluido = aviso.Tipo == TipoDoAviso.EstornoConcluido;
        if (!(concluido ? estorno.Concluir(aviso.Em, traceId) : estorno.Recusar(aviso.Em, traceId))) return (Repetido, pagamento.Id);
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
/// fatura fica livre para um novo pagamento. A consulta vai ao intermediador
/// que gerou o pagamento (RNF-242). Cada mudança leva o traceId da
/// execução do agendamento (RNF-244).
/// </summary>
public sealed class ExpirarPagamentos(IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal, IIntermediadores intermediadores,
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
            var consulta = pagamento.IdExterno is { } externo
                ? await intermediadores.Do(pagamento.Intermediador).Consultar(externo, ct)
                : new ConsultaNoBanco(SituacaoNoBanco.Aguardando);
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
