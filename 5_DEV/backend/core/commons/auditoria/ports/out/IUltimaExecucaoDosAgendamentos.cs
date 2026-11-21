namespace Boaz.Core.Commons.Auditoria.Ports.Out;

/// <summary>
/// A última rodada de cada agendamento (auditoria:ultimaExecucao:&lt;agendamento&gt;, 20 RN-315): o AgendamentoDoSac
/// e o AgendamentoDoPagamento a gravam a cada rodada, e a Auditoria a lê para dizer se o agendamento está no ar.
/// </summary>
public interface IUltimaExecucaoDosAgendamentos
{
    /// <summary>O agendamento do SAC.</summary>
    public const string Sac = "sac";

    /// <summary>O agendamento do Pagamento.</summary>
    public const string Pagamento = "pagamento";

    /// <summary>Guarda a hora da rodada do agendamento.</summary>
    Task Gravar(string agendamento, DateTimeOffset quando, CancellationToken ct = default);

    /// <summary>A hora da última rodada; null se o agendamento ainda não rodou.</summary>
    Task<DateTimeOffset?> Ler(string agendamento, CancellationToken ct = default);
}
