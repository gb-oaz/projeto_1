using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>
/// PUT /usuarios/{id}/permissoes auditado pelo Executar, ação EDITAR_PERMISSOES
/// (RN-178, RN-256, RN-270, RN-271): "INICIADO" e "CONCLUIDO" em "auditoria" além
/// de "registrosPermissao"; a recusa é ALERTA e a falha inesperada, ERRO.
/// </summary>
public class SalvarPermissoesAuditadoTests
{
    static readonly TraceId Trace = new("trc-7f3a9c21");
    readonly Cenario mundo = new();

    public SalvarPermissoesAuditadoTests() => mundo.Mock.Contratar("conta-ebfga", "MOD-05");

    Task<PermissoesSalvasDto> Salvar(string quem, string alvo, string ifMatch, params (string Mod, string? Abas)[] mods) =>
        mundo.Mock.SalvarPermissoes.Executar(new SalvamentoDePermissoesDto(quem, alvo, ifMatch, Corpo(mods)), Trace);

    [Fact]
    public async Task RN_178_permissoes_auditadas_nas_duas_colecoes()
    {
        await Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-05", "U"));

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, mundo.Mock.Gravacoes);
        Assert.All(mundo.Mock.Auditoria, a => Assert.Equal(
            (Trace, AcaoAuditada.EditarPermissoes, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-maria"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem, a.Alvo)));
        Assert.Equal(8, Assert.Single(mundo.Mock.Registro).Versao);
        Assert.Empty(mundo.Mock.Erros);
    }

    [Fact]
    public async Task RN_270_recusa_e_alerta_antes_de_sair()
    {
        await Assert.ThrowsAsync<FalhaDeAutorizacao>(() => Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"6\"", ("MOD-05", "U")));

        Assert.Equal(new[] { "INICIADO", "ALERTA VERSAO_DESATUALIZADA" }, mundo.Mock.Gravacoes);
        var erro = Assert.Single(mundo.Mock.Erros);
        Assert.Equal((Trace, AcaoAuditada.EditarPermissoes, NivelDoErro.Alerta, "usr-ebfga-master", "usr-ebfga-maria"),
            (erro.TraceId, erro.Acao, erro.Nivel, erro.Quem, erro.Alvo));
        Assert.Empty(mundo.Mock.Registro);
    }

    [Fact]
    public async Task RN_270_falha_inesperada_e_erro()
    {
        var queda = new InvalidOperationException("o banco caiu no meio da gravação");
        mundo.Mock.FalhaAoSalvar = queda;

        Assert.Same(queda, await Assert.ThrowsAsync<InvalidOperationException>(
            () => Salvar("usr-ebfga-master", "usr-ebfga-maria", "\"7\"", ("MOD-05", "U"))));

        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, mundo.Mock.Gravacoes);
        Assert.Equal((RegistroDeErro.ErroInesperado, NivelDoErro.Erro), (mundo.Mock.Erros[0].Codigo, mundo.Mock.Erros[0].Nivel));
    }

    [Fact]
    public async Task RN_178_quem_nao_existe_vai_sem_conta()
    {
        await Assert.ThrowsAsync<FalhaDeAutorizacao>(() => Salvar("usr-ebfga-ninguem", "usr-ebfga-maria", "\"7\""));
        Assert.Equal(("usr-ebfga-ninguem", (string?)null), (mundo.Mock.Erros[0].Quem, mundo.Mock.Erros[0].ContaId));
    }
}
