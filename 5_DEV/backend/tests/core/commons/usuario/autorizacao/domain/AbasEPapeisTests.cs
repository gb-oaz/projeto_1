using Boaz.Core.Commons.Usuario.Autorizacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Domain;

/// <summary>RN-251 (abas) e RN-232 (papéis).</summary>
public class AbasEPapeisTests
{
    [Theory]
    [InlineData("C", true, false)]
    [InlineData("U", false, true)]
    [InlineData("CU", true, true)]
    public void Le_e_escreve_as_abas(string texto, bool configuracao, bool utilizacao)
    {
        var abas = Abas.De(texto)!.Value;
        Assert.Equal((configuracao, utilizacao, texto), (abas.Configuracao, abas.Utilizacao, abas.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UC")]
    [InlineData("u")]
    [InlineData("X")]
    public void Abas_fora_do_contrato_sao_null(string? texto) => Assert.Null(Abas.De(texto));

    [Fact]
    public void Contem_e_junta()
    {
        Assert.True(Abas.CU.Contem(Abas.U));
        Assert.True(Abas.U.Contem(Abas.U));
        Assert.False(Abas.U.Contem(Abas.CU));
        Assert.False(Abas.C.Contem(Abas.U));
        Assert.Equal(Abas.CU, Abas.C.Com(Abas.U));
        Assert.Equal(Abas.U, Abas.U.Com(Abas.U));
    }

    [Fact]
    public void Papeis_como_no_token()
    {
        Assert.Equal(["ADMINISTRADOR", "MEMBRO_TIME", "MASTER", "AUTONOMO", "STAFF", "PARTICIPANTE", "ANONIMO"],
            Enum.GetValues<Papel>().Select(p => p.Texto()));
        Assert.All(Enum.GetValues<Papel>(), p => Assert.Equal(p, Papeis.De(p.Texto())));
        Assert.Null(Papeis.De("PROFESSOR"));
        Assert.Null(Papeis.De(null));
    }

    [Theory]
    [InlineData("ADMINISTRADOR", false, Papel.Administrador)]
    [InlineData("MEMBRO_TIME", false, Papel.MembroTime)]
    [InlineData("MASTER", false, Papel.Master)]
    [InlineData("MASTER", true, Papel.Autonomo)]
    [InlineData("STAFF", false, Papel.Staff)]
    [InlineData("PARTICIPANTE", false, Papel.Participante)]
    public void RN_232_papel_pelo_tipo_do_cadastro(string tipo, bool autonomo, Papel papel)
    {
        Assert.Equal(papel, Papeis.DoTipo(tipo, autonomo));
    }

    [Fact]
    public void Tipo_desconhecido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Papeis.DoTipo("PROFESSOR", false));
        Assert.True(Papel.Administrador.DoTime());
        Assert.True(Papel.MembroTime.DoTime());
        Assert.False(Papel.Master.DoTime());
    }
}
