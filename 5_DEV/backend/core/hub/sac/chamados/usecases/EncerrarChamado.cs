using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// POST /sac/chamados/{id}/encerrar (RF-177, RN-290): só a conta, com o
/// chamado ainda em andamento. Auditado como ENCERRAR_CHAMADO (RN-297).
/// </summary>
public sealed class EncerrarChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IEncerrarChamado
{
    readonly AuditoriaDoSac auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<StatusDto> Executar(string id, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new ExecucaoDoSac(traceId, AcaoAuditada.EncerrarChamado, quem.ContaId, quem.UsuarioId, id), async () =>
        {
            quem.SoConta();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.Encerrar(autor, agora), ct);
            return (StatusDto.De(chamado.Dados), (string?)chamado.Dados.Id.Valor);
        }, ct);
}
