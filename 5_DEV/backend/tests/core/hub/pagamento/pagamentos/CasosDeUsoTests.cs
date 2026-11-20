using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;
using Boaz.Core.Hub.Tests.Pagamento.Mocks;

namespace Boaz.Core.Hub.Tests.Pagamento.Pagamentos;

/// <summary>Os casos de uso do Pagamento pelos Cenários de API do 19_modulo-pagamento.</summary>
public sealed class CasosDeUsoTests
{
    readonly MundoDoPagamento mundo = new();

    PagamentoEmMemoria Mock => mundo.Mock;

    static PedidoDePagamento Forma(string forma, string? token = null) =>
        new(forma, token is null ? null : new PedidoDoCartao(token, forma == "CARTAO_DEBITO" ? "debmaster" : "visa", "25", null, null, null),
            forma == "BOLETO" ? new EnderecoDto("96015-000", "Rua Gonçalves Chaves", "3218", "Centro", "Pelotas", "RS") : null);

    async Task<Fatura> Fat402()
    {
        var fatura = await mundo.Emitir("conta-ebfga", "2026-11", ("MOD-03", 2224.41m), ("MOD-05", 1473.92m));
        mundo.Relogio.Agora = new DateTimeOffset(2026, 12, 3, 13, 0, 0, TimeSpan.Zero);
        return fatura;
    }

    async Task<string> Falha(Func<Task> acao) => (await Assert.ThrowsAsync<FalhaDoPagamento>(acao)).Codigo;

    [Fact]
    public async Task RN_300_fechamento_emite_uma_fatura_por_conta_sem_repetir_e_sem_o_time()
    {
        mundo.Cobranca("conta-dboaz", "MOD-03", "2026-11", 10m);
        var fatura = await Fat402();
        await mundo.Agendamento.Executar();
        Assert.Single(Mock.Faturas.Values);
        Assert.Equal(("FAT-000001", StatusDaFatura.APagar, Dinheiro.De(3698.33m), new DateOnly(2026, 12, 10)),
            (fatura.Id, fatura.Status, fatura.Valor, fatura.Vencimento));
        Assert.Equal(["Usuários", "Quiz"], fatura.Itens.Select(i => i.Nome));
    }

    [Fact]
    public async Task RF_182_lista_e_RF_183_detalhe_da_conta()
    {
        var fatura = await Fat402();
        var lista = await mundo.Listar.Executar(null, null, MundoDoPagamento.Rita);
        Assert.Equal((1, 3698.33m, new DateOnly(2026, 12, 10)), (lista.Total, lista.Resumo.APagar, lista.Resumo.ProximoVencimento));
        Assert.Empty((await mundo.Listar.Executar("PAGAS", 1, MundoDoPagamento.Rita)).Itens);
        Assert.Single((await mundo.Listar.Executar("TODAS", 1, MundoDoPagamento.Rita)).Itens);
        Assert.Equal("DADOS_INVALIDOS", await Falha(() => mundo.Listar.Executar("OUTRA", 1, MundoDoPagamento.Rita)));
        Assert.Equal("DADOS_INVALIDOS", await Falha(() => mundo.Listar.Executar(null, 0, MundoDoPagamento.Rita)));

        var lida = await mundo.LerFatura.Executar(fatura.Id, MundoDoPagamento.Rita);
        Assert.Equal(("A_PAGAR", 3698.33m, null), (lida.Status, lida.TotalAPagar, lida.Juros));
        Assert.Equal("FATURA_INEXISTENTE", await Falha(() => mundo.LerFatura.Executar(fatura.Id, MundoDoPagamento.Pedro)));
    }

