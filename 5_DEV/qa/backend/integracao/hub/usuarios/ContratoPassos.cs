using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos de contrato.feature: respostas do contrato usuarios-contrato-api v15
// sem cenário no requisito (401 do card, do ao vivo e das preferências,
// Cache-Control, "card" só com versao nova, PUT sem o corpo inteiro, ordem dos
// nomes do apoio e da lista, relógio de teste e o 404 fora de hml com
// Teste__Relogio=sim). Propostos ao deboaz-po.
[Binding]
public sealed partial class ContratoPassos(Mundo mundo, AoVivo aoVivo, Relogio relogio, Partida partida)
{
    const string TokenInvalido = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJxYSJ9.assinatura-invalida";
    static readonly TimeSpan Perto = TimeSpan.FromMinutes(5);

    int participantes;
    Resposta? daPartida;

    // ── 401 SESSAO_TERMINADA: sem token, token inválido, sessão que terminou ─
    [When(@"^envia (GET|PUT) (/usuarios/card|/usuarios/eu/preferencias) (sem token|com um token inválido|com a sessão da Maria terminada)$")]
    public async Task SemSessao(string metodo, string caminho, string credencial)
    {
        var corpo = metodo == "PUT" ? new JsonObject { ["atualizacaoCards"] = "AO_VIVO", ["intervaloAoVivo"] = 15 }.ToJsonString() : null;
        await mundo.Enviar(metodo, caminho, corpo, await Token(credencial));
    }

    [When(@"^envia GET (/usuarios/card/ao-vivo\S*) (sem token|com um token inválido)$")]
    public async Task AoVivoSemSessao(string caminho, string credencial) =>
        await aoVivo.Abrir(credencial, caminho, await Token(credencial));

    async Task<string?> Token(string credencial)
    {
        switch (credencial)
        {
            case "sem token":
                return null;
            case "com um token inválido":
                return TokenInvalido;
            default:
                var sessao = await mundo.SessaoDe(await mundo.Maria(), Navegador);
                await Estado.ExpirarSessao(sessao.Sid);
                return sessao.Acesso;
        }
    }

    // ── nomes do apoio e da lista, na ordem do contrato v15 ───────────────
    // com um usuário criado o card não está vazio e traz as linhas
    [When(@"^(o administrador do deboaz|a conta master da EBFGA|a staff Maria|o educador autônomo Pedro) pede o card com ao menos um usuário criado$")]
    public async Task PedeOCard(string quem)
    {
        Pessoa por;
        switch (quem)
        {
            case "o administrador do deboaz":
                await mundo.Conta();
                por = mundo.Admin();
                break;
            case "a staff Maria":
                por = await mundo.Maria();
                await mundo.Conceder(por, new() { ["MOD-03"] = "CU" });
                await NovoParticipante(por);
                break;
            case "o educador autônomo Pedro":
                await NovoParticipante(por = await mundo.Pedro());
                break;
            default:
                await NovoParticipante(por = await mundo.Master());
                break;
        }
        await mundo.EnviarComo(por, "GET", "/usuarios/card");
    }

    [Then(@"^a normal traz no apoio (.+)$")]
    public void Apoio(string nomes) => NomesEmOrdem("normal", "apoio", nomes);

    [Then(@"^a expandida traz na lista (.+)$")]
    public void Lista(string nomes) => NomesEmOrdem("expandida", "lista", nomes);

