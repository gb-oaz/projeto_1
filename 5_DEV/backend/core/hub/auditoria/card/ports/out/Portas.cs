using Boaz.Core.Hub.Auditoria.Card.Domain;

namespace Boaz.Core.Hub.Auditoria.Card.Ports.Out;

/// <summary>A coleção cardAuditoria (RN-322): um documento por dia de Brasília, fechado a cada minuto, guardado 400 dias.</summary>
public interface ICardDaAuditoria
{
    /// <summary>Guarda os números do dia; a versão sobe se eles mudaram. Devolve o dia como ficou.</summary>
    Task<DiaDoCard> Fechar(DiaDoCard dia, CancellationToken ct = default);

    /// <summary>Os dias guardados desde o dia dado até hoje.</summary>
    Task<IReadOnlyList<DiaDoCard>> Dias(DateOnly desde, DateOnly ate, CancellationToken ct = default);
}

/// <summary>O descarte pela guarda (RNF-248): sem TTL do banco, para valer no MongoDB e no Cosmos DB (RU).</summary>
public interface IDescarteDaAuditoria
{
    /// <summary>Apaga o que passou do guardarAte nas 6 coleções da Auditoria; auditoria e auditoriaErros nunca.</summary>
    Task ApagarVencidos(DateTimeOffset agora, CancellationToken ct = default);
}

/// <summary>O que o ao vivo confere a cada 5 s: a sessão do token e o acesso à Auditoria pelas permissões de agora.</summary>
public interface IAcessoDoAoVivo
{
    /// <summary>A sessão do token segue aberta.</summary>
    Task<bool> SessaoAberta(string sid, CancellationToken ct = default);

    /// <summary>O usuário ainda é ADMINISTRADOR ou MEMBRO_TIME com MOD-17 · U.</summary>
    Task<bool> PodeUsar(string usuarioId, CancellationToken ct = default);
}
