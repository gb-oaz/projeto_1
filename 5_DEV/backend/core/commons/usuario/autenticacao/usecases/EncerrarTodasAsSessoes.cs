using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Auditoria.Domain.AcaoAuditada;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Sair de todos os aparelhos (RF-157), inclusive deste. O token chega já
/// conferido; auditado com o traceId recebido, ação ENCERRAR_SESSOES:
/// "INICIADO" e "CONCLUIDO" com quem, a conta e o próprio usuário como alvo
/// (RN-272).
/// </summary>
public sealed class EncerrarTodasAsSessoes(ISessoes sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IEncerrarTodasAsSessoes
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(AcessoConferido acesso, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.EncerrarSessoes, acesso.ContaId, acesso.Sub, acesso.Sub);
        return auditada.Auditar(execucao, () => Encerrar(acesso, execucao, ct), ct);
    }

    /// <summary>Apaga cada sessão de quem pede e grava o "CONCLUIDO".</summary>
    async Task Encerrar(AcessoConferido acesso, Execucao execucao, CancellationToken ct)
    {
        await ApagarTodas(acesso.Sub, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>Apaga cada sessão do usuário, inclusive a deste aparelho (RF-157).</summary>
    async Task ApagarTodas(string usuarioId, CancellationToken ct)
    {
        foreach (var sessao in await sessoes.DoUsuario(usuarioId, ct))
        {
            await sessoes.Apagar(sessao, MotivoFim.SairDeTodos, ct);
        }
    }
}
