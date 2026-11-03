using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>PATCH /usuarios/{id}: nome, nome curto, matrícula e responsáveis (RF-153, RN-244).</summary>
public interface IEditarUsuario
{
    /// <summary>Edita o usuário.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO, DADOS_INVALIDOS, NOME_CURTO_EM_USO ou MATRICULA_EM_USO.</exception>
    Task<UsuarioEditadoDto> Executar(EdicaoDeUsuarioDto pedido, CancellationToken ct = default);
}
