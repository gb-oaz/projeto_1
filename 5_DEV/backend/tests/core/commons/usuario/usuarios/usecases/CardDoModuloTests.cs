using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>O card guardado, a recontagem, o fechamento, o ao vivo e as preferências (RN-189, RN-280, RN-281, RN-283, 15 RN-276 a RN-279).</summary>
public class CardDoModuloTests
{
    readonly UsuariosEmMemoria mock = new Cenario().Mock;

    CardsEmMemoria Cards => mock.Cards;

    static NumerosDoCard DaEscola(int participantes) => new(2, 37 + participantes,
        [new(NomesDoCard.Staff, 37), new(NomesDoCard.Participantes, participantes), new(NomesDoCard.Pendentes, 4)],
        [new(NomesDoCard.SemAcesso30Dias, 14), new(NomesDoCard.Desativados, 6), new(NomesDoCard.CodigosVencidos, 2)]);

    [Fact]
    public async Task RN_189_card_da_instituicao_contado_na_primeira_leitura()
    {
        Cards.Numeros[(PerfilCard.Instituicao, "conta-ebfga", null)] = DaEscola(298);
        Cards.Tempo[(PerfilCard.Instituicao, "conta-ebfga", null)] = new(2, new Dictionary<string, int> { [NomesDoCard.SemAcesso30Dias] = 14 });

        var card = await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-master"));

        Assert.Equal(("MOD-03", "INSTITUICAO", 1, 2), (card.Modulo, card.Perfil, card.Versao, card.Selo));
        Assert.Equal(new ReduzidaDto(335, "usuários ativos"), card.Reduzida);
        Assert.Equal(1, Cards.Gravacoes["card-ebfga-INSTITUICAO"]);
        // a leitura seguinte não grava nem sobe a versao
        Assert.Equal(1, (await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-master"))).Versao);
        Assert.Equal(1, Cards.Gravacoes["card-ebfga-INSTITUICAO"]);
    }

    [Fact]
    public async Task RN_280_selo_e_sem_acesso_refeitos_na_leitura_sem_subir_a_versao()
    {
        Cards.Numeros[(PerfilCard.Instituicao, "conta-ebfga", null)] = DaEscola(298);
        await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-master"));
        Cards.Tempo[(PerfilCard.Instituicao, "conta-ebfga", null)] = new(3, new Dictionary<string, int>
        {
            [NomesDoCard.SemAcesso30Dias] = 15,
            [NomesDoCard.CodigosVencidos] = 3,
        });

        var card = await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-master"));

        Assert.Equal((1, 3), (card.Versao, card.Selo));
        Assert.Equal([15, 6, 3], card.Expandida.Lista.Select(l => l.Valor));
    }

    [Theory]
    [InlineData("usr-ebfga-maria", "card-ebfga-STAFF-maria", "STAFF")]
    [InlineData("usr-preis-pedro", "card-preis-AUTONOMO", "AUTONOMO")]
    [InlineData("usr-dboaz-admin", "card-dboaz-DEBOAZ", "DEBOAZ")]
    [InlineData("usr-dboaz-caio", "card-dboaz-DEBOAZ", "DEBOAZ")]
    public async Task RN_189_card_de_cada_perfil(string quem, string id, string perfil)
    {
        Assert.Equal(perfil, (await mock.ObterCardUsuarios.Executar(new QuemPedeDto(quem))).Perfil);
        Assert.NotNull(Cards.Card(id));
    }

    [Fact]
    public async Task RN_189_participante_nao_tem_card() =>
        await Falha("ACESSO_NEGADO", () => mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-lucas")));

    [Fact]
    public async Task RN_278_desativar_reconta_o_card_da_conta_na_mesma_execucao()
    {
        Cards.Numeros[(PerfilCard.Instituicao, "conta-ebfga", null)] = DaEscola(298);
        await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-master"));
        Cards.Numeros[(PerfilCard.Instituicao, "conta-ebfga", null)] = DaEscola(297);

        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());

        Assert.Equal((2, 334), (Cards.Card("card-ebfga-INSTITUICAO")!.Versao, Cards.Card("card-ebfga-INSTITUICAO")!.Atual.Valor));
        // o Paulo foi criado pela conta master: nenhum card de staff, nem o DEBOAZ
        Assert.Null(Cards.Card("card-dboaz-DEBOAZ"));
    }

    [Fact]
    public async Task RN_280_usuario_criado_por_uma_staff_reconta_o_card_dela()
    {
        await mock.AtualizarCardUsuarios.Executar(new MudancaNoCadastroDto("conta-ebfga", "usr-ebfga-ana"));
        Assert.NotNull(Cards.Card("card-ebfga-INSTITUICAO"));
        Assert.Equal("usr-ebfga-joao", Cards.Card("card-ebfga-STAFF-joao")!.UsuarioId);
    }

    [Fact]
    public async Task RN_280_mudanca_na_conta_reconta_o_deboaz()
    {
        await mock.AtualizarCardUsuarios.Executar(new MudancaNoCadastroDto("conta-preis"));
        Assert.NotNull(Cards.Card("card-preis-AUTONOMO"));
        Assert.Equal("conta-dboaz", Cards.Card("card-dboaz-DEBOAZ")!.ContaId);
    }

    [Fact]
    public async Task RN_280_conta_do_time_e_conta_inexistente_nao_tem_card_de_conta()
    {
        await mock.AtualizarCardUsuarios.Executar(new MudancaNoCadastroDto("conta-dboaz", "usr-dboaz-caio"));
        await mock.AtualizarCardUsuarios.Executar(new MudancaNoCadastroDto("conta-xxxxx"));
        Assert.Empty(Cards.Gravacoes);
    }

    [Fact]
    public async Task RN_278_fechamento_uma_vez_por_dia_e_sob_demanda()
    {
        await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-ebfga-master"));
        await mock.ObterCardUsuarios.Executar(new QuemPedeDto("usr-preis-pedro"));
        mock.Relogio.Avancar(TimeSpan.FromDays(1));

        Assert.True(await mock.FecharCardsDoDia.Executar());
        Assert.Equal((2, 2), (Cards.Card("card-ebfga-INSTITUICAO")!.Versao, Cards.Card("card-ebfga-INSTITUICAO")!.Dias.Count));
        Assert.False(await mock.FecharCardsDoDia.Executar());
        Assert.True(await mock.FecharCardsDoDia.Executar(sobDemanda: true));
        Assert.Equal((3, 2), (Cards.Card("card-preis-AUTONOMO")!.Versao, Cards.Card("card-preis-AUTONOMO")!.Dias.Count));
    }

    [Fact]
    public async Task RN_279_intervalo_invalido_e_participante_nao_conectam()
    {
        await Falha("DADOS_INVALIDOS", () => mock.AcompanharCardAoVivo.Abrir(new AberturaAoVivoDto("usr-ebfga-master", "s1", "10")));
        await Falha("ACESSO_NEGADO", () => mock.AcompanharCardAoVivo.Abrir(new AberturaAoVivoDto("usr-ebfga-lucas", "s1", "30")));
        Assert.Empty(Cards.Abertas);
    }

    [Fact]
    public async Task RN_279_quarta_conexao_encerra_a_mais_antiga()
    {
        var conexoes = new List<ConexaoAoVivo>();
        for (var i = 0; i < 4; i++) conexoes.Add((await mock.AcompanharCardAoVivo.Abrir(new AberturaAoVivoDto("usr-ebfga-master", $"s{i}", null))).Conexao);

        Assert.Equal(MotivoEncerramento.LimiteConexoes, Assert.Single(Cards.Encerradas).Value);
        Assert.Equal(conexoes[0].Id, Cards.Encerradas.Keys.Single());
        Assert.Equal(new PassoAoVivoDto(Encerrado: MotivoEncerramento.LimiteConexoes), await mock.AcompanharCardAoVivo.Proximo(conexoes[0]));

        await mock.AcompanharCardAoVivo.Fechar(conexoes[1]);
        Assert.Equal(2, Cards.Abertas["usr-ebfga-master"].Count);
    }

    [Fact]
    public async Task RN_279_card_so_quando_a_versao_muda_e_ping_a_cada_30_s()
    {
        Cards.Numeros[(PerfilCard.Instituicao, "conta-ebfga", null)] = DaEscola(298);
        var (conexao, primeiro) = await mock.AcompanharCardAoVivo.Abrir(new AberturaAoVivoDto("usr-ebfga-master", "s1", "5"));
        Cards.AoEsperar = tempo => mock.Relogio.Avancar(tempo);

        // a versao não mudou: o primeiro passo é o ping de 30 s
        Assert.True((await mock.AcompanharCardAoVivo.Proximo(conexao)).Ping);
        Assert.True(Cards.Renovacoes >= 5);

        Cards.Numeros[(PerfilCard.Instituicao, "conta-ebfga", null)] = DaEscola(299);
        await mock.AtualizarCardUsuarios.Executar(new MudancaNoCadastroDto("conta-ebfga", "usr-ebfga-lucas"));
        var passo = await mock.AcompanharCardAoVivo.Proximo(conexao);

        Assert.Equal(primeiro.Versao + 1, passo.Card!.Versao);
        Assert.All(Cards.Esperas, e => Assert.True(e <= TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RN_281_sessao_terminada_e_usuarios_retirado_encerram()
    {
        var (conexao, _) = await mock.AcompanharCardAoVivo.Abrir(new AberturaAoVivoDto("usr-ebfga-maria", "s1", "5"));
        Cards.AoEsperar = tempo => mock.Relogio.Avancar(tempo);
        Cards.SemOCard.Add("usr-ebfga-maria");
        Assert.Equal(MotivoEncerramento.AcessoNegado, (await mock.AcompanharCardAoVivo.Proximo(conexao)).Encerrado);

        Cards.SessoesTerminadas.Add("s1");
        Assert.Equal(MotivoEncerramento.SessaoTerminada, (await mock.AcompanharCardAoVivo.Proximo(conexao)).Encerrado);
    }

    [Fact]
    public async Task RN_283_preferencias_padrao_salvas_e_invalidas()
    {
        Assert.Equal(new PreferenciasDto("NA_ABERTURA", 300), await mock.ObterPreferencias.Executar(new QuemPedeDto("usr-ebfga-lucas")));

        Assert.Equal(new PreferenciasDto("AO_VIVO", 15),
            await mock.SalvarPreferencias.Executar(new NovasPreferenciasDto("usr-ebfga-master", "AO_VIVO", 15)));
        Assert.Equal(new PreferenciasDto("AO_VIVO", 15), await mock.ObterPreferencias.Executar(new QuemPedeDto("usr-ebfga-master")));

        var falha = await Falha("DADOS_INVALIDOS", () => mock.SalvarPreferencias.Executar(new NovasPreferenciasDto("usr-ebfga-master", "AO_VIVO", 10)));
        Assert.Equal("intervaloAoVivo", falha.Campo);
    }
}
