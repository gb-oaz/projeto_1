using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Domain;

/// <summary>O card guardado, o calendário de Brasília, as preferências e a conexão ao vivo (RN-280, RN-281, RN-283, 15 RN-276 a RN-279).</summary>
public class CardUsuariosTests
{
    // 2026-11-08 12:00 em Brasília
    static readonly DateTimeOffset Domingo = new(2026, 11, 8, 15, 0, 0, TimeSpan.Zero);

    static NumerosDoCard DaEscola(int participantes, int selo = 2) => new(selo, 37 + participantes,
        [new(NomesDoCard.Staff, 37), new(NomesDoCard.Participantes, participantes), new(NomesDoCard.Pendentes, 4)],
        [new(NomesDoCard.SemAcesso30Dias, 14), new(NomesDoCard.Desativados, 6), new(NomesDoCard.CodigosVencidos, 2)]);

    static CardUsuarios Novo() => CardUsuarios.Novo("EBFGA", PerfilCard.Instituicao, "conta-ebfga", null, null, Domingo);

    [Fact]
    public void RN_280_id_do_card_guardado()
    {
        Assert.Equal("card-ebfga-INSTITUICAO", CardUsuarios.IdDo("EBFGA", PerfilCard.Instituicao, null));
        Assert.Equal("card-ebfga-STAFF-maria", CardUsuarios.IdDo("EBFGA", PerfilCard.Staff, "maria"));
        Assert.Equal("card-dboaz-DEBOAZ", CardUsuarios.IdDo("DBOAZ", PerfilCard.Deboaz, null));
    }

    [Fact]
    public void RN_278_aplicar_sobe_a_versao_so_quando_muda()
    {
        var card = Novo();
        Assert.True(card.Aplicar(DaEscola(298), Domingo));
        Assert.Equal((1, 335), (card.Versao, card.Atual.Valor));
        Assert.False(card.Aplicar(DaEscola(298), Domingo.AddMinutes(5)));
        Assert.Equal(1, card.Versao);
        // o caso de uso que muda o cadastro sobe a versao mesmo com os números iguais (17 v104 RN-280)
        Assert.True(card.Aplicar(DaEscola(298), Domingo.AddMinutes(6), sempre: true));
        Assert.Equal(2, card.Versao);

        var depois = Domingo.AddHours(1);
        Assert.True(card.Aplicar(DaEscola(299), depois));
        Assert.Equal((3, 336, depois), (card.Versao, card.Atual.Valor, card.AtualizadoEm));
        Assert.Equal([new PontoDaSerie("2026-11-08", 336)], card.Dias);
        Assert.Equal([new PontoDaSerie("2026-W45", 336)], card.Semanas);
        Assert.Equal([new PontoDaSerie("2026-11", 336)], card.Meses);
    }

    [Fact]
    public void RN_278_dia_novo_entra_no_fim_mesmo_sem_mudar_os_numeros()
    {
        var card = Novo();
        card.Aplicar(DaEscola(298), Domingo);
        Assert.True(card.Aplicar(DaEscola(298), Domingo.AddDays(1)));
        Assert.Equal(["2026-11-08", "2026-11-09"], card.Dias.Select(d => d.Chave));
        Assert.Equal(["2026-W45", "2026-W46"], card.Semanas.Select(d => d.Chave));
        Assert.Single(card.Meses);
    }

    [Fact]
    public void RN_278_fechamento_abre_o_dia_a_semana_e_o_mes_e_corta_os_mais_antigos()
    {
        var ultimo = new DateOnly(2027, 2, 28);
        var dias = Enumerable.Range(0, 400).Select(i => new PontoDaSerie(CalendarioDoCard.ChaveDoDia(ultimo.AddDays(i - 399)), 1000 + i));
        var semanas = Enumerable.Range(0, 106).Select(i => new PontoDaSerie(CalendarioDoCard.ChaveDaSemana(ultimo.AddDays(7 * (i - 105))), 2000 + i));
        var meses = Enumerable.Range(0, 24).Select(i => new PontoDaSerie(CalendarioDoCard.ChaveDoMes(ultimo.AddMonths(i - 23)), 3000 + i));
        var card = new CardUsuarios("card-ebfga-INSTITUICAO", "conta-ebfga", PerfilCard.Instituicao, null, 418, Domingo, DaEscola(298),
            dias, semanas, meses);
        var meiaNoite = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.FromHours(-3));

        card.FecharDia(DaEscola(299), meiaNoite);

