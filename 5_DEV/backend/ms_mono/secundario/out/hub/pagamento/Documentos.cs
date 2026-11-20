using System.Globalization;
using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// Os documentos do Pagamento como o pagamento-documentos v03 especifica: valores
// em Decimal128, datas em UTC, dias ("vencimento") como "2026-12-10", status
// com os textos do contrato e o histórico com o traceId de cada mudança (RNF-244).
static class Documentos
{
    const string FormatoDoDia = "yyyy-MM-dd";

    // ── valores ──────────────────────────────────────────────────────────────
    public static BsonValue Valor(Dinheiro dinheiro) => new BsonDecimal128(dinheiro.Reais);

    public static Dinheiro DinheiroEm(this BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && v.IsNumeric ? Dinheiro.De(v.ToDecimal())
            : Dinheiro.Zero;

    public static decimal Decimal(this BsonDocument d, string campo) => d.TryGetValue(campo, out var v) && v.IsNumeric ? v.ToDecimal() : 0m;

    public static string Dia(DateOnly dia) => dia.ToString(FormatoDoDia, CultureInfo.InvariantCulture);

    public static DateOnly? DiaDe(this BsonDocument d, string campo) =>
        DateOnly.TryParseExact(d.Texto(campo), FormatoDoDia, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia) ? dia : null;

    static BsonValue Historico(IEnumerable<Mudanca> historico) => new BsonArray(historico.Select(m => new BsonDocument
    {
        ["de"] = Bson.Valor(m.De),
        ["para"] = m.Para,
        ["em"] = Bson.Valor(m.Em),
        ["traceId"] = m.TraceId,
        ["por"] = Bson.Valor(m.Por),
    }));

    static IEnumerable<Mudanca> HistoricoDe(BsonDocument d) =>
        d.TryGetValue("historico", out var v) && v.IsBsonArray
            ? v.AsBsonArray.OfType<BsonDocument>().Select(m =>
                new Mudanca(m.Texto("de"), m.Texto("para") ?? "", m.Data("em") ?? DateTimeOffset.MinValue, m.Texto("traceId") ?? "", m.Texto("por")))
            : [];

    // ── faturas ──────────────────────────────────────────────────────────────
    // a fatura sem o "emAberto" (a ocupação, RN-303), que só muda por Ocupar e Liberar
    public static BsonDocument Fatura(Fatura f, long versao) => new()
    {
        ["_id"] = f.Id,
        ["versao"] = versao,
        ["conta"] = new BsonDocument { ["id"] = f.Conta.Id, ["sigla"] = f.Conta.Sigla, ["nome"] = f.Conta.Nome, ["tipo"] = f.Conta.Tipo },
        ["competencia"] = f.Competencia.ToString(),
        ["itens"] = new BsonArray(f.Itens.Select(i => new BsonDocument
        {
            ["moduloId"] = i.ModuloId,
            ["nome"] = i.Nome,
            ["cobrancaId"] = i.CobrancaId,
            ["valor"] = Valor(i.Valor),
        })),
        ["valor"] = Valor(f.Valor),
        ["emitidaEm"] = Bson.Valor(f.EmitidaEm),
        ["vencimento"] = Dia(f.Vencimento),
        ["status"] = f.Status.Texto(),
        ["pagaEm"] = Bson.Valor(f.PagaEm),
        ["pagaPor"] = Bson.Valor(f.PagaPor),
        ["valorPago"] = Valor(f.ValorPago),
        ["devolvido"] = Valor(f.Devolvido),
        ["cancelamento"] = f.Cancelamento is { } c
            ? new BsonDocument { ["motivo"] = c.Motivo, ["por"] = c.Por, ["em"] = Bson.Valor(c.Em) }
            : BsonNull.Value,
        ["guardarAte"] = Bson.Valor(f.GuardarAte),
        ["traceId"] = f.TraceId,
        ["historico"] = Historico(f.Historico),
    };

    public static Fatura FaturaDe(BsonDocument d)
    {
        var conta = d.Documento("conta") ?? [];
        var itens = d.TryGetValue("itens", out var v) && v.IsBsonArray
            ? v.AsBsonArray.OfType<BsonDocument>()
                .Select(i => new ItemDaFatura(i.Texto("moduloId") ?? "", i.Texto("nome") ?? "", i.Texto("cobrancaId") ?? "", i.DinheiroEm("valor")))
                .ToList()
            : [];
        var cancelamento = d.Documento("cancelamento") is { } c
            ? new Cancelamento(c.Texto("motivo") ?? "", c.Texto("por") ?? "", c.Data("em") ?? DateTimeOffset.MinValue)
            : null;
        return new Fatura(d["_id"].AsString,
            new ContaDaFatura(conta.Texto("id") ?? "", conta.Texto("sigla") ?? "", conta.Texto("nome") ?? "", conta.Texto("tipo") ?? "INSTITUICAO"),
            Competencia.Ler(d.Texto("competencia")) ?? default, itens, d.Data("emitidaEm") ?? DateTimeOffset.MinValue,
            d.DiaDe("vencimento") ?? DateOnly.MinValue, TextosDoPagamento.LerStatusDaFatura(d.Texto("status")),
            d.Data("pagaEm"), d.Texto("pagaPor"), d.DinheiroEm("valorPago"), d.DinheiroEm("devolvido"), cancelamento, d.Texto("traceId") ?? "",
            HistoricoDe(d), d.Longo("versao"));
    }

    // ── pagamentos ───────────────────────────────────────────────────────────
    // nunca número, validade nem CVV do cartão: só a bandeira e o final (RNF-239)
    public static BsonDocument Pagamento(Core.Hub.Pagamento.Pagamentos.Domain.Pagamento p, long versao) => new()
    {
        ["_id"] = p.Id,
        ["versao"] = versao,
        ["faturaId"] = p.FaturaId,
        ["contaId"] = p.ContaId,
        ["intermediador"] = p.Intermediador,
        ["forma"] = p.Forma.Texto(),
        ["valor"] = Valor(p.Valor),
        ["valorFatura"] = Valor(p.ValorFatura),
        ["juros"] = Valor(p.Juros),
        ["status"] = p.Status.Texto(),
        ["criadoEm"] = Bson.Valor(p.CriadoEm),
        ["criadoPor"] = p.CriadoPor,
        ["expiraEm"] = Bson.Valor(p.ExpiraEm),
        ["pagoEm"] = Bson.Valor(p.PagoEm),
        ["idExterno"] = Bson.Valor(p.IdExterno),
        ["pix"] = p.Pix is { } pix ? new BsonDocument { ["copiaECola"] = pix.CopiaECola, ["qrCodeSvg"] = pix.QrCodeSvg } : BsonNull.Value,
        ["boleto"] = p.Boleto is { } b
            ? new BsonDocument
            {
                ["vencimento"] = Dia(b.Vencimento),
                ["linhaDigitavel"] = b.LinhaDigitavel,
                ["codigoBarras"] = b.CodigoBarras,
                ["endereco"] = b.Endereco is { } e ? Endereco(e) : BsonNull.Value,
            }
            : BsonNull.Value,
        ["confirmacaoUrl"] = Bson.Valor(p.ConfirmacaoUrl),
        ["cartao"] = p.Cartao is { } c ? new BsonDocument { ["bandeira"] = c.Bandeira, ["final"] = c.Final } : BsonNull.Value,
        ["motivo"] = Bson.Valor(p.Motivo),
        ["duplicidade"] = p.Duplicidade,
        ["estornado"] = Valor(p.Estornado),
        ["traceId"] = p.TraceId,
        ["traceIdOrigem"] = Bson.Valor(p.TraceIdOrigem),
        ["historico"] = Historico(p.Historico),
    };

    public static Core.Hub.Pagamento.Pagamentos.Domain.Pagamento PagamentoDe(BsonDocument d)
    {
        var pix = d.Documento("pix") is { } x ? new DadosDoPix(x.Texto("copiaECola") ?? "", x.Texto("qrCodeSvg") ?? "") : null;
        var boleto = d.Documento("boleto") is { } b
            ? new DadosDoBoleto(b.DiaDe("vencimento") ?? DateOnly.MinValue, b.Texto("linhaDigitavel") ?? "", b.Texto("codigoBarras") ?? "",
                b.Documento("endereco") is { } e ? EnderecoDe(e) : null)
            : null;
        var cartao = d.Documento("cartao") is { } c ? new CartaoMascarado(c.Texto("bandeira") ?? "", c.Texto("final") ?? "") : null;
        return new Core.Hub.Pagamento.Pagamentos.Domain.Pagamento(d["_id"].AsString, d.Texto("faturaId") ?? "", d.Texto("contaId") ?? "",
            TextosDoPagamento.LerForma(d.Texto("forma")) ?? FormaDePagamento.Pix, d.DinheiroEm("valorFatura"), d.DinheiroEm("juros"),
            TextosDoPagamento.LerStatusDoPagamento(d.Texto("status")), d.Data("criadoEm") ?? DateTimeOffset.MinValue,
            d.Texto("criadoPor") ?? "", d.Data("expiraEm"), d.Data("pagoEm"), d.Texto("idExterno"), pix, boleto, d.Texto("confirmacaoUrl"), cartao,
            d.Texto("motivo"), d.Logico("duplicidade"), d.DinheiroEm("estornado"), d.Texto("traceId") ?? "", d.Texto("traceIdOrigem"), HistoricoDe(d),
            d.Longo("versao"))
        {
            Intermediador = d.Texto("intermediador") ?? "",
        };
    }

    // o endereço de quem paga o boleto (RF-188)
    public static BsonDocument Endereco(EnderecoDoPagador e) => new()
    {
        ["cep"] = e.Cep,
        ["logradouro"] = e.Logradouro,
        ["numero"] = e.Numero,
        ["bairro"] = e.Bairro,
        ["cidade"] = e.Cidade,
        ["uf"] = e.Uf,
    };

    public static EnderecoDoPagador EnderecoDe(BsonDocument e) => new(e.Texto("cep") ?? "", e.Texto("logradouro") ?? "", e.Texto("numero") ?? "",
        e.Texto("bairro") ?? "", e.Texto("cidade") ?? "", e.Texto("uf") ?? "");

    // ── estornos ─────────────────────────────────────────────────────────────
    public static BsonDocument Estorno(Estorno e, long versao) => new()
    {
        ["_id"] = e.Id,
        ["versao"] = versao,
        ["pagamentoId"] = e.PagamentoId,
        ["faturaId"] = e.FaturaId,
        ["valor"] = Valor(e.Valor),
        ["motivo"] = e.Motivo,
        ["status"] = e.Status.Texto(),
        ["pedidoEm"] = Bson.Valor(e.PedidoEm),
        ["pedidoPor"] = e.PedidoPor,
        ["concluidoEm"] = Bson.Valor(e.ConcluidoEm),
        ["intermediador"] = e.Intermediador,
        ["idExterno"] = Bson.Valor(e.IdExterno),
        ["traceId"] = e.TraceId,
        ["historico"] = Historico(e.Historico),
    };

    public static Estorno EstornoDe(BsonDocument d) =>
        new(d["_id"].AsString, d.Texto("pagamentoId") ?? "", d.Texto("faturaId") ?? "", d.DinheiroEm("valor"), d.Texto("motivo") ?? "",
            TextosDoPagamento.LerStatusDoEstorno(d.Texto("status")), d.Data("pedidoEm") ?? DateTimeOffset.MinValue,
            d.Texto("pedidoPor") ?? "", d.Data("concluidoEm"), d.Texto("idExterno"), d.Texto("traceId") ?? "", HistoricoDe(d), d.Longo("versao"))
        {
            Intermediador = d.Texto("intermediador") ?? "",
        };

    // ── configuração ─────────────────────────────────────────────────────────
    public static BsonDocument Configuracao(ConfiguracaoDoPagamento c) => new()
    {
        ["_id"] = $"cfg-{c.Versao:D6}",
        ["versao"] = c.Versao,
        ["vigenteDesde"] = Bson.Valor(c.VigenteDesde),
        ["formas"] = new BsonDocument(TextosDoPagamento.Formas.Select(f => new BsonElement(f.Texto(), c.Aceita(f)))),
        ["jurosAoDia"] = new BsonDecimal128(c.JurosAoDia),
        ["toleranciaAvisoDias"] = c.ToleranciaAvisoDias,
        ["diaVencimento"] = c.DiaVencimento,
        ["prazos"] = new BsonDocument
        {
            ["pixMinutos"] = c.Prazos.PixMinutos,
            ["debitoMinutos"] = c.Prazos.DebitoMinutos,
            ["boletoDiasUteis"] = c.Prazos.BoletoDiasUteis,
        },
        ["salvaPor"] = Bson.Valor(c.SalvaPor),
    };

    public static ConfiguracaoDoPagamento ConfiguracaoDe(BsonDocument d)
    {
        var padrao = ConfiguracaoDoPagamento.Padrao;
        var formas = d.Documento("formas") ?? [];
        var prazos = d.Documento("prazos") ?? [];
        return new ConfiguracaoDoPagamento(d.Inteiro("versao", padrao.Versao), d.Data("vigenteDesde") ?? DateTimeOffset.MinValue,
            TextosDoPagamento.Formas.ToDictionary(f => f, f => formas.Logico(f.Texto())), d.Decimal("jurosAoDia"),
            d.Inteiro("toleranciaAvisoDias", padrao.ToleranciaAvisoDias), d.Inteiro("diaVencimento", ConfiguracaoDoPagamento.Dia),
            new PrazosDoPagamento(prazos.Inteiro("pixMinutos", padrao.Prazos.PixMinutos), prazos.Inteiro("debitoMinutos", padrao.Prazos.DebitoMinutos),
                prazos.Inteiro("boletoDiasUteis", padrao.Prazos.BoletoDiasUteis)),
            d.Texto("salvaPor"));
    }
}
