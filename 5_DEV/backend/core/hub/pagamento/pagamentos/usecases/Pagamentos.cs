using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.In;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>GET /pagamento/pagamentos/{id}: o pagamento da conta de quem pede, com o status atual (RN-302, RN-303).</summary>
public sealed class LerPagamento(IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal) : ILerPagamento
{
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public async Task<PagamentoDto> Executar(string id, QuemPede quem, CancellationToken ct = default) =>
        PagamentoDto.De(await baixas.Carregar(id, quem, ct));
}

/// <summary>
/// POST /pagamento/pagamentos/{id}/cancelar (RF-184, RN-303): quem paga
/// desiste do pagamento em aberto para trocar de forma; o confirmado não se
/// cancela (409 PAGAMENTO_CONFIRMADO) e o já encerrado volta como está. A
/// cobrança é cancelada no banco quando ele aceita. Auditado como
/// PAGAMENTO_CANCELADO (RN-309).
/// </summary>
public sealed class CancelarPagamento(
    IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal, IIntermediador intermediador, IAuditoria auditoria,
    IAuditoriaDeErros erros, TimeProvider relogio) : ICancelarPagamento
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public Task<PagamentoDto> Executar(string id, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.PagamentoCancelado, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            var pagamento = await baixas.Carregar(id, quem, ct);
            var agora = relogio.GetUtcNow();
            if (await baixas.Encerrar(pagamento, p => p.Cancelar(agora, traceId.Valor, quem.UsuarioId), ct) && pagamento.IdExterno is { } externo)
            {
                await intermediador.Cancelar(externo, ct);
            }
            await auditada.Concluir(andamento.Atual, ct);
            return PagamentoDto.De(pagamento);
        }, ct);
}

/// <summary>
/// GET /pagamento/pagamentos/{id}/boleto (RF-188) e …/comprovante (RF-189): o
/// que vai em cada PDF. Só o boleto tem PDF de boleto (404
/// BOLETO_INEXISTENTE); só o confirmado pelo banco tem comprovante (409
/// PAGAMENTO_NAO_CONFIRMADO).
/// </summary>
public sealed class LerDocumentoDoPagamento(IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal) : ILerDocumentoDoPagamento
{
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public async Task<DocumentoDoPagamentoDto> Boleto(string id, QuemPede quem, CancellationToken ct = default)
    {
        var pagamento = await baixas.Carregar(id, quem, ct);
        if (pagamento.Boleto is not { } boleto) throw FalhaDoPagamento.BoletoInexistente();
        var fatura = await Fatura(pagamento, ct);
        return new DocumentoDoPagamentoDto(fatura.Id, pagamento.Id, fatura.Conta.Nome, fatura.Competencia.ToString(), pagamento.Forma.Texto(),
            pagamento.Valor.Reais, boleto.Vencimento, boleto.LinhaDigitavel, boleto.CodigoBarras, null, pagamento.IdExterno, null);
    }

    /// <inheritdoc />
    public async Task<DocumentoDoPagamentoDto> Comprovante(string id, QuemPede quem, CancellationToken ct = default)
    {
        var pagamento = await baixas.Carregar(id, quem, ct);
        if (!pagamento.Confirmado) throw FalhaDoPagamento.PagamentoNaoConfirmado();
        var fatura = await Fatura(pagamento, ct);
        return new DocumentoDoPagamentoDto(fatura.Id, pagamento.Id, fatura.Conta.Nome, fatura.Competencia.ToString(), pagamento.Forma.Texto(),
            pagamento.Valor.Reais, null, null, null, pagamento.PagoEm, pagamento.IdExterno,
            pagamento.Cartao is { } c ? $"{c.Bandeira} final {c.Final}" : null);
    }

    async Task<Fatura> Fatura(Pagamento pagamento, CancellationToken ct) =>
        await faturas.Obter(pagamento.FaturaId, ct) ?? throw FalhaDoPagamento.PagamentoInexistente();
}

/// <summary>
/// GET /pagamento/pagamentos/{id}/ao-vivo (RNF-238): sem evento ao abrir; um
/// evento "pagamento" a cada mudança de status, com o traceId da execução que
/// a fez (RNF-244), em até 5 s pelo aviso do canal (ou pela releitura a cada
/// espera, se o aviso se perder).
/// </summary>
public sealed class AcompanharPagamento(IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal) : IAcompanharPagamento
{
    readonly Baixas baixas = new(pagamentos, faturas, canal);

    /// <inheritdoc />
    public async Task<EventoDoPagamentoDto> Abrir(string id, QuemPede quem, CancellationToken ct = default) =>
        EventoDoPagamentoDto.De(await baixas.Carregar(id, quem, ct));

    /// <inheritdoc />
    public async Task<EventoDoPagamentoDto?> Proximo(EventoDoPagamentoDto atual, TimeSpan espera, CancellationToken ct = default)
    {
        await canal.Esperar(atual.Id, espera, ct);
        var pagamento = await pagamentos.Obter(atual.Id, ct);
        return pagamento is null || pagamento.Status.Texto() == atual.Status ? null : EventoDoPagamentoDto.De(pagamento);
    }
}
