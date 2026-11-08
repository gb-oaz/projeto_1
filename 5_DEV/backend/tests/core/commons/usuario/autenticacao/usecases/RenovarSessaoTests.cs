using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>Renovação (RF-137, RN-226, RN-230, RNF-209, RNF-213).</summary>
public class RenovarSessaoTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RNF_213_troca_a_cada_uso()
    {
        var a = await c.MariaEntra("APP");
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(20));

        var b = await c.Renovar(a);

        Assert.Equal(Claims(a).Sid, Claims(b).Sid);
        Assert.NotEqual(a.TokenRenovacao, b.TokenRenovacao);
        Assert.Equal(c.Mock.Relogio.Agora.AddMinutes(15), Claims(b).Exp);
        Assert.Equal(TokenDeRenovacao.Ler(b.TokenRenovacao)!.Resumo(), Assert.Single(c.Mock.Sessoes).ResumoRenovacao);
        await c.PedidoAoHub(b);
    }

    [Fact]
    public async Task RNF_213_renovacao_copiada_encerra_a_sessao()
    {
        var a = await c.MariaEntra();
        var b = await c.Renovar(a);

        await Falha("SESSAO_TERMINADA", () => c.Renovar(a));

        Assert.Equal(MotivoFim.CopiaDaRenovacao, c.Mock.Encerradas[Claims(a).Sid]);
        await Falha("SESSAO_TERMINADA", () => c.Renovar(b));
        await Falha("SESSAO_TERMINADA", () => c.PedidoAoHub(b));
    }

    [Fact]
    public async Task RF_137_tempo_sem_uso_passou()
    {
        var sessao = await c.MariaEntra();
        c.Mock.Relogio.Avancar(TimeSpan.FromHours(9));
        var falha = await Falha("SESSAO_TERMINADA", () => c.Renovar(sessao));
        Assert.Equal("Sua sessão terminou. Entre de novo.", falha.Message);
    }

    [Fact]
    public async Task RN_226_cada_pedido_conta_como_uso()
    {
        var sessao = await c.MariaEntra();
        for (var i = 0; i < 3; i++)
        {
            c.Mock.Relogio.Avancar(TimeSpan.FromHours(7));
            sessao = await c.Renovar(sessao);
        }
        c.Mock.Relogio.Avancar(TimeSpan.FromMinutes(14));
        await c.PedidoAoHub(sessao);
    }

    [Fact]
    public async Task RN_230_desativar_encerra()
    {
        var sessao = await c.MariaEntra();
        c.Mock.MudarUsuario("usr-ebfga-maria", EstadoUsuario.Desativado);
        await Falha("SESSAO_TERMINADA", () => c.Renovar(sessao));
        Assert.Equal(MotivoFim.UsuarioDesativado, c.Mock.Encerradas[Claims(sessao).Sid]);
    }

    [Fact]
    public async Task RN_175_conta_fora_de_ativa_encerra()
    {
        var sessao = await c.MariaEntra();
        c.Mock.MudarConta("conta-ebfga", EstadoConta.Desativada);
        await Falha("SESSAO_TERMINADA", () => c.Renovar(sessao));
        Assert.Equal(MotivoFim.ContaIndisponivel, c.Mock.Encerradas[Claims(sessao).Sid]);
    }

    [Fact]
    public async Task RN_230_mudar_permissao_nao_encerra()
    {
        var sessao = await c.MariaEntra();
        c.Mock.DefinirPermissoes("usr-ebfga-maria", new PermissoesNoToken("STAFF", new Dictionary<string, string>(), 8));
        await c.Mock.AvisarPermissoesNovas.Executar("usr-ebfga-maria", 8, Chamada);

        await Falha("PERMISSOES_DESATUALIZADAS", () => c.PedidoAoHub(sessao));

        var renovada = await c.Renovar(sessao);
        Assert.Equal(8, Claims(renovada).Pv);
        Assert.Empty(Claims(renovada).Mods);
        await c.PedidoAoHub(renovada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("lixo")]
    [InlineData("naoexiste.segredo")]
    public async Task Renovacao_desconhecida(string? token)
    {
        await Falha("SESSAO_TERMINADA", () => c.Mock.RenovarSessao.Executar(new RenovacaoDto(token), Trace));
    }

    [Fact]
    public async Task RNF_218_renovacao_assina_com_a_chave_nova()
    {
        var sessao = await c.MariaEntra();
        c.Mock.TrocarChave("deboaz-2027-02");
        c.Mock.RevogarChave("deboaz-2026-11");

        await Falha("TOKEN_INVALIDO", () => c.PedidoAoHub(sessao));
        var renovada = await c.Renovar(sessao);
        Assert.Equal("deboaz-2027-02", TokenDeAcesso.Ler(renovada.TokenAcesso).Kid);
        await c.PedidoAoHub(renovada);
    }
}
