using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>DELETE /auth/sessoes: sair de todos os aparelhos (RF-157).</summary>
public interface IEncerrarTodasAsSessoes
{
    /// <summary>Encerra todas as sessões de quem pede, inclusive a do próprio token, já conferido antes do caso de uso.</summary>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    Task Executar(AcessoConferido acesso, TraceId traceId, CancellationToken ct = default);
}
