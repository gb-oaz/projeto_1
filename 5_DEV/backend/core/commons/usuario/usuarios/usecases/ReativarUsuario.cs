using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/{id}/reativar (RF-112): volta com as mesmas permissões.
/// Auditado com o traceId recebido: "INICIADO" com quem, a conta e o alvo,
/// "CONCLUIDO" antes de responder e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class ReativarUsuario(IUsuarios usuarios, IContas contas, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IReativarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.ReativarUsuario, pedido.QuemPede, pedido.UsuarioId, ct);
        await auditada.Auditar(execucao, () => Reativar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do reativar, de quem pede ao "CONCLUIDO".</summary>
    async Task Reativar(PedidoDeUsuarioDto pedido, Execucao execucao, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        alvo.Reativar();
        await usuarios.Atualizar(alvo, ct);
        await auditada.Concluir(execucao, ct);
    }
}
