using System.Globalization;
using System.Text.RegularExpressions;

namespace Boaz.Core.Hub.Pagamento.Faturas.Domain;

/// <summary>
/// Um valor em reais, guardado em centavos para a soma não perder centavo. Sai
/// sempre com duas casas, como 3698.33 e 0.00 (formato-comum do contrato).
/// </summary>
/// <param name="Centavos">O valor em centavos.</param>
public readonly record struct Dinheiro(long Centavos) : IComparable<Dinheiro>
{
    /// <summary>R$ 0,00.</summary>
    public static readonly Dinheiro Zero = new(0);

    /// <summary>O valor em reais, com duas casas (escala 2 do decimal).</summary>
    public decimal Reais
    {
        get
        {
            var absoluto = (ulong)Math.Abs(Centavos);
            return new decimal(unchecked((int)(uint)(absoluto & 0xFFFFFFFF)), unchecked((int)(uint)(absoluto >> 32)), 0, Centavos < 0, 2);
        }
    }

    /// <summary>Do valor em reais, arredondado ao centavo (metade para cima).</summary>
    public static Dinheiro De(decimal reais) => new((long)Math.Round(reais * 100m, 0, MidpointRounding.AwayFromZero));

    /// <summary>O valor tem no máximo duas casas (RN-306: "10.005" é recusado).</summary>
    public static bool TemDuasCasas(decimal reais) => reais * 100m == Math.Truncate(reais * 100m);

    /// <summary>Soma.</summary>
    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => new(a.Centavos + b.Centavos);

    /// <summary>Diferença.</summary>
    public static Dinheiro operator -(Dinheiro a, Dinheiro b) => new(a.Centavos - b.Centavos);

    /// <summary>Menor.</summary>
    public static bool operator <(Dinheiro a, Dinheiro b) => a.Centavos < b.Centavos;

    /// <summary>Maior.</summary>
    public static bool operator >(Dinheiro a, Dinheiro b) => a.Centavos > b.Centavos;

    /// <summary>Menor ou igual.</summary>
    public static bool operator <=(Dinheiro a, Dinheiro b) => a.Centavos <= b.Centavos;

    /// <summary>Maior ou igual.</summary>
    public static bool operator >=(Dinheiro a, Dinheiro b) => a.Centavos >= b.Centavos;

    /// <summary>A soma de vários.</summary>
    public static Dinheiro Somar(IEnumerable<Dinheiro> valores) => new(valores.Sum(v => v.Centavos));

    /// <inheritdoc />
    public int CompareTo(Dinheiro other) => Centavos.CompareTo(other.Centavos);

    /// <summary>Como a tela mostra, sem o "R$": 2.698,33.</summary>
    public string EmTexto() =>
        Reais.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', ' ').Replace('.', ',').Replace(' ', '.');

    /// <inheritdoc />
    public override string ToString() => Reais.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// O mês de uso que a fatura cobra, como "2026-11" (RN-300). A fatura nasce à
/// 00:00 de Brasília do dia 1 do mês seguinte e vence no dia da Configuração
/// (sempre 10) desse mesmo mês.
/// </summary>
/// <param name="Ano">O ano.</param>
/// <param name="Mes">O mês, de 1 a 12.</param>
public readonly partial record struct Competencia(int Ano, int Mes) : IComparable<Competencia>
{
    /// <summary>"2026-11"; null se o texto não for uma competência.</summary>
    public static Competencia? Ler(string? texto) =>
        texto is not null && Formato().IsMatch(texto)
            ? new Competencia(int.Parse(texto[..4], CultureInfo.InvariantCulture), int.Parse(texto[5..], CultureInfo.InvariantCulture))
            : null;

    /// <summary>A competência do dia.</summary>
    public static Competencia Do(DateOnly dia) => new(dia.Year, dia.Month);

    /// <summary>O mês anterior.</summary>
    public Competencia Anterior => Mes == 1 ? new(Ano - 1, 12) : new(Ano, Mes - 1);

    /// <summary>O mês seguinte.</summary>
    public Competencia Proxima => Mes == 12 ? new(Ano + 1, 1) : new(Ano, Mes + 1);

    /// <summary>O dia 1 do mês.</summary>
    public DateOnly PrimeiroDia => new(Ano, Mes, 1);

    /// <summary>A emissão: 00:00 de Brasília do dia 1 do mês seguinte (RN-300).</summary>
    public DateTimeOffset EmitidaEm => Calendario.InicioDoDia(Proxima.PrimeiroDia);

    /// <summary>O vencimento: o dia dado do mês seguinte (RN-300).</summary>
    public DateOnly Vencimento(int dia) => new(Proxima.Ano, Proxima.Mes, dia);

    /// <inheritdoc />
    public int CompareTo(Competencia other) => (Ano * 12 + Mes).CompareTo(other.Ano * 12 + other.Mes);

    /// <summary>Anterior.</summary>
    public static bool operator <(Competencia a, Competencia b) => a.CompareTo(b) < 0;

    /// <summary>Posterior.</summary>
    public static bool operator >(Competencia a, Competencia b) => a.CompareTo(b) > 0;

    /// <summary>Anterior ou a mesma.</summary>
    public static bool operator <=(Competencia a, Competencia b) => a.CompareTo(b) <= 0;

    /// <summary>Posterior ou a mesma.</summary>
    public static bool operator >=(Competencia a, Competencia b) => a.CompareTo(b) >= 0;

    /// <inheritdoc />
    public override string ToString() => $"{Ano:D4}-{Mes:D2}";

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex Formato();
}

/// <summary>
/// O dia de Brasília (UTC-3, sem horário de verão desde 2019) e os dias úteis
/// (segunda a sexta) dos prazos do boleto e do selo do card.
/// </summary>
public static class Calendario
{
    static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    /// <summary>O dia de Brasília da hora dada.</summary>
    public static DateOnly Dia(DateTimeOffset quando) => DateOnly.FromDateTime(quando.ToOffset(Fuso).DateTime);

    /// <summary>A 00:00 de Brasília do dia, em UTC (03:00Z).</summary>
    public static DateTimeOffset InicioDoDia(DateOnly dia) => new(dia.ToDateTime(TimeOnly.MinValue), Fuso);

    /// <summary>Segunda a sexta.</summary>
    public static bool Util(DateOnly dia) => dia.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    /// <summary>O dia útil que fica a tantos dias úteis depois do dia dado.</summary>
    public static DateOnly SomarDiasUteis(DateOnly dia, int dias)
    {
        var atual = dia;
        for (var contados = 0; contados < dias;)
        {
            atual = atual.AddDays(1);
            if (Util(atual)) contados++;
        }
        return atual;
    }

    /// <summary>Quantos dias úteis há depois de "de" até "ate", contando "ate".</summary>
    public static int DiasUteisAte(DateOnly de, DateOnly ate)
    {
        var dias = 0;
        for (var dia = de.AddDays(1); dia <= ate; dia = dia.AddDays(1)) if (Util(dia)) dias++;
        return dias;
    }
}

/// <summary>
/// Uma mudança de status no histórico da fatura, do pagamento ou do estorno,
/// com o traceId da execução que a fez (RNF-244).
/// </summary>
/// <param name="De">O status anterior; null no nascimento.</param>
/// <param name="Para">O status novo.</param>
/// <param name="Em">Quando.</param>
/// <param name="TraceId">A execução que mudou.</param>
/// <param name="Por">Quem: o usuário, "banco" ou "sistema".</param>
public sealed record Mudanca(string? De, string Para, DateTimeOffset Em, string TraceId, string? Por);

/// <summary>Os números do contrato: FAT-000402, PAG-000773 e EST-000012 (contadores).</summary>
public static class Numeros
{
    /// <summary>A série das faturas.</summary>
    public const string Faturas = "faturas";

    /// <summary>A série dos pagamentos.</summary>
    public const string Pagamentos = "pagamentos";

    /// <summary>A série dos estornos.</summary>
    public const string Estornos = "estornos";

    /// <summary>O número de uma série, como FAT-000402.</summary>
    public static string De(string serie, long valor) => serie switch
    {
        Faturas => $"FAT-{valor:D6}",
        Pagamentos => $"PAG-{valor:D6}",
        _ => $"EST-{valor:D6}",
    };
}
