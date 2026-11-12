using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>GET /usuarios/{id}/permissoes: RF-160, RN-235, RN-238, RN-252.</summary>
public class LerPermissoesTests
{
    readonly Cenario mundo = new();

    public LerPermissoesTests()
    {
        mundo.Mock.Contratar("conta-ebfga", "MOD-05");
        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
    }

    [Fact]
    public async Task RF_160_staff_com_Administrar_usuarios()
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-05", "U"));

        var lidas = await mundo.Ler("usr-ebfga-joao", "usr-ebfga-ana");

        Assert.Equal(("usr-ebfga-ana", 2), (lidas.Usuario, lidas.Versao));
        Assert.Empty(lidas.Mods);
        Assert.Equal([new ConcedivelDto("MOD-03", "Usuários", "CU"), new ConcedivelDto("MOD-05", "Quiz", "U")], lidas.Concediveis);
    }

    [Fact]
    public async Task RF_160_pessoa_que_nao_esta_abaixo()
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"));
        await Falha("ACESSO_NEGADO", () => mundo.Ler("usr-ebfga-joao", "usr-ebfga-rui"));
    }

    [Fact]
    public async Task RF_160_conta_master_ve_tudo_que_a_staff_usa()
    {
        var lidas = await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria");
        Assert.Equal(7, lidas.Versao);
        Assert.Equal(["MOD-03", "MOD-04", "MOD-05", "MOD-06", "MOD-08", "MOD-09"], lidas.Concediveis.Select(c => c.Mod));
        Assert.All(lidas.Concediveis, c => Assert.Equal("CU", c.Abas));
    }

    [Fact]
    public async Task RN_235_registro_de_outra_conta()
    {
        await Falha("USUARIO_INEXISTENTE", () => mundo.Ler("usr-ebfga-master", "usr-colsa-bruno"));
        await Falha("USUARIO_INEXISTENTE", () => mundo.Ler("usr-ebfga-master", "usr-ninguem"));
        await Falha("ACESSO_NEGADO", () => mundo.Ler("usr-ninguem", "usr-ebfga-maria"));
    }

    [Fact]
    public async Task RN_238_descontratacao_e_recontratacao()
    {
        await mundo.Conceder("usr-ebfga-maria", ("MOD-06", "U"));

        mundo.Mock.Descontratar("conta-ebfga", "MOD-06");
        await mundo.Mock.AvisarContratacaoMudou.Executar("conta-ebfga", Catalogo);

        Assert.DoesNotContain("MOD-06", (await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria")).Mods.Keys);
        var token = await mundo.Token("usr-ebfga-maria");
        Assert.DoesNotContain("MOD-06", token.Mods.Keys);
        Assert.Contains(("usr-ebfga-maria", token.Pv), mundo.Mock.Avisos);

        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
        await mundo.Mock.AvisarContratacaoMudou.Executar("conta-ebfga", Catalogo);
        Assert.Equal("U", (await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria")).Mods["MOD-06"]);
    }

    [Fact]
    public async Task RN_285_permissoes_durante_a_suspensao()
    {
        await mundo.Conceder("usr-ebfga-maria", ("MOD-03", "CU"), ("MOD-05", "U"), ("MOD-06", "U"));

        mundo.Mock.Descontratar("conta-ebfga", "MOD-06");
        await mundo.Mock.AvisarContratacaoMudou.Executar("conta-ebfga", Catalogo);

        var lidas = await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria");
        Assert.Equal(9, lidas.Versao);
        Assert.DoesNotContain("MOD-06", lidas.Mods.Keys);
        Assert.DoesNotContain("MOD-06", lidas.Concediveis.Select(c => c.Mod));
    }

    public static TheoryData<string, string, string?> Concessoes() => new()
    {
        { "usr-dboaz-admin", "usr-dboaz-caio", null },
        { "usr-ebfga-master", "usr-ebfga-maria", null },
        { "usr-ebfga-joao", "usr-ebfga-ana", null },
        { "usr-ebfga-joao", "usr-ebfga-joao", "ACESSO_NEGADO" },
        { "usr-ebfga-joao", "usr-ebfga-maria", "ACESSO_NEGADO" },
        { "usr-ebfga-ana", "usr-ebfga-joao", "ACESSO_NEGADO" },
        { "usr-ebfga-master", "usr-ebfga-lucas", "ACESSO_NEGADO" },
        { "usr-dboaz-admin", "usr-ebfga-master", "ACESSO_NEGADO" },
    };

    [Theory]
    [MemberData(nameof(Concessoes))]
    public async Task RN_252_concessoes_permitidas(string quem, string alvo, string? recusa)
    {
        await mundo.Conceder("usr-ebfga-joao", ("MOD-03", "CU"), ("MOD-05", "U"));
        if (recusa is null) Assert.Equal(alvo, (await mundo.Ler(quem, alvo)).Usuario);
        else await Falha(recusa, () => mundo.Ler(quem, alvo));
    }
}
