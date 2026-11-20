using Boaz.Core.Hub.Pagamento.Faturas.Domain;

namespace Boaz.Core.Hub.Pagamento.Configuracao.Domain;

/// <summary>Os prazos de cada forma (RN-302, RF-193).</summary>
/// <param name="PixMinutos">Validade do código PIX, de 5 a 1.440 min.</param>
/// <param name="DebitoMinutos">Tempo para confirmar o débito, de 5 a 30 min.</param>
/// <param name="BoletoDiasUteis">Dias úteis de compensação do boleto, de 1 a 30.</param>
public sealed record PrazosDoPagamento(int PixMinutos, int DebitoMinutos, int BoletoDiasUteis);

/// <summary>
/// Uma versão da Configuração do Pagamento (RF-193): as formas aceitas, os
/// juros ao dia, a tolerância do aviso de atraso, o dia do vencimento e os
/// prazos. Cada PUT grava uma versão nova, nunca apagada; a taxa que vale para
/// uma fatura é a vigente no dia do vencimento (RN-310).
/// </summary>
/// <param name="Versao">1, 2, 3…: o If-Match do PUT.</param>
/// <param name="VigenteDesde">Desde quando vale.</param>
/// <param name="Formas">Forma → aceita.</param>
/// <param name="JurosAoDia">Percentual ao dia, de 0 a 1.</param>
/// <param name="ToleranciaAvisoDias">Dias de atraso antes do aviso, de 0 a 30 (RN-311).</param>
/// <param name="DiaVencimento">Sempre 10 (RN-300).</param>
/// <param name="Prazos">Os prazos de cada forma.</param>
/// <param name="SalvaPor">Quem salvou; null na padrão.</param>
public sealed record ConfiguracaoDoPagamento(
    int Versao, DateTimeOffset VigenteDesde, IReadOnlyDictionary<FormaDePagamento, bool> Formas, decimal JurosAoDia,
    int ToleranciaAvisoDias, int DiaVencimento, PrazosDoPagamento Prazos, string? SalvaPor)
{
    /// <summary>O único dia de vencimento aceito (RN-300).</summary>
    public const int Dia = 10;

    /// <summary>
    /// A configuração de antes de qualquer PUT: as quatro formas, juros 0,
    /// tolerância 0, dia 10, PIX 30 min, débito 10 min e boleto 3 dias úteis.
    /// Não é gravada: é a versão 1.
    /// </summary>
    public static readonly ConfiguracaoDoPagamento Padrao = new(1, DateTimeOffset.MinValue,
        TextosDoPagamento.Formas.ToDictionary(f => f, _ => true), 0m, 0, Dia, new PrazosDoPagamento(30, 10, 3), null);

    /// <summary>A forma está ligada.</summary>
    public bool Aceita(FormaDePagamento forma) => Formas.GetValueOrDefault(forma);

    /// <summary>As formas ligadas, na ordem da tela (RF-184).</summary>
    public IReadOnlyList<FormaDePagamento> Aceitas => [.. TextosDoPagamento.Formas.Where(Aceita)];
}
