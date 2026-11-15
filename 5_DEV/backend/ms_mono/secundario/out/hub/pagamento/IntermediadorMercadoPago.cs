using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// Seção "MercadoPago": o intermediador de produção (RNF-242). Os segredos vêm
// do Key Vault como variáveis de ambiente, nunca do repositório.
public sealed class OpcoesDoMercadoPago
{
    public const string Secao = "MercadoPago";

    // Chave pública, entregue à tela para gerar o token do cartão (MercadoPago__PublicKey).
    public string PublicKey { get; set; } = "";

    // Segredo: o Access Token da conta (MercadoPago__AccessToken).
    public string AccessToken { get; set; } = "";

    // Segredo: a chave secreta das notificações, que assina o x-signature (MercadoPago__SegredoDoWebhook).
    public string SegredoDoWebhook { get; set; } = "";

    // A API (MercadoPago__UrlDaApi).
    public string UrlDaApi { get; set; } = "https://api.mercadopago.com";

    // O endereço público do POST /pagamento/notificacoes (MercadoPago__UrlDeNotificacao); vazio: o do painel.
    public string UrlDeNotificacao { get; set; } = "";

    // O e-mail do pagador que o Mercado Pago exige (MercadoPago__EmailDoPagador).
    public string EmailDoPagador { get; set; } = "";
}

