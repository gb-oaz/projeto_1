using Boaz.Core.Hub.Sac.Chamados.Domain;

namespace Boaz.Core.Hub.Tests.Sac.Chamados;

/// <summary>O agregado Chamado: as mudanças de status da RN-290, o responsável (RF-175), o prazo (RN-289) e a avaliação (RN-296).</summary>
public sealed class ChamadoTests
{
    static readonly DateTimeOffset Abertura = new(2026, 11, 12, 13, 0, 0, TimeSpan.Zero);
    static readonly ContaDoChamado Ebfga = new("conta-ebfga", "EBFGA", "Escola Bento Freitas", "INSTITUICAO");
    static readonly Autor Conta = new(TipoDeAutor.Conta, "usr-ebfga-master", "Escola Bento Freitas");
    static readonly Autor Caio = new(TipoDeAutor.Time, "usr-dboaz-caio", "Caio Lima");
    static readonly Autor Ana = new(TipoDeAutor.Time, "usr-dboaz-ana", "Ana Lima");

    static Chamado Novo() => Chamado.Abrir(new NumeroDoChamado(42), Ebfga, Conta, Categoria.ModuloComProblema, "Presença não salva a chamada",
        "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.", null, [], 8, Abertura);

    static Chamado No(StatusDoChamado status)
    {
        var c = Novo();
        if (status == StatusDoChamado.Aberto) return c;
        c.Assumir(Caio, false, Abertura.AddMinutes(10));
        if (status == StatusDoChamado.AguardandoCliente) c.MudarStatus(Caio, status, "Qual a turma?", Abertura.AddMinutes(20));
        if (status is StatusDoChamado.Resolvido or StatusDoChamado.Fechado) c.MudarStatus(Caio, StatusDoChamado.Resolvido, "Corrigido.", Abertura.AddHours(4));
        if (status == StatusDoChamado.Fechado) c.Avaliar(Conta, 5, null, Abertura.AddHours(5));
        return new Chamado(c.Dados);
    }

    static string Codigo(Action acao) => Assert.Throws<FalhaDoSac>(acao).Codigo;

