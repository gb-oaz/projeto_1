using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// POST /sac/chamados/{id}/avaliacao (RF-178, RN-296): só a conta, uma vez;
/// confirma a solução e fecha. Auditado como AVALIAR_CHAMADO (RN-297).
/// </summary>
public sealed class AvaliarChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IAvaliarChamado
{
    readonly AuditoriaDoSac auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<AvaliadoDto> Executar(string id, PedidoAvaliacao pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new ExecucaoDoSac(traceId, AcaoAuditada.AvaliarChamado, quem.ContaId, quem.UsuarioId, id), async () =>
        {
            quem.SoConta();
            var (nota, comentario) = pedido.Validar();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.Avaliar(autor, nota, comentario, agora), ct);
            var d = chamado.Dados;
            return (new AvaliadoDto(d.Id.Valor, d.Status.Texto(), AvaliacaoDto.De(d.Avaliacao)!), (string?)d.Id.Valor);
        }, ct);
}
