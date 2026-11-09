using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Boaz.Qa.Integracao.Suporte;

// Um evento do fluxo SSE de GET /<modulo>/card/ao-vivo (15 RN-279, 17 RN-281):
// "card" (id = versao, data = o corpo de GET /<modulo>/card), "encerrado"
// (data = {"codigo": …}) e o comentário de manutenção ": ping", aqui com o tipo "ping".
public sealed record Evento(string Tipo, string? Id, string Dados, DateTimeOffset Quando)
{
    public JsonNode? Corpo
    {
        get
        {
            try { return Dados.Length == 0 ? null : JsonNode.Parse(Dados); }
            catch (System.Text.Json.JsonException) { return null; }
        }
    }

    public override string ToString() => Tipo == "ping" ? $": ping ({Quando:HH:mm:ss})" : $"{Tipo} id={Id} ({Quando:HH:mm:ss}) {Dados}";
}

// Uma conexão ao vivo aberta pela suíte: lê o corpo em fluxo, como o hub faz
// (fetch com Authorization; o EventSource não manda o token). A recusa (401,
// 403, 422) chega como JSON e o fluxo termina; Texto guarda o corpo cru.
public sealed class Fluxo : IDisposable
{
    readonly HttpClient http = new(new HttpClientHandler { UseCookies = false })
    {
        BaseAddress = new Uri(Ambiente.ApiUrl),
        Timeout = Timeout.InfiniteTimeSpan,
    };
    readonly CancellationTokenSource parar = new();
    readonly List<Evento> eventos = [];
    readonly StringBuilder texto = new();
    readonly SemaphoreSlim sinal = new(0);
    readonly Lock trava = new();
    HttpResponseMessage? resposta;

    public required string Nome { get; init; }
    public int Status { get; private set; }
    public string? TipoDoConteudo { get; private set; }
    public DateTimeOffset Aberto { get; private set; }
    public bool Fechado { get; private set; }

    public string Texto { get { lock (trava) return texto.ToString(); } }
    public IReadOnlyList<Evento> Eventos { get { lock (trava) return [.. eventos]; } }

    // o corpo da recusa, em JSON (null se não for JSON)
    public JsonNode? Corpo
    {
        get
        {
            try { return Texto.Length == 0 ? null : JsonNode.Parse(Texto); }
            catch (System.Text.Json.JsonException) { return null; }
        }
    }

    public static async Task<Fluxo> Abrir(string nome, string caminho, string token)
    {
        var fluxo = new Fluxo { Nome = nome };
        using var pedido = new HttpRequestMessage(HttpMethod.Get, caminho.TrimStart('/'));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var cabecalhos = CancellationTokenSource.CreateLinkedTokenSource(fluxo.parar.Token);
        cabecalhos.CancelAfter(TimeSpan.FromSeconds(10));
        fluxo.resposta = await fluxo.http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, cabecalhos.Token);
        fluxo.Status = (int)fluxo.resposta.StatusCode;
        fluxo.TipoDoConteudo = fluxo.resposta.Content.Headers.ContentType?.MediaType;
        fluxo.Aberto = DateTimeOffset.UtcNow;
        _ = Task.Run(fluxo.Ler);
        return fluxo;
    }

    async Task Ler()
    {
        try
        {
            using var corpo = await resposta!.Content.ReadAsStreamAsync(parar.Token);
            using var leitor = new StreamReader(corpo, Encoding.UTF8);
            string? tipo = null, id = null;
            var dados = new StringBuilder();
            while (await leitor.ReadLineAsync(parar.Token) is { } linha)
            {
                lock (trava) texto.AppendLine(linha);
                if (linha.Length == 0)
                {
                    if (tipo is not null || dados.Length > 0) Guardar(new Evento(tipo ?? "message", id, dados.ToString(), DateTimeOffset.UtcNow));
                    (tipo, id) = (null, null);
                    dados.Clear();
                }
                else if (linha.StartsWith(':'))
                {
                    if (linha[1..].Trim() == "ping") Guardar(new Evento("ping", null, "", DateTimeOffset.UtcNow));
                }
                else if (Campo(linha, "event") is { } e) tipo = e;
                else if (Campo(linha, "id") is { } i) id = i;
                else if (Campo(linha, "data") is { } d)
                {
                    if (dados.Length > 0) dados.Append('\n');
                    dados.Append(d);
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or HttpRequestException or ObjectDisposedException)
        {
            // fechado pela suíte ou pela rede: o fim do fluxo é o que importa
        }
        finally
        {
            Fechado = true;
            sinal.Release();
        }
    }

    static string? Campo(string linha, string nome) =>
        linha.StartsWith(nome + ":", StringComparison.Ordinal) ? linha[(nome.Length + 1)..].TrimStart(' ') : null;

    void Guardar(Evento evento)
    {
        lock (trava) eventos.Add(evento);
        sinal.Release();
    }

    // o primeiro evento, a partir do índice desde, que atende o critério; null
    // se o tempo acabou ou o fluxo terminou antes
    public async Task<Evento?> Esperar(Func<Evento, bool> criterio, TimeSpan limite, int desde = 0)
    {
        var ate = DateTimeOffset.UtcNow + limite;
        while (true)
        {
            var achado = Eventos.Skip(desde).FirstOrDefault(criterio);
            if (achado is not null) return achado;
            var resta = ate - DateTimeOffset.UtcNow;
            if (Fechado || resta <= TimeSpan.Zero) return null;
            await sinal.WaitAsync(resta);
        }
    }

    public Task<Evento?> EsperarTipo(string tipo, TimeSpan limite, int desde = 0) => Esperar(e => e.Tipo == tipo, limite, desde);

    // true se o fluxo terminou dentro do limite
    public async Task<bool> EsperarFim(TimeSpan limite)
    {
        var ate = DateTimeOffset.UtcNow + limite;
        while (!Fechado)
        {
            var resta = ate - DateTimeOffset.UtcNow;
            if (resta <= TimeSpan.Zero) return false;
            await sinal.WaitAsync(resta);
        }
        return true;
    }

    public override string ToString() =>
        $"{Nome}: {Status} {TipoDoConteudo}, {(Fechado ? "fechado" : "aberto")}, eventos: [{string.Join("; ", Eventos)}] {(Eventos.Count == 0 ? Texto : "")}";

    public void Dispose()
    {
        parar.Cancel();
        resposta?.Dispose();
        http.Dispose();
        parar.Dispose();
    }
}

// As conexões ao vivo de um cenário, fechadas no fim dele (o Reqnroll cria uma
// por cenário e a descarta no fim).
public sealed class AoVivo : IDisposable
{
    public List<Fluxo> Fluxos { get; } = [];

    public async Task<Fluxo> Abrir(string nome, string caminho, string token)
    {
        var fluxo = await Fluxo.Abrir(nome, caminho, token);
        Fluxos.Add(fluxo);
        return fluxo;
    }

    public Fluxo Ultimo => Fluxos.Count > 0 ? Fluxos[^1] : throw new InvalidOperationException("nenhuma conexão ao vivo aberta neste cenário");

    public void Dispose()
    {
        foreach (var fluxo in Fluxos) fluxo.Dispose();
    }
}
