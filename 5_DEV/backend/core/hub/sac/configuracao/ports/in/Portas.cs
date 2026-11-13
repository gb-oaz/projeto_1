using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;

namespace Boaz.Core.Hub.Sac.Configuracao.Ports.In;

/// <summary>GET /sac/configuracao (RF-181): o time lê.</summary>
public interface ILerPrazos
{
    /// <summary>Os prazos de cada categoria.</summary>
    Task<PrazosDto> Executar(QuemPede quem, CancellationToken ct = default);
}

/// <summary>PUT /sac/configuracao (RF-181): o administrador salva.</summary>
public interface ISalvarPrazos
{
    /// <summary>Salva os prazos que mudam.</summary>
    Task<PrazosDto> Executar(PedidoPrazos pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}
