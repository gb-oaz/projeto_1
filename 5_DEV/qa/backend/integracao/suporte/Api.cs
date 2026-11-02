using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Boaz.Qa.Integracao.Suporte;

public sealed record Resposta(int Status, string Texto, IReadOnlyList<string> Cookies)
{
    public JsonNode? Corpo => Texto.Length == 0 ? null : JsonNode.Parse(Texto);

    public string? Cookie(string nome) =>
        Cookies.FirstOrDefault(c => c.StartsWith(nome + "=", StringComparison.Ordinal));

    public override string ToString() => $"{Status} {Texto}";
}

// Cliente HTTP da suíte. Os cookies são lidos e enviados à mão: o de
// renovação é Secure e o HttpClient não o mandaria por http.
public sealed class Api : IDisposable
{
    readonly HttpClient http = new(new HttpClientHandler { UseCookies = false })
    {
        BaseAddress = new Uri(Ambiente.ApiUrl),
        Timeout = TimeSpan.FromSeconds(10),
    };

    public async Task<Resposta> Enviar(
        string metodo, string caminho, string? corpo = null, string? token = null,
        string? cookie = null, string? ifMatch = null)
    {
        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), caminho.TrimStart('/'));
        if (corpo is not null) pedido.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
        if (token is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cookie is not null) pedido.Headers.Add("Cookie", cookie);
        if (ifMatch is not null) pedido.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        using var resposta = await http.SendAsync(pedido);
        var cookies = resposta.Headers.TryGetValues("Set-Cookie", out var v) ? v.ToList() : [];
        return new Resposta((int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync(), cookies);
    }

    public void Dispose() => http.Dispose();
}
