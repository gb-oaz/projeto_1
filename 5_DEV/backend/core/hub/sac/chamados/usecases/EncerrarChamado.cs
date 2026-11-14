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
/// POST /sac/chamados/{id}/encerrar (RF-177, RN-290): só a conta, com o
/// chamado ainda em andamento; os anexos passam à guarda do fechado
/// (RNF-234). Auditado como ENCERRAR_CHAMADO (RN-297).
/// </summary>
public sealed class EncerrarChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IAnexosDoChamado anexos,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IEncerrarChamado
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<StatusDto> Executar(string id, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.EncerrarChamado, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            quem.SoConta();
            var autor = await conversa.Autor(quem, ct);
            var agora = relogio.GetUtcNow();
            var (chamado, _) = await conversa.Mudar(id, quem, c => c.Encerrar(autor, agora), ct);
            await anexos.AoFechar(chamado.Dados.Id, chamado.Dados.FechadoEm!.Value, ct);
            andamento.Alvejar(chamado.Dados.Id.Valor);
            await auditada.Concluir(andamento.Atual, ct);
            return StatusDto.De(chamado.Dados);
        }, ct);
}