    void NomesEmOrdem(string tamanho, string campo, string nomes)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperado = Aspas().Matches(nomes).Select(m => m.Groups[1].Value).ToList();
        var veio = mundo.Ultima.Corpo?[tamanho]?[campo]?.AsArray().Select(l => l?["nome"]?.GetValue<string>() ?? "").ToList() ?? [];
        Assert.True(veio.SequenceEqual(esperado),
            $"{tamanho}.{campo} devia ser [{string.Join(", ", esperado)}], nessa ordem; veio [{string.Join(", ", veio)}]");
    }

    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex Aspas();

    // ── ao vivo: Cache-Control e "card" só com versao nova ─────────────────
    [Then(@"^o cabeçalho Cache-Control traz ""([\w-]+)""$")]
    public void CacheControl(string valor)
    {
        var fluxo = aoVivo.Ultimo;
        Assert.True(fluxo.CacheControl?.Contains(valor, StringComparison.OrdinalIgnoreCase) == true,
            $"esperava Cache-Control com {valor}, veio \"{fluxo.CacheControl}\": {fluxo}");
    }

    // nada muda depois de abrir: em 3 intervalos (com folga) só o primeiro "card"
    [Then(@"^depois do primeiro ""card"", nenhum outro chega em (\d+) intervalos sem mudança da versao$")]
    public async Task SoComVersaoNova(int intervalos)
    {
        var fluxo = aoVivo.Ultimo;
        var primeiro = await fluxo.EsperarTipo("card", TimeSpan.FromSeconds(10));
        Assert.True(fluxo.Status == 200 && primeiro is not null, $"esperava 200 e o primeiro \"card\": {fluxo}");
        var intervalo = int.Parse(Regex.Match(fluxo.Caminho, @"intervalo=(\d+)").Groups[1].Value);
        var outro = await fluxo.EsperarTipo("card", TimeSpan.FromSeconds(intervalo * intervalos + 2), fluxo.Eventos.ToList().IndexOf(primeiro) + 1);
        Assert.True(outro is null, $"sem mudança da versao, nenhum \"card\" devia chegar depois do primeiro: {fluxo}");
        Assert.False(fluxo.Fechado, $"a conexão devia ficar aberta: {fluxo}");
    }

    // ── preferências: PUT sem o corpo inteiro ──────────────────────────────
    [When(@"^a conta master envia PUT /usuarios/eu/preferencias só com (\w+) ""(\w+)""$")]
    public async Task SoUmCampo(string campo, string valor) =>
        await mundo.EnviarComo(await mundo.Master(), "PUT", "/usuarios/eu/preferencias", new JsonObject { [campo] = valor }.ToJsonString());

    [Then(@"^a mensagem diz que o campo ""(\w+)"" está faltando ou é inválido$")]
    public void MensagemDoCampo(string campo) =>
        Assert.True(mundo.Ultima.Corpo?["mensagem"]?.GetValue<string>() == $"O campo \"{campo}\" está faltando ou é inválido.",
            $"esperava a mensagem do contrato para \"{campo}\": {mundo.Ultima}");

    // ── relógio de teste ───────────────────────────────────────────────────
    // o relógio muda antes de qualquer pedido do cenário: os tokens nascem nele
    [When(@"^o relógio de teste vai para ""(.+)""$")]
    public Task RelogioVai(string agora) => relogio.Definir(DateTimeOffset.Parse(agora));

    [When(@"^a conta master cria um participante$")]
    public async Task MasterCria() => await NovoParticipante(await mundo.Master());

    [Then(@"^o card traz atualizadoEm perto de ""(.+)""$")]
    public async Task AtualizadoPerto(string quando) => await AtualizadoEm(DateTimeOffset.Parse(quando));

    [When(@"^o relógio de teste volta ao real$")]
    public Task RelogioVolta() => mundo.Enviar("DELETE", "/_teste/relogio");

    // os tokens de 2027 não valem mais no relógio real: entra de novo
    [Then(@"^depois de outro participante, o card traz atualizadoEm perto da hora real$")]
    public async Task AtualizadoAgora()
    {
        await mundo.Entrar(await mundo.Master(), Navegador);
        await NovoParticipante(await mundo.Master());
        await AtualizadoEm(DateTimeOffset.UtcNow);
    }

    async Task AtualizadoEm(DateTimeOffset esperado)
    {
        var card = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", "/usuarios/card"), 200, "GET /usuarios/card").Corpo!;
        var texto = card["atualizadoEm"]?.GetValue<string>() ?? "";
        Assert.True(DateTimeOffset.TryParse(texto, out var veio) && (veio - esperado).Duration() < Perto,
            $"atualizadoEm devia estar perto de {esperado:O}, veio \"{texto}\"");
    }

    // ── 404 fora de AMBIENTE=hml com Teste__Relogio=sim ────────────────────
    // uma instância própria (suporte/Partida.cs); a variável vai junto com o Key Vault
    [Given(@"^a aplicação iniciada em (produção|homologação), com Teste__Relogio (""sim""|ausente)$")]
    public async Task IniciadaCom(string ambiente, string flag)
    {
        var sigla = ambiente == "produção" ? "prd" : "hml";
        partida.KeyVaultCompleto(sigla);
        partida.KeyVault["Teste__Relogio"] = flag == "ausente" ? "" : "sim";
        await partida.IniciarPronta($"{ambiente}, Teste__Relogio {flag}");
    }

    [When(@"^envia (PUT|POST) (/_teste/\w+) a ela$")]
    public async Task EnviaAEla(string metodo, string caminho) =>
        daPartida = await partida.Api.Enviar(metodo, caminho,
            metodo == "PUT" ? new JsonObject { ["agora"] = "2027-02-28T23:50:00-03:00" }.ToJsonString() : null);

    [Then(@"^ela responde (\d{3})$")]
    public void ElaResponde(int status) => Respostas.Confere(daPartida!, status);

    async Task NovoParticipante(Pessoa por) =>
        Exigir(await mundo.EnviarComo(por, "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "PARTICIPANTE",
            ["nome"] = $"Participante ctr{Letras(participantes)}",
            ["nomeCurto"] = $"ctr{Letras(participantes++)}",
            ["matricula"] = null,
            ["responsaveis"] = new JsonArray(),
        }.ToJsonString()), 201, "POST /usuarios de participante");
}
