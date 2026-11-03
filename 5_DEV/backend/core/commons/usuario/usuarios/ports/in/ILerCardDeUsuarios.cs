using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /usuarios/card: os números do card do hub (RN-189).</summary>
public interface ILerCardDeUsuarios
{
    /// <summary>Lê o card.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO.</exception>
    Task<CardDeUsuariosDto> Executar(QuemPedeDto pedido, CancellationToken ct = default);
}
