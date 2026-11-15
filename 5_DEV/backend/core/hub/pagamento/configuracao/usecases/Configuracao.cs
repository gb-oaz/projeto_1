using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Dtos;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.In;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.UseCases;

namespace Boaz.Core.Hub.Pagamento.Configuracao.UseCases;

/// <summary>GET /pagamento/recebimentos/configuracao (RF-193): a versão atual, só para o ADMINISTRADOR (RN-298).</summary>
public sealed class LerConfiguracao(IConfiguracoesDoPagamento configuracoes) : ILerConfiguracao
{
    /// <inheritdoc />
    public async Task<ConfiguracaoDto> Executar(QuemPede quem, CancellationToken ct = default)
    {
        quem.SoRecebedor();
        return ConfiguracaoDto.De(await ConfiguracaoAtual.Ler(configuracoes, ct));
    }
}

/// <summary>
/// PUT /pagamento/recebimentos/configuracao (RF-193): o ADMINISTRADOR grava
/// uma versão nova, vigente desde agora; as versões antigas ficam, porque a
/// taxa de uma fatura é a vigente no dia do vencimento (RN-310). O If-Match
/// que não é a versão atual (ou que falta) responde 412 CONFIGURACAO_MUDOU.
/// Desligar uma forma não mexe nos pagamentos em aberto dela. Auditado como
/// CONFIGURACAO_SALVA (RN-309).
/// </summary>
public sealed class SalvarConfiguracao(IConfiguracoesDoPagamento configuracoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : ISalvarConfiguracao
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<ConfiguracaoDto> Executar(PedidoDeConfiguracao pedido, int? versaoLida, QuemPede quem, TraceId traceId,
        CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.ConfiguracaoSalva, quem.ContaId, quem.UsuarioId, null), async andamento =>
        {
            quem.SoRecebedor();
            var (formas, juros, tolerancia, prazos) = pedido.Validar();
            var atual = await ConfiguracaoAtual.Ler(configuracoes, ct);
            if (versaoLida != atual.Versao) throw FalhaDoPagamento.ConfiguracaoMudou();
            var nova = new ConfiguracaoDoPagamento(atual.Versao + 1, relogio.GetUtcNow(), formas, juros, tolerancia, ConfiguracaoDoPagamento.Dia,
                prazos, quem.UsuarioId);
            if (!await configuracoes.Incluir(nova, ct)) throw FalhaDoPagamento.ConfiguracaoMudou();
            await auditada.Concluir(andamento.Atual, ct);
            return ConfiguracaoDto.De(nova);
        }, ct);
}

/// <summary>GET /pagamento/formas (RF-184): as formas ligadas na configuração atual, para quem usa o Pagamento (RN-298).</summary>
public sealed class LerFormas(IConfiguracoesDoPagamento configuracoes) : ILerFormas
{
    /// <inheritdoc />
    public async Task<FormasDto> Executar(QuemPede quem, CancellationToken ct = default)
    {
        if (quem.Recebedor || !quem.UsaPagamento) throw FalhaDoPagamento.AcessoNegado();
        return new FormasDto([.. (await ConfiguracaoAtual.Ler(configuracoes, ct)).Aceitas.Select(f => f.Texto())]);
    }
}
