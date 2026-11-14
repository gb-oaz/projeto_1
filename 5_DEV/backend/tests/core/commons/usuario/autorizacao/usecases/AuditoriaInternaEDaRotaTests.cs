using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>
/// O aviso do Catálogo e o aviso de pv auditados na execução de quem chama
/// (RN-275), e o 403 da rota por faltar o módulo, auditado no endereço que muda
/// estado (RN-270).
/// </summary>
public class AuditoriaInternaEDaRotaTests
{
    static readonly TraceId Trace = new("trc-7f3a9c21");
    static readonly ExigenciaDaRota UsoDeUsuarios = new(CalculoDePermissoes.Usuarios, Abas.U);
    readonly Cenario mundo = new();

    void EsquecerOPreparo()
    {
        mundo.Mock.Gravacoes.Clear();
        mundo.Mock.Auditoria.Clear();
        mundo.Mock.Erros.Clear();
        mundo.Mock.Chamadas.Clear();
    }

    [Fact]
    public async Task RN_275_contratacao_mudou_avisada_pelo_catalogo()
    {
        mundo.Mock.Contratar("conta-ebfga", "MOD-06");
        await mundo.Conceder("usr-ebfga-maria", ("MOD-06", "U"));
        EsquecerOPreparo();

        mundo.Mock.Descontratar("conta-ebfga", "MOD-06");
        await mundo.Mock.AvisarContratacaoMudou.Executar("conta-ebfga", Catalogo);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, mundo.Mock.Gravacoes);
        Assert.All(mundo.Mock.Auditoria, a => Assert.Equal(
            (Catalogo.TraceId, AcaoAuditada.AvisarContratacaoMudou, "conta-ebfga", "usr-dboaz-caio", "conta-ebfga"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem, a.Alvo)));
        Assert.NotEmpty(mundo.Mock.Chamadas);
        Assert.All(mundo.Mock.Chamadas, c => Assert.Equal(Catalogo, c));
    }

    [Fact]
    public async Task RN_275_permissoes_salvas_avisam_as_sessoes_na_execucao_do_put()
    {
        var versao = (await mundo.Ler("usr-ebfga-master", "usr-ebfga-maria")).Versao;
        mundo.Mock.Contratar("conta-ebfga", "MOD-05");

        await mundo.Mock.SalvarPermissoes.Executar(
            new SalvamentoDePermissoesDto("usr-ebfga-master", "usr-ebfga-maria", $"\"{versao}\"", Corpo(("MOD-05", "U"))), Trace);

        Assert.Equal(new ExecucaoDeQuemChama(Trace, "usr-ebfga-master", "conta-ebfga"), Assert.Single(mundo.Mock.Chamadas));
    }

    [Fact]
    public async Task RN_270_rota_atendida_nao_grava_nada()
    {
        await mundo.Mock.AutorizarPedidoAuditado.Executar(UsoDeUsuarios,
            new PedidoAutorizavelDto("STAFF", new Dictionary<string, string> { ["MOD-03"] = "U" }),
            new ExecucaoDaRotaDto(Trace, AcaoAuditada.DesativarUsuario, "conta-ebfga", "usr-ebfga-ana", "usr-ebfga-paulo"));

        Assert.Empty(mundo.Mock.Gravacoes);
    }

    [Fact]
    public async Task RN_270_sem_o_modulo_grava_o_iniciado_e_o_alerta()
    {
        var falha = await Assert.ThrowsAsync<FalhaDeAutorizacao>(() => mundo.Mock.AutorizarPedidoAuditado.Executar(UsoDeUsuarios,
            new PedidoAutorizavelDto("STAFF", new Dictionary<string, string>()),
            new ExecucaoDaRotaDto(Trace, AcaoAuditada.DesativarUsuario, "conta-ebfga", "usr-ebfga-ana", "usr-ebfga-paulo")));

        Assert.Equal("ACESSO_NEGADO", falha.Codigo);
        Assert.Equal(new[] { "INICIADO", "ALERTA ACESSO_NEGADO" }, mundo.Mock.Gravacoes);
        var iniciado = Assert.Single(mundo.Mock.Auditoria);
        Assert.Equal((Trace, AcaoAuditada.DesativarUsuario, "conta-ebfga", "usr-ebfga-ana", "usr-ebfga-paulo"),
            (iniciado.TraceId, iniciado.Acao, iniciado.ContaId, iniciado.Quem, iniciado.Alvo));
        var erro = Assert.Single(mundo.Mock.Erros);
        Assert.Equal((Trace, AcaoAuditada.DesativarUsuario, "usr-ebfga-ana", "usr-ebfga-paulo", NivelDoErro.Alerta),
            (erro.TraceId, erro.Acao, erro.Quem, erro.Alvo, erro.Nivel));
    }

    [Fact]
    public async Task RN_270_post_usuarios_sem_o_modulo_vai_sem_alvo()
    {
        await Assert.ThrowsAsync<FalhaDeAutorizacao>(() => mundo.Mock.AutorizarPedidoAuditado.Executar(UsoDeUsuarios,
            new PedidoAutorizavelDto("STAFF", new Dictionary<string, string> { ["MOD-03"] = "C" }),
            new ExecucaoDaRotaDto(Trace, AcaoAuditada.CriarUsuario, "conta-ebfga", "usr-ebfga-ana", null)));

        Assert.Null(Assert.Single(mundo.Mock.Auditoria).Alvo);
        Assert.Null(Assert.Single(mundo.Mock.Erros).Alvo);
    }
}
