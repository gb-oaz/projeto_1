using System.Globalization;
using Boaz.Core.Hub.Pagamento.Card.Dtos;
using Boaz.Core.Hub.Pagamento.Card.Ports.In;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Card.UseCases;

/// <summary>
/// GET /pagamento/card (RN-307, card comum de 15 RN-276): calculado das
/// faturas na leitura, pelo perfil de quem pede (RN-298: o ADMINISTRADOR ou
/// quem tem MOD-01 com "U"; os demais, 403).
/// <list type="bullet">
/// <item>DEBOAZ: selo = faturas vencidas; o recebido no mês (pela data do
/// pagamento) e a variação contra o mês anterior; apoios e lista com a
/// competência anterior (a receber, vencido, inadimplência, RN-308); série =
/// recebido por mês.</item>
/// <item>INSTITUICAO e AUTONOMO: selo = vencidas e a pagar em até 5 dias
/// úteis; o vencido (se há) ou o a pagar, com a data; apoios = vencidas e
/// pago no ano; série = o valor da fatura de cada competência.</item>
/// </list>
/// </summary>
public sealed class LerCardDoPagamento(IFaturas faturas, TimeProvider relogio) : ILerCardDoPagamento
{
    /// <summary>O módulo do card.</summary>
    public const string Modulo = "MOD-01";

    /// <summary>Pontos de cada série.</summary>
    public const int Pontos = 12;

    /// <summary>Até quantos dias úteis a fatura a pagar entra no selo (RN-307).</summary>
    public const int DiasDoSelo = 5;

    /// <inheritdoc />
    public async Task<CardDoPagamentoDto> Executar(QuemPede quem, CancellationToken ct = default)
    {
        if (!quem.Recebedor && !quem.UsaPagamento) throw FalhaDoPagamento.AcessoNegado();
        var hoje = Calendario.Dia(relogio.GetUtcNow());
        var todas = await faturas.Listar(new FiltroDeFaturas(quem.Recebedor ? null : quem.ContaId, []), ct);
        var versao = todas.Sum(f => f.Versao + 1);
        var atualizadoEm = todas.Count == 0 ? relogio.GetUtcNow() : todas.Max(f => f.AtualizadaEm);
        return quem.Recebedor ? Deboaz(todas, hoje, versao, atualizadoEm) : DoPagador(quem, todas, hoje, versao, atualizadoEm);
    }

    static CardDoPagamentoDto Deboaz(IReadOnlyList<Fatura> todas, DateOnly hoje, long versao, DateTimeOffset atualizadoEm)
    {
        var pagas = todas.Where(f => f.Status == StatusDaFatura.Paga && f.PagaEm is not null).ToList();
        Dinheiro RecebidoEntre(DateOnly de, DateOnly ate) =>
            Dinheiro.Somar(pagas.Where(f => Calendario.Dia(f.PagaEm!.Value) is var d && d >= de && d <= ate).Select(f => f.ValorPago - f.Devolvido));

        var mes = Competencia.Do(hoje);
        var noMes = RecebidoEntre(mes.PrimeiroDia, mes.Proxima.PrimeiroDia.AddDays(-1));
        var mesAnterior = RecebidoEntre(mes.Anterior.PrimeiroDia, mes.PrimeiroDia.AddDays(-1));

        var daAnterior = todas.Where(f => f.Competencia == mes.Anterior && f.Status != StatusDaFatura.Cancelada).ToList();
        var faturado = Valor(daAnterior);
        var vencido = Valor(daAnterior.Where(f => f.Status == StatusDaFatura.Vencida));
        var aReceber = new LinhaDoCardDto("A receber", Valor(daAnterior.Where(f => f.Status == StatusDaFatura.APagar)).Reais);
        var vencidoLinha = new LinhaDoCardDto("Vencido", vencido.Reais);
        var inadimplencia = new LinhaDoCardDto("Inadimplência", faturado.Centavos == 0 ? 0m
            : Math.Round((decimal)vencido.Centavos * 100m / faturado.Centavos, 1, MidpointRounding.AwayFromZero));

        var series = new SeriesDoCardDto(
            Serie(i => RecebidoEntre(hoje.AddDays(-i), hoje.AddDays(-i))),
            Serie(i => RecebidoEntre(hoje.AddDays(-7 * i - 6), hoje.AddDays(-7 * i))),
            Serie(i => Mes(mes, i) is var m ? RecebidoEntre(m.PrimeiroDia, m.Proxima.PrimeiroDia.AddDays(-1)) : Dinheiro.Zero));
        return new CardDoPagamentoDto(Modulo, "DEBOAZ", versao, atualizadoEm, todas.Count == 0, todas.Count(f => f.Status == StatusDaFatura.Vencida),
            new ReduzidaDoCardDto(noMes.Reais, "recebido no mês"),
            new NormalDoCardDto(noMes.Reais, Variacao(noMes, mesAnterior), [aReceber, vencidoLinha, inadimplencia]),
            new ExpandidaDoCardDto([vencidoLinha, aReceber, inadimplencia]), series);
    }

