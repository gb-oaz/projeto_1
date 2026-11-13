namespace Boaz.Core.Hub.Sac.Chamados.Domain;

/// <summary>
/// As horas úteis do prazo de primeira resposta (18 RNF-229): segunda a sexta,
/// das 08:00 às 18:00 de Brasília (UTC-3, sem horário de verão desde 2019),
/// sem os feriados nacionais (com a Sexta-feira Santa e o 20 de novembro); o
/// Carnaval e o Corpus Christi são úteis.
/// </summary>
public static class CalendarioUtil
{
    /// <summary>O fuso de Brasília.</summary>
    public static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    static readonly TimeOnly Abre = new(8, 0), Fecha = new(18, 0);

    // os feriados nacionais de data fixa (mês, dia)
    static readonly (int Mes, int Dia)[] Fixos =
    [
        (1, 1), (4, 21), (5, 1), (9, 7), (10, 12), (11, 2), (11, 15), (11, 20), (12, 25),
    ];

    /// <summary>A hora em que vencem as horas úteis contadas a partir do início, em UTC.</summary>
    /// <param name="inicio">Quando o prazo começa.</param>
    /// <param name="horas">Quantas horas úteis.</param>
    public static DateTimeOffset Somar(DateTimeOffset inicio, int horas)
    {
        var resta = TimeSpan.FromHours(horas);
        var agora = inicio.ToOffset(Brasilia).DateTime;
        while (true)
        {
            var dia = DateOnly.FromDateTime(agora);
            var abre = dia.ToDateTime(Abre);
            var fecha = dia.ToDateTime(Fecha);
            if (!DiaUtil(dia) || agora >= fecha)
            {
                agora = dia.AddDays(1).ToDateTime(Abre);
                continue;
            }
            if (agora < abre) agora = abre;
            var noDia = fecha - agora;
            if (resta <= noDia) return new DateTimeOffset(agora + resta, Brasilia).ToUniversalTime();
            resta -= noDia;
            agora = dia.AddDays(1).ToDateTime(Abre);
        }
    }

    /// <summary>Segunda a sexta, menos os feriados nacionais.</summary>
    public static bool DiaUtil(DateOnly dia) =>
        dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Feriado(dia);

    /// <summary>Feriado nacional: as datas fixas e a Sexta-feira Santa.</summary>
    public static bool Feriado(DateOnly dia) =>
        Fixos.Contains((dia.Month, dia.Day)) || dia == Pascoa(dia.Year).AddDays(-2);

    /// <summary>O domingo de Páscoa do ano (algoritmo de Meeus/Jones/Butcher).</summary>
    public static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mes = (h + l - (7 * m) + 114) / 31;
        var diaDoMes = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(ano, mes, diaDoMes);
    }
}
