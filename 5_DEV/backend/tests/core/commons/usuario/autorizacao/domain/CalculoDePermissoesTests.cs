using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;
using static Boaz.Core.Commons.Usuario.Autorizacao.Domain.FrenteDeUso;
using static Boaz.Core.Commons.Usuario.Autorizacao.Domain.Perfil;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Domain;

/// <summary>RN-233 (quem tem o quê), RN-252 (quem concede a quem), RF-160 (concedíveis) e RNF-220.</summary>
public class CalculoDePermissoesTests
{
    static readonly ModuloRegistrado[] Registro =
    [
        new("MOD-02", "SAC", TipoDeModulo.DaConta, new HashSet<(Perfil, FrenteDeUso)> { (Master, Hub), (MembroTime, Hub) }),
        new("MOD-03", "Usuários", TipoDeModulo.DaConta, new HashSet<(Perfil, FrenteDeUso)> { (Administrador, Hub), (Master, Hub), (Staff, Hub) }),
        new("MOD-05", "Quiz", TipoDeModulo.Contratavel, new HashSet<(Perfil, FrenteDeUso)> { (Staff, Hub), (Participante, Quest) }),
        new("MOD-06", "Presença", TipoDeModulo.Contratavel, new HashSet<(Perfil, FrenteDeUso)> { (Staff, Hub), (Responsavel, Quest) }),
        new("MOD-13", "Perfil", TipoDeModulo.DaConta, new HashSet<(Perfil, FrenteDeUso)> { (Participante, Quest) }),
        new("MOD-14", "Catálogo", TipoDeModulo.DoTime, new HashSet<(Perfil, FrenteDeUso)> { (Administrador, Hub), (MembroTime, Hub) }),
    ];

    static ModulosDaConta Escola(params string[] contratados) => new(Registro, contratados.ToHashSet(), contaDoTime: false);
    static ModulosDaConta Time() => new(Registro, new HashSet<string>(), contaDoTime: true);
    static UsuarioAutorizado Pessoa(string id, Papel papel, string conta = "conta-ebfga") => new(id, conta, id, papel, null);
    static PermissoesDoUsuario Guardadas(params (string, string)[] mods) => new("x", 4, 9, Mods(mods));

    [Fact]
    public void RN_203_modulos_da_conta()
    {
        Assert.Equal(["MOD-02", "MOD-03", "MOD-13"], Escola().Disponiveis.Order());
        Assert.Equal(["MOD-02", "MOD-03", "MOD-05", "MOD-13"], Escola("MOD-05").Disponiveis.Order());
        Assert.Equal(["MOD-02", "MOD-03", "MOD-13", "MOD-14"], Time().Disponiveis.Order());
        Assert.Null(Escola().Registrado("MOD-99"));
    }

    [Fact]
    public void RN_233_administrador_tem_os_do_time_com_as_duas_abas()
    {
        var token = CalculoDePermissoes.Calcular(Pessoa("adm", Papel.Administrador, "conta-dboaz"), Guardadas(), Time());
        Assert.Equal(Mods(("MOD-02", "CU"), ("MOD-03", "CU"), ("MOD-14", "CU")), token.Mods);
        Assert.Equal((Papel.Administrador, 9), (token.Papel, token.Pv));
    }

    [Theory]
    [InlineData(Papel.Master)]
    [InlineData(Papel.Autonomo)]
    public void RN_233_conta_master_e_autonomo_tem_os_da_conta_e_os_contratados_do_hub(Papel papel)
    {
        var token = CalculoDePermissoes.Calcular(Pessoa("m", papel), Guardadas(), Escola("MOD-05"));
        Assert.Equal(Mods(("MOD-02", "CU"), ("MOD-03", "CU"), ("MOD-05", "CU")), token.Mods);
    }

    [Fact]
    public void RN_233_staff_e_membro_tem_os_marcados_que_a_conta_tem()
    {
        var staff = CalculoDePermissoes.Calcular(Pessoa("s", Papel.Staff), Guardadas(("MOD-05", "U"), ("MOD-06", "U")), Escola("MOD-05"));
        Assert.Equal(Mods(("MOD-05", "U")), staff.Mods);
        var membro = CalculoDePermissoes.Calcular(Pessoa("c", Papel.MembroTime, "conta-dboaz"), Guardadas(("MOD-02", "U")), Time());
        Assert.Equal(Mods(("MOD-02", "U")), membro.Mods);
    }

    [Fact]
    public void RF_150_participante_tem_os_do_quest_so_com_utilizacao()
    {
        var token = CalculoDePermissoes.Calcular(Pessoa("l", Papel.Participante), Guardadas(), Escola("MOD-05", "MOD-06"));
        Assert.Equal(Mods(("MOD-05", "U"), ("MOD-06", "U"), ("MOD-13", "U")), token.Mods);
    }