        Assert.Equal(419, card.Versao);
        Assert.Equal((400, 106, 24), (card.Dias.Count, card.Semanas.Count, card.Meses.Count));
        Assert.Equal(("2027-03-01", "2027-W09", "2027-03"), (card.Dias[^1].Chave, card.Semanas[^1].Chave, card.Meses[^1].Chave));
        Assert.Equal(1001, card.Dias[0].Valor);
        var (dia, semana, mes) = card.Series();
        Assert.Equal([.. Enumerable.Range(389, 11).Select(i => 1000 + i), 336], dia);
        Assert.Equal(12, semana.Count);
        Assert.Equal(336, mes[^1]);
    }

    [Fact]
    public void RN_277_series_com_12_numeros_e_zeros_antes_do_historico()
    {
        var card = Novo();
        card.Aplicar(DaEscola(298), Domingo);
        var (dia, semana, mes) = card.Series();
        Assert.Equal([.. Enumerable.Repeat(0, 11), 335], dia);
        Assert.Equal(12, semana.Count);
        Assert.Equal(12, mes.Count);
    }

    [Fact]
    public void RN_280_tempo_refeito_na_leitura()
    {
        var lido = DaEscola(298).ComTempo(new NumerosNoTempo(3, new Dictionary<string, int>
        {
            [NomesDoCard.SemAcesso30Dias] = 15,
            [NomesDoCard.CodigosVencidos] = 1,
        }));
        Assert.Equal(3, lido.Selo);
        Assert.Equal([15, 6, 1], lido.Lista.Select(l => l.Valor));
        Assert.Equal(335, lido.Valor);
    }

    [Fact]
    public void RN_276_vazio_sem_numeros()
    {
        Assert.True(new NumerosDoCard(0, 0, [new(NomesDoCard.Staff, 0)], []).Vazio);
        Assert.False(new NumerosDoCard(0, 0, [], [new(NomesDoCard.Desativados, 1)]).Vazio);
        Assert.False(DaEscola(0, selo: 0).Vazio);
    }

    [Fact]
    public void RN_189_so_a_conta_master_e_vazio()
    {
        NumerosDoCard SoAMaster(int staff, int participantes, int master = 1) => new(0, staff + participantes,
            [new(NomesDoCard.Staff, staff), new(NomesDoCard.Participantes, participantes), new(NomesDoCard.Pendentes, 0)],
            [new(NomesDoCard.SemAcesso30Dias, 0), new(NomesDoCard.Desativados, 0), new(NomesDoCard.CodigosVencidos, 0)], master);

        var soAMaster = SoAMaster(staff: 1, participantes: 0);
        Assert.True(soAMaster.Vazio);
        Assert.Equal((1, 1), (soAMaster.Valor, soAMaster.Apoio[0].Valor));
        Assert.False(SoAMaster(staff: 2, participantes: 0).Vazio);
        Assert.False(SoAMaster(staff: 1, participantes: 1).Vazio);
        Assert.False(SoAMaster(staff: 1, participantes: 0, master: 0).Vazio);
        Assert.False(SoAMaster(staff: 1, participantes: 0).Iguais(SoAMaster(staff: 1, participantes: 0, master: 0)));
    }

    [Fact]
    public void RN_276_dto_no_formato_comum()
    {
        var card = Novo();
        card.Aplicar(DaEscola(298), Domingo);
        var dto = CardDoModuloDto.De(card, card.Atual);
        Assert.Equal(("MOD-03", "INSTITUICAO", 1, false, 2), (dto.Modulo, dto.Perfil, dto.Versao, dto.Vazio, dto.Selo));
        Assert.Equal(new ReduzidaDto(335, "usuários ativos"), dto.Reduzida);
        Assert.Equal(["staff", "participantes", "pendentes"], dto.Normal.Apoio.Select(l => l.Nome));
        Assert.Equal(["Sem acesso há 30 dias", "Desativados", "Códigos vencidos"], dto.Expandida.Lista.Select(l => l.Nome));
    }

    [Fact]
    public void RN_280_nomes_e_rotulos_de_cada_perfil()
    {
        Assert.Equal("contas ativas", NomesDoCard.Rotulo(PerfilCard.Deboaz));
        Assert.Equal("usuários ativos", NomesDoCard.Rotulo(PerfilCard.Autonomo));
        Assert.Equal("usuários ativos", NomesDoCard.Rotulo(PerfilCard.Staff));
        Assert.Equal("Master sem acesso há 30 dias", NomesDoCard.Nome(NomesDoCard.MasterSemAcesso30Dias));
        Assert.Equal("criados no mês", NomesDoCard.Nome(NomesDoCard.CriadosNoMes));
        Assert.Equal("outro", NomesDoCard.Nome("outro"));
    }

    [Fact]
    public void Textos_do_perfil_e_do_motivo()
    {
        foreach (var perfil in Enum.GetValues<PerfilCard>()) Assert.Equal(perfil, TextosDoCard.PerfilDe(perfil.Texto()));
        foreach (var motivo in Enum.GetValues<MotivoEncerramento>()) Assert.Equal(motivo, TextosDoCard.MotivoDe(motivo.Texto()));
        Assert.Null(TextosDoCard.PerfilDe("PARTICIPANTE"));
        Assert.Null(TextosDoCard.MotivoDe(null));
    }

    [Fact]
    public void RN_278_calendario_de_brasilia()
    {
        Assert.Equal(new DateOnly(2026, 11, 8), CalendarioDoCard.Dia(new DateTimeOffset(2026, 11, 9, 2, 59, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 9), CalendarioDoCard.Dia(new DateTimeOffset(2026, 11, 9, 3, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 3, 0, 0, TimeSpan.Zero), CalendarioDoCard.FimDoDia(Domingo));
        Assert.Equal("2027-W09", CalendarioDoCard.ChaveDaSemana(new DateOnly(2027, 3, 1)));
        Assert.Equal("2026-W53", CalendarioDoCard.ChaveDaSemana(new DateOnly(2026, 12, 31)));
    }

    [Theory]
    [InlineData(TipoUsuario.Administrador, false, PerfilCard.Deboaz)]
    [InlineData(TipoUsuario.MembroTime, false, PerfilCard.Deboaz)]
    [InlineData(TipoUsuario.Master, false, PerfilCard.Instituicao)]
    [InlineData(TipoUsuario.Master, true, PerfilCard.Autonomo)]
    [InlineData(TipoUsuario.Staff, false, PerfilCard.Staff)]
    public void RN_189_perfil_de_quem_pede(TipoUsuario tipo, bool autonomo, PerfilCard perfil)
    {
        var conta = Conta(autonomo);
        Assert.Equal(perfil, DonoDoCard.Perfil(Usuario(tipo), conta));
        Assert.Equal(autonomo ? PerfilCard.Autonomo : PerfilCard.Instituicao, DonoDoCard.DaConta(conta));
    }

    [Fact]
    public void RN_189_participante_nao_tem_card() =>
        Assert.Equal("ACESSO_NEGADO", Assert.Throws<FalhaDeUsuarios>(() => DonoDoCard.Perfil(Usuario(TipoUsuario.Participante), Conta(false))).Codigo);

    [Fact]
    public void RN_283_preferencias()
    {
        Assert.Equal(new PreferenciasDto("NA_ABERTURA", 300), PreferenciasDto.De(PreferenciasDeCards.Padrao));
        Assert.Equal(new PreferenciasDeCards(AtualizacaoCards.AoVivo, 15), new NovasPreferenciasDto("u", "AO_VIVO", 15).Validar());
        Assert.Equal("atualizacaoCards", Assert.Throws<FalhaDeUsuarios>(() => new NovasPreferenciasDto("u", "SEMPRE", 15).Validar()).Campo);
        Assert.Equal("intervaloAoVivo", Assert.Throws<FalhaDeUsuarios>(() => new NovasPreferenciasDto("u", "NA_ABERTURA", 10).Validar()).Campo);
        Assert.Equal("intervaloAoVivo", Assert.Throws<FalhaDeUsuarios>(() => new NovasPreferenciasDto("u", "NA_ABERTURA", null).Validar()).Campo);
    }

    [Theory]
    [InlineData(null, 300)]
    [InlineData("5", 5)]
    [InlineData("60", 60)]
    public void RN_279_intervalo_aceito(string? pedido, int segundos) =>
        Assert.Equal(TimeSpan.FromSeconds(segundos), new AberturaAoVivoDto("u", "s", pedido).Validar());

    [Theory]
    [InlineData("10")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void RN_279_intervalo_fora_da_lista(string pedido) =>
        Assert.Equal("intervalo", Assert.Throws<FalhaDeUsuarios>(() => new AberturaAoVivoDto("u", "s", pedido).Validar()).Campo);

    [Fact]
    public void RN_279_conexao_marca_a_conferencia_e_o_ping()
    {
        var conexao = new ConexaoAoVivo("c1", "u", "s", "card", TimeSpan.FromSeconds(5), 7, Domingo);
        Assert.Equal(Domingo.AddSeconds(5), conexao.ProximaAcao);
        Assert.False(conexao.HoraDeConferir(Domingo));
        Assert.True(conexao.HoraDeConferir(Domingo.AddSeconds(5)));
        Assert.Equal(Domingo.AddSeconds(10), conexao.ProximaConferencia);
        Assert.False(conexao.HoraDoPing(Domingo.AddSeconds(29)));
        Assert.True(conexao.HoraDoPing(Domingo.AddSeconds(31)));
        Assert.Equal(Domingo.AddSeconds(61), conexao.ProximoPing);
        conexao.Mandou(8);
        Assert.Equal(8, conexao.Versao);
    }

    static ContaAssinante Conta(bool autonomo) => new("conta-x", "XXXXX", "x", "X", autonomo ? TipoConta.Autonomo : TipoConta.Instituicao,
        EstadoConta.Ativa, "", "", "", Domingo);

    static UsuarioCadastrado Usuario(TipoUsuario tipo) =>
        new("usr-x-a", "conta-x", "XXXXX", tipo, "A", "a", "a", null, EstadoUsuario.Ativo, null, [], null, Domingo);
}
