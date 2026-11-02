using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /usuarios/{id}: o cadastro, sem senha nem código de liberação (RN-170, RN-186).</summary>
public interface ILerUsuario
{
    /// <summary>Lê o usuário.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE ou ACESSO_NEGADO.</exception>
    Task<UsuarioLidoDto> Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default);
}
