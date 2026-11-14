using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Boaz.Qa.Integracao.Suporte;

public sealed record Resposta(int Status, string Texto, IReadOnlyList<string> Cookies,
    IReadOnlyDictionary<string, string>? Cabecalhos = null, byte[]? Bytes = null)
{
    // um cabeçalho da resposta, sem diferença de maiúsculas (X-Trace-Id, 17 RN-271)
    public string? Cabecalho(string nome) =>
        Cabecalhos?.FirstOrDefault(c => string.Equals(c.Key, nome, StringComparison.OrdinalIgnoreCase)).Value;

    // o corpo em JSON; null se vazio ou se não for JSON (um 404 sem corpo, por exemplo)
    public JsonNode? Corpo
    {
        get
        {
            try { return Texto.Length == 0 ? null : JsonNode.Parse(Texto); }
            catch (System.Text.Json.JsonException) { return null; }
        }
    }

    public string? Cookie(string nome) =>
        Cookies.FirstOrDefault(c => c.StartsWith(nome + "=", StringComparison.Ordinal));

    public override string ToString() => $"{Status} {Texto}";
}

// Cliente HTTP da suíte. Os cookies são lidos e enviados à mão: o de
// renovação é Secure e o HttpClient não o mandaria por http.
public sealed class Api : IDisposable
{
    readonly HttpClient http;

    // o backend de API_URL; o Reqnroll usa este construtor
    public Api() : this(Ambiente.ApiUrl) { }

    Api(string url) => http = new(new HttpClientHandler { UseCookies = false })
    {
        BaseAddress = new Uri(url),
        Timeout = TimeSpan.FromSeconds(10),
    };

    // outra instância do backend, subida pelo cenário (Partida)
    public static Api Para(string url) => new(url);

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
        var cabecalhos = resposta.Headers.Concat(resposta.Content.Headers)
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join(",", g.SelectMany(c => c.Value)), StringComparer.OrdinalIgnoreCase);
        // os bytes como vieram (o arquivo de GET /sac/anexos/{id}, 18 RNF-233) e o texto deles
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        return new Resposta((int)resposta.StatusCode, Encoding.UTF8.GetString(bytes), cookies, cabecalhos, bytes);
    }

    // POST multipart/form-data com um arquivo no campo do contrato (18 RN-292:
    // POST /sac/anexos, campo "arquivo"); o tipo vai como o navegador mandaria,
    // pela extensão, e o backend confere pelo conteúdo
    public async Task<Resposta> EnviarArquivo(string caminho, string campo, string nome, byte[] conteudo,
        string tipo, string? token = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, caminho.TrimStart('/'));
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        pedido.Content = new MultipartFormDataContent { { arquivo, campo, nome } };
        if (token is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var cabecalhos = resposta.Headers.Concat(resposta.Content.Headers)
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join(",", g.SelectMany(c => c.Value)), StringComparer.OrdinalIgnoreCase);
        return new Resposta((int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync(), [], cabecalhos);
    }

    public void Dispose() => http.Dispose();
}
