using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Domain;

/// <summary>Sigla, documento, logins, códigos e senha (RN-171, RN-179, RN-180, RN-184, RN-188, RN-242, RNF-202).</summary>
public class FormatosTests
{
    [Theory]
    [InlineData("ebfga", "EBFGA")]
    [InlineData(" Colsa ", "COLSA")]
    public void RN_179_sigla_valida(string sigla, string esperada) => Assert.Equal(esperada, Sigla.Validar(sigla));

    [Theory]
    [InlineData("EBF", "A sigla precisa de 5 letras.")]
    [InlineData("EBFG1", "A sigla precisa de 5 letras.")]
    [InlineData(null, "A sigla precisa de 5 letras.")]
    [InlineData("dboaz", "A sigla DBOAZ é reservada ao time deboaz.")]
    public void RN_179_sigla_recusada(string? sigla, string mensagem)
    {
        var falha = Assert.Throws<FalhaDeUsuarios>(() => Sigla.Validar(sigla));
        Assert.Equal(("SIGLA_INVALIDA", mensagem, "sigla"), (falha.Codigo, falha.Message, falha.Campo));
    }

    [Theory]
    [InlineData("Escola Bento Freitas Gonçalves Araújo", "EBFGA")]
    [InlineData("Escola Estadual José de Souza Andrade e Lima", "EEJSA")]
    [InlineData("Colégio Salesiano", "CSALE")]
    [InlineData("Objetivo", "OBJET")]
    [InlineData("Xuxa", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void RN_179_sigla_sugerida(string? nome, string? sigla) => Assert.Equal(sigla, Sigla.Sugerir(nome));

    [Theory]
    [InlineData(TipoConta.Instituicao, "12.345.678/0001-90", true)]
    [InlineData(TipoConta.Instituicao, "12345678000190", true)]
    [InlineData(TipoConta.Autonomo, "123.456.789-09", true)]
    [InlineData(TipoConta.Autonomo, "12.345.678/0001-90", false)]
    [InlineData(TipoConta.Instituicao, "123.456.789-09", false)]
    [InlineData(TipoConta.Autonomo, "123.456.789-0X", false)]
    [InlineData(TipoConta.Autonomo, null, false)]
    public void RN_242_documento_de_cada_conta(TipoConta tipo, string? documento, bool serve) =>
        Assert.Equal(serve, Documento.ServePara(tipo, documento));

    [Fact]
    public void RN_180_login_de_cada_usuario()
    {
        Assert.Equal("objetivo_unidade1@deboaz.com", Logins.DaMaster("objetivo_unidade1"));
        Assert.Equal("maria.eejsa@deboaz.com", Logins.DoNomeCurto("maria", "EEJSA"));
        Assert.Equal("gustavo.dboaz@deboaz.com", Logins.DoNomeCurto("gustavo", "DBOAZ"));
        Assert.Equal("maria_1", Logins.NomeCurto(" Maria_1 "));
        Assert.Equal("dominio", Assert.Throws<FalhaDeUsuarios>(() => Logins.NomeCurto("maria souza", "dominio")).Campo);
        Assert.Equal("nomeCurto", Assert.Throws<FalhaDeUsuarios>(() => Logins.NomeCurto(null)).Campo);
    }

    [Fact]
    public void RN_184_formato_do_codigo() => Assert.Matches(@"^EBFGA-LUCAS-\d{4}$", Logins.CodigoDeAcesso("ebfga", "lucas"));

    [Fact]
    public void RN_188_e_RN_181_numeros_sorteados()
    {
        Assert.Matches(@"^\d{8}$", Matricula.Gerar());
        Assert.Matches(@"^\d{6}$", CodigoDeLiberacao.Gerar());
        Assert.Matches("^[0-9a-f]{32}$", CodigoDeLiberacao.Comprovante());
        Assert.Equal(TimeSpan.FromHours(72), CodigoDeLiberacao.Validade);
    }

    [Theory]
    [InlineData("Gustavo Boaz de Moura Souza", @"^Gu[!@#$%&*]Boaz\d{6}$")]
    [InlineData("Maria da Silva", @"^Ma[!@#$%&*]Silva\d{6}$")]
    [InlineData("Xuxa", @"^Xu[!@#$%&*]Xa\d{6}$")]
    [InlineData("JOÃO pereira", @"^Jo[!@#$%&*]Pereira\d{6}$")]
    [InlineData("", @"^De[!@#$%&*]Az\d{6}$")]
    public void RN_171_senha_gerada(string nome, string formato)
    {
        var senha = Senha.Gerar(nome);
        Assert.Matches(formato, senha);
        Assert.True(Senha.Forte(senha));
    }

    [Theory]
    [InlineData("Gu#Boaz507318", true)]
    [InlineData("Gu#B5", false)]
    [InlineData("gu#boaz507318", false)]
    [InlineData("GuBoaz507318", false)]
    [InlineData("Gu#Boazxyzwq", false)]
    [InlineData(null, false)]
    public void RNF_202_senha_valida(string? senha, bool aceita) => Assert.Equal(aceita, Senha.Forte(senha));
}
