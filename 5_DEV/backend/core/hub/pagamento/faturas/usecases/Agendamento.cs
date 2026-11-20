using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.In;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.In;

namespace Boaz.Core.Hub.Pagamento.Faturas.UseCases;

/// <summary>
/// Uma rodada do AgendamentoDoPagamento, com um traceId próprio, que não é o
/// de nenhum pedido (RNF-244):
/// <list type="number">
/// <item>fecha a competência anterior (RN-300): uma fatura por conta com as
/// cobranças fechadas do Catálogo, emitida à 00:00 de Brasília do dia 1 e
/// vencida no dia da Configuração; a que já existe não se repete;</item>
/// <item>vira "VENCIDA" a "A_PAGAR" do dia seguinte ao vencimento (RN-301) e
/// abre o aviso de atraso depois da tolerância (RN-311), auditados como
/// FATURA_VENCIDA, com a fatura como alvo (RN-309); encerra os avisos das
/// faturas que deixaram de estar vencidas;</item>
/// <item>expira e consulta no banco os pagamentos em aberto (RN-302).</item>
/// </list>
/// </summary>
public sealed class RodarAgendamentoDoPagamento(
    IFaturas faturas, ICobrancasDoMes cobrancas, ICadastroDoPagamento cadastro, INumeracaoDoPagamento numeracao, IAvisosDeAtraso avisos,
    IConfiguracoesDoPagamento configuracoes, IExpirarPagamentos expirar, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IRodarAgendamentoDoPagamento
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task Executar(CancellationToken ct = default)
    {
        var traceId = TraceId.Novo();
        var agora = relogio.GetUtcNow();
        var hoje = Calendario.Dia(agora);
        await Fechar(Competencia.Do(hoje).Anterior, traceId, ct);
        await Vencer(hoje, agora, traceId, ct);
        await expirar.Executar(traceId, ct);
    }

    /// <summary>O fechamento da competência (RN-300): as cobranças de cada conta viram uma fatura.</summary>
    async Task Fechar(Competencia competencia, TraceId traceId, CancellationToken ct)
    {
        var fechadas = await cobrancas.Fechadas(competencia, ct);
        if (fechadas.Count == 0) return;
        var modulos = await cadastro.Modulos(ct);
        var dia = ((await configuracoes.VigenteEm(competencia.EmitidaEm, ct)) ?? ConfiguracaoDoPagamento.Padrao).DiaVencimento;
        foreach (var daConta in fechadas.GroupBy(c => c.ContaId))
        {
            var itens = daConta.OrderBy(c => c.ModuloId, StringComparer.Ordinal)
                .Select(c => new ItemDaFatura(c.ModuloId, modulos.GetValueOrDefault(c.ModuloId, c.ModuloId), c.Id, c.Total)).ToList();
            if (Dinheiro.Somar(itens.Select(i => i.Valor)) <= Dinheiro.Zero || await faturas.Existe(daConta.Key, competencia, ct)) continue;
            var conta = await cadastro.Conta(daConta.Key, ct);
            if (conta is null || conta.Sigla == Sigla.DoTime) continue;
            var id = Numeros.De(Numeros.Faturas, await numeracao.Proximo(Numeros.Faturas, ct));
            await faturas.Incluir(Fatura.Emitir(id, conta, competencia, itens, dia, traceId.Valor), ct);
        }
    }

    /// <summary>A virada para "VENCIDA" e os avisos de atraso (RN-301, RN-311).</summary>
    async Task Vencer(DateOnly hoje, DateTimeOffset agora, TraceId traceId, CancellationToken ct)
    {
        var tolerancia = (await configuracoes.Atual(ct) ?? ConfiguracaoDoPagamento.Padrao).ToleranciaAvisoDias;
        var abertos = (await avisos.TodosAbertos(ct)).Select(a => a.FaturaId).ToHashSet();
        var candidatas = (await faturas.APagarVencidasAntesDe(hoje, ct))
            .Concat(await faturas.Listar(new FiltroDeFaturas(null, [StatusDaFatura.Vencida]), ct)).ToList();
        var vencidas = new HashSet<string>();
        foreach (var fatura in candidatas)
        {
            var desde = Calendario.InicioDoDia(fatura.Vencimento.AddDays(1 + tolerancia));
            var virar = fatura.Status == StatusDaFatura.APagar;
            var avisar = !abertos.Contains(fatura.Id) && agora >= desde;
            vencidas.Add(fatura.Id);
            if (!virar && !avisar) continue;
            await auditada.Auditar(new Execucao(traceId, AcaoAuditada.FaturaVencida, fatura.Conta.Id, null, fatura.Id), async () =>
            {
                if (virar && (!fatura.Vencer(hoje, agora, traceId.Valor) || !await faturas.Salvar(fatura, ct))) return false;
                if (avisar) await avisos.Abrir(new AvisoDeAtraso(fatura.Id, fatura.Conta.Id, fatura.Vencimento, desde), ct);
                await auditada.Concluir(new Execucao(traceId, AcaoAuditada.FaturaVencida, fatura.Conta.Id, null, fatura.Id), ct);
                return true;
            }, ct);
        }
        foreach (var faturaId in abertos.Where(id => !vencidas.Contains(id))) await avisos.Encerrar(faturaId, agora, ct);
    }
}
