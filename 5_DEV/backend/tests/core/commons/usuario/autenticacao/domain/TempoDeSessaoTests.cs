using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Domain;

/// <summary>RN-226, RN-183, RN-243: de onde vem o tempo sem uso de cada pessoa.</summary>
public class TempoDeSessaoTests
{
    static readonly TempoDeSessao DaConta = new(
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3),
        TimeSpan.FromHours(90), TimeSpan.FromHours(91), TimeSpan.FromHours(92), TimeSpan.FromHours(93));

    static readonly TempoDeSessao DoTime = new(
        TimeSpan.FromHours(80), TimeSpan.FromHours(81), TimeSpan.FromHours(82),
        TimeSpan.FromHours(4), TimeSpan.FromHours(5), TimeSpan.FromHours(6), TimeSpan.FromHours(7));

    [Theory]
    [InlineData(TipoUsuario.Staff, Aparelho.Navegador, 1)]
    [InlineData(TipoUsuario.Staff, Aparelho.App, 2)]
    [InlineData(TipoUsuario.Participante, Aparelho.Navegador, 3)]
    [InlineData(TipoUsuario.Participante, Aparelho.App, 3)]
    [InlineData(TipoUsuario.MembroTime, Aparelho.Navegador, 4)]
    [InlineData(TipoUsuario.Administrador, Aparelho.App, 5)]
    [InlineData(TipoUsuario.Master, Aparelho.Navegador, 6)]
    [InlineData(TipoUsuario.Master, Aparelho.App, 7)]
    public void Staff_e_participante_pela_conta_os_demais_pelo_time(TipoUsuario tipo, Aparelho aparelho, int horas)
    {
        Assert.Equal(TimeSpan.FromHours(horas), TempoDeSessao.SemUso(tipo, aparelho, DaConta, DoTime));
    }

    [Fact]
    public void Padroes_do_requisito()
    {
        var p = TempoDeSessao.Padrao;
        Assert.Equal(TimeSpan.FromHours(8), p.StaffNavegador);
        Assert.Equal(TimeSpan.FromDays(7), p.StaffApp);
        Assert.Equal(TimeSpan.FromDays(30), p.Participante);
        Assert.All(new[] { p.MembrosNavegador, p.MembrosApp, p.MastersNavegador, p.MastersApp },
            t => Assert.Equal(TimeSpan.FromHours(8), t));
    }
}
