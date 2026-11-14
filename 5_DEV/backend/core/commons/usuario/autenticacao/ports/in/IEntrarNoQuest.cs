using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/quest/entrar: o código de acesso abre uma sessão do quest (RF-135).</summary>
public interface IEntrarNoQuest
{
    /// <summary>Abre a sessão e devolve os tokens.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// DADOS_INVALIDOS, CODIGO_INVALIDO, ENTRADA_BLOQUEADA ou ACESSO_INDISPONIVEL.
    /// </exception>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    Task<SessaoAbertaDto> Executar(EntradaNoQuestDto pedido, TraceId traceId, CancellationToken ct = default);
}