    [Fact]
    public void RF_151_anonimo_so_tem_o_modulo_da_sala()
    {
        var token = CalculoDePermissoes.DoAnonimo("MOD-05");
        Assert.Equal((Papel.Anonimo, 0), (token.Papel, token.Pv));
        Assert.Equal(Mods(("MOD-05", "U")), token.Mods);
    }

    public static TheoryData<Papel, string, Papel, string, bool, bool, bool> Concessoes() => new()
    {
        // quem, conta, alvo, conta do alvo, quem tem MOD-03 · U, alvo abaixo, pode
        { Papel.Administrador, "conta-dboaz", Papel.MembroTime, "conta-dboaz", true, false, true },
        { Papel.Administrador, "conta-dboaz", Papel.Master, "conta-ebfga", true, false, false },
        { Papel.Master, "conta-ebfga", Papel.Staff, "conta-ebfga", true, false, true },
        { Papel.Autonomo, "conta-preis", Papel.Staff, "conta-preis", true, false, true },
        { Papel.Master, "conta-ebfga", Papel.Staff, "conta-colsa", true, false, false },
        { Papel.Master, "conta-ebfga", Papel.Participante, "conta-ebfga", true, false, false },
        { Papel.Staff, "conta-ebfga", Papel.Staff, "conta-ebfga", true, true, true },
        { Papel.Staff, "conta-ebfga", Papel.Staff, "conta-ebfga", true, false, false },
        { Papel.Staff, "conta-ebfga", Papel.Staff, "conta-ebfga", false, true, false },
        { Papel.Staff, "conta-ebfga", Papel.Staff, "conta-colsa", true, true, false },
        { Papel.Staff, "conta-ebfga", Papel.Participante, "conta-ebfga", true, true, false },
        { Papel.MembroTime, "conta-dboaz", Papel.MembroTime, "conta-dboaz", true, false, false },
    };

    [Theory]
    [MemberData(nameof(Concessoes))]
    public void RN_252_quem_concede_a_quem(Papel papel, string conta, Papel papelDoAlvo, string contaDoAlvo, bool temUsuarios, bool abaixo, bool pode)
    {
        var quem = Pessoa("quem", papel, conta);
        var mods = temUsuarios ? Mods(("MOD-03", "CU")) : Mods(("MOD-03", "C"));
        var alvo = Pessoa("alvo", papelDoAlvo, contaDoAlvo);
        Assert.Equal(pode, CalculoDePermissoes.PodeConceder(quem, mods, alvo, abaixo ? ["quem"] : ["outro"]));
    }

    [Fact]
    public void RN_252_ninguem_concede_a_si_mesmo()
    {
        var master = Pessoa("m", Papel.Master);
        Assert.False(CalculoDePermissoes.PodeConceder(master, Mods(("MOD-03", "CU")), master, []));
    }

    [Fact]
    public void RF_160_conta_master_concede_tudo_que_o_staff_usa()
    {
        var concediveis = CalculoDePermissoes.Concediveis(Pessoa("m", Papel.Master), Mods(), Pessoa("s", Papel.Staff), Escola("MOD-05", "MOD-06"));
        Assert.Equal(Mods(("MOD-03", "CU"), ("MOD-05", "CU"), ("MOD-06", "CU")), concediveis);
    }

    [Fact]
    public void RF_160_staff_concede_so_o_que_tem_sem_administrar_usuarios()
    {
        var concediveis = CalculoDePermissoes.Concediveis(Pessoa("j", Papel.Staff), Mods(("MOD-03", "CU"), ("MOD-05", "U")),
            Pessoa("a", Papel.Staff), Escola("MOD-05", "MOD-06"));
        Assert.Equal(Mods(("MOD-05", "U")), concediveis);
    }

    [Fact]
    public void RF_160_administrador_concede_os_do_time()
    {
        var concediveis = CalculoDePermissoes.Concediveis(Pessoa("adm", Papel.Administrador, "conta-dboaz"), Mods(),
            Pessoa("c", Papel.MembroTime, "conta-dboaz"), Time());
        Assert.Equal(Mods(("MOD-02", "CU"), ("MOD-03", "CU"), ("MOD-14", "CU")), concediveis);
    }

    [Fact]
    public void Quem_o_alvo_usa()
    {
        Assert.True(CalculoDePermissoes.AlvoUsa(Pessoa("s", Papel.Staff), Registro[2]));
        Assert.False(CalculoDePermissoes.AlvoUsa(Pessoa("s", Papel.Staff), Registro[0]));
        Assert.True(CalculoDePermissoes.AlvoUsa(Pessoa("c", Papel.MembroTime), Registro[0]));
        Assert.False(CalculoDePermissoes.AlvoUsa(Pessoa("c", Papel.MembroTime), Registro[2]));
    }
}
