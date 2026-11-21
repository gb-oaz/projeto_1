namespace Boaz.Core.Hub.Auditoria.Metricas.Dtos;

/// <summary>O resumo da janela em GET /auditoria/metricas (RF-197).</summary>
/// <param name="Pedidos">Quantos pedidos.</param>
/// <param name="PorMinuto">Pedidos por minuto, com 1 casa.</param>
/// <param name="P50Ms">O p50; null acima de 10 s ou sem pedido.</param>
/// <param name="P95Ms">O p95.</param>
/// <param name="P99Ms">O p99.</param>
/// <param name="Erros4xx">Com 4xx.</param>
/// <param name="Erros5xx">Com 5xx.</param>
/// <param name="TaxaErro5xx">Os 5xx sobre os pedidos, em %; null sem pedido.</param>
public sealed record ResumoDto(
    long Pedidos, decimal PorMinuto, int? P50Ms, int? P95Ms, int? P99Ms, long Erros4xx, long Erros5xx, decimal? TaxaErro5xx);

/// <summary>Um ponto da série das métricas (RF-197).</summary>
/// <param name="Inicio">O começo do minuto, da hora ou do dia.</param>
/// <param name="Pedidos">Quantos pedidos.</param>
/// <param name="P95Ms">O p95; null sem pedido.</param>
/// <param name="Erros5xx">Com 5xx.</param>
public sealed record PontoDto(DateTimeOffset Inicio, long Pedidos, int? P95Ms, long Erros5xx);

/// <summary>200 de GET /auditoria/metricas (RF-197).</summary>
/// <param name="Janela">1h, 24h, 7d ou 30d.</param>
/// <param name="Modulo">O módulo pedido; null para todos.</param>
/// <param name="De">O começo da janela.</param>
/// <param name="Ate">O fim da janela.</param>
/// <param name="Resumo">Os números da janela.</param>
/// <param name="Serie">60, 24, 7 ou 30 pontos, do mais antigo ao atual.</param>
public sealed record MetricasDto(string Janela, string? Modulo, DateTimeOffset De, DateTimeOffset Ate, ResumoDto Resumo, IReadOnlyList<PontoDto> Serie);

/// <summary>Um endereço em GET /auditoria/metricas/enderecos (RF-198).</summary>
/// <param name="Metodo">GET, POST…</param>
/// <param name="Endereco">O modelo da rota.</param>
/// <param name="Modulo">O módulo.</param>
/// <param name="Pedidos">Quantos pedidos.</param>
/// <param name="P50Ms">O p50.</param>
/// <param name="P95Ms">O p95.</param>
/// <param name="P99Ms">O p99.</param>
/// <param name="Erros4xx">Com 4xx.</param>
/// <param name="Erros5xx">Com 5xx.</param>
/// <param name="Lento">O p95 passa de 1 s, ou está acima de 10 s.</param>
public sealed record EnderecoDto(
    string Metodo, string Endereco, string? Modulo, long Pedidos, int? P50Ms, int? P95Ms, int? P99Ms, long Erros4xx, long Erros5xx, bool Lento);

/// <summary>200 de GET /auditoria/metricas/enderecos (RF-198).</summary>
/// <param name="Janela">1h, 24h, 7d ou 30d.</param>
/// <param name="Modulo">O módulo pedido; null para todos.</param>
/// <param name="Ordem">p95, pedidos ou erros.</param>
/// <param name="Itens">Até 20 por padrão, até 100.</param>
public sealed record EnderecosDto(string Janela, string? Modulo, string Ordem, IReadOnlyList<EnderecoDto> Itens);