    static CardDoPagamentoDto DoPagador(QuemPede quem, IReadOnlyList<Fatura> todas, DateOnly hoje, long versao, DateTimeOffset atualizadoEm)
    {
        var vencidas = todas.Where(f => f.Status == StatusDaFatura.Vencida).OrderBy(f => f.Vencimento).ToList();
        var aPagar = todas.Where(f => f.Status == StatusDaFatura.APagar).OrderBy(f => f.Vencimento).ToList();
        var pagas = todas.Where(f => f.Status == StatusDaFatura.Paga && f.PagaEm is not null).ToList();
        Dinheiro PagoEntre(DateOnly de, DateOnly ate) =>
            Dinheiro.Somar(pagas.Where(f => Calendario.Dia(f.PagaEm!.Value) is var d && d >= de && d <= ate).Select(f => f.ValorPago - f.Devolvido));

        // o vencido manda: com fatura vencida, o card mostra o vencido e a data do mais antigo
        var (valor, rotulo, texto, linha) = vencidas.Count > 0
            ? (Valor(vencidas), "vencido", $"Venceu em {DiaMes(vencidas[0].Vencimento)}", "Vencido")
            : aPagar.Count > 0 ? (Valor(aPagar), "a pagar", $"Vence em {DiaMes(aPagar[0].Vencimento)}", "A pagar")
            : (Dinheiro.Zero, "a pagar", "Nada a pagar", "A pagar");
        var ano = new DateOnly(hoje.Year, 1, 1);
        var vencidasLinha = new LinhaDoCardDto("Vencidas", vencidas.Count);
        var pagoNoAno = new LinhaDoCardDto("Pago no ano", PagoEntre(ano, hoje).Reais);

        var ultima = Competencia.Do(hoje).Anterior;
        var series = new SeriesDoCardDto(
            Serie(i => PagoEntre(hoje.AddDays(-i), hoje.AddDays(-i))),
            Serie(i => PagoEntre(hoje.AddDays(-7 * i - 6), hoje.AddDays(-7 * i))),
            Serie(i => Valor(todas.Where(f => f.Competencia == Mes(ultima, i) && f.Status != StatusDaFatura.Cancelada))));
        var selo = vencidas.Count + aPagar.Count(f => Calendario.DiasUteisAte(hoje, f.Vencimento) <= DiasDoSelo);
        return new CardDoPagamentoDto(Modulo, quem.Papel == "AUTONOMO" ? "AUTONOMO" : "INSTITUICAO", versao, atualizadoEm, todas.Count == 0, selo,
            new ReduzidaDoCardDto(valor.Reais, rotulo),
            new NormalDoCardDto(valor.Reais, texto, [vencidasLinha, pagoNoAno]),
            new ExpandidaDoCardDto([new LinhaDoCardDto(linha, valor.Reais), vencidasLinha, pagoNoAno]), series);
    }

    // os 12 pontos, do mais antigo (i = 11) ao atual (i = 0)
    static IReadOnlyList<decimal> Serie(Func<int, Dinheiro> ponto) => [.. Enumerable.Range(0, Pontos).Reverse().Select(i => ponto(i).Reais)];

    static Competencia Mes(Competencia atual, int voltar)
    {
        for (var i = 0; i < voltar; i++) atual = atual.Anterior;
        return atual;
    }

    static Dinheiro Valor(IEnumerable<Fatura> faturas) => Dinheiro.Somar(faturas.Select(f => f.Valor));

    static string DiaMes(DateOnly dia) => dia.ToString("dd/MM", CultureInfo.InvariantCulture);

    // "+3,1 % no mês": o recebido no mês contra o do mês anterior
    static string Variacao(Dinheiro atual, Dinheiro anterior)
    {
        if (anterior.Centavos == 0) return "sem o mês anterior";
        var pct = Math.Round((decimal)(atual.Centavos - anterior.Centavos) * 100m / anterior.Centavos, 1, MidpointRounding.AwayFromZero);
        return $"{(pct >= 0 ? "+" : "-")}{Math.Abs(pct).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} % no mês";
    }
}

/// <summary>
/// GET /pagamento/card/ao-vivo (RN-307, 15 RN-279): o card ao abrir e, a cada
/// intervalo, só se a versão mudou.
/// </summary>
public sealed class AcompanharCardDoPagamento(ILerCardDoPagamento ler) : IAcompanharCardDoPagamento
{
    static readonly int[] Intervalos = [5, 15, 30, 60, 300];

    /// <inheritdoc />
    public async Task<(TimeSpan Intervalo, CardDoPagamentoDto Card)> Abrir(string? intervalo, QuemPede quem, CancellationToken ct = default)
    {
        var segundos = intervalo is null ? 300
            : int.TryParse(intervalo, NumberStyles.None, CultureInfo.InvariantCulture, out var s) && Intervalos.Contains(s) ? s
            : throw FalhaDoPagamento.DadosInvalidos("intervalo");
        return (TimeSpan.FromSeconds(segundos), await ler.Executar(quem, ct));
    }

    /// <inheritdoc />
    public async Task<CardDoPagamentoDto?> Proximo(long versao, QuemPede quem, CancellationToken ct = default)
    {
        var card = await ler.Executar(quem, ct);
        return card.Versao == versao ? null : card;
    }
}
