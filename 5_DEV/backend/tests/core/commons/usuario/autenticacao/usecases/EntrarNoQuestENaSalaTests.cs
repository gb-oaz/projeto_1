using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>Entrar no quest (RF-135, RN-228, RN-247, RNF-204) e na sala (RF-136, RN-231).</summary>
public class EntrarNoQuestENaSalaTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RF_135_a_mae_entra_com_o_codigo_do_filho()
    {
        var sessao = await c.LucasEntra(tipo: "RESPONSAVEL", parentesco: "Mãe");

        var claims = Claims(sessao);
        Assert.Equal(("quest", "usr-ebfga-lucas", "PARTICIPANTE"), (claims.Aud, claims.Sub, claims.Tipo));
        Assert.Equal(new QuemEntrou(TipoQuemEntrou.Responsavel, "Mãe"), claims.Quem);
        var guardada = Assert.Single(c.Mock.Sessoes);
        Assert.Equal((Frente.Quest, TimeSpan.FromDays(30)), (guardada.Frente, guardada.TempoSemUso));
        Assert.Equal(claims.Quem, guardada.QuemEntrou);
        Assert.Equal("MAE", c.Mock.QuemDosUltimosAcessos["usr-ebfga-lucas"]);
    }

    [Fact]
    public async Task RN_184_codigo_em_minusculas()
    {
        var sessao = await c.LucasEntra("ebfga-lucas-0412");
        Assert.Equal(new QuemEntrou(TipoQuemEntrou.Participante), Claims(sessao).Quem);
        Assert.Null(c.Mock.QuemDosUltimosAcessos["usr-ebfga-lucas"]);
    }

    [Theory]
    [InlineData("EBFGA-ZECA-0000")]
    [InlineData("EBFGA-MARIA")]
    public async Task Codigo_errado_ou_de_quem_nao_e_participante(string codigo)
    {
        c.Mock.NovoUsuario("usr-x", "conta-ebfga", TipoUsuario.Staff, codigo: "EBFGA-MARIA");
        var falha = await Falha("CODIGO_INVALIDO", () => c.LucasEntra(codigo));
        Assert.Equal("Código de acesso inválido.", falha.Message);
    }

    [Fact]
    public async Task RNF_204_codigo_de_acesso_tambem_bloqueia()
    {
        for (var i = 0; i < 4; i++) await Falha("CODIGO_INVALIDO", () => c.LucasEntra("EBFGA-LUCAS-9999"));
        var bloqueio = await Falha("ENTRADA_BLOQUEADA", () => c.LucasEntra("EBFGA-LUCAS-9999"));
        Assert.Equal(c.Mock.Relogio.Agora.AddMinutes(15), bloqueio.TentarEm);
        await Falha("ENTRADA_BLOQUEADA", () => c.LucasEntra("ebfga-lucas-9999"));
    }

    [Fact]
    public async Task RF_140_participante_desativado()
    {
        c.Mock.MudarUsuario("usr-ebfga-lucas", EstadoUsuario.Desativado);
        await Falha("ACESSO_INDISPONIVEL", () => c.LucasEntra());
    }

    [Fact]
    public async Task RN_247_quest_sem_limite()
    {
        var sessoes = new List<SessaoAbertaDto>();
        for (var i = 0; i < 6; i++) sessoes.Add(await c.LucasEntra());

        Assert.Equal(6, c.Mock.Sessoes.Count);
        foreach (var sessao in sessoes) await c.Renovar(sessao);
    }

    [Fact]
    public async Task RF_136_entra_na_sala_aberta_e_a_sala_fecha()
    {
        c.Mock.AbrirSala("482107", "conta-ebfga", "MOD-05");

        var sessao = await c.Mock.EntrarNaSala.Executar(new EntradaNaSalaDto("482107"));

        var claims = Claims(sessao);
        Assert.Equal(("quest", "ANONIMO", "ANONIMO", "482107"), (claims.Aud, claims.Tipo, claims.Papel, claims.Sala));
        Assert.Equal(new Dictionary<string, string> { ["MOD-05"] = "U" }, claims.Mods);
        var acesso = await c.Mock.ConferirAcesso.Executar(new ConferenciaDeAcessoDto(sessao.TokenAcesso, Frente.Quest));
        Assert.Equal((null, "482107", Frente.Sala), (acesso.UsuarioId, acesso.Sala, acesso.Frente));
        var renovada = await c.Renovar(sessao);

        c.Mock.FecharSala("482107");
        await Falha("SESSAO_TERMINADA", () => c.Renovar(renovada));
        Assert.Equal(MotivoFim.SalaFechada, c.Mock.Encerradas[claims.Sid]);
    }

    [Fact]
    public async Task RN_231_sessao_anonima_sem_tempo_sem_uso()
    {
        c.Mock.AbrirSala("482107", "conta-ebfga", "MOD-05");
        var sessao = await c.Mock.EntrarNaSala.Executar(new EntradaNaSalaDto("482107"));

        c.Mock.Relogio.Avancar(TimeSpan.FromHours(2));

        var renovada = await c.Renovar(sessao);
        await c.Mock.ConferirAcesso.Executar(new ConferenciaDeAcessoDto(renovada.TokenAcesso, Frente.Quest));
    }

    [Fact]
    public async Task RF_136_sala_fechada()
    {
        await Falha("SALA_FECHADA", () => c.Mock.EntrarNaSala.Executar(new EntradaNaSalaDto("000000")));
    }
}
