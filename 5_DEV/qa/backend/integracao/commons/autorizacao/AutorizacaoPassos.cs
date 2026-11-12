using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Commons.Autorizacao;

// Passos dos Cenários de API de 1_Requisitos/17_modulo-usuario-b-autorizacao.md.
// Como os passos casam com as frases: comentário no topo de AutenticacaoPassos.cs
// e README. Os passos de resposta ("a resposta é 403 ACESSO_NEGADO") e os
// de sessão já existentes (Autenticação) valem aqui também.
//
// Permissões são dadas pela API (PUT /usuarios/{id}/permissoes, Mundo.Conceder).
// O que a API não cria vem do banco (Estado): o módulo contratado pela escola
// (o catálogo ainda não tem o endereço) e a versão das permissões dos exemplos
// ("na versão 6"). O "Sujeito" é de quem o Dado fala: "Dado o João …" e depois
// "Quando envia …" é o João quem envia.
[Binding]
public sealed partial class AutorizacaoPassos(Mundo mundo, Execucoes execucoes)
{
    const string SemSala = "abrir e fechar uma sala não está no contrato da Autenticação";
    const string SemEndereco = "o contrato ainda não tem endereço deste módulo";

    // linha do Esquema de RN-233, para o Então saber o que conferir
    string quemEntrou = "";

    // ── quem tem o quê ─────────────────────────────────────────────────────
    // "a Maria com Administrar usuários e a Utilização da Sugestão e do Quiz"
    [Given(@"^(a Maria|o staff João Lima|o João|o membro do time Caio Lima) (?:só )?com (Administrar usuários.*|a Utilização.*)$")]
    // "a Maria com ""MOD-03"" só com ""C""", "a Ana com ""MOD-05"" com ""U"" concedida pelo João"
    // (", na versão 7" e "em mods" no fim são de SuspensoPassos.cs)
    [Given(@"^(a Maria|o professor João|o João|a Ana|o Paulo)(?:, da escola ""EBFGA"",)? com (""MOD-(?:(?!, na versão).)+)(?<! em mods)$")]
    public async Task ComPermissoes(string quem, string permissoes)
    {
        var pessoa = await Pessoa(quem);
        var por = permissoes.EndsWith("concedida pelo João") ? await PodeConceder(await mundo.Joao(), Mods(permissoes)) : null;
        await mundo.Conceder(pessoa, Mods(permissoes), por);
        mundo.Sujeito = pessoa;
    }

    [Given(@"^o token da Maria sem ""MOD-03""$")]
    public async Task MariaSemUsuarios()
    {
        mundo.Sujeito = await mundo.Maria();
        await mundo.SessaoDe(mundo.Sujeito, Navegador);
    }

    [Given(@"^o token da Maria com o papel ""STAFF"" e ""MOD-05"" com ""U""$")]
    public async Task MariaComQuiz()
    {
        mundo.Sujeito = await mundo.Maria();
        await mundo.Conceder(mundo.Sujeito, new() { ["MOD-05"] = "U" });
    }

    [Given(@"^a escola sem a Presença contratada$")]
    public Task SemPresenca() => mundo.Descontratar("MOD-06");

    [Given(@"^a escola com a Presença e a Carteirinha contratadas$")]
    public async Task PresencaECarteirinha()
    {
        await mundo.Contratar("MOD-06");
        await mundo.Contratar("MOD-07");
    }

    // o João tinha a Presença (e o Quiz) para conceder; a conta master a tira
    [Given(@"^a conta master tendo tirado a Presença (?:do João|dele há pouco)$")]
    public async Task TiraAPresencaDoJoao()
    {
        var joao = await mundo.Joao();
        await mundo.Ana();
        await mundo.Conceder(joao, new() { ["MOD-03"] = "CU", ["MOD-05"] = "U", ["MOD-06"] = "U" });
        var mods = await ModsDe(joao);
        mods.Remove("MOD-06");
        await mundo.Definir(joao, mods);
        mundo.Sujeito = joao;
    }

    [Given(@"^o João com um token que ainda tem ""MOD-06""$")]
    public async Task JoaoComPresenca()
    {
        var joao = await mundo.Joao();
        await mundo.Ana();
        await mundo.Conceder(joao, new() { ["MOD-03"] = "CU", ["MOD-06"] = "U" });
        mundo.Atual = await mundo.Entrar(joao, Navegador);
        mundo.Sujeito = joao;
    }

