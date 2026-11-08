using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas/{id}/estado: ativa, suspensa, desativada ou encerrada (RF-113, RN-173 a RN-175).</summary>
public interface IMudarEstadoDaConta
{
    /// <summary>Muda o estado, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, CONTA_INEXISTENTE, DADOS_INVALIDOS ou TRANSICAO_INVALIDA.</exception>
    Task<ContaComEstadoDto> Executar(EstadoDaContaDto pedido, TraceId traceId, CancellationToken ct = default);
}
