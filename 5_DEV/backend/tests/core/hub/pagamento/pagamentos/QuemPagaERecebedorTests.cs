using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.UseCases;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;
using Boaz.Core.Hub.Tests.Pagamento.Mocks;

namespace Boaz.Core.Hub.Tests.Pagamento.Pagamentos;

/// <summary>
/// O 19 v05: quem paga (o boleto com o endereço, o cartão com método e emissor,
/// as formas com a chave pública), o recebedor (o detalhe da fatura e do
/// pagamento, com os estornos), o aviso sem intermediador e a fatura em aberto.
/// </summary>
public sealed class QuemPagaERecebedorTests
{
    readonly MundoDoPagamento mundo = new();

    PagamentoEmMemoria Mock => mundo.Mock;

    static readonly EnderecoDto Endereco = new("96015000", "Rua Gonçalves Chaves", "3218", "Centro", "Pelotas", "rs");

    static PedidoDePagamento Boleto(EnderecoDto? endereco) => new("BOLETO", null, endereco);

    static PedidoDePagamento Credito(string? metodo = "visa", string? emissor = "25") =>
        new("CARTAO_CREDITO", new PedidoDoCartao("tok_9f2c71", metodo, emissor, null, null, null));

    async Task<Fatura> Fat402()
    {
        var fatura = await mundo.Emitir("conta-ebfga", "2026-11", ("MOD-03", 2224.41m), ("MOD-05", 1473.92m));
        mundo.Relogio.Agora = new DateTimeOffset(2026, 12, 3, 13, 0, 0, TimeSpan.Zero);
        return fatura;
    }

    static async Task<(string Codigo, string? Campo)> Falha(Func<Task> acao)
    {
        var falha = await Assert.ThrowsAsync<FalhaDoPagamento>(acao);
        return (falha.Codigo, falha.Campo);
    }

