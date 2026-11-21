using Boaz.Core.Hub.Auditoria.Card.Domain;

namespace Boaz.Core.Hub.Auditoria.Registros.Domain;

/// <summary>
/// O período de uma consulta (RN-321): sem "de" nem "ate", as últimas 24 h até agora; só "de", dele até agora; só
/// "ate", as 24 h antes dele. Passa de 31 dias nos registros e nas ocorrências, e de 7 dias nas execuções: PERIODO_LONGO;
/// o fim que não é depois do começo: PERIODO_INVERTIDO.
/// </summary>
/// <param name="De">O começo.</param>
/// <param name="Ate">O fim.</param>
public readonly record struct Periodo(DateTimeOffset De, DateTimeOffset Ate)
{
    /// <summary>O máximo dos registros e das ocorrências.</summary>
    public const int MaximoDeDias = 31;

    /// <summary>O máximo das execuções.</summary>
    public const int MaximoDeDiasDasExecucoes = 7;

    /// <summary>O período padrão, em horas.</summary>
    public const int PadraoEmHoras = 24;

    /// <summary>Monta o período do pedido, já com as datas lidas.</summary>
    public static Periodo Criar(DateTimeOffset? de, DateTimeOffset? ate, int maximoDeDias, DateTimeOffset agora)
    {
        var fim = ate ?? agora;
        var comeco = de ?? fim.AddHours(-PadraoEmHoras);
        if (fim <= comeco) throw FalhaDaAuditoria.PeriodoInvertido();
        if (fim - comeco > TimeSpan.FromDays(maximoDeDias)) throw FalhaDaAuditoria.PeriodoLongo(maximoDeDias);
        return new Periodo(comeco, fim);
    }

    /// <summary>A data ISO 8601 do pedido; DADOS_INVALIDOS no campo se não for uma data com fuso.</summary>
    public static DateTimeOffset? Ler(string? texto, string campo)
    {
        if (texto is null) return null;
        if (!System.Text.RegularExpressions.Regex.IsMatch(texto, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")
            || !DateTimeOffset.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var data))
        {
            throw FalhaDaAuditoria.DadosInvalidos(campo);
        }
        return data.ToUniversalTime();
    }
}
