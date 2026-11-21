using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Dtos;

namespace Boaz.Core.Hub.Auditoria.Saude.Ports.In;

/// <summary>GET /auditoria/saude (RF-194, RF-196, RNF-252): lida da última verificação desta instância, sem o banco.</summary>
public interface IVerSaude
{
    /// <summary>A saúde dos serviços, dos módulos e das instâncias; grava a consulta (RN-325) se o banco está no ar.</summary>
    Task<SaudeDto> Executar(ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>GET /auditoria/saude/{servico}/historico (RF-195).</summary>
public interface IVerHistoricoDoServico
{
    /// <summary>O histórico do serviço: 24h (padrão), 7d ou 30d.</summary>
    Task<HistoricoDto> Executar(string servico, string? periodo, ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>GET /auditoria/saude/ao-vivo (RNF-246, RNF-249): a saúde ao abrir e a cada verificação, mais as ocorrências.</summary>
public interface IAcompanharSaudeAoVivo
{
    /// <summary>Abre a conexão (até 3 por usuário) e grava a consulta uma vez.</summary>
    Task<AoVivoAberto> Abrir(ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>A verificação dos serviços e dos módulos, a cada 30 s pelo AgendamentoDaAuditoria e na hora pelo gancho de teste (RNF-246).</summary>
public interface IVerificarServicos
{
    /// <summary>
    /// Verifica agora, cada serviço com até 3 s. Só a instância com a trava guarda o histórico, as quedas e as
    /// ocorrências, a menos que <paramref name="forcar"/> (o gancho de teste).
    /// </summary>
    Task<SaudeDoSistema> Executar(bool forcar = false, CancellationToken ct = default);
}
