namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// Uma conexão ao vivo aberta (RN-281, 15 RN-279): de quem, com qual sessão e
/// qual card, e quando mandar a próxima conferência e o próximo ": ping".
/// </summary>
public sealed class ConexaoAoVivo
{
    /// <summary>A cada quanto o ": ping" mantém a conexão aberta, em qualquer intervalo.</summary>
    public static readonly TimeSpan Ping = TimeSpan.FromSeconds(30);

    /// <summary>Abre a conexão agora, já com o primeiro card mandado.</summary>
    public ConexaoAoVivo(string id, string usuarioId, string sid, string cardId, TimeSpan intervalo, int versao, DateTimeOffset agora)
    {
        Id = id;
        UsuarioId = usuarioId;
        Sid = sid;
        CardId = cardId;
        Intervalo = intervalo;
        Versao = versao;
        ProximaConferencia = agora + intervalo;
        ProximoPing = agora + Ping;
    }

    /// <summary>conexaoId (GUID).</summary>
    public string Id { get; }

    /// <summary>De quem.</summary>
    public string UsuarioId { get; }

    /// <summary>A sessão do token da abertura.</summary>
    public string Sid { get; }

    /// <summary>O _id do card acompanhado.</summary>
    public string CardId { get; }

    /// <summary>O intervalo pedido.</summary>
    public TimeSpan Intervalo { get; }

    /// <summary>A versao do último card mandado.</summary>
    public int Versao { get; private set; }

    /// <summary>Quando conferir de novo a sessão, o MOD-03 e a versao.</summary>
    public DateTimeOffset ProximaConferencia { get; private set; }

    /// <summary>Quando mandar o próximo ": ping".</summary>
    public DateTimeOffset ProximoPing { get; private set; }

    /// <summary>A próxima hora em que há algo a fazer.</summary>
    public DateTimeOffset ProximaAcao => ProximaConferencia < ProximoPing ? ProximaConferencia : ProximoPing;

    /// <summary>Hora do ping: marca o seguinte, 30 s depois de agora.</summary>
    public bool HoraDoPing(DateTimeOffset agora)
    {
        if (agora < ProximoPing) return false;
        ProximoPing = agora + Ping;
        return true;
    }

    /// <summary>Hora de conferir: marca a seguinte, um intervalo depois de agora.</summary>
    public bool HoraDeConferir(DateTimeOffset agora)
    {
        if (agora < ProximaConferencia) return false;
        ProximaConferencia = agora + Intervalo;
        return true;
    }

    /// <summary>Um card novo foi mandado.</summary>
    public void Mandou(int versao) => Versao = versao;
}
