using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// POST /sac/chamados/{id}/assumir (RF-175): só o time; com outro
/// responsável, 409 JA_ASSUMIDO sem "substituir". Auditado como ASSUMIR_CHAMADO (RN-297).
/// </summary>
public sealed class AssumirChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IAssumirChamado
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<ResponsavelDto> Executar(string id, PedidoAssumir pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.AssumirChamado, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            quem.SoTime();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.Assumir(autor, pedido.Substituir ?? false, agora), ct);
            andamento.Alvejar(chamado.Dados.Id.Valor);
            await auditada.Concluir(andamento.Atual, ct);
            return ResponsavelDto.De(chamado.Dados);
        }, ct);
}
