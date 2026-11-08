using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// Uma execução auditada (RN-178, RN-271): o traceId, a ação e o que o
/// "INICIADO" já leva — quem pede, a conta e o alvo, quando existem ao chegar.
/// </summary>
/// <param name="TraceId">A execução, devolvida no cabeçalho X-Trace-Id.</param>
/// <param name="Acao">O que se faz.</param>
/// <param name="ContaId">Conta do alvo, se já existe.</param>
/// <param name="Quem">Quem pede, se há login.</param>
/// <param name="Alvo">Sobre quem ou o quê, se já se sabe.</param>
readonly record struct Execucao(TraceId TraceId, AcaoAuditada Acao, string? ContaId, string? Quem, string? Alvo);

/// <summary>
/// O que os casos de uso auditados repetem (RN-178, RN-270, RN-271): o
/// "INICIADO" ao chegar, a regra, e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair; o "CONCLUIDO" a regra grava antes de responder.
/// O pedido cancelado não é erro. Sem log.
/// </summary>
sealed class AuditoriaDaExecucao(IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
{
    /// <summary>Executa a regra entre o "INICIADO" e o registro de erro, se ela recusar ou falhar.</summary>
    public async Task<T> Auditar<T>(Execucao execucao, Func<Task<T>> regra, CancellationToken ct)
    {
        await Iniciar(execucao, ct);
        try
        {
            return await regra();
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            await RegistrarErro(execucao, falha, ct);
            throw;
        }
    }

    /// <summary>A regra sem resposta, como o desativar (204).</summary>
    public Task Auditar(Execucao execucao, Func<Task> regra, CancellationToken ct) =>
        Auditar(execucao, async () =>
        {
            await regra();
            return true;
        }, ct);

    /// <summary>O "CONCLUIDO" antes de responder, com o mesmo traceId do "INICIADO" (RN-178).</summary>
    public Task Concluir(Execucao execucao, CancellationToken ct) => Registrar(execucao, EtapaDaExecucao.Concluido, ct);

    /// <summary>O "INICIADO" ao chegar (RN-178).</summary>
    Task Iniciar(Execucao execucao, CancellationToken ct) => Registrar(execucao, EtapaDaExecucao.Iniciado, ct);

    /// <summary>Um registro da auditoria, na etapa dada.</summary>
    Task Registrar(Execucao execucao, EtapaDaExecucao etapa, CancellationToken ct) =>
        auditoria.Registrar(new RegistroDeAuditoria(execucao.TraceId, etapa, execucao.ContaId, execucao.Quem, execucao.Acao, execucao.Alvo,
            relogio.GetUtcNow()), ct);

    /// <summary>A recusa ou a falha em auditoriaErros, com o traceId do "INICIADO" (RN-270).</summary>
    Task RegistrarErro(Execucao execucao, Exception falha, CancellationToken ct)
    {
        var (codigo, nivel) = Classificar(falha);
        return erros.Registrar(new RegistroDeErro(execucao.TraceId, execucao.ContaId, execucao.Quem, execucao.Acao, execucao.Alvo, codigo, nivel,
            relogio.GetUtcNow()), ct);
    }

    /// <summary>Recusa prevista (FalhaDeUsuarios) é ALERTA com o código dela; o resto é ERRO_INESPERADO, nivel ERRO (RN-270).</summary>
    static (string Codigo, NivelDoErro Nivel) Classificar(Exception falha) => falha is FalhaDeUsuarios recusa
        ? (recusa.Codigo, NivelDoErro.Alerta)
        : (RegistroDeErro.ErroInesperado, NivelDoErro.Erro);
}
