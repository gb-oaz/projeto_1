namespace Boaz.Core.Hub.Auditoria.Card.Dtos;

/// <summary>
/// Quem consulta a Auditoria, como o token conferido diz. O acesso (ADMINISTRADOR ou MEMBRO_TIME com MOD-17 · U,
/// RN-314) já foi conferido pela rota; o caso de uso só usa o usuário e a sessão.
/// </summary>
/// <param name="UsuarioId">O sub do token.</param>
/// <param name="Sid">A sessão do token.</param>
/// <param name="Papel">ADMINISTRADOR ou MEMBRO_TIME.</param>
public sealed record QuemPede(string UsuarioId, string Sid, string Papel);

/// <summary>
/// Uma consulta à Auditoria que fica registrada (RN-325): a pessoa, o endereço lido com os parâmetros e a
/// execução (o X-Trace-Id da resposta).
/// </summary>
/// <param name="Quem">Quem consulta.</param>
/// <param name="Alvo">O endereço com os parâmetros em ordem alfabética, sem depois nem limite.</param>
/// <param name="TraceId">A execução, devolvida no cabeçalho X-Trace-Id.</param>
public sealed record ConsultaPedida(QuemPede Quem, string Alvo, Commons.Auditoria.Domain.TraceId TraceId);

/// <summary>A conexão ao vivo aberta: os primeiros eventos e a fonte dos seguintes, até a conexão fechar ou ser encerrada.</summary>
public abstract class AoVivoAberto
{
    /// <summary>Os eventos, na hora da abertura e depois, até o fim; fecha a conexão ao terminar.</summary>
    public abstract IAsyncEnumerable<EventoAoVivo> Eventos(CancellationToken ct);
}

/// <summary>Um evento do fluxo ao vivo (RNF-246, RNF-249): o tipo, o id (opcional) e o corpo, que o adaptador serializa.</summary>
/// <param name="Tipo">saude, registro, ocorrencia, abertas, card, encerrado ou ping.</param>
/// <param name="Id">O id do evento no fluxo, se há.</param>
/// <param name="Corpo">O que vai em data; null no ping.</param>
public sealed record EventoAoVivo(string Tipo, string? Id, object? Corpo)
{
    /// <summary>O ": ping" que mantém a conexão aberta.</summary>
    public static readonly EventoAoVivo Ping = new("ping", null, null);

    /// <summary>O "encerrado" com o motivo (LIMITE_CONEXOES, SESSAO_TERMINADA ou ACESSO_NEGADO).</summary>
    public static EventoAoVivo Encerrado(string codigo) => new("encerrado", null, new { codigo });
}
