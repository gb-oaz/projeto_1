namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Tempos sem uso que encerram a sessão (RN-183, RN-243). A escola define os do
/// staff e dos participantes; o time deboaz, os dos membros e das contas master.
/// </summary>
/// <param name="StaffNavegador">Staff no navegador (padrão 8 h).</param>
/// <param name="StaffApp">Staff no app (padrão 7 dias).</param>
/// <param name="Participante">Participante e responsável, nos dois aparelhos (padrão 30 dias).</param>
/// <param name="MembrosNavegador">Membros do time e administrador no navegador (padrão 8 h).</param>
/// <param name="MembrosApp">Membros do time e administrador no app (padrão 8 h).</param>
/// <param name="MastersNavegador">Contas master e autônomos no navegador (padrão 8 h).</param>
/// <param name="MastersApp">Contas master e autônomos no app (padrão 8 h).</param>
public sealed record TempoDeSessao(
    TimeSpan StaffNavegador,
    TimeSpan StaffApp,
    TimeSpan Participante,
    TimeSpan MembrosNavegador,
    TimeSpan MembrosApp,
    TimeSpan MastersNavegador,
    TimeSpan MastersApp)
{
    /// <summary>Os padrões de RN-183 e RN-243, para conta sem tempo salvo.</summary>
    public static TempoDeSessao Padrao { get; } = new(
        TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(30),
        TimeSpan.FromHours(8), TimeSpan.FromHours(8),
        TimeSpan.FromHours(8), TimeSpan.FromHours(8));

    /// <summary>
    /// Tempo sem uso de uma pessoa (RN-226): staff e participante pelo tempo da
    /// conta; contas master, membros e administrador pelo tempo do time deboaz.
    /// </summary>
    /// <param name="tipo">Tipo do usuário.</param>
    /// <param name="aparelho">Aparelho da sessão.</param>
    /// <param name="daConta">Tempo da conta do usuário.</param>
    /// <param name="doTime">Tempo da conta do time deboaz.</param>
    public static TimeSpan SemUso(TipoUsuario tipo, Aparelho aparelho, TempoDeSessao daConta, TempoDeSessao doTime)
    {
        var navegador = aparelho == Aparelho.Navegador;
        return tipo switch
        {
            TipoUsuario.Staff => navegador ? daConta.StaffNavegador : daConta.StaffApp,
            TipoUsuario.Participante => daConta.Participante,
            TipoUsuario.Master => navegador ? doTime.MastersNavegador : doTime.MastersApp,
            _ => navegador ? doTime.MembrosNavegador : doTime.MembrosApp,
        };
    }
}
