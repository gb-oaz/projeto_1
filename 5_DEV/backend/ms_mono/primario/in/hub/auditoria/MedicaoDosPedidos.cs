using System.Diagnostics;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Microsoft.AspNetCore.Routing;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

// A medição de todo pedido aos módulos (RNF-247, 20 RN-316): o tempo da chegada ao fim da resposta, com o método, o modelo da rota
// (/pagamento/faturas/{id}/pagamentos, nunca o id), o módulo e o status, somados na memória por minuto (AcumuladorDeMedicoes) e gravados
// pelo AgendamentoDaAuditoria. Ficam fora o que não é de um módulo (/health, /_teste, /doc, o que não existe), as conexões ao vivo,
// que ficam abertas por minutos, e tudo o que identifica quem pediu: a medição não guarda quem, a conta nem o corpo.
// É o primeiro da fila, para medir também o que as respostas de erro devolvem.
public sealed class MedicaoDosPedidos(RequestDelegate proximo, AcumuladorDeMedicoes acumulador, TimeProvider relogio)
{
    // o endereço de um módulo que a aplicação não tem (o Catálogo ainda sem endpoint): o caminho com o que parece id trocado por {id}
    const int MaximoDeTrechos = 4;

    public async Task InvokeAsync(HttpContext http)
    {
        var chegada = Stopwatch.GetTimestamp();
        var quando = relogio.GetUtcNow();
        try
        {
            await proximo(http);
        }
        finally
        {
            Medir(http, chegada, quando);
        }
    }

    void Medir(HttpContext http, long chegada, DateTimeOffset quando)
    {
        if (http.RequestAborted.IsCancellationRequested) return;
        var endereco = Modelo(http);
        if (endereco is null || ModuloDoEndereco.De(endereco) is null || ConexaoAoVivo(http, endereco)) return;
        acumulador.Somar(http.Request.Method.ToUpperInvariant(), endereco, http.Response.StatusCode, (long)Stopwatch.GetElapsedTime(chegada).TotalMilliseconds, quando);
    }

    // o modelo da rota; sem rota, o caminho de um módulo com os ids trocados; o resto não é medido
    static string? Modelo(HttpContext http)
    {
        if (http.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { Length: > 0 } modelo }) return "/" + modelo.TrimStart('/');
        var trechos = (http.Request.Path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (trechos.Length == 0) return null;
        return "/" + string.Join('/', trechos.Take(MaximoDeTrechos).Select(t => t.Any(char.IsDigit) || t.Length >= 20 ? "{id}" : t));
    }

    static bool ConexaoAoVivo(HttpContext http, string endereco) =>
        endereco.EndsWith("/ao-vivo", StringComparison.Ordinal)
        || (http.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) ?? false);
}
