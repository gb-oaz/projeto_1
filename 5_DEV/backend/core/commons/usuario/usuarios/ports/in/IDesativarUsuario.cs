using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/desativar: 204, e as sessões dele terminam (RF-112).</summary>
public interface IDesativarUsuario
{
    /// <summary>Desativa o usuário.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou ESTADO_IGUAL.</exception>
    Task Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default);
}
