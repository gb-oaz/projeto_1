using Boaz.Core.Hub.Sac.Card.Domain;

namespace Boaz.Core.Hub.Sac.Card.Ports.Out;

/// <summary>A contagem do card em chamados, agora (sac-documentos v01).</summary>
public interface IContagemDoSac
{
    /// <summary>Os números do escopo: a conta, ou null para todas (o time).</summary>
    Task<ContagemDoSac> Contar(string? contaId, DateTimeOffset agora, CancellationToken ct = default);

    /// <summary>As contas com chamado, para o fechamento do dia.</summary>
    Task<IReadOnlyList<string>> ContasComChamado(CancellationToken ct = default);
}

/// <summary>A coleção cardSac: os dias fechados à 00:00 (15 RN-278), para as séries.</summary>
public interface ISeriesDoSac
{
    /// <summary>Em andamento no fim de cada dia fechado do escopo, desde o dia dado.</summary>
    Task<IReadOnlyDictionary<DateOnly, int>> Fechados(string escopo, DateOnly desde, CancellationToken ct = default);

    /// <summary>Guarda o fim do dia do escopo (um documento por escopo e dia).</summary>
    Task Fechar(string escopo, DateOnly dia, ContagemDoSac contagem, CancellationToken ct = default);
}
