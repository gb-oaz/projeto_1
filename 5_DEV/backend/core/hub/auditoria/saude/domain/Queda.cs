namespace Boaz.Core.Hub.Auditoria.Saude.Domain;

/// <summary>
/// Uma queda de um serviço (RF-195, quedasServico): aberta ao ficar Fora do ar, com o começo da 1ª falha, e
/// fechada na volta. Fica guardada 31 dias (RNF-248).
/// </summary>
/// <param name="Id">O identificador (GUID, 32 hexadecimais).</param>
/// <param name="Servico">O serviço.</param>
/// <param name="Inicio">Quando começou.</param>
/// <param name="Fim">Quando voltou; null com a queda aberta.</param>
/// <param name="Falha">Por que caiu.</param>
public sealed record Queda(string Id, Servico Servico, DateTimeOffset Inicio, DateTimeOffset? Fim, FalhaDaVerificacao Falha)
{
    /// <summary>Quantos dias a queda fica guardada.</summary>
    public const int DiasDeGuarda = 31;

    /// <summary>Até quando fica guardada.</summary>
    public DateTimeOffset GuardarAte => Inicio.AddDays(DiasDeGuarda);

    /// <summary>Quanto durou, em minutos inteiros (com 1 s de folga para a hora da verificação); a queda aberta conta até agora.</summary>
    public int DuracaoMin(DateTimeOffset agora) => (int)Math.Floor(((Fim ?? agora) - Inicio).TotalMinutes + (1.0 / 60));

    /// <summary>Quanto desta queda cai dentro do período.</summary>
    public TimeSpan DentroDe(DateTimeOffset de, DateTimeOffset ate)
    {
        var comeco = Inicio > de ? Inicio : de;
        var fim = Fim is { } f && f < ate ? f : ate;
        return fim > comeco ? fim - comeco : TimeSpan.Zero;
    }

    /// <summary>
    /// A disponibilidade do período, em %, com 2 casas: 1 menos o tempo em queda sobre o período. O período
    /// sem tempo é 100.
    /// </summary>
    public static decimal Disponibilidade(IEnumerable<Queda> quedas, DateTimeOffset de, DateTimeOffset ate)
    {
        var periodo = ate - de;
        if (periodo <= TimeSpan.Zero) return 100m;
        var emQueda = quedas.Aggregate(TimeSpan.Zero, (soma, q) => soma + q.DentroDe(de, ate));
        return Math.Round(100m * (1m - (decimal)(emQueda.TotalSeconds / periodo.TotalSeconds)), 2, MidpointRounding.AwayFromZero);
    }
}
