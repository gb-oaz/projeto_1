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
/// Sair deste aparelho (RF-138): só a sessão do token termina. O token chega já
/// conferido; auditado com o traceId recebido, ação SAIR: "INICIADO" e
/// "CONCLUIDO" com quem, a conta e a sessão do token como alvo (RN-272).
/// </summary>
public sealed class SairDoAparelho(ISessoes sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : ISairDoAparelho
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(AcessoConferido acesso, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.Sair, acesso.ContaId, acesso.Sub, acesso.Sid);
        return auditada.Auditar(execucao, () => Sair(acesso, execucao, ct), ct);
    }

    /// <summary>Apaga a sessão do token, se ainda existe, e grava o "CONCLUIDO".</summary>
    async Task Sair(AcessoConferido acesso, Execucao execucao, CancellationToken ct)
    {
        await ApagarSessaoDoToken(acesso.Sid, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>Apaga a sessão do token, se ainda existe (RF-138).</summary>
    async Task ApagarSessaoDoToken(string sid, CancellationToken ct)
    {
        if (await sessoes.Buscar(sid, ct) is { } sessao) await sessoes.Apagar(sessao, MotivoFim.Sair, ct);
    }
}
