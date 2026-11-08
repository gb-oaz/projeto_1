using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/novo-codigo-acesso: o anterior deixa de valer (RF-154, RN-245).</summary>
public interface IGerarNovoCodigoDeAcesso
{
    /// <summary>Gera o código, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou SO_PARTICIPANTE.</exception>
    Task<CodigoDeAcessoDto> Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default);
}
