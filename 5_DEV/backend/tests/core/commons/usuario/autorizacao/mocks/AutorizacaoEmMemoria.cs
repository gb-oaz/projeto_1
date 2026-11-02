using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;

/// <summary>
/// Mock de todas as portas de saída da autorização, em memória, já ligado aos
/// casos de uso. Guarda cópias das permissões (o que não for gravado não muda)
/// e grava permissões e registro juntos, só se a versão não mudou (RNF-223).
/// </summary>
public sealed class AutorizacaoEmMemoria : IUsuarios, IPermissoesGuardadas, IModulos, IContratacoes, ISessoesDoUsuario
{
    readonly Dictionary<string, UsuarioAutorizado> usuarios = [];
    readonly Dictionary<string, PermissoesDoUsuario> permissoes = [];
    readonly List<MudancaDePermissao> registro = [];
    readonly Dictionary<string, HashSet<string>> contratados = [];
    readonly List<ModuloRegistrado> modulos = [];

    /// <summary>Cria o mock e liga os casos de uso.</summary>
    public AutorizacaoEmMemoria()
    {
        CalcularPermissoes = new CalcularPermissoes(this, this, this, this, Relogio);
        CalcularPermissoesDoAnonimo = new CalcularPermissoesDoAnonimo();
        LerPermissoes = new LerPermissoes(this, this, this, this, Relogio);
        SalvarPermissoes = new SalvarPermissoes(this, this, this, this, this, Relogio);
        LerHistoricoDePermissoes = new LerHistoricoDePermissoes(this, this, this, this, Relogio);
        AvisarContratacaoMudou = new AvisarContratacaoMudou(this, this);
        AutorizarPedido = new AutorizarPedido();
    }

    /// <summary>O relógio dos casos de uso.</summary>
    public RelogioDeTeste Relogio { get; } = new();

    /// <summary>Papel, mods e pv do token.</summary>
    public ICalcularPermissoes CalcularPermissoes { get; }
    /// <summary>Permissões do acesso anônimo.</summary>
    public ICalcularPermissoesDoAnonimo CalcularPermissoesDoAnonimo { get; }
    /// <summary>GET /usuarios/{id}/permissoes.</summary>
    public ILerPermissoes LerPermissoes { get; }
    /// <summary>PUT /usuarios/{id}/permissoes.</summary>
    public ISalvarPermissoes SalvarPermissoes { get; }
    /// <summary>GET /usuarios/{id}/permissoes/historico.</summary>
    public ILerHistoricoDePermissoes LerHistoricoDePermissoes { get; }
    /// <summary>Para o Catálogo: a contratação mudou.</summary>
    public IAvisarContratacaoMudou AvisarContratacaoMudou { get; }
    /// <summary>A conferência de cada rota pelo token.</summary>
    public IAutorizarPedido AutorizarPedido { get; }

    // ── preparo do cenário ─────────────────────────────────────────────────────

    /// <summary>Registra um módulo.</summary>
    public void Registrar(ModuloRegistrado modulo) => modulos.Add(modulo);

    /// <summary>Um usuário.</summary>
    public UsuarioAutorizado NovoUsuario(string id, string contaId, string nome, Papel papel, string? criadoPor = null) =>
        usuarios[id] = new UsuarioAutorizado(id, contaId, nome, papel, criadoPor);

    /// <summary>Grava permissões direto, como o preparo pelo banco do QA ("na versão 7").</summary>
    public void Definir(string usuarioId, int versao, IReadOnlyDictionary<string, Abas>? mods = null, int? pv = null) =>
        permissoes[usuarioId] = new PermissoesDoUsuario(usuarioId, versao, pv ?? versao, mods ?? new Dictionary<string, Abas>());

    /// <summary>A conta contrata o módulo.</summary>
    public void Contratar(string contaId, string moduloId)
    {
        if (!contratados.TryGetValue(contaId, out var mods)) contratados[contaId] = mods = [];
        mods.Add(moduloId);
    }

