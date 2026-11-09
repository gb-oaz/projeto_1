using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos do card ao vivo: RN-281 de 1_Requisitos/17_modulo-usuario.md e, pela
// instância do Usuários, RN-279-api de 1_Requisitos/15_modulos.md
// (modulos.feature). Contrato: usuarios-contrato-api v14, GET
// /usuarios/card/ao-vivo por SSE (eventos "card" e "encerrado", ": ping").
//
// A conexão fica aberta pela suíte (suporte/AoVivo.cs) e é fechada no fim do
// cenário. Os tempos do requisito (intervalo, ping de 30 s) são esperados de
// verdade, com uma folga; a mudança da versao é um participante novo criado
// pela conta master.
[Binding]
public sealed class AoVivoPassos(Mundo mundo, AoVivo aoVivo)
{
    const string Caminho = "/usuarios/card/ao-vivo";
    static readonly TimeSpan Folga = TimeSpan.FromSeconds(5);
    static readonly TimeSpan Abertura = TimeSpan.FromSeconds(10);

    readonly Dictionary<string, Fluxo> conexoes = [];
    DateTimeOffset retirada;
    int mudancas;

    // ── RN-281 · quem não conecta ──────────────────────────────────────────
    [When(@"^(o participante Lucas Andrade|um staff sem o Usuários) envia GET (/usuarios/card/ao-vivo\S*)$")]
    public async Task SemOCardAoVivo(string quem, string caminho)
    {
        var pessoa = quem == "um staff sem o Usuários" ? await mundo.StaffSemUsuarios() : await mundo.Lucas();
        await AbrirComo(pessoa, caminho, quem);
    }

