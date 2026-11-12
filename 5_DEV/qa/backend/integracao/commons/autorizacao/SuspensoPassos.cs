using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Commons.Autorizacao;

// Módulo suspenso (17-b RN-285, RN-286, RN-287): os passos ficam na mesma
// classe de AutorizacaoPassos.cs, para usar Mods e ModsDe.
//
// O estado "Presença suspensa" vem do banco (Estado.Suspender), com os mods
// que a linha seguinte do Dado diz ("a Maria com … em mods"). Descontratar e
// contratar de novo passam por Mundo.ContratacaoMudou (POST /_teste/contratacao,
// contrato v11); o pedido da Presença é GET /_teste/modulo/MOD-06 (sequência
// v07, passos 64 e 73). O que só o documento mostra ("suspensos") é lido por
// PermissoesGuardadas (quarta exceção do README, seção 6).
public sealed partial class AutorizacaoPassos
{
    // o pedido da Presença, que não tem endereço próprio (contrato v11)
    const string PedidoDaPresencaDeTeste = "/_teste/modulo/MOD-06";

    // quem está com a Presença suspensa no Dado (RN-286, "só a contratação reativa")
    readonly List<(Pessoa Pessoa, string Abas)> comAPresenca = [];

    // a versão da Presença suspensa, para a linha "em mods" a manter
    int versaoSuspensa;

    // quem descontratou na execução do Catálogo (RN-286)
    Pessoa? quemMudou;

    // registro do histórico que o Então conferiu por último ("essa versão")
    JsonObject? registro;

    // ── preparo ─────────────────────────────────────────────────────────────
    // "a Maria com ""MOD-03"" com ""CU"", ""MOD-05"" com ""U"" e ""MOD-06"" com ""U"", na versão 7"
    [Given(@"^a Maria com (""MOD-.+), na versão(?: de permissões)? (\d+)$")]
    public async Task MariaComNaVersao(string permissoes, int versao) => await MariaCom(permissoes, versao);

    [Given(@"^a Maria com (""MOD-.+), na versão de permissões (\d+), com um token de pv (\d+)$")]
    public async Task MariaComToken(string permissoes, int versao, int pv)
    {
        var maria = await MariaCom(permissoes, versao);
        mundo.Atual = await mundo.Entrar(maria, Navegador);
        mundo.TokenGuardado = mundo.Atual.Acesso;
        Assert.Equal(pv, Jwt.Carga(mundo.TokenGuardado)["pv"]?.GetValue<int>());
    }

    async Task<Pessoa> MariaCom(string permissoes, int versao)
    {
        var maria = await mundo.Maria();
        var mods = Mods(permissoes);
        await mundo.Conceder(maria, mods);
        await Estado.DefinirVersao(maria.Id, versao);
        mundo.Sujeito = maria;
        return maria;
    }

    [Given(@"^a Presença da Maria suspensa na versão (\d+)$")]
    public Task SuspensaNaVersao(int versao) => PresencaSuspensa(versao);

    [Given(@"^a Presença da Maria suspensa$")]
    public Task Suspensa() => PresencaSuspensa(8);

    // só a Presença suspensa, sem contratação em vigor; os mods vêm da linha
    // seguinte (MaisEmMods)
    async Task PresencaSuspensa(int versao)
    {
        var maria = await mundo.Maria();
        versaoSuspensa = versao;
        await mundo.Descontratar("MOD-06");
        await SuspensaComSessao(maria);
        comAPresenca.Add((maria, "U"));
        mundo.Sujeito = maria;
    }

