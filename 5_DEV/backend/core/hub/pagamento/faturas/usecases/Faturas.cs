using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.In;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Faturas.UseCases;

/// <summary>
/// GET /pagamento/faturas (RF-182): as faturas da conta de quem pede (RN-298),
/// na situação pedida (A_PAGAR, que traz também as VENCIDA; PAGAS; TODAS), as
/// VENCIDA antes das A_PAGAR e a competência mais nova primeiro, 50 por
/// página, com o resumo do topo (o total a pagar hoje, com os juros).
/// </summary>
public sealed class ListarFaturas(IFaturas faturas, IPagamentos pagamentos, IConfiguracoesDoPagamento configuracoes, TimeProvider relogio)
    : IListarFaturas
{
    /// <summary>Itens por página.</summary>
    public const int PorPagina = 50;

    /// <inheritdoc />
    public async Task<ListaDeFaturasDto> Executar(string? situacao, int? pagina, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoQuemPaga();
        Func<StatusDaFatura, bool> naSituacao = situacao switch
        {
            null or "A_PAGAR" => s => s is StatusDaFatura.APagar or StatusDaFatura.Vencida,
            "PAGAS" => s => s == StatusDaFatura.Paga,
            "TODAS" => _ => true,
            _ => throw FalhaDoPagamento.DadosInvalidos("situacao"),
        };
        var numero = pagina ?? 1;
        if (numero < 1) throw FalhaDoPagamento.DadosInvalidos("pagina");

        var hoje = Calendario.Dia(relogio.GetUtcNow());
        var todas = await faturas.Listar(new FiltroDeFaturas(quem.ContaId, []), ct);
        var taxas = new TaxaDoVencimento(configuracoes);
        var aPagar = Dinheiro.Zero;
        foreach (var fatura in todas.Where(f => f.EmAberto)) aPagar += fatura.TotalAPagar(await taxas.Juros(fatura, hoje, ct));
        var resumo = new ResumoDasFaturasDto(aPagar.Reais, todas.Count(f => f.Status == StatusDaFatura.Vencida),
            todas.Where(f => f.Status == StatusDaFatura.APagar).Select(f => (DateOnly?)f.Vencimento).Min());

        var escolhidas = todas.Where(f => naSituacao(f.Status))
            .OrderBy(f => Ordem(f.Status)).ThenByDescending(f => f.Competencia).ThenByDescending(f => f.Id, StringComparer.Ordinal).ToList();
        var pagina1 = escolhidas.Skip((numero - 1) * PorPagina).Take(PorPagina).ToList();
        var abertos = (await pagamentos.DasFaturas(pagina1.Select(f => f.Id), ct)).Where(p => p.EmAberto).ToDictionary(p => p.FaturaId);
        return new ListaDeFaturasDto(
            [.. pagina1.Select(f => new ItemDeFaturaDto(f.Id, f.Competencia.ToString(), f.Valor.Reais, f.Vencimento, f.Status.Texto(),
                abertos.TryGetValue(f.Id, out var p) ? new PagamentoEmAbertoDto(p.Id, p.Forma.Texto(), p.Status.Texto()) : null))],
            escolhidas.Count, resumo);
    }

    // VENCIDA, A_PAGAR, PAGA e CANCELADA, nessa ordem
    static int Ordem(StatusDaFatura status) => status switch
    {
        StatusDaFatura.Vencida => 0,
        StatusDaFatura.APagar => 1,
        StatusDaFatura.Paga => 2,
        _ => 3,
    };
}

/// <summary>
/// GET /pagamento/faturas/{id} (RF-183): a fatura da conta de quem pede (a de
/// outra conta é como a que não existe, 404), os itens, os juros de hoje com a
/// taxa do dia do vencimento (RN-310) e as tentativas, a mais nova primeiro,
/// com o traceId de cada uma (RN-304, RNF-244).
/// </summary>
public sealed class LerFatura(IFaturas faturas, IPagamentos pagamentos, ICadastroDoPagamento cadastro, IConfiguracoesDoPagamento configuracoes,
    TimeProvider relogio) : ILerFatura
{
    /// <inheritdoc />
    public async Task<FaturaDto> Executar(string id, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoQuemPaga();
        var fatura = await faturas.Obter(id, ct);
        if (fatura is null || fatura.Conta.Id != quem.ContaId) throw FalhaDoPagamento.FaturaInexistente();
        return await Detalhe(fatura, pagamentos, cadastro, new TaxaDoVencimento(configuracoes), Calendario.Dia(relogio.GetUtcNow()), ct);
    }

    /// <summary>O detalhe da fatura, também para o cancelamento do recebedor (RF-191).</summary>
    public static async Task<FaturaDto> Detalhe(Fatura fatura, IPagamentos pagamentos, ICadastroDoPagamento cadastro, TaxaDoVencimento taxas,
        DateOnly hoje, CancellationToken ct)
    {
        var tentativas = await pagamentos.DasFaturas([fatura.Id], ct);
        var nomes = await cadastro.Nomes(tentativas.Where(p => p.PagoEm is not null).Select(p => p.CriadoPor).Distinct(), ct);
        return FaturaDto.De(fatura, await taxas.Juros(fatura, hoje, ct), tentativas, nomes);
    }
}

/// <summary>
/// GET /pagamento/avisos (RN-311, RN-313): os avisos de atraso abertos da conta
/// de quem pede, das faturas ainda vencidas, com os juros até hoje. Só no
/// módulo: sem e-mail, SMS nem WhatsApp, e não bloqueia a conta.
/// </summary>
public sealed class LerAvisos(IAvisosDeAtraso avisos, IFaturas faturas, IConfiguracoesDoPagamento configuracoes, TimeProvider relogio) : ILerAvisos
{
    /// <summary>O único tipo de aviso.</summary>
    public const string FaturaEmAtraso = "FATURA_EM_ATRASO";

    /// <inheritdoc />
    public async Task<ListaDeAvisosDto> Executar(QuemPede quem, CancellationToken ct = default)
    {
        quem.SoQuemPaga();
        var hoje = Calendario.Dia(relogio.GetUtcNow());
        var taxas = new TaxaDoVencimento(configuracoes);
        var itens = new List<AvisoDto>();
        foreach (var aviso in (await avisos.Abertos(quem.ContaId, ct)).OrderBy(a => a.Vencimento))
        {
            var fatura = await faturas.Obter(aviso.FaturaId, ct);
            if (fatura is null || fatura.Status != StatusDaFatura.Vencida || fatura.Conta.Id != quem.ContaId) continue;
            var juros = await taxas.Juros(fatura, hoje, ct);
            itens.Add(new AvisoDto(FaturaEmAtraso, fatura.Id, fatura.Vencimento, fatura.Valor.Reais, (juros?.Valor ?? Dinheiro.Zero).Reais,
                fatura.TotalAPagar(juros).Reais, aviso.Desde));
        }
        return new ListaDeAvisosDto(itens, itens.Count);
    }
}

/// <summary>
/// A conta tem fatura em aberto (A_PAGAR ou VENCIDA)? O 17 pergunta antes de
/// encerrar a conta (RN-173): com fatura em aberto, 409 FATURA_EM_ABERTO (RN-313).
/// </summary>
public sealed class ContaTemFaturaEmAberto(IFaturas faturas) : IContaTemFaturaEmAberto
{
    /// <inheritdoc />
    public async Task<bool> Executar(string contaId, CancellationToken ct = default) =>
        (await faturas.Listar(new FiltroDeFaturas(contaId, [StatusDaFatura.APagar, StatusDaFatura.Vencida]), ct)).Count > 0;
}
