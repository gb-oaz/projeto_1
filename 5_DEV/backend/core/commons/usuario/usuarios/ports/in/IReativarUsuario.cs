using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/reativar: 204, com as mesmas permissões (RF-112).</summary>
public interface IReativarUsuario
{
    /// <summary>Reativa o usuário, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou ESTADO_IGUAL.</exception>
    Task Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default);
}
