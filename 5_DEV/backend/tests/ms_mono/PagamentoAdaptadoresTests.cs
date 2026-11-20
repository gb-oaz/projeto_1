using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.MsMono.Primario.Out.Hub.Pagamento;
using Boaz.MsMono.Secundario.Out.Hub.Pagamento;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Tests;

// Os adaptadores do Pagamento que não precisam de banco (19_modulo-pagamento,
// pagamento-hexagonal v03): o intermediador falso do qa, o do Mercado Pago
// contra uma API de mentira, a escolha do intermediador pelo aviso, o QR Code
// desenhado pelo deboaz e os PDFs do boleto e do comprovante.
public sealed class PagamentoAdaptadoresTests
{
    static readonly Pagador Ebfga = new("Escola Bento Freitas", "12.345.678/0001-90", "master@ebfga.exemplo");

    static readonly EnderecoDoPagador Endereco = EnderecoDoPagador.Validar("96015000", "Rua Gonçalves Chaves", "3218", "Centro", "Pelotas", "RS");

    static PedidoDeCobranca Cobranca(FormaDePagamento forma, string? token = null) =>
        new("PAG-000773", "FAT-000402", forma, Dinheiro.De(3698.33m), token is null ? null : new CartaoTokenizado(token, "master", "25"),
            DateTimeOffset.UtcNow.AddMinutes(30), new DateOnly(2026, 12, 10), "trc-00000001", Ebfga,
            forma == FormaDePagamento.Boleto ? Endereco : null);