// O Mercado Pago pela API REST v1 de pagamentos (pagamento-hexagonal v02):
// PIX (point_of_interaction), cartão de crédito e débito só pelo token gerado
// na tela (RNF-239), boleto, estorno total ou parcial e o aviso conferido pelo
// x-signature (ts e v1, HMAC-SHA256 do "id:…;request-id:…;ts:…;") e pelo
// x-request-id. Toda chamada leva X-Idempotency-Key, o X-Trace-Id e o traceId
// em metadata e external_reference (RNF-244). Os status do banco viram os do
// deboaz aqui dentro; o domínio não sabe que é o Mercado Pago. Sem resposta em
// 15 s (o HttpClient), a cobrança é Indisponivel: nada foi cobrado (RNF-241).
public sealed class IntermediadorMercadoPago(HttpClient http, IOptions<OpcoesDoMercadoPago> opcoes, ILogger<IntermediadorMercadoPago> log)
    : IIntermediador
{
    public static readonly TimeSpan Prazo = TimeSpan.FromSeconds(15);

    readonly OpcoesDoMercadoPago mp = opcoes.Value;

    public async Task<ResultadoDaCobranca> Cobrar(PedidoDeCobranca pedido, CancellationToken ct = default)
    {
        var corpo = new JsonObject
        {
            ["transaction_amount"] = pedido.Valor.Reais,
            ["description"] = $"Fatura {pedido.FaturaId}",
            ["external_reference"] = $"{pedido.PagamentoId}|{pedido.TraceId}",
            ["metadata"] = new JsonObject { ["pagamento"] = pedido.PagamentoId, ["fatura"] = pedido.FaturaId, ["trace_id"] = pedido.TraceId },
            ["payer"] = new JsonObject { ["email"] = mp.EmailDoPagador },
        };
        if (mp.UrlDeNotificacao.Length > 0) corpo["notification_url"] = mp.UrlDeNotificacao;
        switch (pedido.Forma)
        {
            case FormaDePagamento.Pix:
                corpo["payment_method_id"] = "pix";
                if (pedido.ExpiraEm is { } expira) corpo["date_of_expiration"] = Data(expira);
                break;
            case FormaDePagamento.Boleto:
                corpo["payment_method_id"] = "bolbradesco";
                if (pedido.VencimentoDoBoleto is { } vencimento) corpo["date_of_expiration"] = Data(Calendario.InicioDoDia(vencimento.AddDays(1)).AddSeconds(-1));
                break;
            default:
                corpo["token"] = pedido.TokenDoCartao;
                corpo["installments"] = 1;
                break;
        }
        var resposta = await Enviar(HttpMethod.Post, "/v1/payments", corpo, pedido.PagamentoId, pedido.TraceId, ct);
        if (resposta is null) return new ResultadoDaCobranca(SituacaoDaCobranca.Indisponivel);
        if (resposta.Value.Status is >= 400 and < 500)
        {
            // o token recusado (usado, vencido ou de outra conta) é o 422 "cartao.token"; o resto, banco indisponível
            return Texto(resposta.Value.Corpo, "message")?.Contains("token", StringComparison.OrdinalIgnoreCase) == true
                ? new ResultadoDaCobranca(SituacaoDaCobranca.TokenInvalido)
                : new ResultadoDaCobranca(SituacaoDaCobranca.Indisponivel);
        }
        return Resultado(resposta.Value.Corpo);
    }

    public async Task<ConsultaNoBanco> Consultar(string idExterno, CancellationToken ct = default)
    {
        var resposta = await Enviar(HttpMethod.Get, $"/v1/payments/{Uri.EscapeDataString(idExterno)}", null, null, null, ct);
        if (resposta is not { Status: < 300 } r) return new ConsultaNoBanco(SituacaoNoBanco.Aguardando);
        return Texto(r.Corpo, "status") switch
        {
            "approved" => new ConsultaNoBanco(SituacaoNoBanco.Paga, DataDe(r.Corpo, "date_approved")),
            "rejected" or "cancelled" => new ConsultaNoBanco(SituacaoNoBanco.Recusada, DataDe(r.Corpo, "date_last_updated")),
            _ => new ConsultaNoBanco(SituacaoNoBanco.Aguardando),
        };
    }

    public async Task Cancelar(string idExterno, CancellationToken ct = default) =>
        await Enviar(HttpMethod.Put, $"/v1/payments/{Uri.EscapeDataString(idExterno)}", new JsonObject { ["status"] = "cancelled" }, $"cancelar-{idExterno}",
            null, ct);

    public async Task<ResultadoDoEstorno> Estornar(PedidoDeEstorno pedido, CancellationToken ct = default)
    {
        var resposta = await Enviar(HttpMethod.Post, $"/v1/payments/{Uri.EscapeDataString(pedido.IdExternoDoPagamento)}/refunds",
            new JsonObject { ["amount"] = pedido.Valor.Reais, ["metadata"] = new JsonObject { ["estorno"] = pedido.EstornoId, ["trace_id"] = pedido.TraceId } },
            pedido.EstornoId, pedido.TraceId, ct);
        if (resposta is not { Status: < 300 } r) return new ResultadoDoEstorno(SituacaoDoEstorno.Indisponivel);
        var id = Texto(r.Corpo, "id");
        return Texto(r.Corpo, "status") switch
        {
            "approved" => new ResultadoDoEstorno(SituacaoDoEstorno.Concluido, id),
            "rejected" or "cancelled" => new ResultadoDoEstorno(SituacaoDoEstorno.Recusado, id),
            _ => new ResultadoDoEstorno(SituacaoDoEstorno.Pendente, id),
        };
    }

    // O aviso do Mercado Pago só diz qual pagamento mudou: a situação é lida
    // na API. Assinatura: x-signature "ts=…,v1=…", com v1 = HMAC-SHA256 em hex
    // de "id:<data.id>;request-id:<x-request-id>;ts:<ts>;" com o segredo.
    public async Task<LeituraDoAviso> LerAviso(AvisoRecebido recebido, CancellationToken ct = default)
    {
        var dataId = recebido.Query.GetValueOrDefault("data.id") ?? Texto(Json(recebido.Corpo)?["data"], "id");
        if (!AssinaturaValida(recebido, dataId)) return new LeituraDoAviso(false, null);
        if (dataId is null || Texto(Json(recebido.Corpo), "type") is not ("payment" or null)) return new LeituraDoAviso(true, null);

        var resposta = await Enviar(HttpMethod.Get, $"/v1/payments/{Uri.EscapeDataString(dataId)}", null, null, null, ct);
        if (resposta is not { Status: < 300 } r) throw new HttpRequestException("Mercado Pago sem resposta à consulta do aviso.");
        var origem = Texto(r.Corpo?["metadata"], "trace_id");
        var avisoId = $"mp_{dataId}_{recebido.Cabecalhos.GetValueOrDefault("x-request-id") ?? Texto(r.Corpo, "date_last_updated")}";

        // um estorno aprovado: o mais novo da lista de refunds
        if (r.Corpo?["refunds"] is JsonArray { Count: > 0 } refunds && refunds[^1] is JsonObject estorno)
        {
            var tipo = Texto(estorno, "status") switch
            {
                "approved" => TipoDoAviso.EstornoConcluido,
                "rejected" or "cancelled" => TipoDoAviso.EstornoRecusado,
                _ => (TipoDoAviso?)null,
            };
            if (tipo is { } t && Texto(estorno, "id") is { } refundId)
            {
                return new LeituraDoAviso(true, new AvisoDoBanco($"{avisoId}_{refundId}", t, refundId, DataDe(estorno, "date_created") ?? DateTimeOffset.UtcNow,
                    origem));
            }
        }
        TipoDoAviso? doPagamento = Texto(r.Corpo, "status") switch
        {
            "approved" => TipoDoAviso.PagamentoConfirmado,
            "rejected" or "cancelled" => TipoDoAviso.PagamentoRecusado,
            _ => null,
        };
        return doPagamento is { } tipoDoPagamento
            ? new LeituraDoAviso(true, new AvisoDoBanco(avisoId, tipoDoPagamento, dataId,
                DataDe(r.Corpo, "date_approved") ?? DataDe(r.Corpo, "date_last_updated") ?? DateTimeOffset.UtcNow, origem))
            : new LeituraDoAviso(true, null);
    }

    bool AssinaturaValida(AvisoRecebido recebido, string? dataId)
    {
        if (mp.SegredoDoWebhook.Length == 0 || recebido.Cabecalhos.GetValueOrDefault("x-signature") is not { } assinatura) return false;
        var partes = assinatura.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);
        if (!partes.TryGetValue("ts", out var ts) || !partes.TryGetValue("v1", out var v1)) return false;
        var manifesto = new StringBuilder();
        if (dataId is not null) manifesto.Append(CultureInfo.InvariantCulture, $"id:{dataId.ToLowerInvariant()};");
        if (recebido.Cabecalhos.GetValueOrDefault("x-request-id") is { } requestId) manifesto.Append(CultureInfo.InvariantCulture, $"request-id:{requestId};");
        manifesto.Append(CultureInfo.InvariantCulture, $"ts:{ts};");
        var esperada = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(mp.SegredoDoWebhook),
            Encoding.UTF8.GetBytes(manifesto.ToString())));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperada), Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));
    }

    // approved → PAGO; in_process/authorized → PROCESSANDO; pending → AGUARDANDO; rejected/cancelled → RECUSADO
    static ResultadoDaCobranca Resultado(JsonNode? corpo)
    {
        var id = Texto(corpo, "id");
        var situacao = Texto(corpo, "status") switch
        {
            "approved" => SituacaoDaCobranca.Aprovada,
            "in_process" or "authorized" => SituacaoDaCobranca.EmAnalise,
            "rejected" or "cancelled" => SituacaoDaCobranca.Recusada,
            _ => SituacaoDaCobranca.Aguardando,
        };
        var transacao = corpo?["point_of_interaction"]?["transaction_data"];
        var pix = Texto(transacao, "qr_code") is { } copiaECola
            ? new DadosDoPix(copiaECola, Texto(transacao, "qr_code_base64") is { } png
                ? $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 33 33\"><image width=\"33\" height=\"33\" href=\"data:image/png;base64,{png}\"/></svg>"
                : "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 33 33\"/>")
            : null;
        var detalhes = corpo?["transaction_details"];
        var cartao = corpo?["card"] is JsonObject c && Texto(c, "last_four_digits") is { } final
            ? new CartaoMascarado(Bandeira(Texto(corpo, "payment_method_id")), final)
            : null;
        return new ResultadoDaCobranca(situacao, id, pix, Texto(detalhes, "digitable_line"), Texto(corpo?["barcode"], "content"),
            Texto(corpo?["three_ds_info"], "external_resource_url"), cartao, situacao == SituacaoDaCobranca.Aprovada ? DataDe(corpo, "date_approved") : null);
    }

    static string Bandeira(string? metodo) => metodo switch
    {
        "visa" or "debvisa" => "VISA",
        "master" or "debmaster" => "MASTERCARD",
        "elo" or "debelo" => "ELO",
        "amex" => "AMEX",
        "hipercard" => "HIPERCARD",
        _ => (metodo ?? "").ToUpperInvariant(),
    };

    // a chamada à API; null quando o banco não respondeu (prazo, rede)
    async Task<(int Status, JsonNode? Corpo)?> Enviar(HttpMethod metodo, string caminho, JsonObject? corpo, string? idempotencia, string? traceId,
        CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(metodo, new Uri(new Uri(mp.UrlDaApi), caminho));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mp.AccessToken);
        if (idempotencia is not null) pedido.Headers.Add("X-Idempotency-Key", idempotencia);
        if (traceId is not null) pedido.Headers.Add("X-Trace-Id", traceId);
        if (corpo is not null) pedido.Content = JsonContent.Create(corpo);
        try
        {
            using var resposta = await http.SendAsync(pedido, ct);
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            return ((int)resposta.StatusCode, Json(texto));
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // nada do pedido vai para o log: nem token, nem valores (RNF-239)
            log.LogWarning("Mercado Pago sem resposta em {Metodo} {Recurso} ({Erro})", metodo, caminho.Split('/')[2], erro.GetType().Name);
            return null;
        }
    }

    static JsonNode? Json(string texto)
    {
        try
        {
            return string.IsNullOrWhiteSpace(texto) ? null : JsonNode.Parse(texto);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? Texto(JsonNode? no, string campo) => no is JsonObject o && o[campo] is JsonValue v
        ? v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.Number => v.ToJsonString(),
            _ => null,
        }
        : null;

    static DateTimeOffset? DataDe(JsonNode? no, string campo) =>
        DateTimeOffset.TryParse(Texto(no, campo), CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data.ToUniversalTime() : null;

    // o formato de data do Mercado Pago: 2026-12-03T13:30:00.000-03:00
    static string Data(DateTimeOffset quando) =>
        quando.ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
}