    [Fact]
    public async Task RF_185_pix_aguarda_o_aviso_do_banco_e_RN_305_o_aviso_confirma()
    {
        var fatura = await Fat402();
        var trace = MundoDoPagamento.Trace;
        var pix = await mundo.Criar.Executar(fatura.Id, Forma("PIX"), MundoDoPagamento.Rita, trace, null);
        Assert.Equal(("AGUARDANDO", 3698.33m, new DateTimeOffset(2026, 12, 3, 13, 30, 0, TimeSpan.Zero)), (pix.Status, pix.Valor, pix.ExpiraEm!.Value));
        Assert.NotNull(pix.Pix);
        Assert.Equal(trace.Valor, Mock.Cobrancas_.Single().TraceId);
        Assert.Equal("PAGAMENTO_EM_ABERTO", await Falha(() => mundo.Criar.Executar(fatura.Id, Forma("PIX"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));

        var evento = await mundo.AoVivo.Abrir(pix.Id, MundoDoPagamento.Rita);
        Assert.False(evento.EhFinal());

        Mock.Leitura = new LeituraDoAviso(true, new AvisoDoBanco("evt_1", TipoDoAviso.PagamentoConfirmado, "pix_1", mundo.Relogio.Agora, trace.Valor));
        await mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace);
        await mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace);
        Assert.Equal(StatusDaFatura.Paga, fatura.Status);
        Assert.Equal("APLICADO", Mock.AvisosDoBanco["evt_1"].Resultado);
        // RN-305-api: a segunda chegada fica registrada como REPETIDO, com o traceId dela
        var repetido = Assert.Single(Mock.AvisosDoBanco.Values, a => a.Resultado == "REPETIDO");
        Assert.Equal(("pix_1", pix.Id, $"repetido:{repetido.TraceId}"), (repetido.IdExterno, repetido.PagamentoId, repetido.Id));
        Assert.Equal("PAGO", (await mundo.LerPagamento.Executar(pix.Id, MundoDoPagamento.Rita)).Status);
        Assert.Equal("PAGO", (await mundo.AoVivo.Proximo(evento, TimeSpan.Zero))!.Status);

        var comprovante = await mundo.Documento.Comprovante(pix.Id, MundoDoPagamento.Rita);
        Assert.Equal(fatura.Id, comprovante.FaturaId);
        Assert.Equal("BOLETO_INEXISTENTE", await Falha(() => mundo.Documento.Boleto(pix.Id, MundoDoPagamento.Rita)));
        Assert.Equal("FATURA_PAGA", await Falha(() => mundo.Criar.Executar(fatura.Id, Forma("PIX"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));
    }

    [Fact]
    public async Task RN_305_assinatura_errada_responde_401_e_fica_registrada()
    {
        Mock.Leitura = new LeituraDoAviso(false, null);
        Assert.Equal("ASSINATURA_INVALIDA", await Falha(() =>
            mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace)));
        Assert.Equal("RECUSADO", Assert.Single(Mock.AvisosDoBanco.Values).Resultado);
    }

    [Fact]
    public async Task RF_186_credito_aprovado_paga_na_hora_e_o_token_usado_e_recusado()
    {
        var fatura = await Fat402();
        Mock.Cobranca = _ => new ResultadoDaCobranca(SituacaoDaCobranca.TokenInvalido);
        Assert.Equal("DADOS_INVALIDOS", await Falha(() =>
            mundo.Criar.Executar(fatura.Id, Forma("CARTAO_CREDITO", "tok_vencido"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));
        Mock.Cobranca = _ => new ResultadoDaCobranca(SituacaoDaCobranca.Recusada, "card_0", Cartao: new CartaoMascarado("VISA", "4242"));
        var recusado = await mundo.Criar.Executar(fatura.Id, Forma("CARTAO_CREDITO", "tok_9f2c71"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Assert.Equal("RECUSADO", recusado.Status);
        Mock.Cobranca = _ => new ResultadoDaCobranca(SituacaoDaCobranca.Aprovada, "card_1", Cartao: new CartaoMascarado("VISA", "4242"));
        var pago = await mundo.Criar.Executar(fatura.Id, Forma("CARTAO_CREDITO", "tok_9f2c71"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Assert.Equal(("PAGO", "4242"), (pago.Status, pago.Cartao!.Final));
        Assert.Equal(StatusDaFatura.Paga, fatura.Status);
        Assert.Empty(Mock.Ocupadas);
    }

    [Fact]
    public async Task RNF_241_banco_sem_resposta_nao_deixa_pagamento()
    {
        var fatura = await Fat402();
        Mock.Cobranca = _ => new ResultadoDaCobranca(SituacaoDaCobranca.Indisponivel);
        Assert.Equal("BANCO_INDISPONIVEL", await Falha(() => mundo.Criar.Executar(fatura.Id, Forma("PIX"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));
        Assert.Empty(Mock.Pagamentos);
        Assert.Empty(Mock.Ocupadas);
        Assert.Contains(Mock.Erros, e => e.Codigo == "BANCO_INDISPONIVEL");
    }

    [Fact]
    public async Task RF_187_debito_expira_e_RF_188_boleto_com_pdf()
    {
        var fatura = await Fat402();
        var debito = await mundo.Criar.Executar(fatura.Id, Forma("CARTAO_DEBITO", "tok_d41b07"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Assert.NotNull(debito.Debito);
        mundo.Relogio.Agora = mundo.Relogio.Agora.AddMinutes(11);
        await mundo.Agendamento.Executar();
        Assert.Equal("EXPIRADO", (await mundo.LerPagamento.Executar(debito.Id, MundoDoPagamento.Rita)).Status);

        var boleto = await mundo.Criar.Executar(fatura.Id, Forma("BOLETO"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Assert.Equal((new DateOnly(2026, 12, 10), $"/pagamento/pagamentos/{boleto.Id}/boleto"), (boleto.Boleto!.Vencimento, boleto.Boleto.Pdf));
        Assert.Equal("23793.38128", (await mundo.Documento.Boleto(boleto.Id, MundoDoPagamento.Rita)).LinhaDigitavel);
        Assert.Equal("PAGAMENTO_NAO_CONFIRMADO", await Falha(() => mundo.Documento.Comprovante(boleto.Id, MundoDoPagamento.Rita)));

        // RN-302: antes do prazo, o boleto só espera o aviso do banco; ao fim dos 3 dias úteis
        // do vencimento (10/12 → 15/12), o agendamento consulta o banco e a compensação quita
        Mock.Consulta = new ConsultaNoBanco(SituacaoNoBanco.Paga, mundo.Relogio.Agora);
        await mundo.Expirar.Executar(MundoDoPagamento.Trace);
        Assert.Equal(StatusDaFatura.APagar, fatura.Status);
        mundo.Relogio.Agora = new DateTimeOffset(2026, 12, 16, 3, 1, 0, TimeSpan.Zero);
        await mundo.Expirar.Executar(MundoDoPagamento.Trace);
        Assert.Equal(StatusDaFatura.Paga, fatura.Status);
        Assert.Equal("PAGO", (await mundo.LerPagamento.Executar(boleto.Id, MundoDoPagamento.Rita)).Status);
    }

    [Fact]
    public async Task RN_303_cancelar_o_que_espera_libera_a_fatura()
    {
        var fatura = await Fat402();
        var pix = await mundo.Criar.Executar(fatura.Id, Forma("PIX"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        var cancelado = await mundo.Cancelar.Executar(pix.Id, MundoDoPagamento.Rita, MundoDoPagamento.Trace);
        Assert.Equal("CANCELADO", cancelado.Status);
        Assert.Equal(["pix_1"], Mock.CanceladosNoBanco);
        Assert.Empty(Mock.Ocupadas);
        Assert.Equal("PAGAMENTO_INEXISTENTE", await Falha(() => mundo.Cancelar.Executar(pix.Id, MundoDoPagamento.Pedro, MundoDoPagamento.Trace)));
        Assert.Contains(Mock.Auditoria, r => r.Acao == AcaoAuditada.PagamentoCancelado && r.Etapa == EtapaDaExecucao.Concluido);
    }

    [Fact]
    public async Task RN_301_vencida_RN_311_aviso_e_RN_310_juros()
    {
        mundo.Relogio.Agora = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        mundo.Configurar(juros: 0.1m);
        var fatura = await mundo.Emitir("conta-preis", "2026-10", ("MOD-03", 147m), ("MOD-05", 147m));
        mundo.Relogio.Agora = new DateTimeOffset(2026, 11, 11, 3, 1, 0, TimeSpan.Zero);
        await mundo.Agendamento.Executar();
        Assert.Equal(StatusDaFatura.Vencida, fatura.Status);
        Assert.Contains(Mock.Auditoria, r => r.Acao == AcaoAuditada.FaturaVencida && r.Alvo == fatura.Id);

        mundo.Relogio.Agora = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var avisos = await mundo.LerAvisos.Executar(MundoDoPagamento.Pedro);
        Assert.Equal((1.18m, 295.18m), (avisos.Itens.Single().Juros, avisos.Itens.Single().TotalAPagar));
        var pix = await mundo.Criar.Executar(fatura.Id, Forma("PIX"), MundoDoPagamento.Pedro, MundoDoPagamento.Trace, null);
        Assert.Equal((295.18m, 1.18m), (pix.Valor, pix.Juros));

        var cancelada = await mundo.CancelarFatura.Executar(fatura.Id, new PedidoDeCancelamento("Cobrança em dobro do Quiz"), MundoDoPagamento.Admin,
            MundoDoPagamento.Trace);
        Assert.Equal(("CANCELADA", "Cobrança em dobro do Quiz"), (cancelada.Status, cancelada.Motivo));
        Assert.Equal("CANCELADO", (await mundo.LerPagamento.Executar(pix.Id, MundoDoPagamento.Pedro)).Status);
        await mundo.Agendamento.Executar();
        Assert.Empty((await mundo.LerAvisos.Executar(MundoDoPagamento.Pedro)).Itens);
    }

    [Fact]
    public async Task RF_191_cancelamento_recusado()
    {
        var fatura = await Fat402();
        Assert.Equal("ACESSO_NEGADO", await Falha(() =>
            mundo.CancelarFatura.Executar(fatura.Id, new PedidoDeCancelamento("x"), MundoDoPagamento.Rita, MundoDoPagamento.Trace)));
        Assert.Equal("DADOS_INVALIDOS", await Falha(() =>
            mundo.CancelarFatura.Executar(fatura.Id, new PedidoDeCancelamento(" "), MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
        Assert.Equal("FATURA_INEXISTENTE", await Falha(() =>
            mundo.CancelarFatura.Executar("FAT-999999", new PedidoDeCancelamento("x"), MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
    }

    [Fact]
    public async Task RF_192_estorno_pendente_concluido_pelo_aviso_e_banco_sem_resposta()
    {
        var fatura = await Fat402();
        var pago = await mundo.Criar.Executar(fatura.Id, Forma("CARTAO_CREDITO", "tok_9f2c71"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);

        Mock.Estorno = new ResultadoDoEstorno(SituacaoDoEstorno.Indisponivel);
        Assert.Equal("BANCO_INDISPONIVEL", await Falha(() =>
            mundo.Estornar.Executar(pago.Id, new PedidoDeEstornoDto(1473.92m, "Quiz cancelado"), MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
        Assert.Equal("PAGO", (await mundo.LerPagamento.Executar(pago.Id, MundoDoPagamento.Rita)).Status);

        Mock.Estorno = new ResultadoDoEstorno(SituacaoDoEstorno.Pendente, "ref_1");
        var estorno = await mundo.Estornar.Executar(pago.Id, new PedidoDeEstornoDto(1473.92m, "Quiz cancelado"), MundoDoPagamento.Admin,
            MundoDoPagamento.Trace);
        Assert.Equal(("PENDENTE", 1473.92m), (estorno.Status, estorno.Valor));
        Assert.Equal("ESTORNO_PENDENTE", (await mundo.LerPagamento.Executar(pago.Id, MundoDoPagamento.Rita)).Status);

        Mock.Leitura = new LeituraDoAviso(true, new AvisoDoBanco("evt_2", TipoDoAviso.EstornoConcluido, "ref_1", mundo.Relogio.Agora, null));
        await mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace);
        Assert.Equal("ESTORNADO_PARCIAL", (await mundo.LerPagamento.Executar(pago.Id, MundoDoPagamento.Rita)).Status);
        Assert.Equal(Dinheiro.De(1473.92m), fatura.Devolvido);

        Assert.Equal("DADOS_INVALIDOS", await Falha(() =>
            mundo.Estornar.Executar(pago.Id, new PedidoDeEstornoDto(10.005m, "x"), MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
        Assert.Equal("ACESSO_NEGADO", await Falha(() =>
            mundo.Estornar.Executar(pago.Id, new PedidoDeEstornoDto(1m, "x"), MundoDoPagamento.Rita, MundoDoPagamento.Trace)));
        Assert.Equal("PAGAMENTO_INEXISTENTE", await Falha(() =>
            mundo.Estornar.Executar("PAG-999999", new PedidoDeEstornoDto(1m, "x"), MundoDoPagamento.Admin, MundoDoPagamento.Trace)));

        Mock.Estorno = new ResultadoDoEstorno(SituacaoDoEstorno.Recusado, "ref_2");
        var recusado = await mundo.Estornar.Executar(pago.Id, new PedidoDeEstornoDto(1m, "x"), MundoDoPagamento.Admin, MundoDoPagamento.Trace);
        Assert.Equal("RECUSADO", recusado.Status);
        Assert.Equal("ESTORNADO_PARCIAL", (await mundo.LerPagamento.Executar(pago.Id, MundoDoPagamento.Rita)).Status);
    }

    [Fact]
    public async Task RF_190_resumo_competencias_e_RF_191_lista_do_recebedor()
    {
        var fatura = await Fat402();
        await mundo.Criar.Executar(fatura.Id, Forma("CARTAO_CREDITO", "tok_9f2c71"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        var resumo = await mundo.Recebimentos.Resumo("2026-11", MundoDoPagamento.Admin);
        Assert.Equal((3698.33m, 1, 0m), (resumo.Recebido.Valor, resumo.Recebido.Faturas, resumo.Inadimplencia));
        Assert.Equal(4, resumo.PorForma.Count);
        Assert.Equal(3698.33m, resumo.PorForma.Single(f => f.Forma == "CARTAO_CREDITO").Valor);
        Assert.Equal("2026-11", (await mundo.Recebimentos.Resumo(null, MundoDoPagamento.Admin)).Competencia);
        Assert.Equal("DADOS_INVALIDOS", await Falha(() => mundo.Recebimentos.Resumo("2026-13", MundoDoPagamento.Admin)));
        Assert.Equal("ACESSO_NEGADO", await Falha(() => mundo.Recebimentos.Resumo("2026-11", MundoDoPagamento.Rita)));

        var competencias = await mundo.Recebimentos.Competencias("2026-11", MundoDoPagamento.Admin);
        Assert.Equal(("2026-11", "2025-12", 12), (competencias[0].Competencia, competencias[^1].Competencia, competencias.Count));

        var lista = await mundo.ListarDoRecebedor.Executar(null, "TODAS", "ebfga", 1, MundoDoPagamento.Admin);
        Assert.Equal(("FAT-000001", "Escola Bento Freitas"), (lista.Itens.Single().Id, lista.Itens.Single().Conta.Nome));
        Assert.Empty((await mundo.ListarDoRecebedor.Executar("2026-11", "VENCIDA", null, null, MundoDoPagamento.Admin)).Itens);
        Assert.Equal("DADOS_INVALIDOS", await Falha(() => mundo.ListarDoRecebedor.Executar(null, "X", null, null, MundoDoPagamento.Admin)));
    }

    [Fact]
    public async Task RF_193_configuracao_com_if_match_e_formas()
    {
        var lida = await mundo.LerConfiguracao.Executar(MundoDoPagamento.Admin);
        Assert.Equal((1, 10, 30), (lida.Versao, lida.DiaVencimento, lida.Prazos.PixMinutos));
        var formas = lida.Formas.ToDictionary(f => f.Key, f => (bool?)f.Value);
        formas["BOLETO"] = false;
        var pedido = new PedidoDeConfiguracao(formas, 0.1m, 0, 10, new PrazosDto(30, 10, 3));
        var salva = await mundo.SalvarConfiguracao.Executar(pedido, 1, MundoDoPagamento.Admin, MundoDoPagamento.Trace);
        Assert.Equal((2, false), (salva.Versao, salva.Formas["BOLETO"]));
        Assert.DoesNotContain("BOLETO", (await mundo.Formas.Executar(MundoDoPagamento.Rita)).Formas);
        Assert.Equal("CONFIGURACAO_MUDOU", await Falha(() => mundo.SalvarConfiguracao.Executar(pedido, 1, MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
        Assert.Equal("ACESSO_NEGADO", await Falha(() => mundo.SalvarConfiguracao.Executar(pedido, 2, MundoDoPagamento.Rita, MundoDoPagamento.Trace)));
        Assert.Equal("VERSAO_AUSENTE", await Falha(() => mundo.SalvarConfiguracao.Executar(pedido, null, MundoDoPagamento.Admin, MundoDoPagamento.Trace)));

        var todasDesligadas = formas.ToDictionary(f => f.Key, _ => (bool?)false);
        Assert.Equal("FORMA_OBRIGATORIA", await Falha(() =>
            mundo.SalvarConfiguracao.Executar(pedido with { Formas = todasDesligadas }, 2, MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
        foreach (var errado in new[]
        {
            pedido with { JurosAoDia = 1.01m }, pedido with { ToleranciaAvisoDias = 31 }, pedido with { DiaVencimento = 15 },
            pedido with { Prazos = new PrazosDto(4, 10, 3) }, pedido with { Prazos = new PrazosDto(30, 31, 3) },
            pedido with { Prazos = new PrazosDto(30, 10, 0) }, pedido with { Formas = null },
        })
        {
            Assert.Equal("DADOS_INVALIDOS", await Falha(() => mundo.SalvarConfiguracao.Executar(errado, 2, MundoDoPagamento.Admin, MundoDoPagamento.Trace)));
        }
        Assert.Equal("ACESSO_NEGADO", await Falha(() => mundo.Formas.Executar(MundoDoPagamento.Admin)));
    }

    [Fact]
    public async Task RN_307_card_de_cada_perfil_e_ao_vivo()
    {
        var fatura = await Fat402();
        var instituicao = await mundo.Card.Executar(MundoDoPagamento.Rita);
        Assert.Equal(("INSTITUICAO", 1, 3698.33m, "a pagar", "Vence em 10/12"),
            (instituicao.Perfil, instituicao.Selo, instituicao.Reduzida.Valor, instituicao.Reduzida.Rotulo, instituicao.Normal.Rotulo));
        Assert.All([instituicao.Series.Dia, instituicao.Series.Semana, instituicao.Series.Mes], s => Assert.Equal(12, s.Count));

        await mundo.Criar.Executar(fatura.Id, Forma("CARTAO_CREDITO", "tok_9f2c71"), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        var deboaz = await mundo.Card.Executar(MundoDoPagamento.Admin);
        Assert.Equal(("DEBOAZ", 3698.33m, "recebido no mês"), (deboaz.Perfil, deboaz.Reduzida.Valor, deboaz.Reduzida.Rotulo));
        Assert.Equal("ACESSO_NEGADO", await Falha(() => mundo.Card.Executar(MundoDoPagamento.Maria)));

        var pedro = await mundo.Card.Executar(MundoDoPagamento.Pedro);
        Assert.True(pedro.Vazio);
        var (intervalo, card) = await mundo.CardAoVivo.Abrir("30", MundoDoPagamento.Rita);
        Assert.Equal(TimeSpan.FromSeconds(30), intervalo);
        Assert.Null(await mundo.CardAoVivo.Proximo(card.Versao, MundoDoPagamento.Rita));
        Assert.NotNull(await mundo.CardAoVivo.Proximo(card.Versao - 1, MundoDoPagamento.Rita));
        Assert.Equal("DADOS_INVALIDOS", await Falha(() => mundo.CardAoVivo.Abrir("7", MundoDoPagamento.Rita)));
    }
}
