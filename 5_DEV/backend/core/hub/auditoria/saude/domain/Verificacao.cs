namespace Boaz.Core.Hub.Auditoria.Saude.Domain;

/// <summary>
/// O que uma verificação mostrou de um serviço (RN-315, auditoria-documentos v02 verificacoesSaude): o estado, o
/// tempo, a falha e há quanto tempo o serviço está assim.
/// <list type="bullet">
/// <item>No ar até 1 s; Lento até 3 s ou na 1ª e na 2ª falha seguidas;</item>
/// <item>Fora do ar sem resposta em 3 s ou na 3ª falha seguida (o "desde" é o da 1ª falha);</item>
/// <item>agendamento: até 2 min da última rodada No ar, até 5 min Lento, depois Fora do ar; nunca rodou: Sem verificação.</item>
/// </list>
/// </summary>
/// <param name="Servico">O serviço.</param>
/// <param name="Quando">Quando a verificação terminou.</param>
/// <param name="Instancia">Quem verificou.</param>
/// <param name="Estado">O estado.</param>
/// <param name="TempoMs">O tempo da resposta, se houve.</param>
/// <param name="Falha">Por que falhou, se falhou.</param>
/// <param name="UltimaExecucao">A última rodada, nos agendamentos.</param>
/// <param name="FalhasSeguidas">Quantas verificações seguidas falharam.</param>
/// <param name="Desde">Desde quando o serviço está neste estado.</param>
public sealed record Verificacao(
    Servico Servico, DateTimeOffset Quando, string Instancia, EstadoDoServico Estado, int? TempoMs, FalhaDaVerificacao? Falha,
    DateTimeOffset? UltimaExecucao, int FalhasSeguidas, DateTimeOffset Desde)
{
    /// <summary>Até quanto um serviço responde No ar.</summary>
    public const int LimiteNoArMs = 1000;

    /// <summary>Até quanto um serviço responde Lento; depois, é sem resposta.</summary>
    public const int LimiteLentoMs = 3000;

    /// <summary>A 3ª falha seguida tira o serviço do ar.</summary>
    public const int FalhasParaForaDoAr = 3;

    /// <summary>Até quanto a última rodada de um agendamento ainda o deixa No ar.</summary>
    public static readonly TimeSpan AgendamentoNoAr = TimeSpan.FromMinutes(2);

    /// <summary>Até quanto a última rodada de um agendamento ainda o deixa Lento.</summary>
    public static readonly TimeSpan AgendamentoLento = TimeSpan.FromMinutes(5);

    /// <summary>A verificação seguinte, pelo que a sonda viu agora e pela anterior (null na primeira).</summary>
    public static Verificacao Seguinte(Verificacao? anterior, Servico servico, string instancia, ResultadoDaSonda sonda, DateTimeOffset agora)
    {
        var (estado, falha, falhas) = Avaliar(anterior, servico, sonda, agora);
        var continua = anterior is not null && anterior.Estado == estado;
        // a 3ª falha seguida mantém o "desde" da 1ª: o serviço está assim desde que começou a falhar
        var seguidas = anterior is { FalhasSeguidas: > 0 } && falhas > 0;
        var desde = anterior is not null && (continua || seguidas) ? anterior.Desde : agora;
        return new Verificacao(servico, agora, instancia, estado, sonda.TempoMs, falha, sonda.UltimaExecucao, falhas, desde);
    }

    static (EstadoDoServico Estado, FalhaDaVerificacao? Falha, int Falhas) Avaliar(
        Verificacao? anterior, Servico servico, ResultadoDaSonda sonda, DateTimeOffset agora)
    {
        if (sonda.Desligado) return (EstadoDoServico.Desligado, null, 0);
        if (servico.Agendamento)
        {
            if (sonda.UltimaExecucao is not { } ultima) return (EstadoDoServico.SemVerificacao, null, 0);
            // em segundos inteiros: 13:40:00 e uma verificação às 13:42:00,05 ainda são 2 min
            var idade = Math.Floor((agora - ultima).TotalSeconds);
            if (idade <= AgendamentoNoAr.TotalSeconds) return (EstadoDoServico.NoAr, null, 0);
            return idade <= AgendamentoLento.TotalSeconds
                ? (EstadoDoServico.Lento, null, 0)
                : (EstadoDoServico.ForaDoAr, FalhaDaVerificacao.SemResposta, 0);
        }
        var anteriores = anterior?.FalhasSeguidas ?? 0;
        if (sonda.Falha is { } f)
        {
            var seguidas = anteriores + 1;
            return (f == FalhaDaVerificacao.SemResposta || seguidas >= FalhasParaForaDoAr ? EstadoDoServico.ForaDoAr : EstadoDoServico.Lento, f, seguidas);
        }
        var tempo = sonda.TempoMs ?? 0;
        if (tempo <= LimiteNoArMs) return (EstadoDoServico.NoAr, null, 0);
        return tempo <= LimiteLentoMs
            ? (EstadoDoServico.Lento, null, 0)
            : (EstadoDoServico.ForaDoAr, FalhaDaVerificacao.SemResposta, anteriores + 1);
    }

    /// <summary>O serviço está Fora do ar.</summary>
    public bool ForaDoAr => Estado == EstadoDoServico.ForaDoAr;
}