    /// <summary>A contratação termina.</summary>
    public void Descontratar(string contaId, string moduloId) => contratados.GetValueOrDefault(contaId)?.Remove(moduloId);

    /// <summary>Outra pessoa salva entre a leitura e a gravação: a próxima gravação perde a corrida.</summary>
    public bool PerderAProximaCorrida { get; set; }

    // ── o que os testes conferem ───────────────────────────────────────────────

    /// <summary>O pv avisado a cada sessão, na ordem.</summary>
    public List<(string UsuarioId, int Pv)> Avisos { get; } = [];

    /// <summary>As permissões guardadas.</summary>
    public PermissoesDoUsuario? Guardadas(string usuarioId) => permissoes.GetValueOrDefault(usuarioId) is { } p ? Copia(p) : null;

    /// <summary>O registro de mudanças inteiro.</summary>
    public IReadOnlyList<MudancaDePermissao> Registro => registro;

    // ── portas ─────────────────────────────────────────────────────────────────

    Task<UsuarioAutorizado?> IUsuarios.Buscar(string id, CancellationToken ct) => Task.FromResult(usuarios.GetValueOrDefault(id));

    Task<PermissoesDoUsuario?> IPermissoesGuardadas.Buscar(string usuarioId, CancellationToken ct) => Task.FromResult(Guardadas(usuarioId));

    Task<bool> IPermissoesGuardadas.Salvar(PermissoesDoUsuario novas, MudancaDePermissao mudanca, int versaoLida, CancellationToken ct)
    {
        var atual = permissoes.GetValueOrDefault(novas.UsuarioId)?.Versao ?? 1;
        if (PerderAProximaCorrida)
        {
            PerderAProximaCorrida = false;
            Definir(novas.UsuarioId, atual + 1, permissoes.GetValueOrDefault(novas.UsuarioId)?.Mods);
            return Task.FromResult(false);
        }
        if (atual != versaoLida) return Task.FromResult(false);
        permissoes[novas.UsuarioId] = Copia(novas);
        registro.Add(mudanca);
        return Task.FromResult(true);
    }

    Task<IReadOnlyList<(string UsuarioId, int Pv)>> IPermissoesGuardadas.SubirPvDaConta(string contaId, CancellationToken ct)
    {
        var subiram = new List<(string, int)>();
        foreach (var usuario in usuarios.Values.Where(u => u.ContaId == contaId))
        {
            var p = permissoes.GetValueOrDefault(usuario.Id) ?? PermissoesDoUsuario.Vazias(usuario.Id);
            permissoes[usuario.Id] = new PermissoesDoUsuario(usuario.Id, p.Versao, p.Pv + 1, p.Mods);
            subiram.Add((usuario.Id, p.Pv + 1));
        }
        return Task.FromResult<IReadOnlyList<(string, int)>>(subiram);
    }

    Task<IReadOnlyList<MudancaDePermissao>> IPermissoesGuardadas.Historico(string usuarioId, DateTimeOffset? ate, int limite, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MudancaDePermissao>>(registro
            .Where(m => m.UsuarioId == usuarioId && (ate is null || m.Em < ate))
            .OrderByDescending(m => m.Em).ThenByDescending(m => m.Versao)
            .Take(limite).ToList());

    Task<IReadOnlyList<ModuloRegistrado>> IModulos.Registrados(CancellationToken ct) => Task.FromResult<IReadOnlyList<ModuloRegistrado>>(modulos);

    Task<IReadOnlySet<string>> IContratacoes.EmVigor(string contaId, DateTimeOffset agora, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(contratados.GetValueOrDefault(contaId)?.ToHashSet() ?? []);

    Task ISessoesDoUsuario.AvisarPv(string usuarioId, int pv, CancellationToken ct)
    {
        Avisos.Add((usuarioId, pv));
        return Task.CompletedTask;
    }

    static PermissoesDoUsuario Copia(PermissoesDoUsuario p) => new(p.UsuarioId, p.Versao, p.Pv, p.Mods.ToDictionary());
}
