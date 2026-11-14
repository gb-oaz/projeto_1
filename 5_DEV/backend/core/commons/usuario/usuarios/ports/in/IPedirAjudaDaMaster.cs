using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas/esqueci-senha-master, sem login: 202, e a mensagem vai ao SAC (RF-111, RNF-205).</summary>
public interface IPedirAjudaDaMaster
{
    /// <summary>Envia o pedido, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">DADOS_INVALIDOS ou LIMITE_ATENDIMENTO.</exception>
    Task Executar(AjudaDaMasterDto pedido, TraceId traceId, CancellationToken ct = default);
}
