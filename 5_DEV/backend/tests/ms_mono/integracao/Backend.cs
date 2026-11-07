using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// O backend inteiro em memória, ligado ao MongoDB e ao Redis de verdade
// (ConnectionStrings:MongoDb e :Redis do ambiente; no CI, os serviços do job
// backend). Cada fixture usa um banco próprio, apagado no fim, e só chaves
// novas no Redis: os testes não apagam nada de fora.
public sealed class Backend : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEntrada = "admin.dboaz@deboaz.com";
    public const string AdminSenha = "Ad#Teste271828";
    public const string NomeDoTime = "Time deboaz dos testes";

    public string NomeDoBanco { get; } = $"boaz_testes_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting("Mongo:Banco", NomeDoBanco)
        .UseSetting("USUARIO_ADMIN_ENTRADA", AdminEntrada)
        .UseSetting("USUARIO_ADMIN_SENHA", AdminSenha)
        .UseSetting("Time:Nome", NomeDoTime);

    // O banco do teste, para preparar o que a API não cria (uma contratação).
    public IMongoDatabase Banco => new MongoClient(
        Environment.GetEnvironmentVariable("ConnectionStrings__MongoDb") ?? "mongodb://localhost:27017").GetDatabase(NomeDoBanco);

    public Cliente Cliente() => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false }));

    // A escola contrata um módulo hoje (coleção contratacoes, do Catálogo).
    public Task Contratar(string contaId, string moduloId) => Banco.GetCollection<BsonDocument>("contratacoes").InsertOneAsync(new BsonDocument
    {
        ["_id"] = Guid.NewGuid().ToString("N"),
        ["contaId"] = contaId,
        ["moduloId"] = moduloId,
        ["desde"] = DateTime.UtcNow.AddMinutes(-1),
        ["ate"] = BsonNull.Value,
    });

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Banco.Client.DropDatabaseAsync(NomeDoBanco);
        await base.DisposeAsync();
    }
}

// Uma resposta: o status, o corpo em JSON e os cookies.
public sealed record Resposta(HttpStatusCode Status, JsonNode? Corpo, IReadOnlyList<string> Cookies)
{
    public string Texto(string campo) => Corpo?[campo]?.GetValue<string>() ?? throw new InvalidOperationException($"sem {campo}: {Corpo}");

    public string? Codigo => Corpo?["codigo"]?.GetValue<string>();

    public string? Cookie(string nome) =>
        Cookies.FirstOrDefault(c => c.StartsWith(nome + "=", StringComparison.Ordinal))?.Split(';')[0][(nome.Length + 1)..];
}

// Cliente HTTP dos testes: JSON nos dois sentidos, token e cookie à mão.
public sealed class Cliente(HttpClient http) : IDisposable
{
    public async Task<Resposta> Enviar(string metodo, string caminho, object? corpo = null, string? token = null,
        string? cookie = null, string? ifMatch = null)
    {
        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), caminho);
        if (corpo is not null)
        {
            var texto = corpo as string ?? System.Text.Json.JsonSerializer.Serialize(corpo);
            pedido.Content = new StringContent(texto, Encoding.UTF8, "application/json");
        }
        if (token is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cookie is not null) pedido.Headers.Add("Cookie", cookie);
        if (ifMatch is not null) pedido.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        using var resposta = await http.SendAsync(pedido);
        var conteudo = await resposta.Content.ReadAsStringAsync();
        JsonNode? json = null;
        try { json = conteudo.Length > 0 ? JsonNode.Parse(conteudo) : null; }
        catch (System.Text.Json.JsonException) { }
        var cookies = resposta.Headers.TryGetValues("Set-Cookie", out var v) ? v.ToList() : [];
        return new Resposta(resposta.StatusCode, json, cookies);
    }

    public void Dispose() => http.Dispose();
}
