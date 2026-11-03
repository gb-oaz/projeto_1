namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>Um tempo de sessão com os limites do campo (RN-183, RN-243).</summary>
static class Limites
{
    /// <summary>O valor dentro dos limites, ou FORA_DOS_LIMITES com o campo.</summary>
    public static TimeSpan Conferir(string campo, TimeSpan valor, TimeSpan minimo, TimeSpan maximo, bool emHoras = false) =>
        valor >= minimo && valor <= maximo ? valor : throw FalhaDeUsuarios.ForaDosLimites(campo, minimo, maximo, emHoras);
}

/// <summary>
/// Tempo sem uso do staff e dos participantes, definido pela escola (RN-183):
/// staff no navegador de 15 min a 12 h (8 h), no app de 1 a 30 dias (7 dias),
/// participante de 1 a 90 dias (30 dias).
/// </summary>
/// <param name="StaffNavegador">Staff no navegador.</param>
/// <param name="StaffApp">Staff no app.</param>
/// <param name="Participante">Participante e responsáveis.</param>
public sealed record TempoDaConta(TimeSpan StaffNavegador, TimeSpan StaffApp, TimeSpan Participante)
{
    /// <summary>Os padrões.</summary>
    public static TempoDaConta Padrao { get; } = new(TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(30));

    /// <summary>Confere os limites de cada campo.</summary>
    /// <exception cref="FalhaDeUsuarios">FORA_DOS_LIMITES com o primeiro campo fora.</exception>
    public TempoDaConta Conferido() => new(
        Limites.Conferir("staffNavegador", StaffNavegador, TimeSpan.FromMinutes(15), TimeSpan.FromHours(12), emHoras: true),
        Limites.Conferir("staffApp", StaffApp, TimeSpan.FromDays(1), TimeSpan.FromDays(30)),
        Limites.Conferir("participante", Participante, TimeSpan.FromDays(1), TimeSpan.FromDays(90)));
}

/// <summary>
/// Tempo sem uso dos membros do time e das contas master, definido pelo time
/// deboaz (RN-243): membros de 15 min a 12 h, contas master e autônomos de 15
/// min a 24 h; padrão 8 h em todos.
/// </summary>
/// <param name="MembrosNavegador">Membros e administrador no navegador.</param>
/// <param name="MembrosApp">Membros e administrador no app.</param>
/// <param name="MastersNavegador">Contas master e autônomos no navegador.</param>
/// <param name="MastersApp">Contas master e autônomos no app.</param>
public sealed record TempoDoTime(TimeSpan MembrosNavegador, TimeSpan MembrosApp, TimeSpan MastersNavegador, TimeSpan MastersApp)
{
    /// <summary>Os padrões.</summary>
    public static TempoDoTime Padrao { get; } = new(TimeSpan.FromHours(8), TimeSpan.FromHours(8), TimeSpan.FromHours(8), TimeSpan.FromHours(8));

    /// <summary>Confere os limites de cada campo.</summary>
    /// <exception cref="FalhaDeUsuarios">FORA_DOS_LIMITES com o primeiro campo fora.</exception>
    public TempoDoTime Conferido()
    {
        var minimo = TimeSpan.FromMinutes(15);
        return new(
            Limites.Conferir("membrosNavegador", MembrosNavegador, minimo, TimeSpan.FromHours(12), emHoras: true),
            Limites.Conferir("membrosApp", MembrosApp, minimo, TimeSpan.FromHours(12), emHoras: true),
            Limites.Conferir("mastersNavegador", MastersNavegador, minimo, TimeSpan.FromHours(24), emHoras: true),
            Limites.Conferir("mastersApp", MastersApp, minimo, TimeSpan.FromHours(24), emHoras: true));
    }
}
