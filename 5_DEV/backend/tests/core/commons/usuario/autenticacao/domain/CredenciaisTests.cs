using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Domain;

/// <summary>Formas das credenciais (RN-180, RN-184), resumo de senha (RNF-201) e renovação (RNF-213).</summary>
public class CredenciaisTests
{
    [Fact]
    public void Entrada_em_minusculas_e_codigo_em_maiusculas()
    {
        Assert.Equal("maria.eejsa@deboaz.com", Credenciais.Entrada(" Maria.EEJSA@deboaz.com "));
        Assert.Equal("EBFGA-LUCAS-0412", Credenciais.CodigoDeAcesso("ebfga-lucas-0412"));
    }

    [Fact]
    public void Resumo_e_sha256_em_hexadecimal()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Credenciais.Resumo("abc"));
    }

    [Fact]
    public void Resumo_de_senha_confere_so_a_senha_certa()
    {
        var resumo = ResumoDeSenha.Gerar("Ma!Silva204871", 1_000);
        Assert.StartsWith("pbkdf2-sha256$1000$", resumo);
        Assert.DoesNotContain("Silva", resumo);
        Assert.True(ResumoDeSenha.Confere("Ma!Silva204871", resumo));
        Assert.False(ResumoDeSenha.Confere("Ma!Silva204872", resumo));
        Assert.NotEqual(resumo, ResumoDeSenha.Gerar("Ma!Silva204871", 1_000));
    }

    [Fact]
    public void Resumo_padrao_usa_600_mil_iteracoes()
    {
        Assert.StartsWith("pbkdf2-sha256$600000$", ResumoDeSenha.Gerar("Xu@Xa182482"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("md5$abc")]
    [InlineData("pbkdf2-sha256$x$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$10$***$AAAA")]
    public void Resumo_fora_do_formato_nunca_confere(string? guardado)
    {
        Assert.False(ResumoDeSenha.Confere("Ma!Silva204871", guardado));
    }

    [Fact]
    public void Token_de_renovacao_e_o_sid_e_256_bits()
    {
        var token = TokenDeRenovacao.Novo("3f9c2a71");
        Assert.StartsWith("3f9c2a71.", token.ToString());
        Assert.Equal(43, token.Segredo.Length);
        Assert.Equal(token, TokenDeRenovacao.Ler(token.ToString()));
        Assert.Equal(64, token.Resumo().Length);
        Assert.NotEqual(token.Segredo, TokenDeRenovacao.Novo("3f9c2a71").Segredo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3f9c2a71")]
    [InlineData(".segredo")]
    [InlineData("3f9c2a71.")]
    [InlineData("a.b.c")]
    public void Token_de_renovacao_fora_do_formato_e_null(string? texto)
    {
        Assert.Null(TokenDeRenovacao.Ler(texto));
    }
}
