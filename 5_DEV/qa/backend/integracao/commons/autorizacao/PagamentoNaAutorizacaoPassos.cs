using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Commons.Autorizacao;

// O Pagamento ("MOD-01") nos Cenários de API de
// 1_Requisitos/17_modulo-usuario-b-autorizacao.md (v35): só a conta master o
// concede, e só com a Utilização (RF-160, RF-161), e o token de cada papel
// (RN-233, "Pagamento no token"). O PUT recusado do Esquema de RF-161 é o de
// hub/pagamento/PagamentoPassos.cs (conta master e Rita) e o de
// hub/usuarios/UsuariosPassos.cs (administrador); a frase "as permissões de …
// não mudam" compara com a leitura que eles guardam em Mundo.PermissoesLidas.
public sealed partial class AutorizacaoPassos
{
    // ── RF-160 Pagamento só para a conta master conceder ───────────────────
    [Given(@"^a staff Rita sem permissões, na versão 1$")]
    public async Task RitaSemPermissoes()
    {
        var permissoes = await mundo.Permissoes(await mundo.Rita());
        Assert.True(permissoes["versao"]?.GetValue<int>() == 1 && permissoes["mods"] is JsonObject { Count: 0 },
            $"preparo: a Rita recém-criada devia estar sem permissões, na versão 1: {permissoes.ToJsonString()}");
    }

    [Then(@"^a resposta é 200 e ""concediveis"" traz o Pagamento só com a Utilização:$")]
    public void PagamentoConcedivel(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var itens = mundo.Ultima.Corpo?["concediveis"]?.AsArray() ?? [];
        var pagamento = itens.Where(i => i?["mod"]?.GetValue<string>() == "MOD-01").ToList();
        Assert.True(pagamento.Count == 1, $"esperava o \"MOD-01\" uma vez em \"concediveis\": {mundo.Ultima}");
        Comparacao.Confere(JsonNode.Parse(json), pagamento[0], exato: true);
    }

    // o João tem Administrar usuários e a Ana está abaixo dele (criada por ele)
    [Then(@"^o mesmo GET do João, para a Ana, não traz ""MOD-01"" em ""concediveis""$")]
    public async Task JoaoNaoConcedePagamento()
    {
        var ana = await mundo.Ana();
        var r = Exigir(await mundo.EnviarComo(await mundo.Joao(), "GET", $"/usuarios/{ana.Id}/permissoes"), 200, "GET /usuarios/{id}/permissoes da Ana, pelo João");
        var itens = r.Corpo?["concediveis"]?.AsArray() ?? [];
        Assert.True(itens.All(i => i?["mod"]?.GetValue<string>() != "MOD-01"), $"o João não concede o Pagamento: {r}");
    }

    // ── RF-161 Pagamento recusado ──────────────────────────────────────────
    [Then(@"^as permissões de ([\w-]+) não mudam$")]
    public async Task PermissoesNaoMudam(string apelido)
    {
        var id = await mundo.Traduzir(apelido);
        Assert.True(mundo.PermissoesLidas.TryGetValue(id, out var lida), $"o Quando não leu as permissões de {apelido} antes do PUT");
        var r = Exigir(await mundo.EnviarComo(lida.Por, "GET", $"/usuarios/{id}/permissoes"), 200, $"GET /usuarios/{apelido}/permissoes");
        Assert.True(r.Corpo?["versao"]?.GetValue<int>() == lida.Corpo["versao"]?.GetValue<int>() && JsonNode.DeepEquals(r.Corpo?["mods"], lida.Corpo["mods"]),
            $"as permissões de {apelido} mudaram: eram {lida.Corpo.ToJsonString()}, ficaram {r}");
    }

    // ── RN-233 Pagamento no token ──────────────────────────────────────────
    [When(@"^(o administrador do deboaz|a conta master da EBFGA|o autônomo Pedro Reis|a staff Rita, com Pagamento|a staff Maria, sem ele|o membro do time Caio) entra no hub$")]
    public async Task EntraParaVerOPagamento(string quem)
    {
        var pessoa = quem switch
        {
            "o administrador do deboaz" => mundo.Admin(),
            "a conta master da EBFGA" => await mundo.Master(),
            "o autônomo Pedro Reis" => await mundo.Pedro(),
            "a staff Rita, com Pagamento" => await RitaComPagamento(),
            "a staff Maria, sem ele" => await mundo.Maria(),
            _ => await mundo.Caio(),
        };
        mundo.Atual = await mundo.Entrar(pessoa, Navegador);
    }

    // a conta master concede o Pagamento à Rita, só com a Utilização
    async Task<Pessoa> RitaComPagamento()
    {
        var rita = await mundo.Rita();
        await mundo.Conceder(rita, new() { ["MOD-01"] = "U" });
        return rita;
    }

    [Then(@"^o token tem ""MOD-01"" (?:só )?com ""U""$")]
    public void TemOPagamento() =>
        Assert.True(Carga()["mods"]?["MOD-01"]?.GetValue<string>() == "U", $"esperava \"MOD-01\" com \"U\" no token: {Carga()["mods"]?.ToJsonString()}");

    // o administrador: o "MOD-01" abre o card do Pagamento
    [Then(@"^o token tem ""MOD-01"" só com ""U"", para o card$")]
    public async Task PagamentoParaOCard()
    {
        TemOPagamento();
        Respostas.Confere(await mundo.EnviarComo(mundo.Admin(), "GET", "/pagamento/card"), 200);
    }

    [Then(@"^o token não tem ""MOD-01""$")]
    public void SemOPagamento() =>
        Assert.False(Carga()["mods"]?.AsObject().ContainsKey("MOD-01") ?? false, $"o token não podia ter \"MOD-01\": {Carga()["mods"]?.ToJsonString()}");
}
