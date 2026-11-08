using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
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
/// Encerra todas as sessões de um usuário por uma mudança de outro módulo
/// (RF-141, RNF-203): o próximo pedido e a próxima renovação recebem 401.
/// Auditado na execução de quem chama, ação ENCERRAR_SESSOES_DO_USUARIO: o
/// traceId, quem e a conta dela, o usuário como alvo; na falha, o
/// ERRO_INESPERADO antes de a exceção voltar a quem chama, que grava o dele
/// (RN-275).
/// </summary>
public sealed class EncerrarSessoesDoUsuario(ISessoes sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IEncerrarSessoesDoUsuario
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task Executar(string usuarioId, MotivoFim motivo, ExecucaoDeQuemChama chamada, CancellationToken ct = default)
    {
        var execucao = Execucao.Interna(chamada, AcaoAuditada.EncerrarSessoesDoUsuario, usuarioId);
        return auditada.Auditar(execucao, () => Encerrar(usuarioId, motivo, execucao, ct), ct);
    }

    /// <summary>Apaga cada sessão do usuário com o motivo e grava o "CONCLUIDO".</summary>
    async Task Encerrar(string usuarioId, MotivoFim motivo, Execucao execucao, CancellationToken ct)
    {
        await ApagarTodas(usuarioId, motivo, ct);
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>Apaga cada sessão do usuário com o motivo dado.</summary>
    async Task ApagarTodas(string usuarioId, MotivoFim motivo, CancellationToken ct)
    {
        foreach (var sessao in await sessoes.DoUsuario(usuarioId, ct))
        {
            await sessoes.Apagar(sessao, motivo, ct);
        }
    }
}
