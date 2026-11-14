using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>PATCH /usuarios/{id}: nome, nome curto, matrícula e responsáveis (RF-153, RN-244).</summary>
public interface IEditarUsuario
{
    /// <summary>Edita o usuário, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO, DADOS_INVALIDOS, NOME_CURTO_EM_USO ou MATRICULA_EM_USO.</exception>
    Task<UsuarioEditadoDto> Executar(EdicaoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default);
}
