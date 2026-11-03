using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Domain;

/// <summary>RNF-204: cinco erros seguidos bloqueiam, e cada bloqueio seguido dobra o tempo.</summary>
public class TentativasDeEntradaTests
{
    static readonly DateTimeOffset Nove = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Quatro_erros_nao_bloqueiam_e_o_quinto_bloqueia_por_15_min()
    {
        var t = new TentativasDeEntrada(AlvoTentativa.Entrada, "maria.ebfga@deboaz.com");
        for (var i = 0; i < 4; i++) Assert.False(t.RegistrarErro(Nove));
        Assert.False(t.EstaBloqueada(Nove));

        Assert.True(t.RegistrarErro(Nove));
        Assert.Equal(Nove.AddMinutes(15), t.BloqueadoAte);
        Assert.True(t.EstaBloqueada(Nove.AddMinutes(14)));
        Assert.False(t.EstaBloqueada(Nove.AddMinutes(15)));
        Assert.Equal(0, t.ErrosSeguidos);
        Assert.Equal(1, t.BloqueiosSeguidos);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(6, 960)]
    [InlineData(7, 1440)]
    [InlineData(40, 1440)]
    public void O_bloqueio_dobra_ate_24_h(int bloqueios, int minutos)
    {
        var t = new TentativasDeEntrada(AlvoTentativa.CodigoAcesso, "EBFGA-LUCAS-0412", 4, bloqueios);
        Assert.True(t.RegistrarErro(Nove));
        Assert.Equal(Nove.AddMinutes(minutos), t.BloqueadoAte);
    }

    [Fact]
    public void Guarda_24_h_depois_do_ultimo_erro()
    {
        var t = new TentativasDeEntrada(AlvoTentativa.Entrada, "x");
        Assert.Null(t.ExpiraEm);
        t.RegistrarErro(Nove);
        Assert.Equal(Nove.AddHours(24), t.ExpiraEm);
    }

    [Fact]
    public void Remonta_o_que_foi_gravado()
    {
        var t = new TentativasDeEntrada(AlvoTentativa.CodigoLiberacao, "482913", 3, 2, Nove, Nove);
        Assert.Equal((AlvoTentativa.CodigoLiberacao, "482913", 3, 2), (t.Alvo, t.Chave, t.ErrosSeguidos, t.BloqueiosSeguidos));
        Assert.Equal(Nove, t.UltimoErro);
    }
}
