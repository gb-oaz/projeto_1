using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// POST /sac/chamados (RF-171): só a conta master e o autônomo; a forma do
/// pedido (RN-291), o relacionado da própria conta, os anexos do próprio
/// usuário (RN-292), o limite de 10 em andamento (RN-294) e o prazo em horas
/// úteis da categoria (RN-289, RNF-229). Auditado como ABRIR_CHAMADO (RN-297).
/// </summary>
public sealed class AbrirChamado(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IAnexosDoChamado anexos, IPrazosDoSac prazos,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IAbrirChamado
{
    readonly AuditoriaDoSac auditada = new(auditoria, erros, relogio);
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public Task<ChamadoAbertoDto> Executar(PedidoAbrirChamado pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new ExecucaoDoSac(traceId, AcaoAuditada.AbrirChamado, quem.ContaId, quem.UsuarioId, null), async () =>
        {
            quem.SoConta();
            var (categoria, assunto, descricao, relacionadoA, ids) = pedido.Validar();
            var agora = relogio.GetUtcNow();
            if (relacionadoA is { } relacionado
                && (await chamados.Obter(relacionado, ct))?.Dados.Conta?.Id != quem.ContaId)
            {
                throw FalhaDoSac.DadosInvalidos("relacionadoA");
            }
            if (ids.Count > 0 && !await anexos.Disponiveis(ids, quem.UsuarioId, agora, ct)) throw FalhaDoSac.DadosInvalidos("anexos");
            if (await chamados.EmAndamentoDaConta(quem.ContaId, ct) >= LimitesDoChamado.EmAndamento) throw FalhaDoSac.LimiteChamados();
            var conta = await pessoas.Conta(quem.ContaId, ct) ?? throw FalhaDoSac.AcessoNegado();
            var numero = new NumeroDoChamado(await numeracao.Proximo(INumeracao.Chamados, ct));
            var chamado = Chamado.Abrir(numero, conta, await conversa.Autor(quem, ct), categoria, assunto, descricao, relacionadoA, ids,
                await prazos.HorasUteis(categoria, ct), agora);
            await conversa.Incluir(chamado, ct);
            if (ids.Count > 0) await anexos.Vincular(ids, numero, null, false, ct);
            return (ChamadoAbertoDto.De(chamado.Dados), (string?)numero.Valor);
        }, ct);
}
