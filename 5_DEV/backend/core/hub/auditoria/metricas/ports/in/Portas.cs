using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Metricas.Dtos;

namespace Boaz.Core.Hub.Auditoria.Metricas.Ports.In;

/// <summary>GET /auditoria/metricas (RF-197): o tempo de resposta de todos os módulos ou de um.</summary>
public interface IVerMetricas
{
    /// <summary>As métricas da janela (1h padrão, 24h, 7d ou 30d) e do módulo; 503 sem o banco (RNF-252).</summary>
    Task<MetricasDto> Executar(string? janela, string? modulo, ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>GET /auditoria/metricas/enderecos (RF-198): o tempo de cada endereço, pelo modelo da rota.</summary>
public interface IVerTempoDosEnderecos
{
    /// <summary>Os endereços da janela, ordenados por p95 (padrão), pedidos ou erros; até o limite (20 padrão, 1 a 100).</summary>
    Task<EnderecosDto> Executar(string? janela, string? modulo, string? ordem, string? limite, ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>A gravação das medições a cada minuto pelo AgendamentoDaAuditoria (RNF-247).</summary>
public interface IGravarMedicoes
{
    /// <summary>Grava no banco as medições dos minutos fechados; com <paramref name="tudo"/>, também as do minuto de agora (o gancho de teste).</summary>
    Task Executar(bool tudo = false, CancellationToken ct = default);
}
