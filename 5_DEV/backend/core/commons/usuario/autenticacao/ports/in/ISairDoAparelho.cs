using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/sair: encerra a sessão do token; as dos outros aparelhos continuam (RF-138).</summary>
public interface ISairDoAparelho
{
    /// <summary>
    /// Encerra a sessão do token, já conferido antes do caso de uso: o token
    /// inválido é recusado sem traceId nem registro (RN-273).
    /// </summary>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    Task Executar(AcessoConferido acesso, TraceId traceId, CancellationToken ct = default);
}
