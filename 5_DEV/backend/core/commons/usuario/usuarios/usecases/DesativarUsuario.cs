using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/{id}/desativar (RF-112, RN-174): continua na lista, sem
/// entrar. Auditado com o traceId recebido: "INICIADO" com quem, a conta e o
/// alvo ao chegar, "CONCLUIDO" antes de responder e, na recusa (ESTADO_IGUAL é
/// ALERTA) ou na falha, o registro de erro antes de a exceção sair (RN-178,
/// RN-270, RN-271).
/// </summary>
public sealed class DesativarUsuario(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IDesativarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.DesativarUsuario, pedido.QuemPede, pedido.UsuarioId, ct);
        await auditada.Auditar(execucao, () => Desativar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do desativar, de quem pede ao "CONCLUIDO".</summary>
    async Task Desativar(PedidoDeUsuarioDto pedido, Execucao execucao, CancellationToken ct)
    {
        var alvo = await AlvoAdministravel(pedido, ct);
        alvo.Desativar();
        await usuarios.Atualizar(alvo, ct);
        await sessoes.EncerrarDoUsuario(alvo.Id, MotivoFim.UsuarioDesativado, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>O alvo, se quem pede está acima dele (RN-169).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO ou USUARIO_INEXISTENTE.</exception>
    async Task<UsuarioCadastrado> AlvoAdministravel(PedidoDeUsuarioDto pedido, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        return await apoio.Administravel(quem, pedido.UsuarioId, ct);
    }
}
