using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// POST /sac/chamados/{id}/status (RF-176, RF-179, RN-290): só o time, sempre
/// com a mensagem. Auditado como MUDAR_STATUS (RN-297).
/// </summary>
public sealed class MudarStatus(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IMudarStatus
{
    readonly AuditoriaDoSac auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<StatusDto> Executar(string id, PedidoStatus pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new ExecucaoDoSac(traceId, AcaoAuditada.MudarStatus, quem.ContaId, quem.UsuarioId, id), async () =>
        {
            quem.SoTime();
            var (status, mensagem) = pedido.Validar();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.MudarStatus(autor, status, mensagem, agora), ct);
            return (StatusDto.De(chamado.Dados), (string?)chamado.Dados.Id.Valor);
        }, ct);
}
