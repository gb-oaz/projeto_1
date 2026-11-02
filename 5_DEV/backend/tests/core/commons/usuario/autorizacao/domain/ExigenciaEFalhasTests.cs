using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Domain;

/// <summary>RF-147, RNF-208, RNF-220 (exigência da rota) e os códigos de erro do contrato.</summary>
public class ExigenciaEFalhasTests
{
    [Fact]
    public void RNF_220_rota_sem_exigencia_e_recusada()
    {
        var nada = new ExigenciaDaRota();
        Assert.False(nada.Declarada);
        Assert.False(nada.Atende(Papel.Administrador, Mods(("MOD-03", "CU"))));
    }

    [Fact]
    public void RF_147_modulo_e_aba()
    {
        var usuarios = new ExigenciaDaRota("MOD-03", Abas.U);
        Assert.True(usuarios.Atende(Papel.Staff, Mods(("MOD-03", "CU"))));
        Assert.True(usuarios.Atende(Papel.Staff, Mods(("MOD-03", "U"))));
        Assert.False(usuarios.Atende(Papel.Staff, Mods(("MOD-03", "C"))));
        Assert.False(usuarios.Atende(Papel.Staff, Mods(("MOD-05", "U"))));
        Assert.True(new ExigenciaDaRota("MOD-05").Atende(Papel.Staff, Mods(("MOD-05", "C"))));
    }

    [Fact]
    public void Papel_exigido()
    {
        var soDoTime = new ExigenciaDaRota(Papeis: new HashSet<Papel> { Papel.Administrador });
        Assert.True(soDoTime.Atende(Papel.Administrador, Mods()));
        Assert.False(soDoTime.Atende(Papel.MembroTime, Mods(("MOD-14", "CU"))));
    }

    [Fact]
    public void Codigos_e_campos_das_falhas()
    {
        var acima = FalhaDeAutorizacao.PermissaoAcima("MOD-06", "Presença", Abas.U);
        Assert.Equal(("PERMISSAO_ACIMA", GrupoDeFalha.Proibido, "MOD-06", "U"), (acima.Codigo, acima.Grupo, acima.Mod, acima.Abas));
        Assert.Equal("Você não tem mais o módulo Presença para conceder.", acima.Message);

        var versao = FalhaDeAutorizacao.VersaoDesatualizada(7, "Maria Souza");
        Assert.Equal(("VERSAO_DESATUALIZADA", GrupoDeFalha.VersaoDesatualizada, 7), (versao.Codigo, versao.Grupo, versao.Versao));
        Assert.Equal("As permissões de Maria Souza mudaram agora há pouco. Confira e salve.", versao.Message);

        Assert.Equal(("ACESSO_NEGADO", GrupoDeFalha.Proibido), (FalhaDeAutorizacao.AcessoNegado().Codigo, FalhaDeAutorizacao.AcessoNegado().Grupo));
        Assert.Equal(("USUARIO_INEXISTENTE", GrupoDeFalha.NaoEncontrado),
            (FalhaDeAutorizacao.UsuarioInexistente().Codigo, FalhaDeAutorizacao.UsuarioInexistente().Grupo));
        Assert.Equal(("VERSAO_AUSENTE", GrupoDeFalha.VersaoAusente), (FalhaDeAutorizacao.VersaoAusente().Codigo, FalhaDeAutorizacao.VersaoAusente().Grupo));
        var contratado = FalhaDeAutorizacao.ModuloNaoContratado("MOD-12", "Agenda");
        Assert.Equal(("MODULO_NAO_CONTRATADO", GrupoDeFalha.DadosInvalidos, "MOD-12"), (contratado.Codigo, contratado.Grupo, contratado.Mod));
        var dados = FalhaDeAutorizacao.DadosInvalidos("mods");
        Assert.Equal(("DADOS_INVALIDOS", "mods"), (dados.Codigo, dados.Campo));
    }
}
