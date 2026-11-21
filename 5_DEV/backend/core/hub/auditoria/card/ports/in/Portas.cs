using Boaz.Core.Hub.Auditoria.Card.Dtos;

namespace Boaz.Core.Hub.Auditoria.Card.Ports.In;

/// <summary>GET /auditoria/card (RN-322): o card do time deboaz; não é auditado.</summary>
public interface IVerCardDaAuditoria
{
    /// <summary>O card com a normal, a reduzida, a expandida e as séries de dia, semana e mês.</summary>
    Task<CardDaAuditoriaDto> Executar(CancellationToken ct = default);
}

/// <summary>GET /auditoria/card/ao-vivo (RN-322, 15 RN-279): o card ao abrir e, a cada intervalo, só se a versão mudou.</summary>
public interface IAcompanharCardDaAuditoriaAoVivo
{
    /// <summary>O intervalo conferido (5, 15, 30, 60 ou 300 s; padrão 300) e o primeiro card.</summary>
    Task<(TimeSpan Intervalo, CardDaAuditoriaDto Card)> Abrir(string? intervalo, CancellationToken ct = default);

    /// <summary>O card, se a versão mudou desde a dada; null se não.</summary>
    Task<CardDaAuditoriaDto?> Proximo(long versao, CancellationToken ct = default);
}

/// <summary>O fechamento do minuto do card e o descarte do que passou da guarda, pelo AgendamentoDaAuditoria (RN-322, RNF-248).</summary>
public interface IFecharCardEDescartar
{
    /// <summary>Fecha o dia do card até agora e, com <paramref name="descartar"/>, apaga o vencido das 6 coleções da Auditoria.</summary>
    Task Executar(bool descartar, CancellationToken ct = default);
}
