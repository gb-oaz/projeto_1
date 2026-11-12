using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Domain;

/// <summary>RN-238, RN-254, RN-255, RN-256: salvar substitui tudo, sobe a versão uma vez e registra.</summary>
public class PermissoesDoUsuarioTests
{
    static readonly DateTimeOffset Agora = new(2026, 11, 1, 13, 20, 0, TimeSpan.Zero);
    static readonly IReadOnlySet<string> Disponiveis = new HashSet<string> { "MOD-03", "MOD-05", "MOD-06" };

    [Fact]
    public void Vazias_comecam_na_versao_1()
    {
        var p = PermissoesDoUsuario.Vazias("usr-ebfga-ana");
        Assert.Equal((1, 1), (p.Versao, p.Pv));
        Assert.Empty(p.Mods);
    }

    [Fact]
    public void RN_254_salvar_substitui_e_registra_so_o_que_mudou()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 7, Mods(("MOD-03", "CU"), ("MOD-06", "U")));

        var mudanca = p.Salvar(Mods(("MOD-03", "CU"), ("MOD-05", "U")), Disponiveis, "usr-ebfga-master", Agora)!;

        Assert.Equal((8, 8), (p.Versao, p.Pv));
        Assert.Equal(Mods(("MOD-03", "CU"), ("MOD-05", "U")), p.Mods);
        Assert.Equal(("usr-ebfga-maria", "usr-ebfga-master", Agora, 8), (mudanca.UsuarioId, mudanca.Por, mudanca.Em, mudanca.Versao));
        Assert.Equal(new Dictionary<string, Abas?> { ["MOD-05"] = null, ["MOD-06"] = Abas.U }, mudanca.Antes);
        Assert.Equal(new Dictionary<string, Abas?> { ["MOD-05"] = Abas.U, ["MOD-06"] = null }, mudanca.Depois);
    }

    [Fact]
    public void RN_255_sem_mudanca_nao_sobe_a_versao()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 7, Mods(("MOD-05", "U")));
        Assert.Null(p.Salvar(Mods(("MOD-05", "U")), Disponiveis, "usr-ebfga-master", Agora));
        Assert.Equal((7, 7), (p.Versao, p.Pv));
    }

    [Fact]
    public void Mudar_as_abas_e_mudanca()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 9, Mods(("MOD-05", "U")));
        var mudanca = p.Salvar(Mods(("MOD-05", "CU")), Disponiveis, "usr-ebfga-master", Agora)!;
        Assert.Equal((8, 10), (p.Versao, p.Pv));
        Assert.Equal(Abas.CU, mudanca.Depois["MOD-05"]);
    }

    [Fact]
    public void RN_238_modulo_descontratado_fica_guardado_e_escondido()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 7, Mods(("MOD-06", "U"), ("MOD-12", "U")));

        Assert.Equal(Mods(("MOD-06", "U")), p.Visiveis(Disponiveis));
        p.Salvar(Mods(("MOD-06", "U"), ("MOD-05", "C")), Disponiveis, "usr-ebfga-master", Agora);

        Assert.Equal(Mods(("MOD-05", "C"), ("MOD-06", "U"), ("MOD-12", "U")), p.Mods);
    }

    static ModulosDaConta ModulosCom(params string[] contratados) => new(
        [
            new ModuloRegistrado("MOD-03", "Usuários", TipoDeModulo.DaConta, new HashSet<(Perfil, FrenteDeUso)>()),
            new ModuloRegistrado("MOD-05", "Quiz", TipoDeModulo.Contratavel, new HashSet<(Perfil, FrenteDeUso)>()),
            new ModuloRegistrado("MOD-06", "Presença", TipoDeModulo.Contratavel, new HashSet<(Perfil, FrenteDeUso)>()),
        ],
        contratados.ToHashSet(), contaDoTime: false);

    [Fact]
    public void RN_287_descontratar_suspende_com_as_abas()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 7, Mods(("MOD-03", "CU"), ("MOD-05", "U"), ("MOD-06", "U")));

        var mudanca = p.AcompanharContratacao(ModulosCom("MOD-05"), "usr-dboaz-caio", Agora)!;

        Assert.Equal((8, 8), (p.Versao, p.Pv));
        Assert.Equal(Mods(("MOD-03", "CU"), ("MOD-05", "U")), p.Mods);
        Assert.Equal(Mods(("MOD-06", "U")), p.Suspensos);
        Assert.Equal(("usr-dboaz-caio", 8), (mudanca.Por, mudanca.Versao));
        Assert.Equal(new Dictionary<string, Abas?> { ["MOD-06"] = Abas.U }, mudanca.Antes);
        Assert.Equal(new Dictionary<string, Abas?> { ["MOD-06"] = null }, mudanca.Depois);
    }

    [Fact]
    public void RN_285_contratar_de_novo_devolve_as_abas()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 8, 8, Mods(("MOD-03", "CU")), Mods(("MOD-06", "U")));

        var mudanca = p.AcompanharContratacao(ModulosCom("MOD-06"), "usr-dboaz-caio", Agora)!;

        Assert.Equal((9, 9), (p.Versao, p.Pv));
        Assert.Equal(Mods(("MOD-03", "CU"), ("MOD-06", "U")), p.Mods);
        Assert.Empty(p.Suspensos);
        Assert.Equal(new Dictionary<string, Abas?> { ["MOD-06"] = null }, mudanca.Antes);
        Assert.Equal(new Dictionary<string, Abas?> { ["MOD-06"] = Abas.U }, mudanca.Depois);
    }

    [Fact]
    public void RN_287_sem_modulo_a_mover_nada_muda()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 7, Mods(("MOD-03", "CU")));

        Assert.Null(p.AcompanharContratacao(ModulosCom(), "usr-dboaz-caio", Agora));
        Assert.Equal((7, 7), (p.Versao, p.Pv));
    }

    [Fact]
    public void Suspenso_que_ja_voltou_a_mods_so_sai_dos_suspensos()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 8, 8, Mods(("MOD-06", "CU")), Mods(("MOD-06", "U")));

        Assert.Null(p.AcompanharContratacao(ModulosCom("MOD-06"), "usr-dboaz-caio", Agora));
        Assert.Equal(Mods(("MOD-06", "CU")), p.Mods);
        Assert.Empty(p.Suspensos);
    }

    [Fact]
    public void RN_287_salvar_nao_apaga_o_suspenso()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 8, 8, Mods(("MOD-03", "CU"), ("MOD-05", "U")), Mods(("MOD-06", "U")));

        p.Salvar(Mods(("MOD-03", "CU")), new HashSet<string> { "MOD-03", "MOD-05" }, "usr-ebfga-master", Agora);

        Assert.Equal(Mods(("MOD-06", "U")), p.Suspensos);
    }

    [Fact]
    public void RN_239_subir_o_pv_nao_muda_a_versao()
    {
        var p = new PermissoesDoUsuario("usr-ebfga-maria", 7, 7, Mods(("MOD-03", "CU")));
        p.SubirPv();
        Assert.Equal((7, 8), (p.Versao, p.Pv));
    }
}
