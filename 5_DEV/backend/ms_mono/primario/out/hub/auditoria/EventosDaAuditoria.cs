using System.Text;
using System.Text.Json;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Microsoft.AspNetCore.Http.Features;

namespace Boaz.MsMono.Primario.Out.Hub.Auditoria;

// O fluxo SSE da Auditoria (RNF-246, RNF-249, 15 RN-279): o evento com o tipo, o id e o corpo em JSON (uma linha de "data:"), o
// comentário ": ping" e o "encerrado" com o código. Cada evento sai na hora, sem buffer; Cache-Control: no-store.
public sealed class EventosDaAuditoria(HttpContext http, JsonSerializerOptions json)
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
        // os cabeçalhos saem já: a conexão pode ficar sem evento até a primeira verificação
        await resposta.Body.FlushAsync(ct);
    }

    public Task Evento(EventoAoVivo evento, CancellationToken ct)
    {
        if (evento.Tipo == "ping") return Ping(ct);
        var id = evento.Id is null ? "" : $"id: {evento.Id}\n";
        var dados = evento.Corpo is null ? "{}" : JsonSerializer.Serialize(evento.Corpo, evento.Corpo.GetType(), json);
        return Escrever($"{id}event: {evento.Tipo}\ndata: {dados}\n\n", ct);
    }

    public Task Evento(string tipo, string id, object corpo, CancellationToken ct) => Evento(new EventoAoVivo(tipo, id, corpo), ct);

    public Task Ping(CancellationToken ct) => Escrever(": ping\n\n", ct);

    async Task Escrever(string evento, CancellationToken ct)
    {
        await http.Response.WriteAsync(evento, Encoding.UTF8, ct);
        await http.Response.Body.FlushAsync(ct);
    }

    // abre o fluxo e entrega os eventos da conexão até ela fechar: o cliente que sai, ou o "encerrado"
    public static async Task Servir(HttpContext http, JsonSerializerOptions json, AoVivoAberto aberto, CancellationToken ct)
    {
        var fluxo = new EventosDaAuditoria(http, json);
        try
        {
            await fluxo.Abrir(ct);
            await foreach (var evento in aberto.Eventos(ct)) await fluxo.Evento(evento, ct);
        }
        catch (Exception falha) when (falha is IOException || (falha is OperationCanceledException && ct.IsCancellationRequested))
        {
            // a conexão fechou do outro lado
        }
    }
}
