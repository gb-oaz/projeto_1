using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

// Uma instância própria do backend, para os cenários que falam da
// inicialização (17 RN-179, RN-190, RN-266 a RN-268, RNF-225, RNF-226): subir pela
// primeira vez, subir de novo com outro segredo, subir em produção, subir sem
// um segredo. O backend do API_URL já está no ar e não serve para isso.
//
// Continua caixa-preta (RN-151): a suíte roda o artefato publicado pelo
// comando de API_COMANDO, rodado por /bin/sh -c (ex.: "cd /app && exec dotnet
// Boaz.MsMono.dll": a pasta do artefato, onde está o appsettings.json) e só
// fala com ele pelo HTTP, pelo código de saída e pelo log. O que recebe:
//   - as variáveis PARTIDA_* do ambiente da suíte, sem o prefixo (conexões,
//     chave, um banco próprio, diferente do MONGO_BANCO do backend principal);
//   - AMBIENTE (hml ou prd) e o "Key Vault" do cenário: as variáveis USUARIO_*,
//     com entradas do requisito e senhas geradas na hora (RNF-202);
//   - a porta, em ASPNETCORE_HTTP_PORTS.
// As USUARIO_* e o AMBIENTE herdados da suíte não passam: cada cenário diz o
// que o Key Vault tem. O banco da instância é limpo antes de cada cenário com
// os outros (Estado.Limpar) e fica entre as partidas do mesmo cenário.
//
// O Reqnroll cria uma por cenário e a descarta no fim, parando o processo.
public sealed class Partida : IDisposable
{
    static readonly TimeSpan LimiteDaPartida = TimeSpan.FromSeconds(90);

    readonly StringBuilder log = new();
    Process? processo;
    int porta;
    Api? api;

    public string AmbienteDaPartida { get; set; } = "hml";

    // o Key Vault do cenário: nome da variável → valor (null = segredo ausente)
    public Dictionary<string, string?> KeyVault { get; } = [];

    public int? CodigoDeSaida { get; private set; }
    public bool AtendeuPedido { get; private set; }

    public string Log { get { lock (log) return log.ToString(); } }

    public Api Api => api ?? throw new InvalidOperationException("a instância não foi iniciada");

    // o Key Vault completo do ambiente: o administrador e, em homologação, as
    // contas qa e a lista de módulos delas (RN-268). Entradas do requisito,
    // senhas novas que seguem a RNF-202
    public void KeyVaultCompleto(string ambiente)
    {
        AmbienteDaPartida = ambiente;
        KeyVault.Clear();
        foreach (var conta in ambiente == "hml" ? ContasIniciais.Todas : [ContasIniciais.Admin])
        {
            KeyVault[conta.VariavelEntrada] = conta.EntradaDoExemplo;
            if (conta.VariavelSenha is { } senha) KeyVault[senha] = ContasIniciais.SenhaNova();
        }
        if (ambiente == "hml") Modulos(ContasIniciais.ModulosDaStaff, ContasIniciais.ModulosDoTime);
    }

    // a lista de módulos iniciais das contas qa no Key Vault (RN-268):
    // {"staff": {"mods": {…}}, "time": {"mods": {…}}}
    public void Modulos(IReadOnlyDictionary<string, string> staff, IReadOnlyDictionary<string, string> time) =>
        KeyVault[ContasIniciais.VariavelModulos] = new JsonObject
        {
            ["staff"] = Parte(staff),
            ["time"] = Parte(time),
        }.ToJsonString();