    static string Hmac(string segredo, string texto) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes(texto)));

    static AvisoRecebido Aviso(string corpo, params (string Nome, string Valor)[] cabecalhos) =>
        new(corpo, cabecalhos.ToDictionary(c => c.Nome, c => c.Valor), new Dictionary<string, string>());

    [Fact]
    public async Task RF_185_a_RF_188_o_falso_responde_cada_forma_e_guarda_o_pedido()
    {
        var falso = new IntermediadorFalso(IntermediadorFalso.SegredoPadrao, TimeProvider.System);
        var pix = await falso.Cobrar(Cobranca(FormaDePagamento.Pix));
        Assert.Equal(SituacaoDaCobranca.Aguardando, pix.Situacao);
        Assert.StartsWith("000201", pix.PixCopiaECola, StringComparison.Ordinal);
        Assert.NotNull((await falso.Cobrar(Cobranca(FormaDePagamento.Boleto))).LinhaDigitavel);
        Assert.Equal(("Escola Bento Freitas", "96015-000"), (falso.Pedidos[^1].Pagador?.Nome, falso.Pedidos[^1].Endereco?.Cep));
        Assert.Equal(("VISA", "4242"), ((await falso.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_9f2c71"))).Cartao!.Bandeira, "4242"));
        Assert.Equal("https://banco.exemplo/confirmar/d41b07",
            (await falso.Cobrar(Cobranca(FormaDePagamento.CartaoDebito, "tok_d41b07"))).ConfirmacaoUrl);
        Assert.Equal(SituacaoDaCobranca.TokenInvalido, (await falso.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_outro"))).Situacao);

        falso.Comportamento = ComportamentoDoBanco.Recusar;
        Assert.Equal(SituacaoDaCobranca.Recusada, (await falso.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_9f2c71"))).Situacao);
        falso.Comportamento = ComportamentoDoBanco.EmAnalise;
        Assert.Equal(SituacaoDaCobranca.EmAnalise, (await falso.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_9f2c71"))).Situacao);
        falso.Comportamento = ComportamentoDoBanco.SemResposta;
        Assert.Equal(SituacaoDaCobranca.Indisponivel, (await falso.Cobrar(Cobranca(FormaDePagamento.Pix))).Situacao);
        Assert.Equal(SituacaoDoEstorno.Indisponivel,
            (await falso.Estornar(new PedidoDeEstorno("EST-000001", "PAG-000773", "card_1", Dinheiro.De(1m), "trc-00000002"))).Situacao);
        falso.Comportamento = ComportamentoDoBanco.Normal;
        Assert.Equal(SituacaoDoEstorno.Pendente,
            (await falso.Estornar(new PedidoDeEstorno("EST-000002", "PAG-000773", "card_1", Dinheiro.De(1m), "trc-00000003"))).Situacao);

        Assert.Equal(SituacaoNoBanco.Aguardando, (await falso.Consultar("pix_1")).Situacao);
        await falso.Cancelar("pix_1");
        Assert.All(falso.Pedidos, p => Assert.StartsWith("trc-", p.TraceId, StringComparison.Ordinal));
        Assert.Equal(("ESTORNO", "EST-000002"), (falso.Pedidos[^1].Tipo, falso.Pedidos[^1].Estorno));
    }

    [Fact]
    public async Task RN_305_o_falso_confere_a_assinatura_do_aviso()
    {
        var falso = new IntermediadorFalso("segredo", TimeProvider.System);
        const string corpo = """{"id":"evt_1","tipo":"PAGAMENTO_CONFIRMADO","idExterno":"pix_1","em":"2026-12-03T13:05:00Z","metadados":{"traceId":"trc-1"}}""";
        var lido = await falso.LerAviso(Aviso(corpo, ("x-assinatura", "sha256=" + Hmac("segredo", corpo))));
        Assert.True(lido.AssinaturaValida);
        Assert.Equal(("evt_1", TipoDoAviso.PagamentoConfirmado, "pix_1", "trc-1"),
            (lido.Aviso!.Id, lido.Aviso.Tipo, lido.Aviso.IdExterno, lido.Aviso.TraceIdOrigem));
        Assert.False((await falso.LerAviso(Aviso(corpo, ("x-assinatura", "sha256=00")))).AssinaturaValida);
        Assert.False((await falso.LerAviso(Aviso(corpo))).AssinaturaValida);

        foreach (var outro in new[] { """{"id":"evt_2","tipo":"OUTRO","idExterno":"x"}""", "não é json" })
        {
            var semEfeito = await falso.LerAviso(Aviso(outro, ("x-assinatura", "sha256=" + Hmac("segredo", outro))));
            Assert.True(semEfeito.AssinaturaValida);
            Assert.Null(semEfeito.Aviso);
        }
    }

    // A API do Mercado Pago de mentira: responde pela rota e guarda o que recebeu.
    sealed class ApiDeMentira(Func<HttpRequestMessage, (HttpStatusCode, string)?> responder) : HttpMessageHandler
    {
        public List<(HttpMethod Metodo, string Caminho, string? Corpo, string? Idempotencia, string? Trace)> Recebidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            var corpo = pedido.Content is null ? null : await pedido.Content.ReadAsStringAsync(ct);
            Recebidos.Add((pedido.Method, pedido.RequestUri!.AbsolutePath, corpo,
                pedido.Headers.TryGetValues("X-Idempotency-Key", out var i) ? i.Single() : null,
                pedido.Headers.TryGetValues("X-Trace-Id", out var t) ? t.Single() : null));
            var resposta = responder(pedido) ?? throw new HttpRequestException("sem rede");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        }
    }

    static (IntermediadorMercadoPago, ApiDeMentira) MercadoPago(Func<HttpRequestMessage, (HttpStatusCode, string)?> responder)
    {
        var api = new ApiDeMentira(responder);
        var opcoes = Options.Create(new OpcoesDoMercadoPago
        {
            AccessToken = "token-de-teste",
            SegredoDoWebhook = "segredo-do-webhook",
            UrlDeNotificacao = "https://api.exemplo/pagamento/notificacoes",
        });
        return (new IntermediadorMercadoPago(new HttpClient(api), opcoes, NullLogger<IntermediadorMercadoPago>.Instance), api);
    }

    [Fact]
    public async Task RNF_244_o_mercado_pago_leva_o_trace_e_traduz_os_status()
    {
        var (mp, api) = MercadoPago(p => p.Method == HttpMethod.Post
            ? (HttpStatusCode.Created, """
                {"id":123,"status":"pending","point_of_interaction":{"transaction_data":{"qr_code":"000201abc","qr_code_base64":"iVBOR"}}}
                """)
            : (HttpStatusCode.OK, """{"id":123,"status":"approved","date_approved":"2026-12-03T10:05:00.000-03:00"}"""));
        var pix = await mp.Cobrar(Cobranca(FormaDePagamento.Pix));
        Assert.Equal((SituacaoDaCobranca.Aguardando, "123", "000201abc"), (pix.Situacao, pix.IdExterno, pix.PixCopiaECola));
        var enviado = api.Recebidos.Single();
        Assert.Equal(("/v1/payments", "PAG-000773", "trc-00000001"), (enviado.Caminho, enviado.Idempotencia, enviado.Trace));
        var corpo = JsonNode.Parse(enviado.Corpo!)!;
        Assert.Equal(("pix", "PAG-000773|trc-00000001", "trc-00000001"),
            (corpo["payment_method_id"]!.GetValue<string>(), corpo["external_reference"]!.GetValue<string>(), corpo["metadata"]!["trace_id"]!.GetValue<string>()));
        Assert.Equal("master@ebfga.exemplo", corpo["payer"]!["email"]!.GetValue<string>());

        await mp.Cobrar(Cobranca(FormaDePagamento.Boleto));
        var boleto = JsonNode.Parse(api.Recebidos[^1].Corpo!)!;
        Assert.Equal(("bolbradesco", "CNPJ", "12345678000190", "96015000", "RS"),
            (boleto["payment_method_id"]!.GetValue<string>(), boleto["payer"]!["identification"]!["type"]!.GetValue<string>(),
                boleto["payer"]!["identification"]!["number"]!.GetValue<string>(), boleto["payer"]!["address"]!["zip_code"]!.GetValue<string>(),
                boleto["payer"]!["address"]!["federal_unit"]!.GetValue<string>()));
        await mp.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_x"));
        var cartao = JsonNode.Parse(api.Recebidos[^1].Corpo!)!;
        Assert.Equal(("tok_x", "master", "25"),
            (cartao["token"]!.GetValue<string>(), cartao["payment_method_id"]!.GetValue<string>(), cartao["issuer_id"]!.GetValue<string>()));

        var consulta = await mp.Consultar("123");
        Assert.Equal((SituacaoNoBanco.Paga, new DateTimeOffset(2026, 12, 3, 13, 5, 0, TimeSpan.Zero)), (consulta.Situacao, consulta.Em!.Value));
        await mp.Cancelar("123");
        Assert.Equal((HttpMethod.Put, "cancelar-123"), (api.Recebidos[^1].Metodo, api.Recebidos[^1].Idempotencia));
    }

    [Theory]
    [InlineData("approved", SituacaoDaCobranca.Aprovada)]
    [InlineData("in_process", SituacaoDaCobranca.EmAnalise)]
    [InlineData("rejected", SituacaoDaCobranca.Recusada)]
    public async Task RF_186_o_cartao_no_mercado_pago(string status, SituacaoDaCobranca esperada)
    {
        var (mp, _) = MercadoPago(_ => (HttpStatusCode.Created,
            $$"""{"id":9,"status":"{{status}}","payment_method_id":"master","card":{"last_four_digits":"5100"},"date_approved":"2026-12-03T10:05:00.000-03:00"}"""));
        var cartao = await mp.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_x"));
        Assert.Equal((esperada, "MASTERCARD", "5100"), (cartao.Situacao, cartao.Cartao!.Bandeira, cartao.Cartao.Final));
        Assert.Equal(esperada == SituacaoDaCobranca.Aprovada, cartao.PagoEm is not null);
    }

    [Fact]
    public async Task RNF_241_mercado_pago_sem_resposta_ou_token_recusado()
    {
        var (semRede, _) = MercadoPago(_ => null);
        Assert.Equal(SituacaoDaCobranca.Indisponivel, (await semRede.Cobrar(Cobranca(FormaDePagamento.Pix))).Situacao);
        Assert.Equal(SituacaoNoBanco.Aguardando, (await semRede.Consultar("1")).Situacao);
        Assert.Equal(SituacaoDoEstorno.Indisponivel,
            (await semRede.Estornar(new PedidoDeEstorno("EST-1", "PAG-1", "1", Dinheiro.De(1m), "t"))).Situacao);

        var (token, _) = MercadoPago(_ => (HttpStatusCode.BadRequest, """{"message":"Invalid card_token_id"}"""));
        Assert.Equal(SituacaoDaCobranca.TokenInvalido, (await token.Cobrar(Cobranca(FormaDePagamento.CartaoCredito, "tok_x"))).Situacao);
        var (outro, _) = MercadoPago(_ => (HttpStatusCode.BadRequest, """{"message":"payer.email"}"""));
        Assert.Equal(SituacaoDaCobranca.Indisponivel, (await outro.Cobrar(Cobranca(FormaDePagamento.Pix))).Situacao);
    }

    [Theory]
    [InlineData("approved", SituacaoDoEstorno.Concluido)]
    [InlineData("rejected", SituacaoDoEstorno.Recusado)]
    [InlineData("pending", SituacaoDoEstorno.Pendente)]
    public async Task RF_192_o_estorno_no_mercado_pago(string status, SituacaoDoEstorno esperada)
    {
        var (mp, api) = MercadoPago(_ => (HttpStatusCode.Created, $$"""{"id":77,"status":"{{status}}"}"""));
        var estorno = await mp.Estornar(new PedidoDeEstorno("EST-000012", "PAG-000773", "123", Dinheiro.De(1473.92m), "trc-00000009"));
        Assert.Equal((esperada, "77"), (estorno.Situacao, estorno.IdExterno));
        Assert.Equal(("/v1/payments/123/refunds", "EST-000012"), (api.Recebidos[^1].Caminho, api.Recebidos[^1].Idempotencia));
    }

    static AvisoRecebido AvisoDoMercadoPago(string dataId, string segredo = "segredo-do-webhook")
    {
        const string ts = "1765000000";
        const string requestId = "req-1";
        var v1 = Hmac(segredo, $"id:{dataId};request-id:{requestId};ts:{ts};");
        return new AvisoRecebido($$$"""{"type":"payment","data":{"id":"{{{dataId}}}"}}""",
            new Dictionary<string, string> { ["x-signature"] = $"ts={ts},v1={v1}", ["x-request-id"] = requestId },
            new Dictionary<string, string> { ["data.id"] = dataId });
    }

    [Fact]
    public async Task RN_305_o_aviso_do_mercado_pago_e_conferido_e_lido_na_api()
    {
        var (mp, _) = MercadoPago(p => p.RequestUri!.AbsolutePath.EndsWith("/1", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"id":1,"status":"approved","date_approved":"2026-12-03T10:05:00.000-03:00","metadata":{"trace_id":"trc-7"}}""")
            : p.RequestUri.AbsolutePath.EndsWith("/2", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, """{"id":2,"status":"approved","refunds":[{"id":55,"status":"approved","date_created":"2026-12-04T10:00:00.000-03:00"}]}""")
                : (HttpStatusCode.OK, """{"id":3,"status":"pending"}"""));

        var pago = await mp.LerAviso(AvisoDoMercadoPago("1"));
        Assert.Equal((true, TipoDoAviso.PagamentoConfirmado, "1", "trc-7"),
            (pago.AssinaturaValida, pago.Aviso!.Tipo, pago.Aviso.IdExterno, pago.Aviso.TraceIdOrigem));
        var estorno = await mp.LerAviso(AvisoDoMercadoPago("2"));
        Assert.Equal((TipoDoAviso.EstornoConcluido, "55"), (estorno.Aviso!.Tipo, estorno.Aviso.IdExterno));
        Assert.Null((await mp.LerAviso(AvisoDoMercadoPago("3"))).Aviso);
        Assert.False((await mp.LerAviso(AvisoDoMercadoPago("1", "outro-segredo"))).AssinaturaValida);
        Assert.False((await mp.LerAviso(new AvisoRecebido("{}", new Dictionary<string, string>(), new Dictionary<string, string>()))).AssinaturaValida);

        // RNF-242: a conferência dá o data.id sem chamar a API
        Assert.Equal(new ConferenciaDoAviso(true, "1"), mp.Conferir(AvisoDoMercadoPago("1")));
        Assert.Equal(new ConferenciaDoAviso(false, null), mp.Conferir(AvisoDoMercadoPago("1", "outro-segredo")));
    }

    [Fact]
    public void RF_188_e_RF_189_boleto_e_comprovante_em_pdf()
    {
        var documento = new DocumentoDoPagamentoDto("FAT-000402", "PAG-000790", "Escola Bento Freitas (EBFGA)", "2026-11", "BOLETO", 3698.33m,
            new DateOnly(2026, 12, 10), "23793.38128 60000.000003", "23791129600003698333381286000000000300000000",
            new DateTimeOffset(2026, 12, 4, 13, 0, 0, TimeSpan.Zero), "AUT-1", null, "12.345.678/0001-90", EnderecoDto.De(Endereco));
        foreach (var pdf in new[] { PdfDoPagamento.Boleto(documento), PdfDoPagamento.Comprovante(documento with { Forma = "CARTAO_CREDITO", Cartao = "VISA 4242" }) })
        {
            var texto = Encoding.Latin1.GetString(pdf);
            Assert.StartsWith("%PDF-1.4", texto, StringComparison.Ordinal);
            Assert.EndsWith("%%EOF\n", texto, StringComparison.Ordinal);
            Assert.Contains("FAT-000402", texto, StringComparison.Ordinal);
            Assert.Contains("3.698,33", texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RF_188_o_boleto_traz_o_codigo_de_barras_itf_e_quem_paga()
    {
        var documento = new DocumentoDoPagamentoDto("FAT-000402", "PAG-000790", "Escola Bento Freitas (EBFGA)", "2026-11", "BOLETO", 3698.33m,
            new DateOnly(2026, 12, 10), "23793.38128 60000.000003", "23791129600003698333381286000000000300000000", null, null, null,
            "12.345.678/0001-90", EnderecoDto.De(Endereco));
        var texto = Encoding.Latin1.GetString(PdfDoPagamento.Boleto(documento));
        Assert.Contains("12.345.678/0001-90", texto, StringComparison.Ordinal);
        Assert.Contains("Rua Gonçalves Chaves, 3218", texto, StringComparison.Ordinal);
        // início (4 estreitas, 2 barras) + 22 pares (5 barras cada) + fim (2 barras): 2 + 110 + 2 retângulos
        Assert.Equal(114, texto.Split(" re\n").Length - 1);

        var semBarras = Encoding.Latin1.GetString(PdfDoPagamento.Boleto(documento with { CodigoBarras = "123" }));
        Assert.DoesNotContain(" re\n", semBarras, StringComparison.Ordinal);
    }

    [Fact]
    public void RF_185_o_qr_code_do_pix_e_vetorial()
    {
        var desenho = new DesenhoDoQrCode();
        var curto = desenho.Svg("000201");
        Assert.StartsWith("<svg", curto, StringComparison.Ordinal);
        Assert.EndsWith("</svg>", curto, StringComparison.Ordinal);
        // versão 1 (21 módulos) com a margem de 4 de cada lado
        Assert.Contains("viewBox=\"0 0 29 29\"", curto, StringComparison.Ordinal);
        Assert.DoesNotContain("base64", curto, StringComparison.Ordinal);
        // o canto do localizador de cima à esquerda é escuro
        Assert.Contains("M4 4h1v1h-1z", curto, StringComparison.Ordinal);

        var copiaECola = "00020126580014br.gov.bcb.pix0136" + new string('a', 36) + "5204000053039865406" + "3698.335802BR5913DEBOAZ PAGTOS6007PELOTAS62070503***6304ABCD";
        Assert.Contains("viewBox=\"0 0 ", desenho.Svg(copiaECola), StringComparison.Ordinal);
        Assert.NotEqual(desenho.Svg(copiaECola), desenho.Svg(copiaECola + "X"));
        Assert.Throws<ArgumentException>(() => desenho.Svg(new string('9', 3000)));
    }

    [Fact]
    public void RNF_242_o_aviso_vai_ao_intermediador_do_cabecalho()
    {
        var falso = new IntermediadorFalso(IntermediadorFalso.SegredoPadrao, TimeProvider.System);
        var (mp, _) = MercadoPago(_ => null);
        var ambos = new IntermediadoresDoPagamento(falso, [mp, falso]);
        Assert.Same(falso, ambos.Ligado);
        Assert.Same(mp, ambos.Do(NomeDoIntermediador.MercadoPago));
        Assert.Same(falso, ambos.Do(null));
        Assert.Same(falso, ambos.Do("OUTRO"));
        Assert.Same(mp, ambos.DoAviso(Aviso("{}", ("x-signature", "ts=1,v1=00"), ("x-request-id", "r"))));
        Assert.Same(falso, ambos.DoAviso(Aviso("{}", ("x-assinatura", "sha256=00"))));
        // sem cabeçalho conhecido: o ligado, que recusa a assinatura (401)
        Assert.Same(falso, ambos.DoAviso(Aviso("{}", ("x-assinatura", "md5=00"))));
        Assert.Same(falso, ambos.DoAviso(Aviso("{}")));
        Assert.False(falso.Conferir(Aviso("{}")).AssinaturaValida);

        // em produção não há o falso: o aviso dele vai ao Mercado Pago, que não o confere (401)
        var producao = new IntermediadoresDoPagamento(mp, []);
        var doFalso = Aviso("{}", ("x-assinatura", "sha256=00"));
        Assert.Same(mp, producao.DoAviso(doFalso));
        Assert.False(mp.Conferir(doFalso).AssinaturaValida);
        Assert.Equal((NomeDoIntermediador.Falso, NomeDoIntermediador.MercadoPago), (falso.Nome, mp.Nome));
    }
}
