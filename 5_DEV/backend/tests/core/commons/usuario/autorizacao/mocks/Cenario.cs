using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using static Boaz.Core.Commons.Usuario.Autorizacao.Domain.FrenteDeUso;
using static Boaz.Core.Commons.Usuario.Autorizacao.Domain.Perfil;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;

/// <summary>
/// O mundo dos exemplos do requisito 17-b: o registro de módulos do 15_modulos,
/// a escola EBFGA (conta master, Maria na versão 7, João, Rui com Administrar
/// usuários, Ana criada pelo João na versão 2 e o participante Lucas), o
/// autônomo Pedro e o time deboaz (administrador e Caio).
/// </summary>
public sealed class Cenario
{
    /// <summary>Monta o mundo.</summary>
    public Cenario()
    {
        Modulo("MOD-01", "Contas", TipoDeModulo.DaConta, (Master, Hub), (Autonomo, Hub));
        Modulo("MOD-02", "SAC", TipoDeModulo.DaConta, (Master, Hub), (Autonomo, Hub), (MembroTime, Hub));
        Modulo("MOD-03", "Usuários", TipoDeModulo.DaConta, (Administrador, Hub), (MembroTime, Hub), (Master, Hub), (Autonomo, Hub), (Staff, Hub));
        Modulo("MOD-04", "Sugestão", TipoDeModulo.DaConta, (Staff, Hub), (Master, Hub));
        Modulo("MOD-05", "Quiz", TipoDeModulo.Contratavel, (Staff, Hub), (Autonomo, Hub), (Participante, Quest), (Anonimo, Quest));
        Modulo("MOD-06", "Presença", TipoDeModulo.Contratavel, (Master, Hub), (Staff, Hub), (Participante, Quest), (Responsavel, Quest));
        Modulo("MOD-07", "Carteirinha", TipoDeModulo.Contratavel, (Master, Hub), (Participante, Quest), (Responsavel, Quest));
        Modulo("MOD-08", "Turmas", TipoDeModulo.DaConta, (Master, Hub), (Staff, Hub));
        Modulo("MOD-09", "Tarefas", TipoDeModulo.DaConta, (Staff, Hub), (Autonomo, Hub), (Participante, Quest));
        Modulo("MOD-12", "Agenda", TipoDeModulo.Contratavel, (Master, Hub), (Participante, Quest), (Responsavel, Quest));
        Modulo("MOD-13", "Perfil", TipoDeModulo.DaConta, (Participante, Quest), (Responsavel, Quest));
        Modulo("MOD-14", "Catálogo", TipoDeModulo.DoTime, (Administrador, Hub), (MembroTime, Hub));

        Mock.NovoUsuario("usr-dboaz-admin", "conta-dboaz", "Administrador do deboaz", Papel.Administrador);
        Mock.NovoUsuario("usr-dboaz-caio", "conta-dboaz", "Caio Lima", Papel.MembroTime, "usr-dboaz-admin");
        Mock.NovoUsuario("usr-ebfga-master", "conta-ebfga", "Escola Bento Freitas", Papel.Master);
        Mock.NovoUsuario("usr-preis-pedro", "conta-preis", "Pedro Reis", Papel.Autonomo);
        Mock.NovoUsuario("usr-ebfga-maria", "conta-ebfga", "Maria Souza", Papel.Staff, "usr-ebfga-master");
        Mock.NovoUsuario("usr-ebfga-joao", "conta-ebfga", "João Lima", Papel.Staff, "usr-ebfga-master");
        Mock.NovoUsuario("usr-ebfga-rui", "conta-ebfga", "Rui Alves", Papel.Staff, "usr-ebfga-master");
        Mock.NovoUsuario("usr-ebfga-ana", "conta-ebfga", "Ana Costa", Papel.Staff, "usr-ebfga-joao");
        Mock.NovoUsuario("usr-ebfga-lucas", "conta-ebfga", "Lucas Andrade", Papel.Participante, "usr-ebfga-master");
        Mock.NovoUsuario("usr-colsa-bruno", "conta-colsa", "Bruno Dias", Papel.Staff);
        Mock.Definir("usr-ebfga-maria", 7);
        Mock.Definir("usr-ebfga-ana", 2);
        Mock.Definir("usr-ebfga-rui", 2, Mods(("MOD-03", "CU")));
    }

    /// <summary>As portas em memória e os casos de uso.</summary>
    public AutorizacaoEmMemoria Mock { get; } = new();

    /// <summary>Um mapa de módulos a partir de pares ("MOD-05", "U").</summary>
    public static IReadOnlyDictionary<string, Abas> Mods(params (string Mod, string Abas)[] pares) =>
        pares.ToDictionary(p => p.Mod, p => Abas.De(p.Abas)!.Value);

    /// <summary>O mesmo mapa, em texto, como no corpo do PUT.</summary>
    public static IReadOnlyDictionary<string, string?> Corpo(params (string Mod, string? Abas)[] pares) =>
        pares.ToDictionary(p => p.Mod, p => p.Abas);

    /// <summary>Lê as permissões do alvo, como quem pede.</summary>
    public Task<PermissoesLidasDto> Ler(string quem, string alvo) => Mock.LerPermissoes.Executar(new PedidoDePermissoesDto(quem, alvo));

    /// <summary>Salva as permissões do alvo, como quem pede, com a versão lida.</summary>
    public Task<PermissoesSalvasDto> Salvar(string quem, string alvo, string? ifMatch, params (string Mod, string? Abas)[] mods) =>
        Mock.SalvarPermissoes.Executar(new SalvamentoDePermissoesDto(quem, alvo, ifMatch, Corpo(mods)), TraceId.Novo());

    /// <summary>Salva com a versão atual, como a conta master (ou o administrador para o time).</summary>
    public async Task<PermissoesSalvasDto> Conceder(string alvo, params (string Mod, string? Abas)[] mods)
    {
        var quem = alvo.StartsWith("usr-dboaz", StringComparison.Ordinal) ? "usr-dboaz-admin" : "usr-ebfga-master";
        var versao = (await Ler(quem, alvo)).Versao;
        return await Salvar(quem, alvo, $"\"{versao}\"", mods);
    }

    /// <summary>Papel, mods e pv do token.</summary>
    public Task<PermissoesNoTokenDto> Token(string usuarioId) => Mock.CalcularPermissoes.Executar(usuarioId);

    /// <summary>Confere que a ação falha com o código dado.</summary>
    public static async Task<FalhaDeAutorizacao> Falha(string codigo, Func<Task> acao)
    {
        var falha = await Assert.ThrowsAsync<FalhaDeAutorizacao>(acao);
        Assert.Equal(codigo, falha.Codigo);
        return falha;
    }

    void Modulo(string id, string nome, TipoDeModulo tipo, params (Perfil, FrenteDeUso)[] quemUsa) =>
        Mock.Registrar(new ModuloRegistrado(id, nome, tipo, quemUsa.ToHashSet()));
}
