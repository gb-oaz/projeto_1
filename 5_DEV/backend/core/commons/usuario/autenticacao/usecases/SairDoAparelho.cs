using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;
using AuditoriaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.AuditoriaDaExecucao;
using Execucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.Execucao;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

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
        if (await sessoes.Buscar(acesso.Sid, ct) is { } sessao) await sessoes.Apagar(sessao, MotivoFim.Sair, ct);
        await auditada.Concluir(execucao, ct);
    }
}
