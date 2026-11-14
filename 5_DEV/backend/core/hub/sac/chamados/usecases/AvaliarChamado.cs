using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// POST /sac/chamados/{id}/avaliacao (RF-178, RN-296): só a conta, uma vez;
/// confirma a solução e fecha; os anexos passam à guarda do fechado (RNF-234).
/// Auditado como AVALIAR_CHAMADO (RN-297).
/// </summary>
public sealed class AvaliarChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IAnexosDoChamado anexos,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IAvaliarChamado
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<AvaliadoDto> Executar(string id, PedidoAvaliacao pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.AvaliarChamado, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            quem.SoConta();
            var (nota, comentario) = pedido.Validar();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.Avaliar(autor, nota, comentario, agora), ct);
            var d = chamado.Dados;
            if (d.FechadoEm is { } fechadoEm) await anexos.AoFechar(d.Id, fechadoEm, ct);
            andamento.Alvejar(d.Id.Valor);
            await auditada.Concluir(andamento.Atual, ct);
            return new AvaliadoDto(d.Id.Valor, d.Status.Texto(), AvaliacaoDto.De(d.Avaliacao)!);
        }, ct);
}
