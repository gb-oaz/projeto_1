using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;
using AuditoriaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.AuditoriaDaExecucao;
using Execucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.Execucao;
using ExecucaoDeQuemChama = Boaz.Core.Commons.Usuario.Usuarios.Domain.ExecucaoDeQuemChama;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// A contratação da conta mudou (RN-238, RN-239): o pv de todos sobe e as
/// sessões recebem o pv novo, para o próximo token vir com ou sem o módulo.
/// Auditado na execução do Catálogo, ação AVISAR_CONTRATACAO_MUDOU: o traceId,
/// quem e a conta dela, a conta como alvo; cada usuário avisado grava o
/// AVISAR_PERMISSOES_NOVAS dele na mesma execução; na falha, o ERRO_INESPERADO
/// antes de a exceção voltar ao Catálogo (RN-275).
/// </summary>
public sealed class AvisarContratacaoMudou(
    IPermissoesGuardadas guardadas, ISessoesDoUsuario sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IAvisarContratacaoMudou
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(string contaId, ExecucaoDeQuemChama chamada, CancellationToken ct = default)
    {
        var execucao = Execucao.Interna(chamada, AcaoAuditada.AvisarContratacaoMudou, contaId);
        return auditada.Auditar(execucao, () => Avisar(contaId, execucao, ct), ct);
    }

    /// <summary>Sobe o pv da conta, avisa as sessões de cada usuário e grava o "CONCLUIDO".</summary>
    async Task Avisar(string contaId, Execucao execucao, CancellationToken ct)
    {
        await SubirPvEAvisar(contaId, execucao.Chamada, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>O pv de cada usuário da conta sobe, e as sessões dele recebem o novo (RN-238).</summary>
    async Task SubirPvEAvisar(string contaId, ExecucaoDeQuemChama chamada, CancellationToken ct)
    {
        foreach (var (usuarioId, pv) in await guardadas.SubirPvDaConta(contaId, ct))
        {
            await sessoes.AvisarPv(usuarioId, pv, chamada, ct);
        }
    }
}
