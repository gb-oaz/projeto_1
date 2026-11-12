using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;

/// <summary>
/// Mock das portas do card do Usuários, em memória: os cards guardados, a
/// contagem (números definidos pelo teste), as conexões ao vivo, a trava do
/// fechamento, a sessão e o MOD-03 e as preferências.
/// </summary>
public sealed class CardsEmMemoria : IRepositorioCardUsuarios, IContagemDoCard, IConexoesAoVivo, IAcessoAoVivo, ITravaDoFechamento, IPreferencias
{
    readonly Dictionary<string, CardUsuarios> cards = [];
    readonly Dictionary<string, PreferenciasDeCards> preferencias = [];
    readonly HashSet<DateOnly> travas = [];

    // ── preparo ────────────────────────────────────────────────────────────────

    /// <summary>Os números que a contagem devolve, por perfil, conta e staff.</summary>
    public Dictionary<(PerfilCard, string, string?), NumerosDoCard> Numeros { get; } = [];

    /// <summary>O que muda só com o tempo, por perfil, conta e staff.</summary>
    public Dictionary<(PerfilCard, string, string?), NumerosNoTempo> Tempo { get; } = [];

    /// <summary>Sessões que terminaram.</summary>
    public HashSet<string> SessoesTerminadas { get; } = [];

    /// <summary>Quem perdeu o MOD-03.</summary>
    public HashSet<string> SemOCard { get; } = [];

    // ── o que os testes conferem ───────────────────────────────────────────────

    /// <summary>Um card guardado.</summary>
    public CardUsuarios? Card(string id) => cards.GetValueOrDefault(id);

    /// <summary>Quantas vezes cada card foi gravado.</summary>
    public Dictionary<string, int> Gravacoes { get; } = [];

    /// <summary>As conexões abertas de cada usuário, da mais antiga à mais nova.</summary>
    public Dictionary<string, List<string>> Abertas { get; } = [];

    /// <summary>As conexões encerradas e por quê.</summary>
    public Dictionary<string, MotivoEncerramento> Encerradas { get; } = [];

    /// <summary>Quantas renovações de prazo.</summary>
    public int Renovacoes { get; private set; }

    /// <summary>Quanto tempo cada espera pediu.</summary>
    public List<TimeSpan> Esperas { get; } = [];

    /// <summary>Chamado a cada espera, para o teste mudar o mundo enquanto o tempo passa.</summary>
    public Action<TimeSpan>? AoEsperar { get; set; }

    // ── IRepositorioCardUsuarios ───────────────────────────────────────────────

    Task<CardUsuarios?> IRepositorioCardUsuarios.Obter(string id, CancellationToken ct) => Task.FromResult(cards.GetValueOrDefault(id));

    Task IRepositorioCardUsuarios.Salvar(CardUsuarios card, CancellationToken ct)
    {
        cards[card.Id] = card;
        Gravacoes[card.Id] = Gravacoes.GetValueOrDefault(card.Id) + 1;
        return Task.CompletedTask;
    }

    Task<int?> IRepositorioCardUsuarios.Versao(string id, CancellationToken ct) => Task.FromResult(cards.GetValueOrDefault(id)?.Versao);

    Task<IReadOnlyList<CardUsuarios>> IRepositorioCardUsuarios.Todos(CancellationToken ct) => Task.FromResult<IReadOnlyList<CardUsuarios>>([.. cards.Values]);

    // ── IContagemDoCard ────────────────────────────────────────────────────────

    Task<NumerosDoCard> IContagemDoCard.Contar(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora, CancellationToken ct) =>
        Task.FromResult(Numeros.GetValueOrDefault((perfil, contaId, usuarioId)) ?? new NumerosDoCard(0, 0, [], []));

    Task<NumerosNoTempo> IContagemDoCard.NoTempo(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora, CancellationToken ct) =>
        Task.FromResult(Tempo.GetValueOrDefault((perfil, contaId, usuarioId)) ?? new NumerosNoTempo(0, new Dictionary<string, int>()));

    // ── IConexoesAoVivo ────────────────────────────────────────────────────────

    Task<IReadOnlyList<string>> IConexoesAoVivo.Abrir(string usuarioId, string conexaoId, CancellationToken ct)
    {
        var abertas = Abertas.TryGetValue(usuarioId, out var lista) ? lista : Abertas[usuarioId] = [];
        abertas.Add(conexaoId);
        var sobra = abertas.Take(Math.Max(0, abertas.Count - 3)).ToList();
        abertas.RemoveAll(sobra.Contains);
        return Task.FromResult<IReadOnlyList<string>>(sobra);
    }

    Task IConexoesAoVivo.Encerrar(string conexaoId, MotivoEncerramento motivo, CancellationToken ct)
    {
        Encerradas[conexaoId] = motivo;
        return Task.CompletedTask;
    }

    Task IConexoesAoVivo.Renovar(string usuarioId, string conexaoId, CancellationToken ct)
    {
        Renovacoes++;
        return Task.CompletedTask;
    }

    Task IConexoesAoVivo.Fechar(string usuarioId, string conexaoId, CancellationToken ct)
    {
        if (Abertas.TryGetValue(usuarioId, out var abertas)) abertas.Remove(conexaoId);
        return Task.CompletedTask;
    }

    Task<MotivoEncerramento?> IConexoesAoVivo.Esperar(string conexaoId, TimeSpan tempo, CancellationToken ct)
    {
        Esperas.Add(tempo);
        AoEsperar?.Invoke(tempo);
        return Task.FromResult(Encerradas.TryGetValue(conexaoId, out var motivo) ? motivo : (MotivoEncerramento?)null);
    }

    // ── IAcessoAoVivo ──────────────────────────────────────────────────────────

    Task<bool> IAcessoAoVivo.SessaoAberta(string sid, CancellationToken ct) => Task.FromResult(!SessoesTerminadas.Contains(sid));

    Task<bool> IAcessoAoVivo.PodeVerOCard(string usuarioId, CancellationToken ct) => Task.FromResult(!SemOCard.Contains(usuarioId));

    // ── ITravaDoFechamento ─────────────────────────────────────────────────────

    Task<bool> ITravaDoFechamento.Pegar(DateOnly dia, bool renovar, CancellationToken ct) => Task.FromResult(travas.Add(dia) || renovar);

    // ── IPreferencias ──────────────────────────────────────────────────────────

    Task<PreferenciasDeCards?> IPreferencias.Ler(string usuarioId, CancellationToken ct) => Task.FromResult(preferencias.GetValueOrDefault(usuarioId));

    Task IPreferencias.Salvar(string usuarioId, PreferenciasDeCards p, CancellationToken ct)
    {
        preferencias[usuarioId] = p;
        return Task.CompletedTask;
    }
}
