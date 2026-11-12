using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>Módulo suspenso pela contratação: RN-285, RN-286, RN-287.</summary>
public class ModuloSuspensoTests
{
    readonly Cenario mundo = new();

    public ModuloSuspensoTests()
    {
        mundo.Mock.Contratar("conta-ebfga", "MOD-05");
        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
        mundo.Mock.Definir("usr-ebfga-maria", 7, Mods(("MOD-03", "CU"), ("MOD-05", "U"), ("MOD-06", "U")));
        mundo.Mock.Definir("usr-ebfga-joao", 3, Mods(("MOD-06", "U")));
    }

    async Task Descontratar()
    {
        mundo.Mock.Descontratar("conta-ebfga", "MOD-06");
        await mundo.Mock.AvisarContratacaoMudou.Executar("conta-ebfga", Catalogo);
    }

    async Task ContratarDeNovo()
    {
        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
        await mundo.Mock.AvisarContratacaoMudou.Executar("conta-ebfga", Catalogo);
    }

    [Fact]
    public async Task RN_287_o_que_fica_guardado()
    {
        await Descontratar();

        var maria = mundo.Mock.Guardadas("usr-ebfga-maria")!;
        Assert.Equal((8, 8), (maria.Versao, maria.Pv));
        Assert.Equal(Mods(("MOD-03", "CU"), ("MOD-05", "U")), maria.Mods);
        Assert.Equal(Mods(("MOD-06", "U")), maria.Suspensos);
    }

    [Fact]
    public async Task RN_286_so_a_contratacao_suspende()
    {
        await Descontratar();

        Assert.Equal(Mods(("MOD-06", "U")), mundo.Mock.Guardadas("usr-ebfga-joao")!.Suspensos);
        var versao8 = Assert.Single(mundo.Mock.Registro, m => m.UsuarioId == "usr-ebfga-maria");
        Assert.Equal(("usr-dboaz-caio", 8), (versao8.Por, versao8.Versao));
        Assert.Equal((Abas.U, (Abas?)null), (versao8.Antes["MOD-06"], versao8.Depois["MOD-06"]));
        Assert.Contains(("usr-ebfga-maria", 8), mundo.Mock.Avisos);
        Assert.All(mundo.Mock.Chamadas, c => Assert.Equal(Catalogo, c));
    }

    [Fact]
    public async Task RN_239_quem_nao_tem_o_modulo_so_sobe_o_pv()
    {
        await Descontratar();

        var ana = mundo.Mock.Guardadas("usr-ebfga-ana")!;
        Assert.Equal((2, 3), (ana.Versao, ana.Pv));
        Assert.Contains(("usr-ebfga-ana", 3), mundo.Mock.Avisos);
        Assert.DoesNotContain(mundo.Mock.Registro, m => m.UsuarioId == "usr-ebfga-ana");
    }

    [Fact]
    public async Task RN_286_a_conta_master_tenta_reativar()
    {
        await Descontratar();

        var falha = await Falha("MODULO_NAO_CONTRATADO",
            () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"8\"", ("MOD-03", "CU"), ("MOD-06", "U")));

        Assert.Equal("O módulo Presença não está contratado.", falha.Message);
        var maria = mundo.Mock.Guardadas("usr-ebfga-maria")!;
        Assert.Equal(8, maria.Versao);
        Assert.Equal(Mods(("MOD-06", "U")), maria.Suspensos);
    }

    [Fact]
    public async Task RN_286_suspenso_com_a_conta_ainda_contratando_e_recusado()
    {
        mundo.Mock.Definir("usr-ebfga-maria", 8, Mods(("MOD-03", "CU")), suspensos: Mods(("MOD-06", "U")));

        await Falha("MODULO_NAO_CONTRATADO", () => mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"8\"", ("MOD-03", "CU"), ("MOD-06", "U")));
    }

    [Fact]
    public async Task RN_285_a_escola_contrata_a_presenca_de_novo()
    {
        await Descontratar();
        await ContratarDeNovo();

        var maria = mundo.Mock.Guardadas("usr-ebfga-maria")!;
        Assert.Equal((9, 9), (maria.Versao, maria.Pv));
        Assert.Equal(Abas.U, maria.Mods["MOD-06"]);
        Assert.Empty(maria.Suspensos);
        Assert.Equal("U", (await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria")).Mods["MOD-06"]);
    }

    [Fact]
    public async Task RN_287_salvar_nao_apaga_o_suspenso()
    {
        await Descontratar();

        var salvas = await mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"8\"", ("MOD-03", "CU"));
        Assert.Equal(9, salvas.Versao);
        Assert.DoesNotContain("MOD-05", salvas.Mods.Keys);

        await ContratarDeNovo();
        Assert.Equal("U", (await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria")).Mods["MOD-06"]);
    }

    [Fact]
    public async Task RN_287_desmarcado_antes_nao_volta()
    {
        await mundo.Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-03", "CU"), ("MOD-05", "U"));

        await Descontratar();
        await ContratarDeNovo();

        Assert.DoesNotContain("MOD-06", (await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria")).Mods.Keys);
    }

    [Fact]
    public async Task Outra_pessoa_salvou_no_meio_rele_e_tenta_de_novo()
    {
        mundo.Mock.PerderAProximaCorrida = true;

        await Descontratar();

        var joao = mundo.Mock.Guardadas("usr-ebfga-joao")!;
        Assert.Equal(Mods(("MOD-06", "U")), joao.Suspensos);
        Assert.Equal(5, joao.Versao);
    }
}
