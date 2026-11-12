using System.Text;
using System.Text.Json;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Microsoft.AspNetCore.Http.Features;

namespace Boaz.MsMono.Primario.Commons.Usuario.Usuarios;

// O fluxo SSE de GET /usuarios/card/ao-vivo (contrato usuarios v15): o evento
// "card" (id = versao, o corpo de GET /usuarios/card expandido, uma linha por
// "data:"), o comentário ": ping" e o "encerrado" com o código. Cada evento sai
// na hora, sem buffer; Cache-Control: no-store.
sealed class FluxoDeEventos(HttpContext http, JsonSerializerOptions json)
{
    readonly JsonSerializerOptions expandido = new(json) { WriteIndented = true };

    public async Task Abrir(CancellationToken ct)
    {
        var resposta = http.Response;
        resposta.StatusCode = StatusCodes.Status200OK;
        resposta.ContentType = "text/event-stream";
        resposta.Headers.CacheControl = "no-store";
        // proxy (nginx) sem buffer
        resposta.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await resposta.StartAsync(ct);
    }

    public Task Card(CardDoModuloDto card, CancellationToken ct) =>
        Escrever($"id: {card.Versao}\nevent: card\n{Dados(JsonSerializer.Serialize(card, expandido))}\n", ct);

    public Task Ping(CancellationToken ct) => Escrever(": ping\n\n", ct);

    public Task Encerrado(MotivoEncerramento motivo, CancellationToken ct) =>
        Escrever($"event: encerrado\n{Dados(JsonSerializer.Serialize(new { codigo = motivo.Texto() }, expandido))}\n", ct);

    static string Dados(string texto)
    {
        var linhas = new StringBuilder();
        foreach (var linha in texto.Replace("\r\n", "\n").Split('\n')) linhas.Append("data: ").Append(linha).Append('\n');
        return linhas.ToString();
    }

    async Task Escrever(string evento, CancellationToken ct)
    {
        await http.Response.WriteAsync(evento, Encoding.UTF8, ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
