using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/desativar: 204, e as sessões dele terminam (RF-112).</summary>
public interface IDesativarUsuario
{
    /// <summary>Desativa o usuário, auditado do começo ao fim com o traceId da execução (RN-178, RN-270, RN-271).</summary>
    /// <param name="pedido">Quem pede e o alvo.</param>
    /// <param name="traceId">A execução, que o endereço devolve no cabeçalho X-Trace-Id.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <exception cref="FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou ESTADO_IGUAL.</exception>
    Task Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default);
}
