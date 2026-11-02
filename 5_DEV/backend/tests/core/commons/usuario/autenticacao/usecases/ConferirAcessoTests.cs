using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>Conferência de cada pedido (RN-224, RN-227, RNF-216, RNF-218).</summary>
public class ConferirAcessoTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task Pedido_valido_devolve_quem_pediu_e_conta_como_uso()
    {
        var sessao = await c.MariaEntra();
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(10));

        var acesso = await c.PedidoAoHub(sessao);

        Assert.Equal(("usr-ebfga-maria", "usr-ebfga-maria", "conta-ebfga", "EBFGA", "STAFF", "STAFF"),
            (acesso.Sub, acesso.UsuarioId, acesso.ContaId, acesso.Sigla, acesso.Tipo, acesso.Papel));
        Assert.Equal((Frente.Hub, Aparelho.Navegador, Claims(sessao).Sid), (acesso.Frente, acesso.Aparelho, acesso.Sid));
        Assert.Null(acesso.Quem);
        Assert.Null(acesso.Sala);
        Assert.Equal(3, acesso.Mods.Count);
        Assert.Equal(c.Mock.Relogio.Agora, Assert.Single(c.Mock.Sessoes).UltimaAcao);
    }

    [Fact]
    public async Task RNF_216_assinatura_errada()
    {
        var sessao = await c.MariaEntra();
        var partes = sessao.TokenAcesso.Split('.');
        var outro = (await c.MariaEntra("APP")).TokenAcesso.Split('.');
        await Falha("TOKEN_INVALIDO", () => c.PedidoAoHub(sessao with { TokenAcesso = $"{partes[0]}.{partes[1]}.{outro[2]}" }));
    }

    [Fact]
    public async Task RNF_216_token_vencido()
    {
        var sessao = await c.MariaEntra();
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(15));
        await Falha("TOKEN_INVALIDO", () => c.PedidoAoHub(sessao));
    }

    [Fact]
    public async Task RN_227_token_da_outra_frente()
    {
        var doHub = await c.MariaEntra();
        var doQuest = await c.LucasEntra();
        await Falha("TOKEN_INVALIDO", () => c.Mock.ConferirAcesso.Executar(new ConferenciaDeAcessoDto(doHub.TokenAcesso, Frente.Quest)));
        await Falha("TOKEN_INVALIDO", () => c.PedidoAoHub(doQuest));
    }

    [Fact]
    public async Task RN_224_a_sessao_vale_mais_que_o_token()
    {
        var sessao = await c.MariaEntra();
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(5));
        await c.Mock.EncerrarSessoesDoUsuario.Executar("usr-ebfga-maria", MotivoFim.UsuarioDesativado);
        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(sessao));
    }

    [Fact]
    public async Task RN_226_staff_no_app_com_tempo_curto_da_escola()
    {
        c.Mock.NovaConta("conta-ebfga", "EBFGA", tempo: TempoDeSessao.Padrao with { StaffApp = TimeSpan.FromMinutes(10) });
        var sessao = await c.MariaEntra("APP");
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(11));
        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(sessao));
    }

    [Fact]
    public async Task RNF_218_chave_antiga_aceita_ate_vencer()
    {
        var antigo = await c.MariaEntra();
        c.Mock.TrocarChave("deboaz-2027-02");
        var novo = await c.MariaEntra("APP");

        Assert.Equal("deboaz-2027-02", TokenDeAcesso.Ler(novo.TokenAcesso).Kid);
        await c.PedidoAoHub(antigo);
        await c.PedidoAoHub(novo);
        var chaves = await c.Mock.LerChavesPublicas.Executar();
        Assert.Equal(["deboaz-2026-11", "deboaz-2027-02"], chaves.Keys.Select(k => k.Kid).Order());
        Assert.All(chaves.Keys, k => Assert.Equal(("RSA", "RS256", "sig", "AQAB"), (k.Kty, k.Alg, k.Use, k.E)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nao.e.jwt")]
    public async Task Token_ausente_ou_malformado(string? token)
    {
        await Falha("TOKEN_INVALIDO", () => c.Mock.ConferirAcesso.Executar(new ConferenciaDeAcessoDto(token, Frente.Hub)));
    }
}
