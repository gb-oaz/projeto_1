using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>
/// Os casos de uso internos da Autenticação gravam na execução de quem chama:
/// o traceId, quem e a conta dela, o usuário como alvo; a falha é ERRO, sem
/// "CONCLUIDO" (RN-275).
/// </summary>
public class CasosInternosAuditadosTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RN_275_encerrar_sessoes_do_usuario_na_execucao_de_quem_chama()
    {
        await c.Mock.EncerrarSessoesDoUsuario.Executar("usr-ebfga-maria", MotivoFim.UsuarioDesativado, Chamada);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, c.Mock.Gravacoes);
        Assert.All(c.Mock.Auditoria, a => Assert.Equal(
            (Trace, AcaoAuditada.EncerrarSessoesDoUsuario, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-maria"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem, a.Alvo)));
    }

    [Fact]
    public async Task RN_275_avisar_permissoes_novas_na_execucao_de_quem_chama()
    {
        await c.Mock.AvisarPermissoesNovas.Executar("usr-ebfga-maria", 8, Chamada);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, c.Mock.Gravacoes);
        Assert.All(c.Mock.Auditoria, a => Assert.Equal(
            (Trace, AcaoAuditada.AvisarPermissoesNovas, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-maria"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem, a.Alvo)));
    }

    [Fact]
    public async Task RN_275_falha_na_execucao_interna_e_erro_sem_concluido()
    {
        var queda = new InvalidOperationException("o Redis caiu no meio da gravação");
        c.Mock.FalhaDoRedis = queda;

        Assert.Same(queda, await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Mock.EncerrarSessoesDoUsuario.Executar("usr-ebfga-maria", MotivoFim.UsuarioDesativado, Chamada)));

        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, c.Mock.Gravacoes);
        var erro = Assert.Single(c.Mock.Erros);
        Assert.Equal((Trace, AcaoAuditada.EncerrarSessoesDoUsuario, "usr-ebfga-maria", NivelDoErro.Erro),
            (erro.TraceId, erro.Acao, erro.Alvo, erro.Nivel));
    }
}
