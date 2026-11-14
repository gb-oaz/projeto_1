using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/renovar: troca o token de renovação por tokens novos (RF-137, RNF-213).</summary>
public interface IRenovarSessao
{
    /// <summary>Renova a sessão, com as permissões recalculadas.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    Task<SessaoAbertaDto> Executar(RenovacaoDto pedido, TraceId traceId, CancellationToken ct = default);
}
