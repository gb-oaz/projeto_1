using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>Sair e aparelhos conectados (RF-138, RF-141, RF-156, RF-157, RNF-203).</summary>
public class SessoesTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RF_138_sair_do_navegador_mantem_o_app()
    {
        var navegador = await c.MariaEntra();
        var app = await c.MariaEntra("APP");

        await c.Mock.SairDoAparelho.Executar(await c.Conferido(navegador), Trace);

        Assert.Equal(MotivoFim.Sair, c.Mock.Encerradas[Claims(navegador).Sid]);
        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(navegador));
        await c.PedidoAoHub(app);
    }

    [Fact]
    public async Task RF_156_lista_das_sessoes()
    {
        var app = await c.MariaEntra("APP");
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(90));
        var navegador = await c.MariaEntra();
        await c.LucasEntra();

        var lista = await c.Mock.ListarSessoes.Executar(new ConferenciaDeAcessoDto(navegador.TokenAcesso, Frente.Hub));

        Assert.Equal(
            [
                new SessaoListadaDto(Claims(navegador).Sid, "NAVEGADOR", c.Mock.Relogio.Agora, true),
                new SessaoListadaDto(Claims(app).Sid, "APP", c.Mock.Relogio.Agora.AddMinutes(-90), false),
            ],
            lista);
    }

    [Fact]
    public async Task RF_157_sair_de_um_aparelho_da_lista()
    {
        var navegador = await c.MariaEntra();
        var app = await c.MariaEntra("APP");

        await c.Mock.EncerrarSessao.Executar(await c.Conferido(app), Claims(navegador).Sid, Trace);

        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(navegador));
        await c.PedidoAoHub(app);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RF_157_sessao_de_outra_pessoa_ou_inexistente(bool doJoao)
    {
        var maria = await c.MariaEntra();
        var joao = await c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto("joao.eejsa@deboaz.com", "Xu@Xa182482", "APP"), Trace);
        var sid = doJoao ? Claims(joao).Sid : "naoexiste";

        var acesso = await c.Conferido(maria);
        await Falha("SESSAO_NAO_ENCONTRADA", () => c.Mock.EncerrarSessao.Executar(acesso, sid, Trace));
        await c.PedidoAoHub(joao);
    }

    [Fact]
    public async Task RF_157_sair_de_todos()
    {
        var navegador = await c.MariaEntra();
        var app = await c.MariaEntra("APP");
        var joao = await c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto("joao.eejsa@deboaz.com", "Xu@Xa182482", "APP"), Trace);

        await c.Mock.EncerrarTodasAsSessoes.Executar(await c.Conferido(app), Trace);

        Assert.All([navegador, app], s => Assert.Equal(MotivoFim.SairDeTodos, c.Mock.Encerradas[Claims(s).Sid]));
        await Falha("SESSAO_TERMINADA", () => c.Renovar(navegador));
        await Falha("SESSAO_TERMINADA", () => c.Renovar(app));
        await c.PedidoAoHub(joao);
    }

    [Theory]
    [InlineData(MotivoFim.UsuarioDesativado)]
    [InlineData(MotivoFim.NovaSenha)]
    [InlineData(MotivoFim.ContaIndisponivel)]
    public async Task RNF_203_mudanca_que_invalida_o_token_nos_dois_aparelhos(MotivoFim motivo)
    {
        var navegador = await c.MariaEntra();
        var app = await c.MariaEntra("APP");

        await c.Mock.EncerrarSessoesDoUsuario.Executar("usr-ebfga-maria", motivo, Chamada);

        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(navegador));
        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(app));
        Assert.Equal(motivo, c.Mock.Encerradas[Claims(app).Sid]);
    }
}
