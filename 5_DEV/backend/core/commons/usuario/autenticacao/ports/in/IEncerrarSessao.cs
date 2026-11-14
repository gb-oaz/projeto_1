using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>DELETE /auth/sessoes/{sid}: sair de um aparelho da lista (RF-157).</summary>
public interface IEncerrarSessao
{
    /// <summary>Encerra a sessão <paramref name="sid"/> de quem pede, com o token já conferido antes do caso de uso.</summary>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    /// <exception cref="Domain.FalhaDeAutenticacao">SESSAO_NAO_ENCONTRADA, se a sessão não existir ou for de outra pessoa.</exception>
    Task Executar(AcessoConferido acesso, string sid, TraceId traceId, CancellationToken ct = default);
}
