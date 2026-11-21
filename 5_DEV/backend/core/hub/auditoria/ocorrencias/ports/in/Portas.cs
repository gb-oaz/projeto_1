using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Dtos;

namespace Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.In;

/// <summary>GET /auditoria/ocorrencias/abertas (RF-204): a faixa ao abrir, pelo Redis, sem o banco (RNF-252); não é auditada.</summary>
public interface IVerOcorrenciasAbertas
{
    /// <summary>As abertas e as voltas dos últimos 10 min.</summary>
    Task<AbertasDto> Executar(CancellationToken ct = default);
}

/// <summary>GET /auditoria/ocorrencias (RF-205): as ocorrências do período.</summary>
public interface IListarOcorrencias
{
    /// <summary>A lista filtrada, da mais nova para a mais antiga; 503 sem o banco (RNF-252).</summary>
    Task<OcorrenciasDto> Executar(
        string? de, string? ate, string? tipo, string? alvo, string? para, string? depois, string? limite, ConsultaPedida consulta,
        CancellationToken ct = default);
}

/// <summary>GET /auditoria/ocorrencias/ao-vivo (RF-204, RNF-249): as abertas ao abrir e cada ocorrência nova; não é auditada.</summary>
public interface IAcompanharOcorrenciasAoVivo
{
    /// <summary>Abre a conexão (conta entre as 3 do usuário).</summary>
    Task<AoVivoAberto> Abrir(QuemPede quem, CancellationToken ct = default);
}
