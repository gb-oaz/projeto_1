using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// O banco dos cenários de 1_Requisitos/19_modulo-pagamento.md: o intermediador
// falso do backend (IntermediadorFalso, pagamento-hexagonal v02, RNF-242). A
// suíte nunca chama serviço externo. Ela fala com o falso de dois jeitos:
//
// 1 · Ganchos de teste, só com AMBIENTE=hml e Teste__Relogio=sim (o backend
//     do qa no ci.sh), como o PUT /_teste/relogio (suporte/Relogio.cs):
//
//   PUT /_teste/pagamento/banco   {"comportamento": "RECUSAR"}        → 204
//       o que o falso responde às próximas cobranças, até o DELETE:
//       "RECUSAR" (cartão recusado pelo emissor), "EM_ANALISE" (cartão em
//       análise: PROCESSANDO) ou "SEM_RESPOSTA" (o banco não responde no
//       prazo: 503 BANCO_INDISPONIVEL, sem esperar os 15 s de verdade)
//   DELETE /_teste/pagamento/banco                                    → 204
//       volta ao normal: crédito aprovado na hora (PAGO); PIX, débito e
//       boleto ficam AGUARDANDO o aviso
//   GET /_teste/pagamento/banco/pedidos                               → 200
//       o que o falso recebeu do deboaz, na ordem de chegada:
//       { "itens": [ { "tipo": "COBRANCA" | "ESTORNO", "idExterno": "pix_8c1e5d",
//           "forma": "PIX", "valor": 3698.33,
//           "traceId": "trc-1d6e3f08",                (o cabeçalho X-Trace-Id recebido)
//           "metadados": { "pagamento": "PAG-000781", "estorno": null,
//                          "traceId": "trc-1d6e3f08" } } ] }
//
//     Cartões do falso: o token "tok_9f2c71" é um VISA de final 4242 e o
//     "tok_d41b07" um MASTERCARD de final 5100 (os dos exemplos); o
//     "tok_vencido" é um token já usado (422 DADOS_INVALIDOS, "cartao.token").
//
// 2 · O aviso do banco, pelo endereço do contrato (POST /pagamento/notificacoes,
//     RN-305), no formato do falso. O contrato deixa o corpo e a assinatura
//     para o adaptador de cada intermediador; o do falso é:
//
//   X-Assinatura: sha256=<HMAC-SHA256 do corpo, em hexadecimal, com o segredo>
//   {
//     "id": "evt_7d02c4",                  o id do aviso (idempotência)
//     "tipo": "PAGAMENTO_CONFIRMADO",      ou PAGAMENTO_RECUSADO, ESTORNO_CONCLUIDO, ESTORNO_RECUSADO
//     "idExterno": "pix_8c1e5d",           o da cobrança ou do estorno no banco
//     "em": "2026-12-03T13:12:00Z",        quando o banco pagou, recusou ou devolveu
//     "metadados": { "traceId": "trc-2e7d90aa" }    só quando o banco devolve o da criação
//   }
//
//     O segredo é o de PAGAMENTO_SEGREDO_DO_AVISO (suporte/Ambiente.cs), o
//     mesmo que o backend do qa entrega ao falso.
public sealed class BancoFalso(Api api)
{
    public const string Confirmado = "PAGAMENTO_CONFIRMADO", Recusado = "PAGAMENTO_RECUSADO",
        EstornoConcluido = "ESTORNO_CONCLUIDO", EstornoRecusado = "ESTORNO_RECUSADO";
    public const string Recusar = "RECUSAR", EmAnalise = "EM_ANALISE", SemResposta = "SEM_RESPOSTA";

    public bool Mudou { get; private set; }
    // o último aviso enviado e a resposta dele (o repetido da RN-305 manda o mesmo)
    public JsonObject? UltimoAviso { get; private set; }
    public Resposta? RespostaDoAviso { get; private set; }

    public async Task Proximo(string comportamento)
    {
        Mudou = true;
        Exigir(await api.Enviar("PUT", "/_teste/pagamento/banco", new JsonObject { ["comportamento"] = comportamento }.ToJsonString()), 204,
            $"PUT /_teste/pagamento/banco {comportamento} (intermediador falso, Teste__Relogio=sim)");
    }

    public async Task Normal()
    {
        if (!Mudou) return;
        Mudou = false;
        try { await api.Enviar("DELETE", "/_teste/pagamento/banco"); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // o próximo cenário que mudar o banco o define de novo
        }
    }

    public async Task<List<JsonObject>> Pedidos()
    {
        var r = Exigir(await api.Enviar("GET", "/_teste/pagamento/banco/pedidos"), 200,
            "GET /_teste/pagamento/banco/pedidos (o que o intermediador falso recebeu, Teste__Relogio=sim)");
        return [.. (r.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>()];
    }

    // o pedido que o falso recebeu para o pagamento ou o estorno: o último, porque
    // os números recomeçam a cada cenário e o falso guarda os dos anteriores
    public async Task<JsonObject> PedidoDe(string id)
    {
        var pedidos = await Pedidos();
        return pedidos.LastOrDefault(p => Texto(p["metadados"], "pagamento") == id && Texto(p, "tipo") == "COBRANCA" || Texto(p["metadados"], "estorno") == id)
            ?? throw new InvalidOperationException(
                $"preparo: o intermediador falso não recebeu o pedido do {id}: [{string.Join(", ", pedidos.Select(p => p.ToJsonString()))}]");
    }

    public async Task<string> IdExterno(string id) =>
        Texto(await PedidoDe(id), "idExterno") ?? throw new InvalidOperationException($"preparo: o pedido do {id} no intermediador falso veio sem idExterno");

    // o corpo do aviso, no formato do falso; cada aviso com o seu id
    public static JsonObject Aviso(string tipo, string idExterno, DateTimeOffset em, string? traceId = null)
    {
        var corpo = new JsonObject
        {
            ["id"] = $"evt_{Guid.NewGuid():N}"[..10],
            ["tipo"] = tipo,
            ["idExterno"] = idExterno,
            ["em"] = em.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };
        if (traceId is not null) corpo["metadados"] = new JsonObject { ["traceId"] = traceId };
        return corpo;
    }

    // POST /pagamento/notificacoes, sem token: vale a assinatura
    public async Task<Resposta> Avisar(JsonObject aviso, bool assinaturaValida = true)
    {
        var corpo = aviso.ToJsonString();
        UltimoAviso = aviso;
        return RespostaDoAviso = await api.Enviar("POST", "/pagamento/notificacoes", corpo, doPedido: new Dictionary<string, string>
        {
            ["X-Assinatura"] = assinaturaValida ? Assinar(corpo) : "sha256=" + new string('0', 64),
        });
    }

    // o aviso do pagamento ou do estorno, com a assinatura certa
    public async Task<Resposta> Avisar(string id, string tipo, DateTimeOffset em, string? traceId = null) =>
        await Avisar(Aviso(tipo, await IdExterno(id), em, traceId));

    static string Assinar(string corpo) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Ambiente.PagamentoSegredoDoAviso), Encoding.UTF8.GetBytes(corpo)));

    static string? Texto(JsonNode? no, string campo) =>
        no?[campo] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String ? v.GetValue<string>() : null;
}

// o banco volta ao normal no fim do cenário que o mudou
[Binding]
public sealed class BancoNoFim(BancoFalso banco)
{
    [AfterScenario]
    public Task VoltarAoNormal() => banco.Normal();
}
