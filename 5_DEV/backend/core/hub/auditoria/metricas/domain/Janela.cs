using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Hub.Auditoria.Metricas.Domain;

/// <summary>A janela das métricas (RF-197): 1 h por minuto, 24 h por hora, 7 e 30 dias por dia de Brasília.</summary>
public enum Janela
{
    /// <summary>1h: 60 minutos, de medicoesMinuto.</summary>
    H1,
    /// <summary>24h: 24 horas, de medicoesHora.</summary>
    H24,
    /// <summary>7d: 7 dias, de medicoesHora.</summary>
    D7,
    /// <summary>30d: 30 dias, de medicoesHora.</summary>
    D30,
}

/// <summary>Os textos e os pontos de cada janela.</summary>
public static class Janelas
{
    /// <summary>A janela pelo texto (1h, 24h, 7d ou 30d); null se não for nenhuma.</summary>
    public static Janela? De(string? texto) => texto switch
    {
        "1h" => Janela.H1,
        "24h" => Janela.H24,
        "7d" => Janela.D7,
        "30d" => Janela.D30,
        _ => null,
    };

    /// <summary>1h, 24h, 7d ou 30d.</summary>
    public static string Texto(this Janela janela) => janela switch
    {
        Janela.H1 => "1h",
        Janela.H24 => "24h",
        Janela.D7 => "7d",
        _ => "30d",
    };

    /// <summary>Quantos pontos tem a série: 60, 24, 7 ou 30.</summary>
    public static int Pontos(this Janela janela) => janela switch
    {
        Janela.H1 => 60,
        Janela.H24 => 24,
        Janela.D7 => 7,
        _ => 30,
    };

    /// <summary>A série sai de medicoesHora (24h, 7d e 30d), não de medicoesMinuto.</summary>
    public static bool PorHora(this Janela janela) => janela != Janela.H1;

    /// <summary>O passo de cada ponto: 1 min, 1 h, 1 dia, 1 dia.</summary>
    public static TimeSpan Passo(this Janela janela) => janela switch
    {
        Janela.H1 => TimeSpan.FromMinutes(1),
        Janela.H24 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromDays(1),
    };

    /// <summary>
    /// O começo do 1º ponto e o fim da janela: o fim é o minuto de agora; o começo, 60 minutos antes (1h), 23 horas
    /// antes da hora atual (24h) ou 6 e 29 dias antes do começo do dia de Brasília (7d e 30d).
    /// </summary>
    public static (DateTimeOffset De, DateTimeOffset Ate) Limites(this Janela janela, DateTimeOffset agora)
    {
        var ate = Minuto(agora);
        return janela switch
        {
            Janela.H1 => (ate.AddMinutes(-60), ate),
            Janela.H24 => (new DateTimeOffset(agora.Year, agora.Month, agora.Day, agora.Hour, 0, 0, TimeSpan.Zero).AddHours(-23), ate),
            _ => (InicioDoDia(CalendarioDoCard.Dia(agora).AddDays(-(janela.Pontos() - 1))), ate),
        };
    }

    /// <summary>O minuto cheio da hora dada, em UTC.</summary>
    public static DateTimeOffset Minuto(DateTimeOffset hora)
    {
        var utc = hora.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }

    /// <summary>A hora cheia da hora dada, em UTC.</summary>
    public static DateTimeOffset Hora(DateTimeOffset hora)
    {
        var utc = hora.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    /// <summary>As 00:00 de Brasília do dia, em UTC.</summary>
    public static DateTimeOffset InicioDoDia(DateOnly dia)
    {
        var meiaNoite = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(meiaNoite, CalendarioDoCard.Fuso.GetUtcOffset(meiaNoite)).ToUniversalTime();
    }
}
