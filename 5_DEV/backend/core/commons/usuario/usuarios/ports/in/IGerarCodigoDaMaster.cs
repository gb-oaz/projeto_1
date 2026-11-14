using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas/{id}/codigo-master: o SAC confere a escola e gera o código da conta master (RN-172).</summary>
public interface IGerarCodigoDaMaster
{
    /// <summary>Gera o código, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, CONTA_INEXISTENTE, DADOS_INVALIDOS ou CONFERENCIA_INCOMPLETA.</exception>
    Task<CodigoDaMasterDto> Executar(ConferenciaDaMasterDto pedido, TraceId traceId, CancellationToken ct = default);
}
