using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// Os 7 dias (RN-290), pelo AgendamentoDoSac a cada 1 min: Aguardando cliente
/// sem resposta passa a Resolvido, com a mensagem do sistema; Resolvido sem
/// reabrir passa a Fechado, e os anexos dele à guarda do fechado (RNF-234).
/// Cada mudança é auditada como FECHAR_POR_PRAZO, sem
/// quem, com o chamado como alvo (RN-297).
/// </summary>
public sealed class AplicarPrazos(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IAnexosDoChamado anexos,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IAplicarPrazos
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public async Task<int> Executar(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var mudaram = 0;
        foreach (var chamado in await chamados.VencidosEm(agora, ct))
        {
            var d = chamado.Dados;
            var execucao = new Execucao(TraceId.Novo(), AcaoAuditada.FecharPorPrazo, d.Conta?.Id, null, d.Id.Valor);
            var mudou = await auditada.Auditar(execucao, async () =>
            {
                var gravou = chamado.AplicarPrazos(agora) && await conversa.Gravar(chamado, ct);
                if (gravou && chamado.Dados.FechadoEm is { } fechadoEm) await anexos.AoFechar(chamado.Dados.Id, fechadoEm, ct);
                await auditada.Concluir(execucao, ct);
                return gravou;
            }, ct);
            if (mudou) mudaram++;
        }
        return mudaram;
    }
}
