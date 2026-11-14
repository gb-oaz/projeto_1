using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// A exigência da rota de um endereço que muda estado (RN-270): atendida, segue
/// sem registro; recusada por faltar o módulo, grava o "INICIADO" da ação do
/// endereço, com quem, a conta dele e o alvo, e o ALERTA ACESSO_NEGADO com o
/// mesmo traceId, sem "CONCLUIDO", antes de a recusa sair. A leitura (GET) usa
/// <see cref="AutorizarPedido"/>, sem registro.
/// </summary>
public sealed class AutorizarPedidoAuditado(IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IAutorizarPedidoAuditado
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(ExigenciaDaRota? exigencia, PedidoAutorizavelDto pedido, ExecucaoDaRotaDto execucao, CancellationToken ct = default) =>
        AutorizarPedido.Atende(exigencia, pedido) ? Task.CompletedTask : AuditarRecusa(execucao, ct);

    /// <summary>O "INICIADO" da ação do endereço e o ALERTA ACESSO_NEGADO, antes da recusa sair (RN-270).</summary>
    /// <exception cref="FalhaDeAutorizacao">ACESSO_NEGADO, sempre.</exception>
    Task AuditarRecusa(ExecucaoDaRotaDto rota, CancellationToken ct)
    {
        var execucao = new Execucao(rota.TraceId, rota.Acao, rota.ContaId, rota.Quem, rota.Alvo);
        return auditada.Auditar(execucao, () => Task.FromException(FalhaDeAutorizacao.AcessoNegado()), ct);
    }
}
