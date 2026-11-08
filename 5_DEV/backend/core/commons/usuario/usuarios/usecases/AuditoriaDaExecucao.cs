using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
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
readonly record struct Execucao(TraceId TraceId, AcaoAuditada Acao, string? ContaId, string? Quem, string? Alvo)
{
    /// <summary>Esta execução para o caso de uso interno que ela chama (RN-275).</summary>
    public ExecucaoDeQuemChama Chamada => new(TraceId, Quem, ContaId);

    /// <summary>
    /// A execução de um caso de uso interno: o traceId, quem e a conta de quem
    /// chama, com a ação e o alvo dele (RN-275).
    /// </summary>
    public static Execucao Interna(ExecucaoDeQuemChama chamada, AcaoAuditada acao, string alvo) =>
        new(chamada.TraceId, acao, chamada.ContaId, chamada.Quem, alvo);
}

/// <summary>
/// A execução enquanto a regra corre: começa como o "INICIADO" e ganha quem e a
/// conta quando a pessoa é identificada, para a recusa seguinte já ir com eles
/// (RN-273).
/// </summary>
/// <param name="inicio">A execução do "INICIADO".</param>
sealed class ExecucaoEmAndamento(Execucao inicio)
{
    /// <summary>A execução como está agora.</summary>
    public Execucao Atual { get; private set; } = inicio;

    /// <summary>A pessoa foi identificada: o registro de erro daqui em diante vai com ela e a conta.</summary>
    public void Identificar(string contaId, string quem) => Atual = Atual with { ContaId = contaId, Quem = quem };
}

/// <summary>
/// O que os casos de uso auditados repetem (RN-178, RN-270, RN-271): o
/// "INICIADO" ao chegar, a regra, e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair; o "CONCLUIDO" a regra grava antes de responder.
/// O pedido cancelado não é erro. Sem log.
/// </summary>
sealed class AuditoriaDaExecucao(IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
{
    /// <summary>Executa a regra entre o "INICIADO" e o registro de erro, se ela recusar ou falhar.</summary>
    public Task<T> Auditar<T>(Execucao execucao, Func<Task<T>> regra, CancellationToken ct) =>
        Auditar(execucao, _ => regra(), ct);

    /// <summary>
    /// A regra que identifica a pessoa no meio do caminho, como a entrada depois
    /// da senha conferida (RN-273): a recusa dali em diante já vai com quem e a
    /// conta que ela registrou no andamento.
    /// </summary>
    public async Task<T> Auditar<T>(Execucao execucao, Func<ExecucaoEmAndamento, Task<T>> regra, CancellationToken ct)
    {
        var andamento = new ExecucaoEmAndamento(execucao);
        await Iniciar(execucao, ct);
        try
        {
            return await regra(andamento);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            await RegistrarErro(andamento.Atual, falha, ct);
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

    /// <summary>
    /// Recusa prevista (de Usuários, da Autorização ou da Autenticação) é ALERTA
    /// com o código dela; o resto é ERRO_INESPERADO, nivel ERRO (RN-270).
    /// </summary>
    static (string Codigo, NivelDoErro Nivel) Classificar(Exception falha) => falha switch
    {
        FalhaDeUsuarios recusa => (recusa.Codigo, NivelDoErro.Alerta),
        FalhaDeAutorizacao recusa => (recusa.Codigo, NivelDoErro.Alerta),
        FalhaDeAutenticacao recusa => (recusa.Codigo, NivelDoErro.Alerta),
        _ => (RegistroDeErro.ErroInesperado, NivelDoErro.Erro),
    };
}
