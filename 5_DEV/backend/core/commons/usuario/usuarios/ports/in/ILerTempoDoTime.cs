using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /time/tempo-sessao: o tempo do time e das contas master (RF-152, RN-243).</summary>
public interface ILerTempoDoTime
{
    /// <summary>Lê o tempo.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO.</exception>
    Task<TempoDoTimeLidoDto> Executar(QuemPedeDto pedido, CancellationToken ct = default);
}