    static JsonObject Parte(IReadOnlyDictionary<string, string> mods) => new()
    {
        ["mods"] = new JsonObject(mods.Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value))),
    };

    // entrada de uma conta inicial nesta instância, com o que o Key Vault do cenário tem
    public Task<Resposta> Entrar(ContaInicial conta) => conta.Participante
        ? Api.Enviar("POST", "/auth/quest/entrar", CorpoDoQuest(Entrada(conta)))
        : Api.Enviar("POST", "/auth/hub/entrar", CorpoDoHub(Entrada(conta), Senha(conta)));

    public async Task<string> Token(ContaInicial conta) =>
        Mundo.Texto(Mundo.Exigir(await Entrar(conta), 200, $"POST /auth/hub/entrar de {conta.Nome}"), "tokenAcesso");

    public static string CorpoDoHub(string entrada, string senha) => new JsonObject
    {
        ["entrada"] = entrada,
        ["senha"] = senha,
        ["aparelho"] = Mundo.Navegador,
    }.ToJsonString();

    public static string CorpoDoQuest(string codigo) => new JsonObject
    {
        ["codigo"] = codigo,
        ["aparelho"] = Mundo.Navegador,
        ["quem"] = new JsonObject { ["tipo"] = "PARTICIPANTE" },
    }.ToJsonString();

    public string Entrada(ContaInicial conta) => KeyVault.GetValueOrDefault(conta.VariavelEntrada) ?? conta.EntradaDoExemplo;
    public string Senha(ContaInicial conta) => KeyVault.GetValueOrDefault(conta.VariavelSenha ?? "") ?? "";

    // sobe a instância e espera: true quando /health responde 200; false se o
    // processo terminou antes disso (o caso da RNF-226)
    public async Task<bool> Iniciar()
    {
        if (Ambiente.ApiComando.Length == 0)
        {
            throw new PendingStepException(
                "API_COMANDO ausente: os cenários de inicialização sobem uma instância própria do backend (README, seção 5)");
        }
        Parar();
        lock (log) log.Clear();
        CodigoDeSaida = null;
        AtendeuPedido = false;
        porta = PortaLivre();
        var inicio = new ProcessStartInfo("/bin/sh", ["-c", Ambiente.ApiComando])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var nome in inicio.Environment.Keys.ToList())
        {
            if (nome.StartsWith("USUARIO_", StringComparison.Ordinal) || nome.StartsWith("PARTIDA_", StringComparison.Ordinal)
                || nome is "AMBIENTE" or "ADMIN_ENTRADA" or "ADMIN_SENHA" or "ASPNETCORE_URLS")
            {
                inicio.Environment.Remove(nome);
            }
        }
        foreach (var (nome, valor) in Ambiente.DaPartida()) inicio.Environment[nome] = valor;
        inicio.Environment["AMBIENTE"] = AmbienteDaPartida;
        inicio.Environment["ASPNETCORE_HTTP_PORTS"] = porta.ToString();
        foreach (var (nome, valor) in KeyVault)
        {
            if (valor is not null) inicio.Environment[nome] = valor;
        }

        processo = new Process { StartInfo = inicio, EnableRaisingEvents = true };
        processo.OutputDataReceived += (_, e) => Anotar(e.Data);
        processo.ErrorDataReceived += (_, e) => Anotar(e.Data);
        processo.Start();
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();
        api?.Dispose();
        api = Api.Para($"http://127.0.0.1:{porta}");

        using var saude = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var limite = DateTime.UtcNow + LimiteDaPartida;
        while (DateTime.UtcNow < limite)
        {
            if (processo.HasExited)
            {
                await processo.WaitForExitAsync();
                CodigoDeSaida = processo.ExitCode;
                return false;
            }
            try
            {
                if ((await saude.GetAsync($"http://127.0.0.1:{porta}/health")).StatusCode == HttpStatusCode.OK)
                {
                    AtendeuPedido = true;
                    return true;
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                // ainda subindo
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"a instância não respondeu /health nem terminou em {LimiteDaPartida.TotalSeconds} s. Log:\n{Log}");
    }

    // sobe e exige que fique pronta, dizendo o log se não ficou
    public async Task IniciarPronta(string motivo)
    {
        if (!await Iniciar())
        {
            throw new InvalidOperationException($"preparo: a aplicação ({motivo}) terminou com {CodigoDeSaida} antes de atender. Log:\n{Log}");
        }
    }

    public void Parar()
    {
        if (processo is { HasExited: false })
        {
            processo.Kill(entireProcessTree: true);
            processo.WaitForExit(10_000);
        }
        processo?.Dispose();
        processo = null;
    }

    void Anotar(string? linha)
    {
        if (linha is null) return;
        lock (log) log.AppendLine(linha);
    }

    static int PortaLivre()
    {
        using var ouvinte = new TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        return ((IPEndPoint)ouvinte.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        Parar();
        api?.Dispose();
    }
}