    // a recusa chega como JSON, antes de abrir o fluxo, e o corpo termina
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) e nenhuma conexão fica aberta$")]
    public async Task RecusadoSemConexao(int status, string codigo)
    {
        var fluxo = aoVivo.Ultimo;
        Assert.True(await fluxo.EsperarFim(Folga), $"a conexão ficou aberta: {fluxo}");
        Recusa(fluxo, status, codigo);
    }

    // ── RN-281 · sessão termina com a conexão aberta ───────────────────────
    [Given(@"^a staff Maria conectada por GET (/usuarios/card/ao-vivo\?intervalo=\d+), no navegador$")]
    public async Task MariaConectada(string caminho)
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "U" });
        await Conectada(maria, caminho, "Maria");
    }

    // as 8 h sem uso são a chave sessao:<sid> que expira no Redis (RNF-217):
    // o Quando a apaga, o que o backend vê quando o tempo passa. A duração em
    // si não muda o que se confere
    [Given(@"^a sessão dela de 8 h no navegador$")]
    public static void SessaoDeOitoHoras() { }

    [When(@"^a sessão expira por 8 h sem uso, com a conexão ao vivo aberta$")]
    public async Task SessaoExpira() => await Estado.ExpirarSessao((await mundo.SessaoDe(await mundo.Maria(), Navegador)).Sid);

    [Then(@"^ela recebe o evento ""encerrado"" com o código ""(\w+)"" e a conexão fecha$")]
    public async Task Encerrada(string codigo)
    {
        var fluxo = conexoes["Maria"];
        await Encerrado(fluxo, codigo, TimeSpan.FromSeconds(5) + Folga);
        Assert.True(await fluxo.EsperarFim(Folga), $"depois do \"encerrado\", a conexão devia fechar: {fluxo}");
    }

    // ── RN-281 · Usuários retirado com a conexão aberta ────────────────────
    [Given(@"^a staff Maria conectada ao vivo com ""A cada (\d+) s"" e ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task MariaConectadaCom(int intervalo, string mod, string abas)
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { [mod] = abas });
        await Conectada(maria, $"{Caminho}?intervalo={intervalo}", "Maria");
    }

    [When(@"^a conta master retira dela o ""(MOD-\d+)""$")]
    public async Task RetiraOModulo(string mod)
    {
        var maria = await mundo.Maria();
        var mods = (await mundo.Permissoes(maria))["mods"]!.AsObject()
            .Where(m => m.Key != mod).ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());
        await mundo.Definir(maria, mods);
        retirada = DateTimeOffset.UtcNow;
    }

    [Then(@"^em até (\d+) s ela recebe ""encerrado"" com o código ""(\w+)""$")]
    public async Task EncerradaEmAte(int segundos, string codigo)
    {
        var limite = TimeSpan.FromSeconds(segundos);
        var evento = await Encerrado(conexoes["Maria"], codigo, limite - (DateTimeOffset.UtcNow - retirada) + Folga);
        Assert.True(evento.Quando - retirada <= limite + Folga,
            $"o \"encerrado\" devia chegar em até {segundos} s, chegou em {(evento.Quando - retirada).TotalSeconds:0.0} s");
    }

    [Then(@"^a conexão fecha$")]
    public async Task ConexaoFecha()
    {
        var fluxo = conexoes["Maria"];
        Assert.True(await fluxo.EsperarFim(Folga), $"a conexão devia fechar: {fluxo}");
    }

    // ── 15 RN-279-api · conectar com o intervalo ───────────────────────────
    [Given(@"^a conta master da EBFGA com o token de acesso$")]
    public async Task MasterComToken()
    {
        mundo.Sujeito = await mundo.Master();
        await mundo.TokenDe(mundo.Sujeito);
    }

    [When(@"^envia GET (/usuarios/card/ao-vivo\S*) com Accept ""text/event-stream""$")]
    public async Task SujeitoConecta(string caminho) => conexoes["conta master"] = await AbrirComo(mundo.Sujeito!, caminho, "conta master");

    [Then(@"^a resposta é 200 e a conexão fica aberta$")]
    public async Task Aberta() => await PrimeiroCard(conexoes["conta master"]);

    [Then(@"^o primeiro evento ""card"" traz o mesmo corpo de GET /usuarios/card, com id igual à versao$")]
    public async Task PrimeiroIgualAoGet()
    {
        var primeiro = await PrimeiroCard(conexoes["conta master"]);
        var card = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", "/usuarios/card"), 200, "GET /usuarios/card").Corpo;
        Comparacao.Confere(card, primeiro.Corpo, exato: true);
        Assert.True(primeiro.Id == card!["versao"]!.ToJsonString(), $"o id do evento ({primeiro.Id}) devia ser a versao: {card.ToJsonString()}");
    }

    [Then(@"^a cada (\d+) s chega um evento ""card"", se a versao mudou$")]
    public async Task CardACada(int segundos) => await CardDepoisDaMudanca(conexoes["conta master"], segundos);

    [Then(@"^um "": ping"" chega a cada (\d+) s, qualquer que seja o intervalo, para manter a conexão$")]
    public async Task PingACada(int segundos) => await Ping(conexoes["conta master"], segundos);

    // ── 15 RN-279-api · intervalo pedido ───────────────────────────────────
    [When(@"^a conta master envia GET /usuarios/card/ao-vivo (\?intervalo=\d+|sem o parâmetro)$")]
    public async Task MasterConecta(string pedido) =>
        conexoes["conta master"] = await AbrirComo(await mundo.Master(), pedido.StartsWith('?') ? Caminho + pedido : Caminho, "conta master");

    [Then(@"^é 200, com um ""card"" a cada (\d+) s, se a versao mudou$")]
    public async Task CardNoIntervalo(int segundos)
    {
        await PrimeiroCard(conexoes["conta master"]);
        await CardDepoisDaMudanca(conexoes["conta master"], segundos);
    }

    [Then(@"^é 200, com um ""card"" a cada (\d+) s e um "": ping"" a cada (\d+) s$")]
    public async Task CardEPing(int card, int ping) => await SoOPingAntesDoIntervalo(card, ping);

    [Then(@"^é 200, como \?intervalo=(\d+), o padrão$")]
    public async Task ComoOPadrao(int intervalo) => await SoOPingAntesDoIntervalo(intervalo, 30);

    [Then(@"^é (\d{3}) ([A-Z_]+) com o campo ""(\w+)""$")]
    public async Task IntervaloRecusado(int status, string codigo, string campo)
    {
        var fluxo = conexoes["conta master"];
        Assert.True(await fluxo.EsperarFim(Folga), $"a conexão ficou aberta: {fluxo}");
        Recusa(fluxo, status, codigo);
        Assert.True(fluxo.Corpo?["campo"]?.GetValue<string>() == campo, $"esperava o campo \"{campo}\": {fluxo}");
    }

    // ── 15 RN-279-api · quarta conexão do mesmo usuário ────────────────────
    // três sessões da conta master (o app no celular e no tablet, o navegador
    // no computador), cada uma com a sua conexão, abertas nessa ordem
    [Given(@"^a conta master com 3 conexões ao vivo abertas: celular, tablet e computador$")]
    public async Task TresConexoes()
    {
        var master = await mundo.Master();
        foreach (var (aparelho, nome) in new[] { (App, "celular"), (App, "tablet"), (Navegador, "computador") })
        {
            var sessao = await mundo.Entrar(master, aparelho);
            var fluxo = await aoVivo.Abrir(nome, Caminho, sessao.Acesso);
            await PrimeiroCard(fluxo, preparo: true);
            conexoes[nome] = fluxo;
        }
    }

    [When(@"^ela envia GET /usuarios/card/ao-vivo de um segundo computador$")]
    public async Task QuartaConexao()
    {
        var sessao = await mundo.Entrar(await mundo.Master(), Navegador);
        conexoes["segundo computador"] = await aoVivo.Abrir("segundo computador", Caminho, sessao.Acesso);
    }

    [Then(@"^a 4ª conexão recebe 200 e fica aberta$")]
    public async Task QuartaAberta() => await PrimeiroCard(conexoes["segundo computador"]);

    [Then(@"^a mais antiga recebe o evento ""encerrado"" com o código ""(\w+)"" e fecha$")]
    public async Task MaisAntigaSai(string codigo)
    {
        var antiga = conexoes["celular"];
        await Encerrado(antiga, codigo, Abertura);
        Assert.True(await antiga.EsperarFim(Folga), $"a mais antiga devia fechar: {antiga}");
        foreach (var nome in new[] { "tablet", "computador", "segundo computador" })
        {
            Assert.False(conexoes[nome].Fechado, $"só a mais antiga sai; {nome} fechou: {conexoes[nome]}");
        }
    }

    // ── apoio ───────────────────────────────────────────────────────────────
    // abre com a sessão de quem pede; token de permissões antigas (o Conceder
    // do preparo) é renovado uma vez, como o hub faz (RNF-224)
    async Task<Fluxo> AbrirComo(Pessoa pessoa, string caminho, string nome)
    {
        var sessao = await mundo.SessaoParaPedido(pessoa);
        var fluxo = await aoVivo.Abrir(nome, caminho, sessao.Acesso);
        if (fluxo.Status == 401 && await fluxo.EsperarFim(Folga) && fluxo.Corpo?["codigo"]?.GetValue<string>() == "PERMISSOES_DESATUALIZADAS")
        {
            Exigir(await mundo.RenovarEGuardar(sessao), 200, $"POST /auth/renovar de {pessoa.Nome}");
            fluxo = await aoVivo.Abrir(nome, caminho, sessao.Acesso);
        }
        return fluxo;
    }

    async Task Conectada(Pessoa pessoa, string caminho, string nome)
    {
        var fluxo = await AbrirComo(pessoa, caminho, nome);
        await PrimeiroCard(fluxo, preparo: true);
        conexoes[nome] = fluxo;
    }

    // 200 em text/event-stream, o primeiro "card" logo ao abrir e a conexão aberta
    static async Task<Evento> PrimeiroCard(Fluxo fluxo, bool preparo = false)
    {
        var evento = fluxo.Status == 200 ? await fluxo.EsperarTipo("card", Abertura) : null;
        if (preparo && evento is null) throw new InvalidOperationException($"preparo: GET {Caminho} não abriu: {fluxo}");
        Assert.True(fluxo.Status == 200 && fluxo.TipoDoConteudo == "text/event-stream", $"esperava 200 em text/event-stream: {fluxo}");
        Assert.True(evento is not null, $"o primeiro \"card\" devia chegar logo ao abrir: {fluxo}");
        Assert.False(fluxo.Fechado, $"a conexão devia ficar aberta: {fluxo}");
        return evento;
    }

    // a versao muda (um participante novo) e um "card" novo chega até o fim do
    // intervalo; nenhum "card" repete a versao
    async Task CardDepoisDaMudanca(Fluxo fluxo, int segundos)
    {
        var primeiro = await PrimeiroCard(fluxo);
        var desde = fluxo.Eventos.Count;
        await MudarAVersao();
        var mudanca = DateTimeOffset.UtcNow;
        var novo = await fluxo.Esperar(e => e.Tipo == "card" && e.Id != primeiro.Id, TimeSpan.FromSeconds(segundos) + Folga, desde);
        Assert.True(novo is not null, $"depois da mudança, um \"card\" novo devia chegar em até {segundos} s: {fluxo}");
        Assert.True(CardPassos.Versao(novo.Corpo!) > CardPassos.Versao(primeiro.Corpo!), $"o \"card\" novo devia trazer a versao nova: {novo}");
        Assert.True(novo.Quando - mudanca <= TimeSpan.FromSeconds(segundos) + Folga, $"o \"card\" chegou {(novo.Quando - mudanca).TotalSeconds:0.0} s depois da mudança");
        var ids = fluxo.Eventos.Where(e => e.Tipo == "card").Select(e => e.Id).ToList();
        Assert.True(ids.Distinct().Count() == ids.Count, $"um \"card\" só chega se a versao mudou: {fluxo}");
    }

    // o ": ping" chega em até o seu intervalo depois de abrir
    static async Task<Evento> Ping(Fluxo fluxo, int segundos)
    {
        var ping = await fluxo.EsperarTipo("ping", TimeSpan.FromSeconds(segundos) + Folga - (DateTimeOffset.UtcNow - fluxo.Aberto));
        Assert.True(ping is not null && ping.Quando - fluxo.Aberto <= TimeSpan.FromSeconds(segundos) + Folga,
            $"um \": ping\" devia chegar em até {segundos} s: {fluxo}");
        return ping;
    }

    // intervalo longo (300 s): a versao muda logo depois de abrir, o ": ping"
    // chega em 30 s e nenhum "card" novo vem antes do fim do intervalo
    async Task SoOPingAntesDoIntervalo(int intervalo, int ping)
    {
        var fluxo = conexoes["conta master"];
        var primeiro = await PrimeiroCard(fluxo);
        await MudarAVersao();
        var chegou = await Ping(fluxo, ping);
        Assert.True(chegou.Quando - fluxo.Aberto < TimeSpan.FromSeconds(intervalo), "o ping devia vir antes do fim do intervalo");
        Assert.False(fluxo.Eventos.Any(e => e.Tipo == "card" && e.Id != primeiro.Id),
            $"com intervalo de {intervalo} s, nenhum \"card\" novo devia chegar nos primeiros {ping} s: {fluxo}");
        Assert.False(fluxo.Fechado, $"a conexão devia ficar aberta: {fluxo}");
    }

    async Task MudarAVersao() =>
        Exigir(await mundo.EnviarComo(await mundo.Master(), "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "PARTICIPANTE",
            ["nome"] = $"Participante vivo{Letras(mudancas)}",
            ["nomeCurto"] = $"vivo{Letras(mudancas++)}",
            ["matricula"] = null,
            ["responsaveis"] = new JsonArray(),
        }.ToJsonString()), 201, "POST /usuarios (mudança da versao)");

    static async Task<Evento> Encerrado(Fluxo fluxo, string codigo, TimeSpan limite)
    {
        var evento = await fluxo.EsperarTipo("encerrado", limite);
        Assert.True(evento is not null, $"esperava o evento \"encerrado\" com {codigo}: {fluxo}");
        Assert.True(evento.Corpo?["codigo"]?.GetValue<string>() == codigo, $"esperava o código {codigo}: {evento}");
        return evento;
    }

    static void Recusa(Fluxo fluxo, int status, string codigo)
    {
        Assert.True(fluxo.Status == status && fluxo.TipoDoConteudo != "text/event-stream", $"esperava {status} sem abrir o fluxo: {fluxo}");
        Assert.True(fluxo.Corpo?["codigo"]?.GetValue<string>() == codigo, $"esperava o código {codigo}: {fluxo}");
    }
}
