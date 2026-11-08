using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;
using AuditoriaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.AuditoriaDaExecucao;
using Execucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.Execucao;
using ExecucaoDeQuemChama = Boaz.Core.Commons.Usuario.Usuarios.Domain.ExecucaoDeQuemChama;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// As permissões do usuário mudaram (RN-230): as sessões continuam, com a versão
/// nova, e o token antigo recebe PERMISSOES_DESATUALIZADAS até ser renovado.
/// Auditado na execução de quem chama, ação AVISAR_PERMISSOES_NOVAS: o traceId,
/// quem e a conta dela, o usuário como alvo; na falha, o ERRO_INESPERADO antes
/// de a exceção voltar a quem chama (RN-275).
/// </summary>
public sealed class AvisarPermissoesNovas(ISessoes sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IAvisarPermissoesNovas
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(string usuarioId, int pv, ExecucaoDeQuemChama chamada, CancellationToken ct = default)
    {
        var execucao = Execucao.Interna(chamada, AcaoAuditada.AvisarPermissoesNovas, usuarioId);
        return auditada.Auditar(execucao, () => Avisar(usuarioId, pv, execucao, ct), ct);
    }

    /// <summary>Grava o pv novo nas sessões do usuário e o "CONCLUIDO".</summary>
    async Task Avisar(string usuarioId, int pv, Execucao execucao, CancellationToken ct)
    {
        await GravarPv(usuarioId, pv, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>A versão nova em cada sessão do usuário.</summary>
    async Task GravarPv(string usuarioId, int pv, CancellationToken ct)
    {
        foreach (var sessao in await sessoes.DoUsuario(usuarioId, ct))
        {
            sessao.AtualizarPv(pv);
            await sessoes.Gravar(sessao, ct);
        }
    }
}
