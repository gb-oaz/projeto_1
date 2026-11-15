using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace Boaz.MsMono.Primario.Out.Hub.Pagamento;

// O fluxo SSE do Pagamento: o pagamento ao vivo (evento "pagamento", id = o
// status, RNF-238) e o card ao vivo (evento "card", id = versao, RN-307), com
// ": ping". Cada evento sai na hora, sem buffer; Cache-Control: no-store.
public sealed class EventosDoPagamento(HttpContext http, JsonSerializerOptions json)
{
    public async Task Abrir(CancellationToken ct)
    {
        var resposta = http.Response;
        resposta.StatusCode = StatusCodes.Status200OK;
        resposta.ContentType = "text/event-stream";
        resposta.Headers.CacheControl = "no-store";
        resposta.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await resposta.StartAsync(ct);
        // os cabeçalhos (com o X-Trace-Id) saem já: o pagamento pode ficar sem evento até o aviso do banco
        await resposta.Body.FlushAsync(ct);
    }

    public Task Evento(string tipo, string id, object corpo, CancellationToken ct) =>
        Escrever($"id: {id}\nevent: {tipo}\ndata: {JsonSerializer.Serialize(corpo, corpo.GetType(), json)}\n\n", ct);

    public Task Ping(CancellationToken ct) => Escrever(": ping\n\n", ct);

    async Task Escrever(string evento, CancellationToken ct)
    {
        await http.Response.WriteAsync(evento, Encoding.UTF8, ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