    [Fact]
    public async Task RF_188_api_o_boleto_leva_o_endereco_e_sai_em_nome_da_conta()
    {
        var fatura = await Fat402();
        Assert.Equal(("DADOS_INVALIDOS", "endereco"),
            await Falha(() => mundo.Criar.Executar(fatura.Id, Boleto(null), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));
        Assert.Equal(("DADOS_INVALIDOS", "endereco.cep"), await Falha(() =>
            mundo.Criar.Executar(fatura.Id, Boleto(Endereco with { Cep = "9601" }), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));
        Assert.Equal(("DADOS_INVALIDOS", "endereco.uf"), await Falha(() =>
            mundo.Criar.Executar(fatura.Id, Boleto(Endereco with { Uf = "XX" }), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));

        var boleto = await mundo.Criar.Executar(fatura.Id, Boleto(Endereco), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        var cobranca = Mock.Cobrancas_.Single();
        Assert.Equal(("96015-000", "RS", "master@ebfga.exemplo"), (cobranca.Endereco!.Cep, cobranca.Endereco.Uf, cobranca.Pagador.Email));
        Assert.Equal(NomeDoIntermediador.Falso, Mock.Pagamentos[boleto.Id].Intermediador);

        var documento = await mundo.Documento.Boleto(boleto.Id, MundoDoPagamento.Rita);
        Assert.Equal(("Escola Bento Freitas", "12345678000190", "Pelotas"), (documento.Conta, documento.Documento, documento.Endereco!.Cidade));

        // RF-184-api: o último endereço do boleto volta em GET /pagamento/formas
        var formas = await mundo.Formas.Executar(MundoDoPagamento.Rita);
        Assert.Equal(("96015-000", NomeDoIntermediador.Falso, "chave-publica-de-teste"),
            (formas.EnderecoDoBoleto!.Cep, formas.Cartao.Intermediador, formas.Cartao.ChavePublica));
        Assert.Null((await mundo.Formas.Executar(MundoDoPagamento.Pedro)).EnderecoDoBoleto);
    }

    [Fact]
    public async Task RF_186_api_o_cartao_vai_com_o_metodo_e_o_emissor_e_o_pix_com_o_qr_do_deboaz()
    {
        var fatura = await Fat402();
        Assert.Equal(("DADOS_INVALIDOS", "cartao.metodo"),
            await Falha(() => mundo.Criar.Executar(fatura.Id, Credito(metodo: " "), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null)));
        await mundo.Criar.Executar(fatura.Id, Credito(emissor: null), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Assert.Equal(new CartaoTokenizado("tok_9f2c71", "visa", null), Mock.Cobrancas_.Single().Cartao);

        var outra = await mundo.Emitir("conta-preis", "2026-11", ("MOD-03", 89.90m));
        mundo.Relogio.Agora = new DateTimeOffset(2026, 12, 3, 13, 0, 0, TimeSpan.Zero);
        var pix = await mundo.Criar.Executar(outra.Id, new PedidoDePagamento("PIX", null), MundoDoPagamento.Pedro, MundoDoPagamento.Trace, null);
        Assert.Equal(("000201", "<svg viewBox=\"0 0 33 33\"><desc>000201</desc></svg>"), (pix.Pix!.CopiaECola, pix.Pix.QrCodeSvg));
    }

    [Fact]
    public async Task RN_298_api_o_administrador_com_MOD_01_so_ve_o_card()
    {
        var fatura = await Fat402();
        var admin = MundoDoPagamento.Admin;
        Assert.True(admin.UsaPagamento);
        Assert.Equal("ACESSO_NEGADO", (await Falha(() => mundo.Listar.Executar(null, null, admin))).Codigo);
        Assert.Equal("ACESSO_NEGADO", (await Falha(() => mundo.LerFatura.Executar(fatura.Id, admin))).Codigo);
        Assert.Equal("ACESSO_NEGADO", (await Falha(() => mundo.Formas.Executar(admin))).Codigo);
        Assert.Equal("ACESSO_NEGADO",
            (await Falha(() => mundo.Criar.Executar(fatura.Id, new PedidoDePagamento("PIX", null), admin, MundoDoPagamento.Trace, null))).Codigo);
        Assert.Equal("DEBOAZ", (await mundo.Card.Executar(admin)).Perfil);
    }

    [Fact]
    public async Task RF_191_api_e_RF_192_api_o_recebedor_le_a_fatura_e_o_pagamento_com_os_estornos()
    {
        var fatura = await Fat402();
        var pago = await mundo.Criar.Executar(fatura.Id, Credito(), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Mock.Estorno = new ResultadoDoEstorno(SituacaoDoEstorno.Pendente, "ref_1");
        var estorno = await mundo.Estornar.Executar(pago.Id, new PedidoDeEstornoDto(1473.92m, "Quiz cancelado"), MundoDoPagamento.Admin,
            MundoDoPagamento.Trace);

        var lida = await mundo.FaturaDoRecebedor.Executar(fatura.Id, MundoDoPagamento.Admin);
        Assert.Equal((fatura.Id, "PAGA", 1), (lida.Id, lida.Status, lida.Pagamentos.Count));
        Assert.Equal("FATURA_INEXISTENTE", (await Falha(() => mundo.FaturaDoRecebedor.Executar("FAT-999999", MundoDoPagamento.Admin))).Codigo);
        Assert.Equal("ACESSO_NEGADO", (await Falha(() => mundo.FaturaDoRecebedor.Executar(fatura.Id, MundoDoPagamento.Rita))).Codigo);

        var doRecebedor = await mundo.PagamentoDoRecebedor.Executar(pago.Id, MundoDoPagamento.Admin);
        Assert.Equal(("EBFGA", "CARTAO_CREDITO", 3698.33m, estorno.Id), (doRecebedor.Conta.Sigla, doRecebedor.Forma, doRecebedor.Valor,
            doRecebedor.Estornos.Single().Id));
        Assert.Equal("PAGAMENTO_INEXISTENTE",
            (await Falha(() => mundo.PagamentoDoRecebedor.Executar("PAG-999999", MundoDoPagamento.Admin))).Codigo);
        Assert.Equal("ACESSO_NEGADO", (await Falha(() => mundo.PagamentoDoRecebedor.Executar(pago.Id, MundoDoPagamento.Rita))).Codigo);

        // RF-192-api: quem pagou vê os estornos no GET /pagamento/pagamentos/{id}
        var daConta = await mundo.LerPagamento.Executar(pago.Id, MundoDoPagamento.Rita);
        Assert.Equal(("PENDENTE", 1473.92m), (daConta.Estornos.Single().Status, daConta.Estornos.Single().Valor));
    }

    [Fact]
    public async Task RNF_242_aviso_sem_intermediador_responde_401_e_o_pagamento_e_achado_pelo_dele()
    {
        Mock.AvisoSemDono = true;
        Assert.Equal("ASSINATURA_INVALIDA", (await Falha(() =>
            mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace))).Codigo);
        var registro = Assert.Single(Mock.AvisosDoBanco.Values);
        Assert.Equal(("RECUSADO", null), (registro.Resultado, registro.Intermediador));

        // o aviso de um id externo que existe, mas de outro intermediador, não acha o pagamento
        Mock.AvisoSemDono = false;
        var fatura = await Fat402();
        var pix = await mundo.Criar.Executar(fatura.Id, new PedidoDePagamento("PIX", null), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Mock.Leitura = new LeituraDoAviso(true, new AvisoDoBanco("evt_9", TipoDoAviso.PagamentoConfirmado, "pix_1", mundo.Relogio.Agora, null));
        await mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace);
        Assert.Equal(NomeDoIntermediador.Falso, Mock.AvisosDoBanco["evt_9"].Intermediador);
        Assert.Equal(StatusDaFatura.Paga, fatura.Status);
    }

    [Fact]
    public async Task RNF_242_aviso_do_mercado_pago_de_pagamento_desconhecido_nao_consulta_o_banco()
    {
        var fatura = await Fat402();
        await mundo.Criar.Executar(fatura.Id, new PedidoDePagamento("PIX", null), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Mock.IdDoPagamentoNoAviso = "1325001234";
        var trace = MundoDoPagamento.Trace;
        await mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), trace);
        var registro = Assert.Single(Mock.AvisosDoBanco.Values);
        Assert.Equal(("DESCONHECIDO", "1325001234", true, null, trace.Valor),
            (registro.Resultado, registro.IdExterno, registro.AssinaturaValida, registro.PagamentoId, registro.TraceId));
        Assert.Equal(0, Mock.LeiturasDoAviso);

        // o pagamento conhecido segue para a leitura
        Mock.IdDoPagamentoNoAviso = "pix_1";
        Mock.Leitura = new LeituraDoAviso(true, new AvisoDoBanco("evt_3", TipoDoAviso.PagamentoConfirmado, "pix_1", mundo.Relogio.Agora, null));
        await mundo.Aviso.Executar(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()), MundoDoPagamento.Trace);
        Assert.Equal((1, StatusDaFatura.Paga), (Mock.LeiturasDoAviso, fatura.Status));
    }

    [Fact]
    public async Task RN_313_a_conta_com_fatura_em_aberto_nao_se_encerra()
    {
        var emAberto = new ContaTemFaturaEmAberto(Mock);
        Assert.False(await emAberto.Executar("conta-ebfga"));
        var fatura = await Fat402();
        Assert.True(await emAberto.Executar("conta-ebfga"));
        Assert.False(await emAberto.Executar("conta-preis"));
        await mundo.Criar.Executar(fatura.Id, Credito(), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        Assert.False(await emAberto.Executar("conta-ebfga"));
    }

    [Fact]
    public async Task RN_307_sem_recebido_no_mes_anterior()
    {
        var fatura = await Fat402();
        await mundo.Criar.Executar(fatura.Id, Credito(), MundoDoPagamento.Rita, MundoDoPagamento.Trace, null);
        var deboaz = await mundo.Card.Executar(MundoDoPagamento.Admin);
        Assert.Equal("Sem recebido no mês anterior", deboaz.Normal.Rotulo);
    }
}
