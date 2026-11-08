using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>PUT /usuarios/{id}/permissoes: RF-161, RF-162, RN-241, RN-254, RN-255, RNF-222.</summary>
public class SalvarPermissoesTests
{
    readonly Cenario mundo = new();

    public SalvarPermissoesTests()
    {
        mundo.Mock.Contratar("conta-ebfga", "MOD-05");
        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
    }

    [Fact]
    public async Task RF_161_quem_concede_perdeu_a_aba()
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-05", "U"), ("MOD-06", "U"));
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-05", "U"));

        var falha = await Falha("PERMISSAO_ACIMA",
            () => mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"2\"", ("MOD-05", "U"), ("MOD-06", "U")));

        Assert.Equal(("Você não tem mais o módulo Presença para conceder.", "MOD-06", "U"), (falha.Message, falha.Mod, falha.Abas));
        var ana = await mundo.Ler("usr-ebfga-joao", "usr-ebfga-ana");
        Assert.Equal(2, ana.Versao);
        Assert.Empty(ana.Mods);
    }

    [Fact]
    public async Task RF_161_staff_nao_tira_o_que_nao_pode_conceder()
    {
        mundo.Mock.Definir("usr-ebfga-ana", 2, Mods(("MOD-06", "CU")));
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-06", "U"));
        var falha = await Falha("PERMISSAO_ACIMA", () => mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "2"));
        Assert.Equal(("MOD-06", "CU"), (falha.Mod, falha.Abas));
    }

    [Fact]
    public async Task RN_252_staff_concede_so_com_as_abas_que_tem()
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-05", "U"));
        var salvas = await mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"2\"", ("MOD-05", "U"));
        Assert.Equal((3, "U"), (salvas.Versao, salvas.Mods["MOD-05"]));
        await Falha("PERMISSAO_ACIMA", () => mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"3\"", ("MOD-05", "CU")));
        await Falha("PERMISSAO_ACIMA", () => mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"3\"", ("MOD-05", "U"), ("MOD-06", "U")));
    }

    [Fact]
    public async Task RF_162_duas_pessoas_salvando()
    {
        await mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-05", "U"));

        var falha = await Falha("VERSAO_DESATUALIZADA",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-06", "U")));

        Assert.Equal(8, falha.Versao);
        Assert.Equal("As permissões de Maria Souza mudaram agora há pouco. Confira e salve.", falha.Message);
        Assert.Equal(Mods(("MOD-05", "U")), mundo.Mock.Guardadas("usr-ebfga-maria")!.Mods);
    }

    [Fact]
    public async Task RF_162_quem_perde_a_corrida_na_gravacao_recebe_412()
    {
        mundo.Mock.PerderAProximaCorrida = true;
        var falha = await Falha("VERSAO_DESATUALIZADA",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-05", "U")));
        Assert.Equal(8, falha.Versao);
        Assert.Empty(mundo.Mock.Avisos);
        Assert.Empty(mundo.Mock.Registro);
    }

    [Theory]
    [InlineData("\"7\"", null)]
    [InlineData("\"6\"", "VERSAO_DESATUALIZADA")]
    [InlineData(null, "VERSAO_AUSENTE")]
    public async Task RNF_222_versao_no_salvamento(string? ifMatch, string? recusa)
    {
        Task Salvar() => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", ifMatch, ("MOD-05", "U"));
        if (recusa is null)
        {
            await Salvar();
            Assert.Equal(8, mundo.Mock.Guardadas("usr-ebfga-maria")!.Versao);
            return;
        }
        await Falha(recusa, Salvar);
        Assert.Equal(7, mundo.Mock.Guardadas("usr-ebfga-maria")!.Versao);
    }

    [Fact]
    public async Task Recusa_de_acesso_vem_antes_da_versao()
    {
        await Falha("ACESSO_NEGADO", () => mundo.Salvar("usr-ebfga-maria", "usr-ebfga-maria", null));
        await Falha("USUARIO_INEXISTENTE", () => mundo.Salvar("usr-ebfga-master", "usr-colsa-bruno", null));
    }

    [Fact]
    public async Task Dados_invalidos()
    {
        var corpo = await Falha("DADOS_INVALIDOS", () => mundo.Mock.SalvarPermissoes.Executar(
            new("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", null), TraceId.Novo()));
        Assert.Equal("mods", corpo.Campo);
        Assert.Equal("If-Match", (await Falha("DADOS_INVALIDOS",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"sete\"", ("MOD-05", "U")))).Campo);
    }

    [Fact]
    public async Task RN_252_modulo_inexistente_ou_que_a_pessoa_nao_usa()
    {
        var inexistente = await Falha("DADOS_INVALIDOS",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-99", "U")));
        Assert.Equal(("mods.MOD-99", "O módulo MOD-99 não existe."), (inexistente.Campo, inexistente.Message));
        var naoUsa = await Falha("DADOS_INVALIDOS",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-13", "U")));
        Assert.Equal(("mods.MOD-13", "Maria Souza não usa o módulo Perfil."), (naoUsa.Campo, naoUsa.Message));
    }

    [Fact]
    public async Task RN_252_staff_repassa_Administrar_usuarios_com_as_abas_que_tem()
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "U"), ("MOD-05", "U"));

        var salvas = await mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"2\"", ("MOD-03", "U"));
        var falha = await Falha("PERMISSAO_ACIMA",
            () => mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"3\"", ("MOD-03", "CU")));

        Assert.Equal((3, "U"), (salvas.Versao, salvas.Mods["MOD-03"]));
        Assert.Equal(("Você não tem mais o módulo Usuários para conceder.", "MOD-03", "CU"), (falha.Message, falha.Mod, falha.Abas));
    }

    [Fact]
    public async Task Modulo_nao_contratado()
    {
        mundo.Mock.Descontratar("conta-ebfga", "MOD-06");
        var falha = await Falha("MODULO_NAO_CONTRATADO",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-06", "U")));
        Assert.Equal(("MOD-06", "O módulo Presença não está contratado."), (falha.Mod, falha.Message));
        mundo.Mock.Contratar("conta-ebfga", "MOD-07");
        Assert.Equal("Maria Souza não usa o módulo Carteirinha.", (await Falha("DADOS_INVALIDOS",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-07", "U")))).Message);
    }

    // RN-166: o que não está contratado responde que não está contratado, antes
    // de olhar se a pessoa usaria o módulo (a Agenda, para um staff)
    [Fact]
    public async Task RN_166_nao_contratado_antes_de_quem_usa()
    {
        var falha = await Falha("MODULO_NAO_CONTRATADO",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-12", "U")));
        Assert.Equal(("MOD-12", "O módulo Agenda não está contratado."), (falha.Mod, falha.Message));
    }

    [Fact]
    public async Task RN_241_confere_no_servidor_nao_no_token()
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-06", "U"));
        var tokenAntigo = await mundo.Token("usr-ebfga-joao");
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"));

        Assert.Contains("MOD-06", tokenAntigo.Mods.Keys);
        await Falha("PERMISSAO_ACIMA", () => mundo.Salvar("usr-ebfga-joao", "usr-ebfga-ana", "\"2\"", ("MOD-06", "U")));
    }

    [Fact]
    public async Task RN_254_desmarcar_retira()
    {
        mundo.Mock.Definir("usr-ebfga-maria", 7, Mods(("MOD-03", "CU"), ("MOD-06", "U")));
        var salvas = await mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-03", "CU"));
        Assert.Equal(new Dictionary<string, string> { ["MOD-03"] = "CU" }, salvas.Mods);
        Assert.Equal(Mods(("MOD-03", "CU")), mundo.Mock.Guardadas("usr-ebfga-maria")!.Mods);
    }

    [Fact]
    public async Task RN_255_salvar_com_mudanca()
    {
        var salvas = await mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-05", "U"));
        Assert.Equal(8, salvas.Versao);
        Assert.Equal([("usr-ebfga-maria", 8)], mundo.Mock.Avisos);
        Assert.Equal(8, (await mundo.Token("usr-ebfga-maria")).Pv);
    }

    [Fact]
    public async Task RN_255_salvar_sem_mudanca()
    {
        var salvas = await mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"");
        Assert.Equal(7, salvas.Versao);
        Assert.Empty(mundo.Mock.Avisos);
        Assert.Empty(mundo.Mock.Registro);
    }
}
