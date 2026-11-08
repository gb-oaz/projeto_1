using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas/esqueci-senha-master (RF-111, RNF-205): até 3 pedidos por
/// aparelho em 24 h. Sem login, auditado com o traceId recebido (RN-274):
/// "INICIADO" sem quem, sem conta e sem alvo; "CONCLUIDO" com o alvo, o pedido
/// gravado em "pedidosAjuda"; na recusa (429 LIMITE_ATENDIMENTO é ALERTA) ou na
/// falha, o registro de erro antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class PedirAjudaDaMaster(IPedidosDeAjuda pedidos, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IPedirAjudaDaMaster
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(AjudaDaMasterDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.EsqueciSenhaMaster, null, null, null);
        return auditada.Auditar(execucao, () => Pedir(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do pedido de ajuda, do formulário ao "CONCLUIDO".</summary>
    async Task Pedir(AjudaDaMasterDto pedido, Execucao execucao, CancellationToken ct)
    {
        var (instituicao, contato) = pedido.Validar();
        var agora = relogio.GetUtcNow();
        await GarantirLimite(pedido.Aparelho, agora, ct);
        var id = await pedidos.Enviar(pedido.Aparelho, instituicao, contato, agora, ct);
        await RegistrarConclusao(execucao, id, ct);
    }

    /// <summary>O "CONCLUIDO" com o pedido gravado em "pedidosAjuda" como alvo; o "INICIADO" foi sem alvo (RN-178).</summary>
    Task RegistrarConclusao(Execucao execucao, string pedidoId, CancellationToken ct) =>
        auditada.Concluir(execucao with { Alvo = pedidoId }, ct);

    /// <summary>O aparelho ainda tem envio nas últimas 24 h (RNF-205).</summary>
    /// <exception cref="FalhaDeUsuarios">LIMITE_ATENDIMENTO com o tentarEm.</exception>
    async Task GarantirLimite(string aparelho, DateTimeOffset agora, CancellationToken ct)
    {
        var anteriores = await pedidos.EnviosDoAparelho(aparelho, agora - LimiteDeAtendimento.Janela, ct);
        if (LimiteDeAtendimento.TentarEm(anteriores, agora) is { } tentarEm) throw FalhaDeUsuarios.LimiteAtendimento(tentarEm);
    }
}