    [Fact]
    public void RF_171_abrir_com_o_prazo_de_8_h_uteis()
    {
        var c = Novo();
        Assert.Equal((StatusDoChamado.Aberto, 1L), (c.Dados.Status, c.Dados.Versao));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero), c.Dados.PrazoResposta);
        Assert.Equal(Abertura, c.Dados.EsperandoTimeDesde);
        var evento = Assert.Single(c.Novos);
        Assert.Equal((TipoDeItem.Evento, TipoDeEvento.Aberto), (evento.Tipo, evento.Evento));
        Assert.Equal(0, c.VersaoLida);
    }

    [Fact]
    public void RF_171_senha_da_conta_master_so_sem_login() =>
        Assert.Equal("DADOS_INVALIDOS", Codigo(() => Chamado.Abrir(new NumeroDoChamado(1), Ebfga, Conta, Categoria.SenhaContaMaster,
            "Assunto", "Descrição longa o bastante", null, [], 4, Abertura)));

    [Fact]
    public void RF_175_assumir_e_ja_assumido()
    {
        var c = Novo();
        c.Assumir(Ana, false, Abertura);
        Assert.Equal((StatusDoChamado.EmAtendimento, "Ana Lima"), (c.Dados.Status, c.Dados.Responsavel?.Nome));
        var falha = Assert.Throws<FalhaDoSac>(() => c.Assumir(Caio, false, Abertura));
        Assert.Equal(("JA_ASSUMIDO", "Ana Lima"), (falha.Codigo, falha.Responsavel));
        c.Assumir(Caio, true, Abertura);
        Assert.Equal("Caio Lima", c.Dados.Responsavel?.Nome);
        Assert.Contains(c.Novos, i => i.Evento == TipoDeEvento.TrocouResponsavel);
        var versao = c.Dados.Versao;
        c.Assumir(Caio, false, Abertura);
        Assert.Equal(versao, c.Dados.Versao);
        Assert.Null(c.Dados.PrimeiraRespostaEm);
    }

    [Fact]
    public void RN_289_a_primeira_mensagem_cumpre_o_prazo_e_a_nota_nao()
    {
        var c = Novo();
        c.Responder(Caio, "Conferir o log", [], true, Abertura.AddHours(1));
        Assert.Equal((StatusDoChamado.Aberto, (DateTimeOffset?)null), (c.Dados.Status, c.Dados.PrimeiraRespostaEm));
        Assert.True(c.Atrasado(Abertura.AddHours(9)));
        c.Responder(Caio, "Qual a turma?", [], false, Abertura.AddHours(2));
        Assert.Equal((StatusDoChamado.EmAtendimento, "Caio Lima"), (c.Dados.Status, c.Dados.Responsavel?.Nome));
        Assert.Equal(Abertura.AddHours(2), c.Dados.PrimeiraRespostaEm);
        Assert.True(c.Dados.NaoLidoPelaConta);
        Assert.Null(c.Dados.EsperandoTimeDesde);
        Assert.False(c.Atrasado(Abertura.AddHours(9)));
    }

    [Fact]
    public void RN_290_a_conta_responde_e_volta_a_em_atendimento()
    {
        var c = No(StatusDoChamado.AguardandoCliente);
        c.ResponderDaConta(Conta, "Turma 7A", ["anx-81bc09"], Abertura.AddHours(1));
        Assert.Equal(StatusDoChamado.EmAtendimento, c.Dados.Status);
        Assert.True(c.Dados.NaoLidoPeloTime);
        Assert.Equal(Abertura.AddHours(1), c.Dados.EsperandoTimeDesde);
        Assert.Equal(["anx-81bc09"], c.Novos[0].Anexos);
    }

    [Theory]
    [InlineData(StatusDoChamado.Aberto, StatusDoChamado.Resolvido, "TRANSICAO_INVALIDA")]
    [InlineData(StatusDoChamado.Aberto, StatusDoChamado.AguardandoCliente, "TRANSICAO_INVALIDA")]
    [InlineData(StatusDoChamado.Resolvido, StatusDoChamado.Fechado, "TRANSICAO_INVALIDA")]
    [InlineData(StatusDoChamado.AguardandoCliente, StatusDoChamado.Resolvido, "TRANSICAO_INVALIDA")]
    [InlineData(StatusDoChamado.EmAtendimento, StatusDoChamado.Aberto, "TRANSICAO_INVALIDA")]
    [InlineData(StatusDoChamado.Fechado, StatusDoChamado.Resolvido, "CHAMADO_FECHADO")]
    public void RN_290_mudancas_recusadas_pelo_time(StatusDoChamado de, StatusDoChamado para, string codigo) =>
        Assert.Equal(codigo, Codigo(() => No(de).MudarStatus(Caio, para, "Texto", Abertura.AddHours(6))));

    [Fact]
    public void RN_290_responda_antes_de_resolver() =>
        Assert.Equal("Responda à conta antes de resolver.",
            Assert.Throws<FalhaDoSac>(() => No(StatusDoChamado.Aberto).MudarStatus(Caio, StatusDoChamado.Resolvido, "x", Abertura)).Message);

    [Fact]
    public void RF_176_pedir_informacao_e_resolver()
    {
        var c = No(StatusDoChamado.EmAtendimento);
        c.MudarStatus(Caio, StatusDoChamado.AguardandoCliente, "Qual a turma?", Abertura.AddHours(1));
        Assert.Equal((StatusDoChamado.AguardandoCliente, Abertura.AddHours(1)), (c.Dados.Status, c.Dados.AguardandoDesde));
        c.ResponderDaConta(Conta, "Turma 7A", [], Abertura.AddHours(2));
        c.MudarStatus(Caio, StatusDoChamado.Resolvido, "Corrigido.", Abertura.AddHours(3));
        Assert.Equal((StatusDoChamado.Resolvido, Abertura.AddHours(3)), (c.Dados.Status, c.Dados.ResolvidoEm));
        Assert.Contains(c.Novos, i => i.Tipo == TipoDeItem.Mensagem && i.Texto == "Corrigido.");
    }

    [Fact]
    public void RF_177_reabrir_em_ate_7_dias_com_o_mesmo_responsavel()
    {
        var c = No(StatusDoChamado.Resolvido);
        Assert.True(c.PodeReabrir(Abertura.AddDays(2)));
        c.Reabrir(Conta, "Ainda aparece o erro na 8B", Abertura.AddDays(2));
        Assert.Equal((StatusDoChamado.EmAtendimento, "Caio Lima"), (c.Dados.Status, c.Dados.Responsavel?.Nome));
        Assert.Null(c.Dados.ResolvidoEm);
    }

    [Theory]
    [InlineData(StatusDoChamado.Fechado, 1, "CHAMADO_FECHADO")]
    [InlineData(StatusDoChamado.Resolvido, 8, "PRAZO_REABERTURA_VENCIDO")]
    [InlineData(StatusDoChamado.EmAtendimento, 1, "TRANSICAO_INVALIDA")]
    public void RN_290_reabrir_recusado(StatusDoChamado de, int dias, string codigo) =>
        Assert.Equal(codigo, Codigo(() => No(de).Reabrir(Conta, "De novo", Abertura.AddDays(dias))));

    [Theory]
    [InlineData(StatusDoChamado.Aberto)]
    [InlineData(StatusDoChamado.EmAtendimento)]
    [InlineData(StatusDoChamado.AguardandoCliente)]
    public void RF_177_encerrar_em_andamento(StatusDoChamado de)
    {
        var c = No(de);
        c.Encerrar(Conta, Abertura.AddHours(1));
        Assert.Equal((StatusDoChamado.Fechado, Fechamento.PelaConta), (c.Dados.Status, c.Dados.Fechamento));
        Assert.False(c.EmAndamento);
    }

    [Fact]
    public void RF_177_encerrar_o_resolvido_e_recusado() =>
        Assert.Equal("TRANSICAO_INVALIDA", Codigo(() => No(StatusDoChamado.Resolvido).Encerrar(Conta, Abertura)));

    [Fact]
    public void RF_178_avaliar_fecha_confirmado()
    {
        var c = No(StatusDoChamado.Resolvido);
        Assert.True(c.PodeAvaliar(Abertura.AddHours(5)));
        c.Avaliar(Conta, 5, "Resolvido em 4 h, obrigado", Abertura.AddHours(5));
        Assert.Equal((StatusDoChamado.Fechado, Fechamento.Confirmado, 5), (c.Dados.Status, c.Dados.Fechamento, c.Dados.Avaliacao?.Nota));
        Assert.Equal("JA_AVALIADO", Codigo(() => c.Avaliar(Conta, 4, null, Abertura.AddHours(6))));
        Assert.False(c.PodeAvaliar(Abertura.AddHours(6)));
    }

    [Theory]
    [InlineData(6, null)]
    [InlineData(8, "PRAZO_AVALIACAO_VENCIDO")]
    public void RN_296_fechado_pelo_prazo_avalia_por_7_dias(int dias, string? codigo)
    {
        var c = No(StatusDoChamado.Resolvido);
        var fechado = Abertura.AddHours(4).AddDays(7);
        Assert.True(c.AplicarPrazos(fechado));
        Assert.Equal((StatusDoChamado.Fechado, Fechamento.Prazo), (c.Dados.Status, c.Dados.Fechamento));
        if (codigo is null)
        {
            c.Avaliar(Conta, 4, null, fechado.AddDays(dias));
            Assert.Equal(4, c.Dados.Avaliacao?.Nota);
        }
        else Assert.Equal(codigo, Codigo(() => c.Avaliar(Conta, 4, null, fechado.AddDays(dias))));
    }

    [Theory]
    [InlineData(StatusDoChamado.Aberto)]
    [InlineData(StatusDoChamado.EmAtendimento)]
    public void RN_296_so_resolvido_ou_fechado_pelo_prazo(StatusDoChamado de) =>
        Assert.Equal("TRANSICAO_INVALIDA", Codigo(() => No(de).Avaliar(Conta, 4, null, Abertura)));

    [Fact]
    public void RN_296_encerrado_pela_conta_nao_avalia()
    {
        var c = Novo();
        c.Encerrar(Conta, Abertura);
        Assert.Equal("TRANSICAO_INVALIDA", Codigo(() => c.Avaliar(Conta, 4, null, Abertura)));
        Assert.False(c.PodeAvaliar(Abertura));
    }

    [Fact]
    public void RN_290_os_7_dias_sem_resposta_da_conta()
    {
        var c = No(StatusDoChamado.AguardandoCliente);
        Assert.False(c.AplicarPrazos(Abertura.AddDays(7)));
        var sete = Abertura.AddMinutes(20).AddDays(7);
        Assert.True(c.AplicarPrazos(sete));
        Assert.Equal((StatusDoChamado.Resolvido, sete), (c.Dados.Status, c.Dados.ResolvidoEm));
        Assert.Contains(c.Novos, i => i.Autor == Autor.Sistema && i.Tipo == TipoDeItem.Mensagem
            && i.Texto == "Encerramos por falta de resposta. Se ainda precisar, toque em Reabrir.");
        Assert.False(c.AplicarPrazos(sete.AddDays(1)));
        Assert.True(c.AplicarPrazos(sete.AddDays(7)));
        Assert.Equal(StatusDoChamado.Fechado, c.Dados.Status);
    }

    [Fact]
    public void RF_179_pedido_de_senha_sem_conversa()
    {
        var c = Chamado.DoPedidoDeSenha(new NumeroDoChamado(87), "Escola Bento Freitas", "(51) 99999-0000", "10.0.0.1", 4, Abertura);
        Assert.Equal(("ajuda-000087", Categoria.SenhaContaMaster, (ContaDoChamado?)null), (c.Dados.Origem.Pedido, c.Dados.Categoria, c.Dados.Conta));
        Assert.False(c.Dados.Origem.TemConversa);
        Assert.Equal("SEM_CONVERSA", Codigo(() => c.Responder(Caio, "Olá", [], false, Abertura)));
        c.Responder(Caio, "Contato confirmado", [], true, Abertura);
        c.MudarStatus(Caio, StatusDoChamado.Resolvido, "Código enviado ao contato", Abertura.AddHours(3));
        Assert.Equal((StatusDoChamado.Resolvido, "Caio Lima"), (c.Dados.Status, c.Dados.Responsavel?.Nome));
        Assert.Contains(c.Novos, i => i.Tipo == TipoDeItem.NotaInterna && i.Texto == "Código enviado ao contato");
        Assert.False(c.Atrasado(Abertura.AddDays(4)));
    }

    [Fact]
    public void RN_290_conta_nao_responde_resolvido_nem_fechado()
    {
        Assert.Equal("TRANSICAO_INVALIDA", Codigo(() => No(StatusDoChamado.Resolvido).ResponderDaConta(Conta, "Oi", [], Abertura)));
        Assert.Equal("CHAMADO_FECHADO", Codigo(() => No(StatusDoChamado.Fechado).ResponderDaConta(Conta, "Oi", [], Abertura)));
        Assert.Equal("CHAMADO_FECHADO", Codigo(() => No(StatusDoChamado.Fechado).Assumir(Caio, true, Abertura)));
    }

    [Fact]
    public void RNF_231_guardado_5_anos_depois_de_fechado()
    {
        var c = No(StatusDoChamado.Fechado);
        Assert.Equal(c.Dados.FechadoEm!.Value.AddYears(5), c.Dados.GuardarAte);
        Assert.Null(Novo().Dados.GuardarAte);
    }
}