    // ── versões ─────────────────────────────────────────────────────────────
    [Given(@"^as permissões da Maria lidas na versão (\d+) pela conta master e pelo João$")]
    public async Task LidasPelosDois(int versao)
    {
        // para o João poder salvar, a Maria fica abaixo dele: criada por ele
        var joao = await mundo.Joao();
        await mundo.Conceder(joao, new() { ["MOD-03"] = "CU" });
        var maria = await mundo.Maria(criador: joao);
        await Estado.DefinirVersao(maria.Id, versao);
        mundo.Sujeito = joao;
    }

    [Given(@"^a conta master tendo salvado (?:antes|as permissões dela), passando à versão (\d+)$")]
    public async Task MasterSalvou(int versao)
    {
        await mundo.Conceder(await mundo.Maria(), new() { ["MOD-08"] = "C" });
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(versao, mundo.Ultima.Corpo?["versao"]?.GetValue<int>());
    }

    [Given(@"^a Maria com um token de pv (\d+)$")]
    public async Task TokenDePv(int pv)
    {
        var maria = await mundo.Maria();
        await Estado.DefinirVersao(maria.Id, pv);
        mundo.Atual = await mundo.Entrar(maria, Navegador);
        mundo.TokenGuardado = mundo.Atual.Acesso;
        mundo.Sujeito = maria;
    }

    [Given(@"^a Maria com um token de versão de permissões antiga$")]
    public async Task TokenAntigo()
    {
        var maria = await mundo.Maria();
        mundo.Atual = await mundo.Entrar(maria, Navegador);
        mundo.TokenGuardado = mundo.Atual.Acesso;
        await mundo.Conceder(maria, new() { ["MOD-08"] = "C" });
    }

    [Given(@"^a Maria na versão de permissões (\d+), com sessões no navegador e no app$")]
    public async Task MariaNaVersao(int versao)
    {
        var maria = await mundo.Maria();
        await Estado.DefinirVersao(maria.Id, versao);
        await mundo.SessaoDe(maria, Navegador);
        await mundo.SessaoDe(maria, App);
    }

    [Given(@"^as permissões da Maria na versão (\d+)$")]
    public async Task VersaoDaMaria(int versao) => await Estado.DefinirVersao((await mundo.Maria()).Id, versao);

    [Given(@"^a conta master tendo marcado a Presença da Maria em .+$")]
    public async Task MarcouAPresenca()
    {
        await mundo.Conceder(await mundo.Maria(), new() { ["MOD-06"] = "U" });
        mundo.Sujeito = await mundo.Master();
    }

    [Given(@"^a casca adulterada mostrando a aba Utilização de Usuários$")]
    public static void CascaAdulterada()
    {
        // o servidor não vê a casca: o pedido seguinte é o que importa
    }

    // ── entrar e pedir ─────────────────────────────────────────────────────
    // sem sujeito no Dado, é a Maria dos exemplos (17-a RN-272); auditado
    [When(@"^envia POST /auth/hub/entrar(?: com a entrada e a senha dela)?$")]
    public async Task SujeitoEntra()
    {
        var quem = mundo.Sujeito ?? await mundo.Maria();
        Sessao? sessao = null;
        await execucoes.Executar(async () =>
        {
            sessao = await mundo.Entrar(quem, Navegador);
            return mundo.Ultima;
        });
        mundo.Atual = sessao;
    }

    [When(@"^envia POST /usuarios para criar um staff$")]
    public Task CriaStaff() => mundo.EnviarComo(mundo.Sujeito!, "POST", "/usuarios", new JsonObject
    {
        ["tipo"] = "STAFF",
        ["nome"] = "Teste Recusado",
        ["nomeCurto"] = "recusado",
        ["matricula"] = null,
    }.ToJsonString());

    [When(@"^a Carla, mãe do Lucas, envia POST /auth/quest/entrar com o código dele$")]
    public async Task CarlaEntra() =>
        mundo.Atual = await mundo.EntrarNoQuest(App, new JsonObject { ["tipo"] = "RESPONSAVEL", ["parentesco"] = "Mãe" });

    [When(@"^envia GET (\S+)$")]
    [When(@"^envia GET (\S+), de um usuário da escola ""COLSA""$")]
    // RN-252: abaixo é quem a pessoa criou (a Ana pelo João; o Rui pela conta master)
    [When(@"^envia GET (\S+), de (?:a Ana, que ele criou|o Rui, criado pela conta master como ele)$")]
    public async Task SujeitoPede(string caminho) => await mundo.EnviarComo(await mundo.SujeitoOu(caminho), "GET", await mundo.Traduzir(caminho));

