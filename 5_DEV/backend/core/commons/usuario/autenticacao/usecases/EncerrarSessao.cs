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
/// Sair de um aparelho da lista (RF-157). A sessão de outra pessoa responde como
/// inexistente, para não revelar que existe. O token chega já conferido;
/// auditado com o traceId recebido, ação ENCERRAR_SESSAO: "INICIADO" com quem,
/// a conta e o sid pedido como alvo; a recusa vai com eles (RN-272, RN-273).
/// </summary>
public sealed class EncerrarSessao(ISessoes sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IEncerrarSessao
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(AcessoConferido acesso, string sid, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.EncerrarSessao, acesso.ContaId, acesso.Sub, sid);
        return auditada.Auditar(execucao, () => Encerrar(acesso, sid, execucao, ct), ct);
    }

    /// <summary>Apaga a sessão pedida, se é de quem pede, e grava o "CONCLUIDO".</summary>
    async Task Encerrar(AcessoConferido acesso, string sid, Execucao execucao, CancellationToken ct)
    {
        var sessao = await DeQuemPede(acesso, sid, ct);
        await sessoes.Apagar(sessao, MotivoFim.Sair, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>A sessão <paramref name="sid"/>, se existe e é de quem pede.</summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_NAO_ENCONTRADA.</exception>
    async Task<Sessao> DeQuemPede(AcessoConferido acesso, string sid, CancellationToken ct)
    {
        var sessao = await sessoes.Buscar(sid, ct);
        return sessao is null || sessao.UsuarioId != acesso.Sub ? throw FalhaDeAutenticacao.SessaoNaoEncontrada() : sessao;
    }
}
