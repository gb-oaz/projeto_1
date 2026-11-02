using System.Security.Cryptography;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Sessão de um aparelho (sessao:&lt;sid&gt;, autenticacao-classes). É a fonte da
/// verdade (RN-224): o token de acesso só prova que o aparelho é dono dela. Hub e
/// quest têm sessões separadas (RN-227), uma por aparelho (RN-225); a sessão
/// termina depois do tempo sem uso, e cada pedido a renova (RN-226, RNF-217).
/// </summary>
public sealed class Sessao
{
    /// <summary>Máximo de sessões do hub por usuário; a sexta encerra a mais antiga (RN-247).</summary>
    public const int LimiteDoHub = 5;

    /// <summary>
    /// Remonta uma sessão gravada. Para abrir uma nova, use <see cref="Abrir"/>.
    /// </summary>
    public Sessao(
        string sid, string usuarioId, string contaId, string sigla, Frente frente, Aparelho aparelho,
        QuemEntrou? quemEntrou, string? sala, int pv, string resumoRenovacao,
        DateTimeOffset inicio, DateTimeOffset ultimaAcao, TimeSpan? tempoSemUso)
    {
        Sid = sid;
        UsuarioId = usuarioId;
        ContaId = contaId;
        Sigla = sigla;
        Frente = frente;
        Aparelho = aparelho;
        QuemEntrou = quemEntrou;
        Sala = sala;
        Pv = pv;
        ResumoRenovacao = resumoRenovacao;
        Inicio = inicio;
        UltimaAcao = ultimaAcao;
        TempoSemUso = tempoSemUso;
    }

    /// <summary>Id da sessão, que vai no token (sid).</summary>
    public string Sid { get; }

    /// <summary>Usuário dono da sessão; na sala, o id do acesso anônimo (o sub do token).</summary>
    public string UsuarioId { get; }

    /// <summary>Conta da sessão.</summary>
    public string ContaId { get; }

    /// <summary>Sigla da conta.</summary>
    public string Sigla { get; }

    /// <summary>Hub, quest ou sala.</summary>
    public Frente Frente { get; }

    /// <summary>Navegador ou app.</summary>
    public Aparelho Aparelho { get; }

    /// <summary>No quest, quem entrou com o código; null no hub.</summary>
    public QuemEntrou? QuemEntrou { get; }

    /// <summary>Código da sala, só no acesso anônimo.</summary>
    public string? Sala { get; }

    /// <summary>Versão das permissões do token em uso; outro pv no token é recusado (RN-230).</summary>
    public int Pv { get; private set; }

    /// <summary>Resumo do token de renovação válido agora.</summary>
    public string ResumoRenovacao { get; private set; }

    /// <summary>Quando a sessão foi aberta.</summary>
    public DateTimeOffset Inicio { get; }

    /// <summary>Último pedido com a sessão: conta para o tempo sem uso e para a mais antiga do hub.</summary>
    public DateTimeOffset UltimaAcao { get; private set; }

    /// <summary>Tempo sem uso; null no acesso anônimo, que termina com a sala (RN-231).</summary>
    public TimeSpan? TempoSemUso { get; }

    /// <summary>Quando a sessão termina se não houver outro pedido; null no acesso anônimo.</summary>
    public DateTimeOffset? ExpiraEm => TempoSemUso is { } tempo ? UltimaAcao + tempo : null;

    /// <summary>
    /// Abre uma sessão nova, com um sid aleatório, e devolve o primeiro token de
    /// renovação dela, que só sai daqui (a sessão guarda o resumo).
    /// </summary>
    public static (Sessao Sessao, TokenDeRenovacao Renovacao) Abrir(
        string usuarioId, string contaId, string sigla, Frente frente, Aparelho aparelho,
        QuemEntrou? quemEntrou, string? sala, int pv, TimeSpan? tempoSemUso, DateTimeOffset agora)
    {
        var sid = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var renovacao = TokenDeRenovacao.Novo(sid);
        var sessao = new Sessao(sid, usuarioId, contaId, sigla, frente, aparelho, quemEntrou, sala, pv,
            renovacao.Resumo(), agora, agora, tempoSemUso);
        return (sessao, renovacao);
    }

    /// <summary>Ativa se o tempo sem uso não passou.</summary>
    public bool EstaAtiva(DateTimeOffset agora) => ExpiraEm is not { } fim || agora < fim;

    /// <summary>Um pedido com a sessão: refaz o tempo sem uso (RNF-217).</summary>
    public void Tocar(DateTimeOffset agora) => UltimaAcao = agora;

    /// <summary>O token apresentado é o último entregue? Outro, já trocado, é sinal de cópia (RNF-213).</summary>
    public bool RenovacaoConfere(TokenDeRenovacao apresentado) =>
        apresentado.Sid == Sid && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(apresentado.Resumo()), Convert.FromHexString(ResumoRenovacao));

    /// <summary>
    /// Troca o token de renovação e grava o pv das permissões recalculadas
    /// (RF-137, RN-230). O token anterior deixa de valer.
    /// </summary>
    public TokenDeRenovacao Renovar(int pv, DateTimeOffset agora)
    {
        var nova = TokenDeRenovacao.Novo(Sid);
        ResumoRenovacao = nova.Resumo();
        Pv = pv;
        Tocar(agora);
        return nova;
    }

    /// <summary>As permissões mudaram: o próximo pedido com o token antigo recebe PERMISSOES_DESATUALIZADAS.</summary>
    public void AtualizarPv(int pv) => Pv = pv;

    /// <summary>
    /// A sessão do hub que sai quando outra é aberta e o usuário já tem
    /// <see cref="LimiteDoHub"/>: a de última ação mais antiga (RN-247). O quest
    /// não tem limite.
    /// </summary>
    /// <param name="doUsuario">As sessões ativas do usuário, de qualquer frente.</param>
    public static Sessao? MaisAntigaAlemDoLimite(IEnumerable<Sessao> doUsuario)
    {
        var doHub = doUsuario.Where(s => s.Frente == Frente.Hub).ToList();
        return doHub.Count >= LimiteDoHub ? doHub.MinBy(s => s.UltimaAcao) : null;
    }
}