    // os outros GET da Maria são da auditoria (hub/usuarios/AuditoriaPassos.cs)
    [When(@"^a Maria envia GET (/usuarios/\S+/permissoes/historico)$")]
    public async Task MariaPede(string caminho) => await mundo.EnviarComo(await mundo.Maria(), "GET", await mundo.Traduzir(caminho));

    [When(@"^o João envia GET (\S+), de um par dele$")]
    public async Task JoaoPede(string caminho) => await mundo.EnviarComo(await mundo.Joao(), "GET", await mundo.Traduzir(caminho));

    [When(@"^o João envia PUT (\S+) com If-Match ""(\d+)"" e:$")]
    public async Task JoaoSalvaCorpo(string caminho, string versao, string json) =>
        await mundo.EnviarComo(await mundo.Joao(), "PUT", await mundo.Traduzir(caminho), await mundo.Traduzir(json), versao);

    // salva as mesmas permissões que leu, na versão que leu
    [When(@"^o João envia PUT (\S+) com If-Match ""(\d+)""$")]
    public async Task JoaoSalva(string caminho, string versao)
    {
        var mods = new JsonObject { ["mods"] = new JsonObject((await ModsDe(await mundo.Maria())).Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value))) };
        await mundo.EnviarComo(await mundo.Joao(), "PUT", await mundo.Traduzir(caminho), mods.ToJsonString(), versao);
    }

    // o token do João ainda tem a Presença; a renovação (EnviarComo) traz o token atual
    [When(@"^envia PUT (/usuarios/\S+/permissoes) com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task SujeitoConcede(string caminho, string mod, string abas)
    {
        var ana = await mundo.Ana();
        var atuais = await mundo.Permissoes(ana);
        var mods = (JsonObject)atuais["mods"]!.DeepClone();
        mods[mod] = abas;
        await mundo.EnviarComo(mundo.Sujeito!, "PUT", await mundo.Traduzir(caminho),
            new JsonObject { ["mods"] = mods }.ToJsonString(), atuais["versao"]!.GetValue<int>().ToString());
    }

    [When(@"^o membro do time Caio, com ""MOD-03"" com ""U"", envia GET (\S+)$")]
    public async Task CaioPede(string caminho)
    {
        var caio = await mundo.Caio();
        await mundo.Conceder(caio, new() { ["MOD-03"] = "U" });
        await mundo.EnviarComo(caio, "GET", await mundo.Traduzir(caminho));
    }

    // papel de cada pessoa (RN-232)
    [When(@"^(o administrador do deboaz|o membro do time Caio Lima|a conta master da EBFGA|o educador autônomo Pedro Reis|a staff Maria Souza|o participante Lucas Andrade|a Carla, mãe do Lucas|uma pessoa sem código de acesso) envia POST (\S+)$")]
    public async Task EntraParaVerOPapel(string quem, string _)
    {
        mundo.Atual = quem switch
        {
            "o administrador do deboaz" => await mundo.Entrar(mundo.Admin(), Navegador),
            "o membro do time Caio Lima" => await mundo.Entrar(await mundo.Caio(), Navegador),
            "a conta master da EBFGA" => await mundo.Entrar(await mundo.Master(), Navegador),
            "o educador autônomo Pedro Reis" => await mundo.Entrar(await mundo.Pedro(), Navegador),
            "a staff Maria Souza" => await mundo.Entrar(await mundo.Maria(), Navegador),
            // sem exigir 200: em Usuários (RN-175) a mesma frase espera 403 com a escola fora do ar
            "o participante Lucas Andrade" => mundo.Registrar(await mundo.Lucas(), App, await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" })),
            "a Carla, mãe do Lucas" => mundo.Registrar(await mundo.Lucas(), App, await mundo.TentarQuest(App, Carla())),
            _ => throw new PendingStepException(SemSala),
        };
    }

    // módulos de cada papel (RN-233): cada linha prepara o que a coluna "módulos" pede
    [When(@"^(administrador|membro do time|conta master|autônomo|staff|participante|responsável|anônimo) entra no (hub|quest)$")]
    public async Task EntraParaVerOsModulos(string quem, string _)
    {
        quemEntrou = quem;
        switch (quem)
        {
            case "administrador":
                mundo.Atual = await mundo.Entrar(mundo.Admin(), Navegador);
                break;
            case "membro do time":
                var caio = await mundo.Caio();
                await mundo.Conceder(caio, new() { ["MOD-02"] = "U" });
                mundo.Atual = await mundo.Entrar(caio, Navegador);
                break;
            case "conta master":
                await mundo.Contratar("MOD-05");
                mundo.Atual = await mundo.Entrar(await mundo.Master(), Navegador);
                break;
            case "autônomo":
                mundo.Atual = await mundo.Entrar(await mundo.Pedro(), Navegador);
                break;
            case "staff":
                var maria = await mundo.Maria();
                await mundo.Conceder(maria, new() { ["MOD-04"] = "U" });
                mundo.Atual = await mundo.Entrar(maria, Navegador);
                break;
            case "participante":
                mundo.Atual = await mundo.EntrarNoQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" });
                break;
            case "responsável":
                mundo.Atual = await mundo.EntrarNoQuest(App, new JsonObject { ["tipo"] = "RESPONSAVEL", ["parentesco"] = "Mãe" });
                break;
            default:
                throw new PendingStepException(SemSala);
        }
    }

    [When(@"^ela envia um pedido com o token de pv \d+$")]
    public Task PedidoComPvAntigo() => mundo.Enviar("GET", "/auth/sessoes", token: mundo.TokenGuardado);

    // POST /_teste/contratacao faz o papel do Catálogo (Mundo.ContratacaoMudou, RN-285)
    [When(@"^a escola descontrata a Presença$")]
    public Task Descontrata() => mundo.ContratacaoMudou("MOD-06", contratada: false);

    [When(@"^a conta master salva para a Maria (Configuração|Utilização|Configuração e Utilização) do Quiz$")]
    public async Task SalvaOQuiz(string abas) =>
        await mundo.Definir(await mundo.Maria(), new() { ["MOD-05"] = abas switch { "Configuração" => "C", "Utilização" => "U", _ => "CU" } });

    [When(@"^a conta master salva Administrar usuários para a Maria$")]
    public async Task SalvaUsuarios() => await mundo.Definir(await mundo.Maria(), new() { ["MOD-03"] = "CU" });

    [When(@"^a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes sem ""(MOD-\d+)""$")]
    public async Task SalvaSem(string mod)
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { [mod] = "U" });
        var mods = await ModsDe(maria);
        mods.Remove(mod);
        await mundo.Definir(maria, mods, exigir: false);
    }

    [When(@"^a conta master envia PUT (/usuarios/\S+/permissoes) com:$")]
    public async Task MasterSalvaCorpo(string caminho, string json)
    {
        var maria = await mundo.Maria();
        var versao = (await mundo.Permissoes(maria))["versao"]!.GetValue<int>().ToString();
        await mundo.EnviarComo(await mundo.Master(), "PUT", await mundo.Traduzir(caminho), await mundo.Traduzir(json), versao);
    }

    // RNF-222: muda uma aba (Turmas) e salva com a versão da linha do Exemplo
    // o Reqnroll não passa o grupo que não casou: um atributo para cada forma
    [When(@"^a conta master envia PUT (/usuarios/\S+/permissoes) com If-Match ""(\d+)""$")]
    public Task MasterSalvaComVersao(string caminho, string versao) => MasterSalvaVersao(caminho, versao);

    [When(@"^a conta master envia PUT (/usuarios/\S+/permissoes) com sem If-Match$")]
    public Task MasterSalvaSemVersao(string caminho) => MasterSalvaVersao(caminho, null);

    async Task MasterSalvaVersao(string caminho, string? versao)
    {
        var mods = new JsonObject((await ModsDe(await mundo.Maria())).Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value)))
        {
            ["MOD-08"] = "C",
        };
        await mundo.EnviarComo(await mundo.Master(), "PUT", await mundo.Traduzir(caminho),
            new JsonObject { ["mods"] = mods }.ToJsonString(), versao);
    }

    [When(@"^a conta master salva com o Quiz marcado$")]
    public async Task SalvaComQuiz() => await mundo.Conceder(await mundo.Maria(), new() { ["MOD-05"] = "U" });

    [When(@"^a conta master salva as permissões da Maria sem mudar nada$")]
    public async Task SalvaSemMudar()
    {
        var maria = await mundo.Maria();
        await mundo.Definir(maria, await ModsDe(maria));
    }

    // quem concede a quem (RN-252)
    [When(@"^(o administrador|a conta master|o João, com Adm\.|a staff Ana) envia PUT /usuarios/\{id\}/permissoes de (o membro Caio|a staff Maria|a Ana, abaixo dele|ele mesmo|a Maria, par dele|o João, acima dela|o participante Lucas)$")]
    public async Task QuemConcede(string quem, string recebe)
    {
        var joao = await mundo.Joao();
        if (quem.StartsWith("o João")) await mundo.Conceder(joao, new() { ["MOD-03"] = "CU", ["MOD-05"] = "U" });
        var por = quem switch
        {
            "o administrador" => mundo.Admin(),
            "a conta master" => await mundo.Master(),
            "o João, com Adm." => joao,
            _ => await mundo.Ana(),
        };
        var (alvo, mods) = recebe switch
        {
            "o membro Caio" => (await mundo.Caio(), new JsonObject { ["MOD-02"] = "U" }),
            "a staff Maria" => (await mundo.Maria(), new JsonObject { ["MOD-04"] = "U" }),
            "a Ana, abaixo dele" => (await mundo.Ana(), new JsonObject { ["MOD-05"] = "U" }),
            "ele mesmo" => (joao, new JsonObject { ["MOD-05"] = "CU" }),
            "a Maria, par dele" => (await mundo.Maria(), new JsonObject { ["MOD-05"] = "U" }),
            "o João, acima dela" => (joao, new JsonObject { ["MOD-05"] = "U" }),
            _ => (await mundo.Lucas(), new JsonObject { ["MOD-09"] = "U" }),
        };
        // a versão vem de quem sempre pode ler: a conta master, ou o administrador para o Caio
        var leitor = recebe == "o membro Caio" ? mundo.Admin() : await mundo.Master();
        var lida = await mundo.EnviarComo(leitor, "GET", $"/usuarios/{alvo.Id}/permissoes");
        var versao = lida.Corpo?["versao"]?.GetValue<int>().ToString() ?? "1";
        await mundo.EnviarComo(por, "PUT", $"/usuarios/{alvo.Id}/permissoes", new JsonObject { ["mods"] = mods }.ToJsonString(), versao);
    }

    [When(@"^envia um pedido (?:da Presença|do Quiz)$")]
    [Given(@"^uma rota nova sem módulo, aba ou papel declarado$")]
    [When(@"^alguém a chama com um token válido$")]
    [Then(@"^a autorização libera o módulo$")]
    [Then(@"^quais turmas ele vê é regra do documento da Presença$")]
    [Then(@"^o pedido é autorizado pelo papel e pelos mods do token, sem ler as permissões$")]
    [Then(@"^o teste automatizado de rotas falha no build$")]
    public static void SemEnderecoNoContrato() => throw new PendingStepException(SemEndereco);

    [When(@"^envia POST /auth/sala/entrar com a sala 482107 do Quiz$")]
    [Then(@"^o token tem o papel ""ANONIMO"", só ""MOD-05"" com ""U"" e a sala 482107$")]
    [Then(@"^os pedidos à Carteira, a Tarefas e ao Perfil recebem 403 ACESSO_NEGADO$")]
    public static void SalaAnonima() => throw new PendingStepException(SemSala);

    [Then(@"^como a pessoa prova quem é .+$")]
    [Then(@"^limites dentro de um módulo, .+$")]
    [Then(@"^o desenho da tela Permissões .+$")]
    [Then(@"^permissões por ação, .+$")]
    public static void ForaDeEscopo() => throw new PendingStepException("fora de escopo: nada a testar");

    // RN-251: o que criar ou usar quer dizer é regra de cada módulo; o valor das abas no token já é testado
    [Then(@"^a Configuração, ""C"", libera .+$")]
    [Then(@"^a Utilização, ""U"", libera .+$")]
    [Then(@"^quem cria recebe ""C"", .+$")]
    [Then(@"^o participante e o responsável só recebem ""U""$")]
    public static void SentidoDasAbas() => throw new PendingStepException("o que cada aba libera é regra de cada módulo");

    // ── o token ─────────────────────────────────────────────────────────────
    [Then(@"^o token (?:de acesso )?tem o papel ""(\w+)""$")]
    public void Papel(string papel) => Assert.Equal(papel, Carga()["papel"]?.GetValue<string>());

    [Then(@"^os mods (""MOD-.+)$")]
    public void ModsExatos(string texto) => Comparacao.Confere(ComoJson(Mods(texto)), Carga()["mods"], exato: true);

    [Then(@"^o token (?:dela|dele) não tem ""(MOD-\d+)""$")]
    public void NaoTem(string mod) => Assert.False(Carga()["mods"]?.AsObject().ContainsKey(mod), $"o token tem {mod}: {Carga()["mods"]}");

    [Then(@"^o token tem o papel ""MEMBRO_TIME"", ""MOD-02"" e ""MOD-03"" com ""U"", e não tem ""MOD-14""$")]
    public void PapelDoCaio()
    {
        Papel("MEMBRO_TIME");
        var mods = Carga()["mods"]!.AsObject();
        Assert.Equal("U", mods["MOD-02"]?.GetValue<string>());
        Assert.Equal("U", mods["MOD-03"]?.GetValue<string>());
        Assert.False(mods.ContainsKey("MOD-14"), $"o token tem MOD-14: {mods}");
    }

    [Then(@"^o token tem o papel ""PARTICIPANTE"" e, em quem, o parentesco ""(.+)""$")]
    public void PapelDaCarla(string parentesco)
    {
        Papel("PARTICIPANTE");
        Assert.Equal(parentesco, Carga()["quem"]?["parentesco"]?.GetValue<string>());
    }

    [Then(@"^os mods de (.+), todos com ""U""$")]
    public void ModsDoQuest(string nomes) =>
        Comparacao.Confere(ComoJson(Nomes(nomes).ToDictionary(id => id, _ => "U")), Carga()["mods"], exato: true);

    // RN-233: a coluna "módulos" de cada linha
    [Then(@"^o token tem em mods .+ com as abas .+$")]
    public async Task ModsDoPapel()
    {
        var mods = Carga()["mods"]!.AsObject();
        switch (quemEntrou)
        {
            case "administrador":
                Comparacao.Confere(ComoJson(new() { ["MOD-02"] = "CU", ["MOD-03"] = "CU", ["MOD-14"] = "CU" }), mods, exato: true);
                break;
            case "membro do time":
                Comparacao.Confere(ComoJson(new() { ["MOD-02"] = "U" }), mods, exato: true);
                break;
            case "conta master" or "autônomo":
                // os da conta e os contratados, com as duas abas; nada de contratável não contratado
                Assert.Equal("CU", mods["MOD-03"]?.GetValue<string>());
                if (quemEntrou == "conta master") Assert.Equal("CU", mods["MOD-05"]?.GetValue<string>());
                Assert.False(mods.ContainsKey("MOD-06"), $"a Presença não foi contratada: {mods}");
                Assert.All(mods, m => Assert.Equal("CU", m.Value?.GetValue<string>()));
                break;
            case "staff":
                Comparacao.Confere(ComoJson(new() { ["MOD-04"] = "U" }), mods, exato: true);
                break;
            default:
                // participante e responsável: Tarefas e Perfil, só Utilização
                Assert.Equal("U", mods["MOD-09"]?.GetValue<string>());
                Assert.Equal("U", mods["MOD-13"]?.GetValue<string>());
                Assert.All(mods, m => Assert.Equal("U", m.Value?.GetValue<string>()));
                break;
        }
        await Task.CompletedTask;
    }

    [Then(@"^o token novo da Maria tem ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task TokenNovoDaMaria(string mod, string abas)
    {
        var sessao = await mundo.Entrar(await mundo.Maria(), Navegador);
        Assert.Equal(abas, Jwt.Carga(sessao.Acesso)["mods"]?[mod]?.GetValue<string>());
    }

    [Then(@"^o próximo token da Maria vem sem ""(MOD-\d+)""$")]
    public async Task ProximoTokenSem(string mod)
    {
        var sessao = await mundo.Entrar(await mundo.Maria(), Navegador);
        Assert.False(Jwt.Carga(sessao.Acesso)["mods"]?.AsObject().ContainsKey(mod), $"o token novo tem {mod}");
    }

    [Then(@"^POST /auth/renovar devolve um token de pv (\d+)$")]
    public async Task RenovaComPv(int pv)
    {
        Respostas.Confere(await mundo.RenovarEGuardar(mundo.Atual!), 200);
        Assert.Equal(pv, Jwt.Carga(mundo.Atual!.Acesso)["pv"]?.GetValue<int>());
    }

    [Then(@"^POST /auth/renovar das duas sessões devolve tokens de pv (\d+)$")]
    public async Task DuasSessoesComPv(int pv)
    {
        var maria = await mundo.Maria();
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa == maria).ToList())
        {
            Respostas.Confere(await mundo.RenovarEGuardar(sessao), 200);
            Assert.Equal(pv, Jwt.Carga(sessao.Acesso)["pv"]?.GetValue<int>());
        }
    }

    [Then(@"^POST /auth/renovar devolve o token novo, sem encerrar a sessão$")]
    public async Task RenovaSemEncerrar()
    {
        Respostas.Confere(await mundo.RenovarEGuardar(mundo.Atual!), 200);
        Assert.Equal(200, await mundo.ProximoPedido(mundo.Atual!.Acesso));
    }

    // RNF-219: as claims que a autorização põe no token; mods e pv dependem do
    // que a pessoa tem no cenário, por isso só o tipo deles é conferido
    [Then(@"^o token (?:tem as claims papel, mods e pv|do quest é):$")]
    public async Task Claims(string json)
    {
        var esperado = JsonNode.Parse(await mundo.Traduzir(json))!.AsObject();
        esperado.Remove("mods");
        esperado.Remove("pv");
        var carga = Carga();
        Comparacao.Confere(esperado, carga);
        Assert.True(carga["mods"] is JsonObject, $"mods não é um objeto: {carga}");
        Assert.True(carga["pv"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number, $"pv não é um número: {carga}");
    }

    [Then(@"^o token inteiro, com a assinatura RS256 de 342 caracteres, cabe em 4 KB$")]
    public void CabeEm4Kb()
    {
        var token = Texto(mundo.Ultima, "tokenAcesso");
        Assert.Equal(342, token.Split('.')[2].Length);
        Assert.True(token.Length <= 4096, $"o token tem {token.Length} caracteres");
    }

    // ── respostas e permissões guardadas ───────────────────────────────────
    [Then(@"^a resposta é 403 ACESSO_NEGADO e nenhum usuário é criado$")]
    public async Task NenhumUsuarioCriado()
    {
        Respostas.Confere(mundo.Ultima, 403, "ACESSO_NEGADO");
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", "/usuarios?tipo=STAFF&busca=recusado"), 200, "GET /usuarios");
        Assert.Empty(r.Corpo!["itens"]!.AsArray());
    }

    [Then(@"^a resposta é 404 USUARIO_INEXISTENTE, e não 403$")]
    public void NaoEncontrado() => Respostas.Confere(mundo.Ultima, 404, "USUARIO_INEXISTENTE");

    [Then(@"^a resposta é 200 com o cadastro da conta$")]
    public void CadastroDaConta()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal("EBFGA", mundo.Ultima.Corpo?["sigla"]?.GetValue<string>());
    }

    [Then(@"^a resposta é 403 PERMISSAO_ACIMA, conferida com as permissões atuais do João$")]
    public void PermissaoAcima() => Respostas.Confere(mundo.Ultima, 403, "PERMISSAO_ACIMA");

    [Then(@"^a resposta é 200, só com as abas que ele tem$")]
    public async Task SoAsAbasDele()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(ComoJson(new() { ["MOD-05"] = "U" }), (await mundo.Permissoes(await mundo.Ana()))["mods"], exato: true);
    }

    [Then(@"^a resposta é 200 e ""(MOD-\d+)"" sai das permissões da Maria$")]
    public async Task SaiDasPermissoes(string mod)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.DoesNotContain(mod, (await ModsDe(await mundo.Maria())).Keys);
    }

    [Then(@"^a resposta é 200 com a versão (\d+)$")]
    public void ComAVersao(int versao)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(versao, mundo.Ultima.Corpo?["versao"]?.GetValue<int>());
    }

    // RNF-222: "200, gravado" sobe a versão; "412 …, nada é gravado" a mantém
    [Then(@"^a resposta é (\d{3}), gravado$")]
    public Task Gravado(int status) => GravadoOuNao(status, null, "gravado");

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+), nada é gravado$")]
    public Task NadaGravado(int status, string codigo) => GravadoOuNao(status, codigo, "nada é gravado");

    async Task GravadoOuNao(int status, string? codigo, string gravado)
    {
        var versaoAntes = 7;
        Respostas.Confere(mundo.Ultima, status, codigo);
        var versao = (await mundo.Permissoes(await mundo.Maria()))["versao"]!.GetValue<int>();
        Assert.Equal(gravado == "gravado" ? versaoAntes + 1 : versaoAntes, versao);
    }

    [Then(@"^GET (/usuarios/\S+/permissoes) continua com a versão (\d+), sem mudança$")]
    public async Task ContinuaNaVersao(string caminho, int versao)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho)), 200, caminho);
        Assert.Equal(versao, r.Corpo?["versao"]?.GetValue<int>());
    }

    [Then(@"^GET (/usuarios/\S+/permissoes) continua com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task ContinuaCom(string caminho, string mod, string abas)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho)), 200, caminho);
        Assert.Equal(abas, r.Corpo?["mods"]?[mod]?.GetValue<string>());
    }

    [Then(@"^GET (/usuarios/\S+/permissoes) não traz ""(MOD-\d+)""(?: em mods)?$")]
    public async Task NaoTraz(string caminho, string mod)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho)), 200, caminho);
        Assert.False(r.Corpo?["mods"]?.AsObject().ContainsKey(mod), $"ainda traz {mod}: {r}");
    }

    [Then(@"^quando a escola contrata a Presença de novo, ""(MOD-\d+)"" volta com ""(\w+)""$")]
    public async Task VoltaComARecontratacao(string mod, string abas)
    {
        await mundo.ContratacaoMudou(mod, contratada: true);
        Assert.Equal(abas, (await ModsDe(await mundo.Maria())).GetValueOrDefault(mod));
    }

    // só inclusão (RN-256): o contrato não tem endereço que apague ou altere o registro
    [Then(@"^nenhum endereço apaga ou altera esse registro$")]
    public async Task RegistroImutavel()
    {
        var caminho = mundo.UltimoPedido.Caminho;
        var master = await mundo.Master();
        foreach (var metodo in new[] { "DELETE", "PUT", "PATCH" })
        {
            var r = await mundo.EnviarComo(master, metodo, caminho, "{}");
            Assert.True(r.Status is 404 or 405, $"{metodo} {caminho} respondeu {r}");
        }
    }

    [Then(@"^as permissões, o registro e a versão nova são gravados numa só operação$")]
    public async Task GravadoJunto()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var versao = mundo.Ultima.Corpo!["versao"]!.GetValue<int>();
        var maria = await mundo.Maria();
        Assert.Equal(versao, (await mundo.Permissoes(maria))["versao"]!.GetValue<int>());
        var historico = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", $"/usuarios/{maria.Id}/permissoes/historico"), 200, "GET …/historico");
        Assert.Equal(versao, historico.Corpo![0]?["versao"]?.GetValue<int>());
    }

    [Then(@"^a versão nova chega às sessões da Maria em até 1 s depois da resposta$")]
    public async Task ChegaEmUmSegundo()
    {
        var limite = DateTimeOffset.UtcNow.AddSeconds(1);
        Resposta r;
        do r = await mundo.Enviar("GET", "/auth/sessoes", token: mundo.TokenGuardado);
        while (r.Status != 401 && DateTimeOffset.UtcNow < limite);
        Respostas.Confere(r, 401, "PERMISSOES_DESATUALIZADAS");
    }

    // ── apoio ───────────────────────────────────────────────────────────────
    JsonObject Carga() => Jwt.Carga(Texto(mundo.Ultima, "tokenAcesso"));

    async Task<Pessoa> Pessoa(string quem) => quem switch
    {
        "a Maria" => await mundo.Maria(),
        "a Ana" => await mundo.Ana(),
        "o membro do time Caio Lima" => await mundo.Caio(),
        "o Paulo" => await mundo.Paulo(),
        _ => await mundo.Joao(),
    };

    // o João só concede o que tem: recebe antes Administrar usuários e os módulos
    async Task<Pessoa> PodeConceder(Pessoa joao, Dictionary<string, string> mods)
    {
        await mundo.Conceder(joao, new Dictionary<string, string>(mods) { ["MOD-03"] = "CU" });
        return joao;
    }

    async Task<Dictionary<string, string>> ModsDe(Pessoa pessoa) =>
        (await mundo.Permissoes(pessoa))["mods"]!.AsObject().ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());

    // "Administrar usuários e a Utilização da Sugestão e do Quiz" → MOD-03 CU, MOD-04 U, MOD-05 U
    // "\"MOD-03\" com \"CU\" e \"MOD-06\" com \"U\"" → MOD-03 CU, MOD-06 U
    static Dictionary<string, string> Mods(string texto)
    {
        var mods = new Dictionary<string, string>();
        foreach (Match m in ParDeMod().Matches(texto)) mods[m.Groups[1].Value] = m.Groups[2].Value;
        if (texto.Contains("Administrar usuários")) mods["MOD-03"] = "CU";
        var i = texto.IndexOf("Utilização", StringComparison.Ordinal);
        if (i >= 0) foreach (var id in Nomes(texto[i..])) mods.TryAdd(id, "U");
        return mods;
    }

    static IEnumerable<string> Nomes(string texto) =>
        ModuloPorNome.Where(m => texto.Contains(m.Key, StringComparison.Ordinal)).Select(m => m.Value);

    static JsonObject ComoJson(Dictionary<string, string> mods) =>
        new(mods.Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value)));

    [GeneratedRegex(@"""(MOD-\d+)"" (?:só )?com ""(\w+)""")]
    private static partial Regex ParDeMod();
}
