using AutenticacaoEmMemoria = Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.AutenticacaoEmMemoria;
using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>
/// O encerramento das sessões vai na execução do pedido de Usuários, e a falha
/// dentro dele grava os dois erros, o interno e o do pedido, sem "CONCLUIDO"
/// (RN-275).
/// </summary>
public class CasosInternosAuditadosTests
{
    static readonly TraceId Trace = new("trc-7f3a9c21");
    readonly UsuariosEmMemoria mock = new Cenario().Mock;

    [Fact]
    public async Task RN_275_desativar_encerra_as_sessoes_na_execucao_do_pedido()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), Trace);

        Assert.Equal(new ExecucaoDeQuemChama(Trace, "usr-ebfga-master", "conta-ebfga"), Assert.Single(mock.Chamadas));
    }

    [Fact]
    public async Task RN_275_falha_na_execucao_interna_grava_os_dois_erros()
    {
        var queda = new InvalidOperationException("a gravação falhou ao encerrar as sessões do Paulo");
        var autenticacao = new AutenticacaoEmMemoria { FalhaDoRedis = queda };
        mock.Autenticacao = new EncerrarSessoesDoUsuario(autenticacao, mock, mock, mock.Relogio);

        Assert.Same(queda, await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), Trace)));

        Assert.Equal(new[] { "INICIADO", "INICIADO", "ERRO ERRO_INESPERADO", "ERRO ERRO_INESPERADO" }, mock.Gravacoes);
        Assert.Equal(new[] { AcaoAuditada.EncerrarSessoesDoUsuario, AcaoAuditada.DesativarUsuario }, mock.Erros.Select(e => e.Acao));
        Assert.All(mock.Erros, e => Assert.Equal((Trace, "usr-ebfga-master", "usr-ebfga-paulo", NivelDoErro.Erro),
            (e.TraceId, e.Quem, e.Alvo, e.Nivel)));
        Assert.DoesNotContain(mock.Auditoria, a => a.Etapa == EtapaDaExecucao.Concluido);
    }
}
