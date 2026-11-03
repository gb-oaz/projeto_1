using Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.UseCases;

/// <summary>Histórico (RN-256) e a conferência das rotas pelo token (RF-147, RNF-208, RNF-220).</summary>
public class HistoricoEAutorizacaoTests
{
    readonly Cenario mundo = new();

    public HistoricoEAutorizacaoTests() => mundo.Mock.Contratar("conta-ebfga", "MOD-06");

    Task<IReadOnlyList<MudancaDto>> Historico(string quem, string alvo, DateTimeOffset? ate = null) =>
        mundo.Mock.LerHistoricoDePermissoes.Executar(new PedidoDePermissoesDto(quem, alvo, ate));

    [Fact]
    public async Task RN_256_o_que_fica_registrado()
    {
        var em = new DateTimeOffset(2026, 11, 1, 13, 20, 0, TimeSpan.Zero);
        mundo.Mock.Relogio.Agora = em;
        await mundo.Conceder("usr-ebfga-maria", ("MOD-06", "U"));

        var historico = await Historico("usr-ebfga-master", "usr-ebfga-maria");

        var mudanca = Assert.Single(historico);
        Assert.Equal((em, "usr-ebfga-master", 8), (mudanca.Em, mudanca.Por, mudanca.Versao));
        Assert.Equal(new Dictionary<string, string?> { ["MOD-06"] = null }, mudanca.Antes);
        Assert.Equal(new Dictionary<string, string?> { ["MOD-06"] = "U" }, mudanca.Depois);
    }

    [Fact]
    public async Task RN_256_a_propria_pessoa_nao_ve() =>
        await Falha("ACESSO_NEGADO", () => Historico("usr-ebfga-maria", "usr-ebfga-maria"));

    [Fact]
    public async Task RN_256_paginas_de_50_da_mais_nova_para_a_mais_antiga()
    {
        for (var i = 0; i < 51; i++)
        {
            mundo.Mock.Relogio.Avancar(TimeSpan.FromMinutes(1));
            await mundo.Conceder("usr-ebfga-maria", ("MOD-06", i % 2 == 0 ? "U" : "CU"));
        }

        var primeira = await Historico("usr-ebfga-master", "usr-ebfga-maria");
        Assert.Equal(50, primeira.Count);
        Assert.Equal(58, primeira[0].Versao);
        var segunda = await Historico("usr-ebfga-master", "usr-ebfga-maria", primeira[^1].Em);
        Assert.Equal(8, Assert.Single(segunda).Versao);
    }

    static PedidoAutorizavelDto Token(string papel, params (string Mod, string Abas)[] mods) =>
        new(papel, mods.ToDictionary(m => m.Mod, m => m.Abas));

    [Fact]
    public void RNF_208_acao_autorizada_pelo_token()
    {
        mundo.Mock.AutorizarPedido.Executar(new ExigenciaDaRota("MOD-05", Abas.U), Token("STAFF", ("MOD-05", "U")));
    }

    [Theory]
    [InlineData("STAFF", "C")]
    [InlineData("STAFF", "X")]
    [InlineData("PROFESSOR", "U")]
    public void RF_147_acao_sem_a_aba(string papel, string abas)
    {
        var falha = Assert.Throws<FalhaDeAutorizacao>(() =>
            mundo.Mock.AutorizarPedido.Executar(new ExigenciaDaRota("MOD-05", Abas.U), Token(papel, ("MOD-05", abas))));
        Assert.Equal("ACESSO_NEGADO", falha.Codigo);
    }

    [Fact]
    public void RNF_220_rota_sem_declaracao()
    {
        var token = Token("ADMINISTRADOR", ("MOD-03", "CU"));
        Assert.Equal("ACESSO_NEGADO", Assert.Throws<FalhaDeAutorizacao>(() => mundo.Mock.AutorizarPedido.Executar(null, token)).Codigo);
        Assert.Equal("ACESSO_NEGADO",
            Assert.Throws<FalhaDeAutorizacao>(() => mundo.Mock.AutorizarPedido.Executar(new ExigenciaDaRota(), token)).Codigo);
    }
}
