using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/nova-geracao-senha: o superior pede uma senha nova (RF-110, RNF-203).</summary>
public interface IPedirNovaGeracaoDeSenha
{
    /// <summary>Pede a geração, auditada do começo ao fim com o traceId da execução (RN-178, RN-270, RN-271).</summary>
    /// <param name="pedido">Quem pede e o alvo.</param>
    /// <param name="traceId">A execução, que o endereço devolve no cabeçalho X-Trace-Id.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <exception cref="FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou SEM_SENHA.</exception>
    Task<CodigoDeLiberacaoDto> Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default);
}
