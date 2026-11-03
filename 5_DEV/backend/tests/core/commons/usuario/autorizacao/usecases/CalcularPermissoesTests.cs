using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>
/// O papel, os mods e o pv que vão no token (RF-144, RF-145, RF-150, RF-151,
/// RN-232, RN-233, RNF-219), como nos cenários de API do 17-b.
/// </summary>
public class CalcularPermissoesTests
{
    readonly Cenario mundo = new();

    public CalcularPermissoesTests()
    {
        mundo.Mock.Contratar("conta-ebfga", "MOD-05");
        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
    }

    static IReadOnlyDictionary<string, string> Texto(params (string Mod, string Abas)[] pares) => pares.ToDictionary(p => p.Mod, p => p.Abas);

    [Fact]
    public async Task RN_233_administrador_com_SAC_Usuarios_e_Catalogo()
    {
        var token = await mundo.Token("usr-dboaz-admin");
        Assert.Equal("ADMINISTRADOR", token.Papel);
        Assert.Equal(Texto(("MOD-02", "CU"), ("MOD-03", "CU"), ("MOD-14", "CU")), token.Mods);
        Assert.Equal(1, token.Pv);
    }

    [Fact]
    public async Task RF_145_membro_do_time_sem_o_Catalogo()
    {
        Assert.Empty((await mundo.Token("usr-dboaz-caio")).Mods);
        await mundo.Conceder("usr-dboaz-caio", ("MOD-03", "U"));
        var token = await mundo.Token("usr-dboaz-caio");
        Assert.Equal(("MEMBRO_TIME", 2), (token.Papel, token.Pv));
        Assert.Equal(Texto(("MOD-03", "U")), token.Mods);
    }

    [Fact]
    public async Task RN_233_conta_master_com_os_da_conta_e_os_contratados()
    {
        var token = await mundo.Token("usr-ebfga-master");
        Assert.Equal("MASTER", token.Papel);
        Assert.Equal(["MOD-01", "MOD-02", "MOD-03", "MOD-04", "MOD-05", "MOD-06", "MOD-08", "MOD-09"], token.Mods.Keys.Order());
        Assert.All(token.Mods.Values, abas => Assert.Equal("CU", abas));
    }

    [Fact]
    public async Task RN_233_autonomo_sem_contratacao()
    {
        var token = await mundo.Token("usr-preis-pedro");
        Assert.Equal("AUTONOMO", token.Papel);
        Assert.Equal(["MOD-01", "MOD-02", "MOD-03", "MOD-04", "MOD-08", "MOD-09"], token.Mods.Keys.Order());
    }

    [Fact]
    public async Task RF_144_staff_com_dois_modulos()
    {
        await mundo.Conceder("usr-ebfga-maria", ("MOD-05", "U"), ("MOD-06", "CU"));
        var token = await mundo.Token("usr-ebfga-maria");
        Assert.Equal(("STAFF", 8), (token.Papel, token.Pv));
        Assert.Equal(Texto(("MOD-05", "U"), ("MOD-06", "CU")), token.Mods);
    }

    [Fact]
    public async Task RF_145_modulo_nao_contratado_nao_vai_no_token()
    {
        mundo.Mock.Definir("usr-ebfga-maria", 7, Mods(("MOD-05", "U"), ("MOD-12", "U")));
        Assert.Equal(Texto(("MOD-05", "U")), (await mundo.Token("usr-ebfga-maria")).Mods);
    }

    [Fact]
    public async Task RF_150_participante_com_os_do_quest()
    {
        var token = await mundo.Token("usr-ebfga-lucas");
        Assert.Equal("PARTICIPANTE", token.Papel);
        Assert.Equal(Texto(("MOD-05", "U"), ("MOD-06", "U"), ("MOD-09", "U"), ("MOD-13", "U")), token.Mods);
    }

    [Fact]
    public void RF_151_so_a_sala()
    {
        var token = mundo.Mock.CalcularPermissoesDoAnonimo.Executar("MOD-05");
        Assert.Equal(new PermissoesNoTokenDto("ANONIMO", token.Mods, 0), token);
        Assert.Equal(Texto(("MOD-05", "U")), token.Mods);
    }

    [Fact]
    public async Task Usuario_inexistente() => await Falha("USUARIO_INEXISTENTE", () => mundo.Token("usr-ninguem"));
}
