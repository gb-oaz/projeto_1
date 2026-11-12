using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /usuarios/card: o card guardado de quem pede (RN-189, RN-280, 15 RN-276).</summary>
public interface IObterCardUsuarios
{
    /// <summary>Lê o card pelo _id e refaz o que muda só com o tempo, sem gravar.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO para o participante.</exception>
    Task<CardDoModuloDto> Executar(QuemPedeDto pedido, CancellationToken ct = default);
}
