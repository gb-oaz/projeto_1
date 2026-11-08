using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/hub/confirmar-senha: o comprovante da senha gerada abre a sessão (RF-143).</summary>
public interface IConfirmarSenha
{
    /// <summary>Confirma a geração e abre a sessão do hub.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// DADOS_INVALIDOS, COMPROVANTE_INVALIDO ou ACESSO_INDISPONIVEL.
    /// </exception>
    /// <remarks>Auditado com o traceId da execução, devolvido no X-Trace-Id (RN-272, RN-273).</remarks>
    Task<SessaoAbertaDto> Executar(ConfirmacaoDeSenhaDto pedido, TraceId traceId, CancellationToken ct = default);
}
