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
/// POST /sac/chamados/{id}/mensagens (RF-173, RF-175, RF-179): a conta
/// responde; o time responde ou escreve a nota interna (só o time). Os anexos
/// são do próprio usuário (RN-292). Auditado como ENVIAR_MENSAGEM (RN-297).
/// </summary>
public sealed class EnviarMensagem(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IAnexosDoChamado anexos,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IEnviarMensagem
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<MensagemEnviadaDto> Executar(string id, PedidoMensagem pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.EnviarMensagem, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            var doTime = quem.Visao == Visao.Time;
            if (!doTime && pedido.Interna == true) throw FalhaDoSac.AcessoNegado();
            var (texto, ids, interna) = pedido.Validar();
            var agora = relogio.GetUtcNow();
            if (ids.Count > 0 && !await anexos.Disponiveis(ids, quem.UsuarioId, agora, ct)) throw FalhaDoSac.DadosInvalidos("anexos");
            var autor = await conversa.Autor(quem, ct);
            var (chamado, itens) = await conversa.Mudar(id, quem, c =>
            {
                if (doTime) c.Responder(autor, texto, ids, interna, agora);
                else c.ResponderDaConta(autor, texto, ids, agora);
            }, ct);
            var mensagem = itens.First(i => i.Tipo != TipoDeItem.Evento);
            if (ids.Count > 0) await anexos.Vincular(ids, chamado.Dados.Id, mensagem.Id, interna, ct);
            andamento.Alvejar(chamado.Dados.Id.Valor);
            await auditada.Concluir(andamento.Atual, ct);
            return new MensagemEnviadaDto(mensagem.Id, autor.Nome, mensagem.Em, chamado.Dados.Status.Texto());
        }, ct);
}
