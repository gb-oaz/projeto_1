using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>Entrar no hub (RF-134, RF-139, RF-140, RF-158, RN-225, RN-226, RN-247, RNF-204, RNF-215).</summary>
public class EntrarNoHubTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RF_134_staff_entra_pelo_navegador()
    {
        var sessao = await c.MariaEntra();

        Assert.Equal(("Bearer", 900, Aparelho.Navegador), (sessao.Tipo, sessao.ExpiraEm, sessao.Aparelho));
        var claims = Claims(sessao);
        Assert.Equal(("hub", "usr-ebfga-maria", "conta-ebfga", "EBFGA", "STAFF", "NAVEGADOR", "STAFF", 7),
            (claims.Aud, claims.Sub, claims.Conta, claims.Sigla, claims.Tipo, claims.Aparelho, claims.Papel, claims.Pv));
        Assert.Equal(new Dictionary<string, string> { ["MOD-03"] = "CU", ["MOD-04"] = "U", ["MOD-05"] = "U" }, claims.Mods);
        Assert.Equal(c.Mock.Relogio.Agora.AddMinutes(15), claims.Exp);
        Assert.Equal(c.Mock.Kid, TokenDeAcesso.Ler(sessao.TokenAcesso).Kid);
        Assert.StartsWith(claims.Sid + ".", sessao.TokenRenovacao);
        Assert.Equal(c.Mock.Relogio.Agora, c.Mock.UltimosAcessos["usr-ebfga-maria"]);
    }

    [Fact]
    public async Task RNF_217_sessao_guardada_com_o_tempo_da_escola()
    {
        var sessao = await c.MariaEntra();

        var guardada = Assert.Single(c.Mock.Sessoes);
        Assert.Equal(Claims(sessao).Sid, guardada.Sid);
        Assert.Equal(("usr-ebfga-maria", "conta-ebfga", "EBFGA", Frente.Hub, Aparelho.Navegador, 7),
            (guardada.UsuarioId, guardada.ContaId, guardada.Sigla, guardada.Frente, guardada.Aparelho, guardada.Pv));
        Assert.Null(guardada.QuemEntrou);
        Assert.Equal(TimeSpan.FromHours(8), guardada.TempoSemUso);
        Assert.Equal(TokenDeRenovacao.Ler(sessao.TokenRenovacao)!.Resumo(), guardada.ResumoRenovacao);
    }

    [Fact]
    public async Task RN_225_uma_sessao_por_aparelho()
    {
        var navegador = await c.MariaEntra();
        var app = await c.MariaEntra("APP");

        Assert.Equal(Aparelho.App, app.Aparelho);
        Assert.Equal(TimeSpan.FromDays(7), c.Mock.Sessoes.Single(s => s.Aparelho == Aparelho.App).TempoSemUso);
        Assert.NotEqual(Claims(navegador).Sid, Claims(app).Sid);
        Assert.Equal(2, c.Mock.Sessoes.Count);
    }

    [Fact]
    public async Task RN_180_entrada_sem_diferenca_de_maiusculas()
    {
        var sessao = await c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto("Maria.EBFGA@Deboaz.com", SenhaDaMaria, "APP"));
        Assert.Equal("usr-ebfga-maria", Claims(sessao).Sub);
    }

    [Theory]
    [InlineData(EntradaDaMaria, "Ma!Silva000000")]
    [InlineData("zeca.ebfga@deboaz.com", SenhaDaMaria)]
    public async Task RF_139_mesma_resposta_para_senha_errada_e_entrada_inexistente(string entrada, string senha)
    {
        var falha = await Falha("ENTRADA_INVALIDA",
            () => c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto(entrada, senha, "NAVEGADOR")));

        Assert.Equal("Entrada ou senha incorretas.", falha.Message);
        Assert.Equal(1, c.Mock.Tentativas(AlvoTentativa.Entrada, entrada)!.ErrosSeguidos);
        Assert.Empty(c.Mock.Sessoes);
    }

    [Fact]
    public async Task Participante_nao_entra_no_hub()
    {
        c.Mock.NovoUsuario("usr-ebfga-ana", "conta-ebfga", TipoUsuario.Participante, "ana.ebfga@deboaz.com", SenhaDaMaria);
        await Falha("ENTRADA_INVALIDA",
            () => c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto("ana.ebfga@deboaz.com", SenhaDaMaria, "APP")));
    }

    [Fact]
    public async Task RF_140_escola_suspensa()
    {
        c.Mock.MudarConta("conta-ebfga", EstadoConta.Suspensa);
        await Falha("ACESSO_INDISPONIVEL", () => c.MariaEntra());
        Assert.Empty(c.Mock.Sessoes);
    }

    [Theory]
    [InlineData(EstadoUsuario.Desativado)]
    [InlineData(EstadoUsuario.Pendente)]
    public async Task RF_140_usuario_fora_de_uso(EstadoUsuario estado)
    {
        c.Mock.MudarUsuario("usr-ebfga-maria", estado);
        await Falha("ACESSO_INDISPONIVEL", () => c.MariaEntra());
        Assert.Empty(c.Mock.Sessoes);
    }

    [Fact]
    public async Task RF_158_quinta_senha_errada_bloqueia_ate_a_senha_certa()
    {
        for (var i = 0; i < 4; i++) await Falha("ENTRADA_INVALIDA", () => c.MariaEntra(senha: "errada"));

        var bloqueio = await Falha("ENTRADA_BLOQUEADA", () => c.MariaEntra(senha: "errada"));
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 9, 15, 0, TimeSpan.Zero), bloqueio.TentarEm);

        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(14));
        await Falha("ENTRADA_BLOQUEADA", () => c.MariaEntra());

        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await c.MariaEntra();
        Assert.Null(c.Mock.Tentativas(AlvoTentativa.Entrada, EntradaDaMaria));
    }

    [Fact]
    public async Task RNF_204_bloqueios_seguidos_dobram()
    {
        for (var i = 0; i < 4; i++) await Falha("ENTRADA_INVALIDA", () => c.MariaEntra(senha: "errada"));
        await Falha("ENTRADA_BLOQUEADA", () => c.MariaEntra(senha: "errada"));
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(15));

        for (var i = 0; i < 4; i++) await Falha("ENTRADA_INVALIDA", () => c.MariaEntra(senha: "errada"));
        var segundo = await Falha("ENTRADA_BLOQUEADA", () => c.MariaEntra(senha: "errada"));
        Assert.Equal(c.Mock.Relogio.Agora.AddMinutes(30), segundo.TentarEm);
    }

    [Fact]
    public async Task RN_247_sexta_sessao_do_hub_encerra_a_mais_antiga()
    {
        var sessoes = new List<SessaoAbertaDto>();
        for (var i = 0; i < 5; i++)
        {
            sessoes.Add(await c.MariaEntra());
            c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(1));
        }
        await c.PedidoAoHub(sessoes[0]);

        await c.MariaEntra("APP");

        Assert.Equal(5, c.Mock.Sessoes.Count);
        Assert.Equal(MotivoFim.LimiteDoHub, c.Mock.Encerradas[Claims(sessoes[1]).Sid]);
        await Falha("SESSAO_TERMINADA", () => c.Renovar(sessoes[1]));
        await c.Renovar(sessoes[0]);
    }

    [Fact]
    public async Task RN_226_conta_master_usa_o_tempo_do_time_deboaz()
    {
        var tempoDoTime = TempoDeSessao.Padrao with { MastersNavegador = TimeSpan.FromHours(2) };
        c.Mock.NovaConta(AutenticacaoEmMemoria.ContaDoTime, "DBOAZ", tempo: tempoDoTime);

        await c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto("master.ebfga@deboaz.com", "Xu@Xa182482", "NAVEGADOR"));

        Assert.Equal(TimeSpan.FromHours(2), Assert.Single(c.Mock.Sessoes).TempoSemUso);
    }

    [Fact]
    public async Task Pedido_sem_aparelho_e_dado_invalido()
    {
        var falha = await Falha("DADOS_INVALIDOS",
            () => c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto(EntradaDaMaria, SenhaDaMaria, null)));
        Assert.Equal("aparelho", falha.Campo);
    }
}
