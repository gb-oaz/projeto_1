using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /usuarios: os usuários de um tipo da conta de quem pede, 50 por página, por nome (RN-185).</summary>
public interface IListarUsuarios
{
    /// <summary>Lista os usuários.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO ou DADOS_INVALIDOS.</exception>
    Task<PaginaDto<UsuarioNaListaDto>> Executar(ListaDeUsuariosDto pedido, CancellationToken ct = default);
}
