using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
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
public sealed class ReativarUsuario(IUsuarios usuarios, IContas contas, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio,
    IAtualizarCardUsuarios cardsDoModulo)
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
        var alvo = await AlvoAdministravel(pedido, ct);
        await Gravar(alvo, ct);
        await cardsDoModulo.Executar(new MudancaNoCadastroDto(alvo.ContaId, alvo.Id), ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>O alvo, se quem pede está acima dele (RN-169).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO ou USUARIO_INEXISTENTE.</exception>
    async Task<UsuarioCadastrado> AlvoAdministravel(PedidoDeUsuarioDto pedido, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        return await apoio.Administravel(quem, pedido.UsuarioId, ct);
    }

    /// <summary>O alvo reativado, com as mesmas permissões, e gravado.</summary>
    /// <exception cref="FalhaDeUsuarios">ESTADO_IGUAL, se já está ativo.</exception>
    async Task Gravar(UsuarioCadastrado alvo, CancellationToken ct)
    {
        alvo.Reativar();
        await usuarios.Atualizar(alvo, ct);
    }
}
