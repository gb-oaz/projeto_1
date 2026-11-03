using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/reativar: 204, com as mesmas permissões (RF-112).</summary>
public interface IReativarUsuario
{
    /// <summary>Reativa o usuário.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou ESTADO_IGUAL.</exception>
    Task Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default);
}
