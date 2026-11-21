using Boaz.Core.Hub.Auditoria.Metricas.Domain;

namespace Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;

/// <summary>Os pedidos de um endereço numa janela, somados no banco (RF-198).</summary>
/// <param name="Metodo">GET, POST…</param>
/// <param name="Endereco">O modelo da rota.</param>
/// <param name="Modulo">O módulo; null fora dos módulos.</param>
/// <param name="Resumo">Os pedidos somados.</param>
public sealed record MedicaoDoEndereco(string Metodo, string Endereco, string? Modulo, ResumoDeMedicoes Resumo);

/// <summary>Os pedidos de um passo da janela (um minuto, uma hora ou um dia).</summary>
/// <param name="Inicio">O começo do passo.</param>
/// <param name="Resumo">Os pedidos somados.</param>
public sealed record PontoDaMedicao(DateTimeOffset Inicio, ResumoDeMedicoes Resumo);

/// <summary>
/// As coleções medicoesMinuto e medicoesHora (RNF-247, RNF-248): cada instância soma o seu minuto e a sua hora. As leituras
/// somam no banco, sem trazer cada hora à aplicação (RNF-250).
/// </summary>
public interface IMedicoes
{
    /// <summary>Soma as medições no minuto e na hora delas ($inc com upsert).</summary>
    Task Somar(IReadOnlyList<Medicao> medicoes, CancellationToken ct = default);

    /// <summary>Os pedidos somados de cada módulo entre as duas horas, de medicoesMinuto (a verificação dos últimos 5 min).</summary>
    Task<IReadOnlyDictionary<string, ResumoDeMedicoes>> PorModulo(DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default);

    /// <summary>O resumo da janela; do módulo ou de todos.</summary>
    Task<ResumoDeMedicoes> Resumo(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default);

    /// <summary>Os pedidos de cada passo da janela que tem pedido.</summary>
    Task<IReadOnlyList<PontoDaMedicao>> Serie(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default);

    /// <summary>Os pedidos somados de cada endereço da janela.</summary>
    Task<IReadOnlyList<MedicaoDoEndereco>> PorEndereco(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default);
}
