using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// O que os casos de uso do pagamento repetem: carregar pelo lado de quem pede
/// (o de outra conta é como o que não existe), e baixar o pagamento — o banco
/// confirmou, recusou ou o prazo passou —, quitando a fatura, liberando-a para
/// outro pagamento (RN-303) e avisando o ao vivo (RNF-238). Cada mudança leva
/// o traceId da execução que a fez (RNF-244).
/// </summary>
public sealed class Baixas(IPagamentos pagamentos, IFaturas faturas, ICanalDoPagamento canal)
{
    /// <summary>O pagamento da conta de quem paga (RN-298).</summary>
    /// <exception cref="FalhaDoPagamento">ACESSO_NEGADO ou PAGAMENTO_INEXISTENTE.</exception>
    public async Task<Pagamento> Carregar(string id, QuemPede quem, CancellationToken ct)
    {
        quem.SoQuemPaga();
        var pagamento = await pagamentos.Obter(id, ct);
        return pagamento is null || pagamento.ContaId != quem.ContaId ? throw FalhaDoPagamento.PagamentoInexistente() : pagamento;
    }

    /// <summary>
    /// O banco confirmou (RN-305): o pagamento fica "PAGO" e quita a fatura em
    /// aberto; se ela já estava paga ou cancelada, o pagamento fica em
    /// duplicidade e a fatura não muda (RN-303). false se nada mudou.
    /// </summary>
    public async Task<bool> Confirmar(Pagamento pagamento, DateTimeOffset pagoEm, string traceId, string por, CancellationToken ct)
    {
        var fatura = await faturas.Obter(pagamento.FaturaId, ct);
        var duplicidade = fatura is null || !fatura.EmAberto;
        if (!pagamento.Pagar(pagoEm, duplicidade, traceId, por)) return false;
        if (!await pagamentos.Salvar(pagamento, ct)) throw Concorrencia(pagamento.Id);
        if (fatura is not null && !duplicidade) await Quitar(fatura, pagamento, traceId, por, ct);
        await Liberar(pagamento, ct);
        return true;
    }

    /// <summary>
    /// A fatura quitada pelo pagamento confirmado. Se outro a mudou no meio
    /// (a virada para vencida), relê e quita de novo.
    /// </summary>
    public async Task Quitar(Fatura fatura, Pagamento pagamento, string traceId, string por, CancellationToken ct)
    {
        var atual = fatura;
        for (var tentativa = 0; tentativa < 3 && atual is not null; tentativa++)
        {
            if (!atual.Quitar(pagamento.Id, pagamento.Valor, pagamento.PagoEm!.Value, traceId, por)) return;
            if (await faturas.Salvar(atual, ct)) return;
            atual = await faturas.Obter(fatura.Id, ct);
        }
        throw Concorrencia(fatura.Id);
    }

    /// <summary>
    /// O pagamento em aberto termina sem pagar: a regra dada (recusar, expirar,
    /// cancelar) muda o status e a fatura fica livre para outro pagamento. false
    /// se a regra não mudou nada ou se outro mudou o pagamento antes.
    /// </summary>
    public async Task<bool> Encerrar(Pagamento pagamento, Func<Pagamento, bool> regra, CancellationToken ct)
    {
        if (!regra(pagamento)) return false;
        if (!await pagamentos.Salvar(pagamento, ct)) return false;
        await Liberar(pagamento, ct);
        return true;
    }

    /// <summary>A fatura livre para outro pagamento e o aviso ao ao vivo.</summary>
    public async Task Liberar(Pagamento pagamento, CancellationToken ct)
    {
        await faturas.Liberar(pagamento.FaturaId, pagamento.Id, ct);
        await canal.Publicar(pagamento.Id, ct);
    }

    /// <summary>Outro mudou o documento duas vezes durante a gravação: 500, e o banco reenvia o aviso.</summary>
    public static InvalidOperationException Concorrencia(string id) => new($"{id} mudou durante a gravação.");
}
