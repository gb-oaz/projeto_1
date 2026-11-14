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
/// POST /sac/chamados/{id}/reabrir (RF-177, RN-290): só a conta, em até 7 dias
/// depois de resolvido, sem conferir o limite de 10 (RN-294); os anexos
/// voltam ao uso no chamado (RNF-234). Auditado como
/// REABRIR_CHAMADO (RN-297).
/// </summary>
public sealed class ReabrirChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IAnexosDoChamado anexos,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IReabrirChamado
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<ResponsavelDto> Executar(string id, PedidoReabrir pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.ReabrirChamado, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            quem.SoConta();
            var mensagem = pedido.Validar();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.Reabrir(autor, mensagem, agora), ct);
            await anexos.AoReabrir(chamado.Dados.Id, ct);
            andamento.Alvejar(chamado.Dados.Id.Valor);
            await auditada.Concluir(andamento.Atual, ct);
            return ResponsavelDto.De(chamado.Dados);
        }, ct);
}