    // "E a Maria com ""MOD-03"" com ""CU"" e ""MOD-05"" com ""U"" em mods": o PUT do
    // preparo sobe a versão; a suspensão é gravada de novo na versão do Dado, e
    // a sessão aberta depois, com o token de pv igual a ela ("o token de pv 8")
    [Given(@"^a Maria com (""MOD-.+) em mods$")]
    public async Task MaisEmMods(string permissoes)
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, Mods(permissoes));
        await SuspensaComSessao(maria);
    }

    async Task SuspensaComSessao(Pessoa maria)
    {
        await Estado.Suspender(maria.Id, "MOD-06", "U", versaoSuspensa);
        mundo.Atual = await mundo.Entrar(maria, Navegador);
        mundo.TokenGuardado = mundo.Atual.Acesso;
    }

    [Given(@"^a Maria com ""(MOD-\d+)"" com ""(\w+)"", na versão (\d+), e o João com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task MariaEJoao(string modMaria, string abasMaria, int versao, string modJoao, string abasJoao)
    {
        var joao = await mundo.Joao();
        await mundo.Conceder(joao, new() { [modJoao] = abasJoao });
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { [modMaria] = abasMaria });
        await Estado.DefinirVersao(maria.Id, versao);
    }

    // o Caio é quem descontrata; o aviso é o Quando seguinte
    [Given(@"^a execução do Catálogo em que o membro do time Caio descontrata a Presença da EBFGA$")]
    public async Task CaioDescontrata() => quemMudou = await mundo.Caio();

    [When(@"^o Catálogo avisa que a contratação da EBFGA mudou$")]
    public async Task CatalogoAvisa() => await mundo.ContratacaoMudou("MOD-06", contratada: false, por: quemMudou);

    [Given(@"^a conta master tendo desmarcado a Presença da Maria, com a Presença contratada$")]
    public async Task MasterDesmarcou()
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "CU", ["MOD-06"] = "U" });
        var mods = await ModsDe(maria);
        mods.Remove("MOD-06");
        await mundo.Definir(maria, mods);
    }

    // ── a contratação muda ─────────────────────────────────────────────────
    [When(@"^a escola contrata a Presença de novo$")]
    [When(@"^o Catálogo avisa que a EBFGA contratou a Presença de novo$")]
    public Task Recontrata() => mundo.ContratacaoMudou("MOD-06", contratada: true);

    [When(@"^a escola descontrata a Presença e depois a contrata de novo$")]
    public async Task DescontrataERecontrata()
    {
        await mundo.ContratacaoMudou("MOD-06", contratada: false);
        await mundo.ContratacaoMudou("MOD-06", contratada: true);
    }

    // ── pedidos ─────────────────────────────────────────────────────────────
    // o token guardado no Dado (pv antigo); depois da renovação, o da sessão
    [When(@"^a Maria envia um pedido da Presença com o token de pv \d+$")]
    public Task PedidoDaPresenca() => mundo.Enviar("GET", PedidoDaPresencaDeTeste, token: mundo.TokenGuardado);

    [Then(@"^o mesmo pedido da Presença com o token de pv \d+ recebe 403 ACESSO_NEGADO$")]
    public async Task PresencaNegada() =>
        Respostas.Confere(await mundo.Enviar("GET", PedidoDaPresencaDeTeste, token: mundo.Atual!.Acesso), 403, "ACESSO_NEGADO");

    [When(@"^a conta master envia PUT (/usuarios/\S+/permissoes) com If-Match ""(\d+)"" e:$")]
    public async Task MasterSalvaNaVersao(string caminho, string versao, string json) =>
        await mundo.EnviarComo(await mundo.Master(), "PUT", await mundo.Traduzir(caminho), await mundo.Traduzir(json), versao);

    // ── token ───────────────────────────────────────────────────────────────
    // a sessão continua: a renovação do passo seguinte responde 200
    [Then(@"^a resposta é 401 PERMISSOES_DESATUALIZADAS, sem encerrar a sessão$")]
    public void Desatualizadas() => Respostas.Confere(mundo.Ultima, 401, "PERMISSOES_DESATUALIZADAS");

    [Then(@"^o token de pv \d+ recebe 401 PERMISSOES_DESATUALIZADAS no próximo pedido$")]
    public async Task TokenAntigoRecusado() =>
        Respostas.Confere(await mundo.Enviar("GET", "/auth/sessoes", token: mundo.TokenGuardado), 401, "PERMISSOES_DESATUALIZADAS");

    [Then(@"^POST /auth/renovar devolve um token de pv (\d+), sem ""(MOD-\d+)"" em mods$")]
    public async Task RenovaSem(int pv, string mod)
    {
        var mods = await Renovado(pv);
        Assert.False(mods.ContainsKey(mod), $"o token renovado tem {mod}: {mods}");
    }

    [Then(@"^POST /auth/renovar devolve um token de pv (\d+) com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task RenovaCom(int pv, string mod, string abas) =>
        Assert.Equal(abas, (await Renovado(pv))[mod]?.GetValue<string>());

    async Task<JsonObject> Renovado(int pv)
    {
        Respostas.Confere(await mundo.RenovarEGuardar(mundo.Atual!), 200);
        var carga = Jwt.Carga(mundo.Atual!.Acesso);
        Assert.Equal(pv, carga["pv"]?.GetValue<int>());
        return carga["mods"]!.AsObject();
    }

    // ── permissões pela API ────────────────────────────────────────────────
    [Then(@"^sem ""(MOD-\d+)"" em mods nem em concediveis$")]
    public void ForaDeModsEConcediveis(string mod)
    {
        var corpo = mundo.Ultima.Corpo!;
        Assert.False(corpo["mods"]?.AsObject().ContainsKey(mod), $"mods traz {mod}: {corpo}");
        Assert.DoesNotContain(corpo["concediveis"]!.AsArray(), c => c?["mod"]?.GetValue<string>() == mod);
    }

    [Then(@"^GET (/usuarios/\S+/permissoes) traz a versão (\d+) com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task TrazAVersaoCom(string caminho, int versao, string mod, string abas)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho)), 200, caminho);
        Assert.Equal(versao, r.Corpo?["versao"]?.GetValue<int>());
        Assert.Equal(abas, r.Corpo?["mods"]?[mod]?.GetValue<string>());
    }

    [Then(@"^a resposta é 200 com a versão (\d+), sem ""(MOD-\d+)""$")]
    public void ComAVersaoSem(int versao, string mod)
    {
        ComAVersao(versao);
        Assert.False(mundo.Ultima.Corpo?["mods"]?.AsObject().ContainsKey(mod), $"a resposta traz {mod}: {mundo.Ultima}");
    }

    [Then(@"^GET (/usuarios/\S+/permissoes/historico) ganha a versão (\d+)$")]
    public async Task HistoricoGanha(string caminho, int versao)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho)), 200, caminho);
        registro = r.Corpo!.AsArray().FirstOrDefault(x => x?["versao"]?.GetValue<int>() == versao)?.AsObject();
        Assert.True(registro is not null, $"o histórico não tem a versão {versao}: {r}");
    }

    [Then(@"^essa versão vem por ""([\w-]+)"", com ""(MOD-\d+)"" de ""(\w+)"" para null$")]
    public async Task VersaoPor(string por, string mod, string antes)
    {
        Assert.Equal(await mundo.Traduzir(por), registro!["por"]?.GetValue<string>());
        Assert.Equal(antes, registro["antes"]?[mod]?.GetValue<string>());
        Assert.True(registro["depois"] is JsonObject depois && depois.ContainsKey(mod) && depois[mod] is null,
            $"\"depois\" devia ter {mod} null: {registro}");
    }

    // ── o que fica guardado ────────────────────────────────────────────────
    [Then(@"^""(MOD-\d+)"" fica suspenso com ""(\w+)"" para a Maria e para o João$")]
    public async Task SuspensoParaOsDois(string mod, string abas)
    {
        foreach (var pessoa in new[] { await mundo.Maria(), await mundo.Joao() })
        {
            Assert.DoesNotContain(mod, (await ModsDe(pessoa)).Keys);
            Assert.Equal(abas, await PermissoesGuardadas.AbasSuspensas(pessoa.Id, mod));
        }
    }

    [Then(@"^""(MOD-\d+)"" volta a mods com ""(\w+)"" em cada usuário da EBFGA que o tinha suspenso$")]
    public async Task VoltaEmCada(string mod, string abas)
    {
        Assert.NotEmpty(comAPresenca);
        foreach (var (pessoa, _) in comAPresenca)
        {
            Assert.Equal(abas, (await ModsDe(pessoa)).GetValueOrDefault(mod));
            Assert.Null(await PermissoesGuardadas.AbasSuspensas(pessoa.Id, mod));
        }
    }

    [Then(@"^as permissões da Maria continuam na versão (\d+), com a Presença suspensa$")]
    public async Task ContinuaSuspensa(int versao)
    {
        var maria = await mundo.Maria();
        var lidas = await mundo.Permissoes(maria);
        Assert.Equal(versao, lidas["versao"]?.GetValue<int>());
        Assert.False(lidas["mods"]?.AsObject().ContainsKey("MOD-06"), $"mods traz MOD-06: {lidas}");
        Assert.Equal("U", await PermissoesGuardadas.AbasSuspensas(maria.Id, "MOD-06"));
    }

    // mods e suspensos exatos; o documento pode ter outros campos
    [Then(@"^as permissões guardadas da Maria ficam:$")]
    public async Task GuardadasFicam(string json)
    {
        var esperado = JsonNode.Parse(json)!.AsObject();
        var guardado = await PermissoesGuardadas.Ler((await mundo.Maria()).Id);
        Comparacao.Confere(esperado, guardado);
        Comparacao.Confere(esperado["mods"], guardado["mods"], "$.mods", exato: true);
        Comparacao.Confere(esperado["suspensos"], guardado["suspensos"], "$.suspensos", exato: true);
    }
}
