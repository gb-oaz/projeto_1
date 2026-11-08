using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>PUT /time/tempo-sessao: muda o tempo do time e das contas master (RF-152, RN-243).</summary>
public interface ISalvarTempoDoTime
{
    /// <summary>Salva o tempo, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, DADOS_INVALIDOS ou FORA_DOS_LIMITES.</exception>
    Task<TempoDoTimeLidoDto> Executar(TempoDoTimeDto pedido, TraceId traceId, CancellationToken ct = default);
}
