using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/hub/entrar: entrada e senha abrem uma sessão do hub (RF-134).</summary>
public interface IEntrarNoHub
{
    /// <summary>Abre a sessão e devolve os tokens.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// DADOS_INVALIDOS, ENTRADA_INVALIDA, ENTRADA_BLOQUEADA ou ACESSO_INDISPONIVEL.
    /// </exception>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    Task<SessaoAbertaDto> Executar(EntradaNoHubDto pedido, TraceId traceId, CancellationToken ct = default);
}
