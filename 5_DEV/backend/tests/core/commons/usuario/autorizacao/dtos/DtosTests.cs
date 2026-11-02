using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autorizacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autorizacao.Dtos;

/// <summary>Validação do PUT /usuarios/{id}/permissoes (RNF-222).</summary>
public class DtosTests
{
    static SalvamentoDePermissoesDto Pedido(string? ifMatch, IReadOnlyDictionary<string, string?>? mods = null) =>
        new("usr-ebfga-master", "usr-ebfga-maria", ifMatch, mods ?? Corpo(("MOD-05", "U")));

    [Theory]
    [InlineData("\"7\"", 7)]
    [InlineData("7", 7)]
    [InlineData(" \"12\" ", 12)]
    public void If_match_com_ou_sem_aspas(string ifMatch, int versao) => Assert.Equal(versao, Pedido(ifMatch).VersaoLida());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sem_if_match_e_versao_ausente(string? ifMatch) =>
        Assert.Equal("VERSAO_AUSENTE", Assert.Throws<FalhaDeAutorizacao>(() => Pedido(ifMatch).VersaoLida()).Codigo);

    [Theory]
    [InlineData("\"sete\"")]
    [InlineData("0")]
    [InlineData("W/\"7\"")]
    public void If_match_invalido(string ifMatch)
    {
        var falha = Assert.Throws<FalhaDeAutorizacao>(() => Pedido(ifMatch).VersaoLida());
        Assert.Equal(("DADOS_INVALIDOS", "If-Match"), (falha.Codigo, falha.Campo));
    }

    [Fact]
    public void Mods_lidos()
    {
        Assert.Equal(Mods(("MOD-03", "CU"), ("MOD-05", "U")), Pedido("1", Corpo(("MOD-03", "CU"), ("MOD-05", "U"))).ModsLidos());
        Assert.Empty(Pedido("1", Corpo()).ModsLidos());
    }

    [Fact]
    public void Mods_ausentes_ou_com_abas_fora_do_contrato()
    {
        var semMods = new SalvamentoDePermissoesDto("a", "b", "1", null);
        Assert.Equal("mods", Assert.Throws<FalhaDeAutorizacao>(() => semMods.ModsLidos()).Campo);
        Assert.Equal("mods.MOD-05", Assert.Throws<FalhaDeAutorizacao>(() => Pedido("1", Corpo(("MOD-05", "X"))).ModsLidos()).Campo);
        Assert.Equal("mods.MOD-06", Assert.Throws<FalhaDeAutorizacao>(() => Pedido("1", Corpo(("MOD-06", null))).ModsLidos()).Campo);
    }
}
