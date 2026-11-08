using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos das contas iniciais de 1_Requisitos/17_modulo-usuario.md: o
// administrador do deboaz (RN-190), as contas qa de homologação (RN-265,
// RN-266), a inicialização que não duplica e volta as contas qa ao estado
// inicial (RN-267), os módulos iniciais das contas qa (RN-268) e os segredos do
// Key Vault (RNF-225, RNF-226).
//
// Duas fontes, conforme o cenário:
//   - "a aplicação de homologação iniciada": o backend de API_URL, que o CI sobe
//     em homologação com o Key Vault nas variáveis USUARIO_*. A suíte lê as
//     mesmas variáveis (ContasIniciais), nunca um valor fixo;
//   - "a aplicação … inicia", "inicia de novo", "sem o segredo": uma instância
//     própria (suporte/Partida.cs), com um Key Vault do cenário (entradas do
//     requisito, senhas geradas na hora). Sem API_COMANDO, ficam pendentes.
[Binding]
public sealed class ContasIniciaisPassos(Mundo mundo, Partida partida)
{
    const string SemAuditoria = "a auditoria não tem endereço no contrato: conferida nos testes do backend";
    const string NaInfra = "segredo no Key Vault: conferido na infraestrutura e no pipeline, sem endereço na API";
    const string SoNoBackend = "conferido nos testes do backend: o qa não olha o banco";

    // o Dado já montou o Key Vault da instância (senão o Quando monta o completo)
    bool keyVaultDoCenario;
    bool pronta;
    string senhaAntiga = "";
    // a conta inicial de que o Dado fala (senha trocada, staff desativada)
    ContaInicial? citada;
    // o token do Dado "o token de …" (RN-265)
    string? tokenDeQuem;
    // a variável que o Dado tirou do Key Vault (RNF-226)
    string variavelAusente = "";
    readonly List<Resposta> respostasDaPartida = [];
    // a lista de módulos iniciais do Key Vault do cenário, por parte (RN-268)
    Dictionary<string, string> staffDoCenario = new(ContasIniciais.ModulosDaStaff);
    Dictionary<string, string> timeDoCenario = new(ContasIniciais.ModulosDoTime);

    Dictionary<string, string> ParteDoCenario(string parte) => parte switch
    {
        "staff" or "da staff" => staffDoCenario,
        "time" or "do time" => timeDoCenario,
        _ => throw new InvalidOperationException($"\"{parte}\" não é uma parte da lista de módulos iniciais (RN-268)"),
    };
    // a última resposta da instância do cenário, para os Então que a leem
    Resposta? ultimaDaPartida;

    static string Sigla(string ambiente) => ambiente == "produção" ? "prd" : "hml";

    // ── administrador do deboaz (RN-190) ───────────────────────────────────
    [Given(@"^o Key Vault do ambiente com a entrada ""(.+)""$")]
    public void EntradaDoAdmin(string entrada)
    {
        partida.AmbienteDaPartida = "prd";
        partida.KeyVault[ContasIniciais.Admin.VariavelEntrada] = entrada;
        keyVaultDoCenario = true;
    }

    [Given(@"^a senha do administrador$")]
    public void SenhaDoAdmin() => partida.KeyVault[ContasIniciais.Admin.VariavelSenha!] = ContasIniciais.SenhaNova();

    // produção numa instância nova, com o banco vazio; homologação é o backend
    // de API_URL, que o CI sobe pela primeira vez a cada execução
    [When(@"^a aplicação inicia pela primeira vez, em homologação ou em produção$")]
    public Task PrimeiraVez() => partida.IniciarPronta("produção, primeira vez");

    [Then(@"^o administrador do deboaz passa a existir, ativo, com essa entrada e essa senha$")]
    public async Task AdminExiste()
    {
        var token = await TokenNaPartida(ContasIniciais.Admin);
        var r = await partida.Api.Enviar("GET", $"/usuarios/{ContasIniciais.Admin.Id}", token: token);
        Respostas.Confere(r, 200);
        Assert.Equal("ATIVO", r.Corpo?["estado"]?.GetValue<string>());
        Assert.Equal(partida.Entrada(ContasIniciais.Admin), r.Corpo?["login"]?.GetValue<string>());
    }

    [Then(@"^POST /auth/hub/entrar com elas devolve 200 e o papel ""(\w+)""$")]
    public async Task AdminEntra(string papel)
    {
        // produção: a instância do cenário
        var r = await EntrarNaPartida(ContasIniciais.Admin);
        Respostas.Confere(r, 200);
        Assert.Equal(papel, Papel(r));
        // homologação: o backend de API_URL, com o Key Vault da suíte
        var hml = await EntrarNoHub(ContasIniciais.Admin.Entrada, ContasIniciais.Admin.Senha);
        Assert.True(hml.Status == 200, $"homologação: esperava 200 do administrador, veio {hml}");
        Assert.Equal(papel, Papel(hml));
    }

    // ── contas qa em homologação (RN-265) ──────────────────────────────────
    [Given(@"^a aplicação de homologação iniciada$")]
    public async Task HomologacaoIniciada() => Exigir(await mundo.Enviar("GET", "/health"), 200, "GET /health");

    [When(@"^envia POST /auth/hub/entrar com a entrada ""(.+)"" e a senha do Key Vault$")]
    public async Task EntraComOKeyVault(string entrada)
    {
        var conta = ContasIniciais.PorEntrada(entrada);
        await EntrarNoHub(conta.Entrada, conta.Senha);
    }

    [When(@"^envia POST /auth/quest/entrar com o código ""(.+)"" do Key Vault$")]
    public async Task EntraNoQuestComOKeyVault(string codigo)
    {
        var conta = ContasIniciais.PorEntrada(codigo);
        await mundo.Enviar("POST", "/auth/quest/entrar", CorpoDoQuest(conta.Entrada));
    }

    [Then(@"^a resposta é 200 e o token tem o papel ""(\w+)""$")]
    public void TokenComPapel(string papel)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(papel, Papel(mundo.Ultima));
    }

    // quem lê: a conta inicial pela entrada do requisito, com a entrada e a senha
    // das USUARIO_* (o administrador vem de USUARIO_ADMIN_ENTRADA)
    [Given(@"^o token de ""(.+)""$")]
    public async Task TokenDe(string entrada)
    {
        var conta = ContasIniciais.PorEntrada(entrada);
        var r = Exigir(await EntrarNoHub(conta.Entrada, conta.Senha), 200, $"POST /auth/hub/entrar de {conta.Nome}");
        tokenDeQuem = Texto(r, "tokenAcesso");
    }

    [When(@"^envia GET /usuarios/\{id\} de ""(.+)""$")]
    public async Task LeOUsuario(string alvo) =>
        await mundo.Enviar("GET", $"/usuarios/{ContasIniciais.PorEntrada(alvo).Id}", token: tokenDeQuem);

    [Then(@"^a resposta é 200 com o tipo ""(\w+)""$")]
    public void ComOTipo(string tipo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(tipo, mundo.Ultima.Corpo?["tipo"]?.GetValue<string>());
    }

    // ── o que cada ambiente garante (RN-266) ───────────────────────────────
    [When(@"^a aplicação de (homologação|produção) inicia$")]
    public async Task AplicacaoInicia(string ambiente)
    {
        if (!keyVaultDoCenario) partida.KeyVaultCompleto(Sigla(ambiente));
        partida.AmbienteDaPartida = Sigla(ambiente);
        pronta = await partida.Iniciar();
    }

    [Then(@"^existem o administrador, as 6 contas qa e as contas ""(\w+)"" e ""(\w+)""$")]
    public async Task ExistemTodas(string qains, string qaaut)
    {
        ExigirPronta();
        foreach (var conta in ContasIniciais.Todas)
        {
            var r = await EntrarNaPartida(conta);
            Assert.True(r.Status == 200, $"{conta.Nome} ({partida.Entrada(conta)}): esperava 200 na entrada, veio {r}");
            Assert.Equal(conta.Papel, Papel(r));
        }
        var admin = await TokenNaPartida(ContasIniciais.Admin);
        foreach (var (id, sigla) in new[] { (ContasIniciais.ContaQains, qains), (ContasIniciais.ContaQaaut, qaaut) })
        {
            var r = await partida.Api.Enviar("GET", $"/contas/{id}", token: admin);
            Respostas.Confere(r, 200);
            Assert.Equal(sigla, r.Corpo?["sigla"]?.GetValue<string>());
        }
    }

    [Then(@"^existem só o administrador, na conta ""(\w+)""$")]
    public async Task SoOAdmin(string sigla)
    {
        ExigirPronta();
        var r = await EntrarNaPartida(ContasIniciais.Admin);
        Respostas.Confere(r, 200);
        var carga = Jwt.Carga(Texto(r, "tokenAcesso"));
        Assert.Equal("ADMINISTRADOR", carga["papel"]?.GetValue<string>());
        // o id do administrador nasce da sigla da conta: usr-dboaz-admin
        Assert.Equal($"usr-{sigla.ToLowerInvariant()}-admin", carga["sub"]?.GetValue<string>());
        await NenhumaQa();
    }

    [Given(@"^o Key Vault de produção com a entrada ""(.+)"" e uma senha$")]
    public void SegredoQaEmProducao(string entrada)
    {
        partida.KeyVaultCompleto("prd");
        var conta = ContasIniciais.PorEntrada(entrada);
        partida.KeyVault[conta.VariavelEntrada] = entrada;
        partida.KeyVault[conta.VariavelSenha!] = ContasIniciais.SenhaNova();
        keyVaultDoCenario = true;
    }

    [Then(@"^nenhuma conta qa nem as contas ""QAINS"" e ""QAAUT"" são criadas$")]
    public async Task NenhumaQa()
    {
        ExigirPronta();
        var admin = await TokenNaPartida(ContasIniciais.Admin);
        foreach (var id in new[] { ContasIniciais.ContaQains, ContasIniciais.ContaQaaut })
        {
            Respostas.Confere(await partida.Api.Enviar("GET", $"/contas/{id}", token: admin), 404, "CONTA_INEXISTENTE");
        }
        foreach (var conta in ContasIniciais.Qa)
        {
            var r = await partida.Api.Enviar("GET", $"/usuarios/{conta.Id}", token: admin);
            Assert.True(r.Status == 404, $"{conta.Nome} ({conta.Id}) existe em produção: GET /usuarios/{{id}} respondeu {r}");
        }
    }

    [Then(@"^POST /auth/hub/entrar com ""(.+)"" recebe 401 ENTRADA_INVALIDA$")]
    public async Task QaNaoEntra(string entrada) =>
        Respostas.Confere(await EntrarNaPartida(ContasIniciais.PorEntrada(entrada)), 401, "ENTRADA_INVALIDA");

    // ── inicialização sem duplicar (RN-267) ────────────────────────────────
    // a data do exemplo é só a ordem das partidas: a suíte não muda o relógio
    [Given(@"^a staff ""(.+)"" criada na inicialização de \d{4}-\d{2}-\d{2}$")]
    public async Task CriadaNaInicializacao(string entrada)
    {
        await HomologacaoPronta();
        Respostas.Confere(await EntrarNaPartida(ContasIniciais.PorEntrada(entrada)), 200);
    }

    [When(@"^a aplicação de homologação inicia de novo em \d{4}-\d{2}-\d{2}$")]
    [When(@"^a aplicação de homologação inicia de novo$")]
    [When(@"^a aplicação inicia$")]
    public async Task IniciaDeNovo() => pronta = await partida.Iniciar();

    [Then(@"^GET /usuarios\?tipo=STAFF da ""QAINS"" traz uma só ""(.+)""$")]
    public async Task UmaSo(string entrada)
    {
        ExigirPronta();
        var master = await TokenNaPartida(ContasIniciais.Master);
        var r = await partida.Api.Enviar("GET", "/usuarios?tipo=STAFF", token: master);
        Respostas.Confere(r, 200);
        var iguais = r.Corpo?["itens"]?.AsArray()
            .Count(i => string.Equals(i?["login"]?.GetValue<string>(), partida.Entrada(ContasIniciais.PorEntrada(entrada)), StringComparison.OrdinalIgnoreCase));
        Assert.True(iguais == 1, $"esperava uma só \"{entrada}\" na QAINS, vieram {iguais}: {r}");
    }

    [Then(@"^a auditoria não ganha um registro novo de criação dela$")]
    public static void SemAuditoriaNova() => throw new PendingStepException(SemAuditoria);

    [Given(@"^a senha de ""(.+)"" trocada no Key Vault$")]
    public async Task SenhaTrocada(string entrada)
    {
        var conta = ContasIniciais.PorEntrada(entrada);
        await HomologacaoPronta();
        Respostas.Confere(await EntrarNaPartida(conta), 200);
        senhaAntiga = partida.Senha(conta);
        partida.KeyVault[conta.VariavelSenha!] = ContasIniciais.SenhaNova();
        citada = conta;
    }

    [Then(@"^POST /auth/hub/entrar aceita só a senha nova$")]
    public async Task SoASenhaNova()
    {
        ExigirPronta();
        var r = await EntrarNaPartida(citada!);
        Assert.True(r.Status == 200, $"a senha nova do Key Vault foi recusada: {r}");
    }

    [Then(@"^a senha antiga recebe 401 ENTRADA_INVALIDA$")]
    public async Task SenhaAntiga() =>
        Respostas.Confere(await partida.Api.Enviar("POST", "/auth/hub/entrar", CorpoDoHub(partida.Entrada(citada!), senhaAntiga)),
            401, "ENTRADA_INVALIDA");

    [Given(@"^a staff ""(.+)"" desativada durante um teste$")]
    public async Task DesativadaNoTeste(string entrada)
    {
        var staff = ContasIniciais.PorEntrada(entrada);
        await HomologacaoPronta();
        await AnotarForaDasQa();
        var master = await TokenNaPartida(ContasIniciais.Master);
        Exigir(await partida.Api.Enviar("POST", $"/usuarios/{staff.Id}/desativar", token: master), 204, "POST /usuarios/{id}/desativar");
        citada = staff;
    }

    [Given(@"^a senha do ""(.+)"" gerada de novo durante um teste$")]
    public async Task SenhaGeradaNoTeste(string entrada)
    {
        var conta = ContasIniciais.PorEntrada(entrada);
        await HomologacaoPronta();
        await AnotarForaDasQa();
        var r = await partida.Api.Enviar("POST", $"/usuarios/{conta.Id}/nova-geracao-senha", token: await TokenNaPartida(QuemConcede(conta)));
        Assert.True(r.Status is >= 200 and < 300, $"preparo: POST /usuarios/{{id}}/nova-geracao-senha de {conta.Nome} respondeu {r}");
        citada = conta;
    }

    [Given(@"^""(MOD-\d+)"" tirado das abas do ""(.+)"" durante um teste$")]
    public async Task ModuloTirado(string mod, string entrada)
    {
        var conta = ContasIniciais.PorEntrada(entrada);
        await HomologacaoPronta();
        await AnotarForaDasQa();
        var mods = await ModsNaPartida(conta);
        Assert.True(mods.Remove(mod), $"preparo: {conta.Nome} não começou com {mod}, da lista do Key Vault: {string.Join(", ", mods.Keys)}");
        await SalvarNaPartida(conta, mods);
        citada = conta;
    }

    [Given(@"^""(MOD-\d+)"" concedido ao ""(.+)"" durante um teste$")]
    public async Task ModuloConcedido(string mod, string entrada)
    {
        var conta = ContasIniciais.PorEntrada(entrada);
        await HomologacaoPronta();
        await AnotarForaDasQa();
        var mods = await ModsNaPartida(conta);
        mods[mod] = "U";
        await SalvarNaPartida(conta, mods);
        citada = conta;
    }

    [Then(@"^a staff fica ativa$")]
    public async Task FicaAtiva()
    {
        ExigirPronta();
        var staff = citada!;
        var r = await EntrarNaPartida(staff);
        Assert.True(r.Status == 200, $"a staff desativada não voltou: a entrada com a senha do Key Vault respondeu {r}");
        await EstaAtiva(staff);
    }

    [Then(@"^vale só a senha do Key Vault$")]
    public async Task SoASenhaDoKeyVault()
    {
        ExigirPronta();
        var conta = citada!;
        var r = await EntrarNaPartida(conta);
        Assert.True(r.Status == 200, $"{conta.Nome}: a senha do Key Vault não voltou a valer, a entrada respondeu {r}");
        Assert.Null(r.Corpo?["comprovante"]);
        await EstaAtiva(conta);
    }

    [Then(@"^o ""(\w+)"" só com as abas da parte (da staff|do time)$")]
    public async Task SoAsAbasDaParte(string apelido, string parte)
    {
        ExigirPronta();
        var conta = ContasIniciais.PorEntrada(apelido);
        Assert.Equal(ParteDoCenario(parte), await ModsNaPartida(conta));
    }

    // o que não é conta qa fica como estava (RN-267): uma staff de teste da
    // QAINS, criada e desativada antes da nova partida, e o administrador real
    string? idDeFora;
    string estadoDeFora = "", estadoDoAdmin = "";

    async Task AnotarForaDasQa()
    {
        var master = await TokenNaPartida(ContasIniciais.Master);
        var criada = Exigir(await partida.Api.Enviar("POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF",
            ["nome"] = "Staff de Teste",
            ["nomeCurto"] = "teste",
            ["matricula"] = "20260999",
        }.ToJsonString(), token: master), 201, "preparo: POST /usuarios da staff de teste");
        idDeFora = Texto(criada, "id");
        Exigir(await partida.Api.Enviar("POST", $"/usuarios/{idDeFora}/desativar", token: master), 204, "preparo: POST /usuarios/{id}/desativar da staff de teste");
        estadoDeFora = await EstadoNaPartida(idDeFora, ContasIniciais.Master);
        estadoDoAdmin = await EstadoNaPartida(ContasIniciais.Admin.Id, ContasIniciais.Admin);
    }

    async Task<string> EstadoNaPartida(string id, ContaInicial por) =>
        Texto(Exigir(await partida.Api.Enviar("GET", $"/usuarios/{id}", token: await TokenNaPartida(por)), 200, $"GET /usuarios/{id}"), "estado");

    [Then(@"^só as contas qa voltam ao estado inicial$")]
    public async Task SoAsQaVoltam()
    {
        ExigirPronta();
        Assert.Equal(estadoDeFora, await EstadoNaPartida(idDeFora!, ContasIniciais.Master));
    }

    [Then(@"^""(.+)"" fica como estava, sem volta nem mudança vinda do ""qaadmin""$")]
    public async Task AdminComoEstava(string entrada)
    {
        ExigirPronta();
        var admin = ContasIniciais.PorEntrada(entrada);
        var r = await EntrarNaPartida(admin);
        Assert.True(r.Status == 200, $"{admin.Nome}: a entrada com a senha do Key Vault respondeu {r}");
        Assert.Equal(estadoDoAdmin, await EstadoNaPartida(admin.Id, admin));
    }

    // ── módulos iniciais das contas qa (RN-268) ────────────────────────────
    [Given(@"^o Key Vault de homologação com a lista de módulos iniciais:$")]
    public void ListaNoKeyVault(string json)
    {
        partida.KeyVaultCompleto("hml");
        var lista = JsonNode.Parse(json)!;
        staffDoCenario = ModsDaParte(lista, "staff");
        timeDoCenario = ModsDaParte(lista, "time");
        partida.Modulos(staffDoCenario, timeDoCenario);
        keyVaultDoCenario = true;
    }

    [Given(@"^a mesma lista no Key Vault de homologação$")]
    public Task MesmaLista() => HomologacaoPronta();

    static Dictionary<string, string> ModsDaParte(JsonNode lista, string parte) =>
        lista[parte]!["mods"]!.AsObject().ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());

    [Then(@"^GET /usuarios/\{id\}/permissoes do ""(.+)"" traz os mods de ""(staff|time)""$")]
    public async Task TrazOsModsDaParte(string entrada, string parte)
    {
        ExigirPronta();
        var conta = ContasIniciais.PorEntrada(entrada);
        Assert.Equal(ParteDoCenario(parte), await ModsNaPartida(conta));
    }

    [When(@"^""(.+)"" envia GET /usuarios/\{id\}/permissoes do ""(.+)""$")]
    public async Task LePermissoesNaPartida(string quem, string alvo) =>
        ultimaDaPartida = await partida.Api.Enviar("GET", $"/usuarios/{ContasIniciais.PorEntrada(alvo).Id}/permissoes",
            token: await TokenNaPartida(ContasIniciais.PorEntrada(quem)));

    // "concediveis traz ""MOD-03"", ""MOD-04"" e ""MOD-05"", da parte ""staff"", com ""CU"""
    [Then(@"^concediveis traz (""MOD-\d+""(?:(?:, | e )""MOD-\d+"")*), da parte ""(staff|time)"", com ""(\w+)""$")]
    public void TrazOsDaParteComAsAbas(string lista, string parte, string abas)
    {
        var r = ultimaDaPartida ?? throw new InvalidOperationException("nenhum pedido à instância do cenário");
        Respostas.Confere(r, 200);
        var esperados = System.Text.RegularExpressions.Regex.Matches(lista, @"MOD-\d+").Select(m => m.Value).ToList();
        Assert.Equal(ParteDoCenario(parte).Keys.Order(), esperados.Order());
        var vieram = Concediveis(r);
        foreach (var mod in esperados)
        {
            Assert.True(vieram.TryGetValue(mod, out var veio), $"concediveis não traz {mod}, da parte \"{parte}\" da lista: {r}");
            Assert.Equal(abas, veio);
        }
    }

    // os módulos da conta entram sozinhos, fora da lista (RN-268)
    [Then(@"^traz também os módulos da conta ""(MOD-\d+)"" \w+ e ""(MOD-\d+)"" \w+, que entram sozinhos$")]
    public void TrazOsDaConta(string um, string outro)
    {
        var r = ultimaDaPartida ?? throw new InvalidOperationException("nenhum pedido à instância do cenário");
        var vieram = Concediveis(r);
        foreach (var mod in new[] { um, outro })
        {
            Assert.True(vieram.ContainsKey(mod), $"concediveis não traz {mod}, módulo da conta: {r}");
        }
    }

    // a parte "time" vai só para o qamembro: o que só ela tem não aparece nos
    // módulos da "QAINS" (token da conta master, concediveis do qastaff) nem nos
    // da "QAAUT" (token do autônomo, que traz os da conta e os contratados)
    [Then(@"^""QAINS"" e ""QAAUT"" não contratam ""(MOD-\d+)"" nem nenhum módulo da parte ""time""$")]
    public async Task ParteDoTimeNaoContratada(string citado)
    {
        ExigirPronta();
        var soDoTime = timeDoCenario.Keys.Except(staffDoCenario.Keys).Append(citado).Distinct().ToList();
        var deCadaConta = new Dictionary<string, IEnumerable<string>>
        {
            ["QAINS, token da conta master"] = Jwt.Carga(await TokenNaPartida(ContasIniciais.Master))["mods"]!.AsObject().Select(m => m.Key),
            ["QAAUT, token do autônomo"] = Jwt.Carga(await TokenNaPartida(ContasIniciais.Autonomo))["mods"]!.AsObject().Select(m => m.Key),
            ["QAINS, concediveis do qastaff"] = Concediveis(await PermissoesNaPartida(ContasIniciais.Staff)).Keys,
        };
        foreach (var (onde, mods) in deCadaConta)
        {
            var vieram = mods.Intersect(soDoTime).ToList();
            Assert.True(vieram.Count == 0, $"{onde}: {string.Join(", ", vieram)}, da parte \"time\", virou contratação");
        }
    }

    [Given(@"^a aplicação de produção iniciada$")]
    public async Task ProducaoIniciada()
    {
        partida.KeyVaultCompleto("prd");
        keyVaultDoCenario = true;
        await partida.IniciarPronta("produção");
        pronta = true;
    }

    // a escola, a conta master e a Maria criadas pela API na instância de
    // produção; a escola não contratou nada (o preparo de contratações é no
    // banco do backend de API_URL, não no desta instância)
    [When(@"^a conta master da EBFGA envia GET /usuarios/usr-ebfga-maria/permissoes$")]
    public async Task MasterDaEbfgaLeAMaria()
    {
        ExigirPronta();
        var conta = Exigir(await partida.Api.Enviar("POST", "/contas",
            ContaNova("INSTITUICAO", "Escola Bento Freitas", "12.345.678/0001-90", "objetivo_unidade1", "EBFGA")), 201, "preparo: POST /contas da EBFGA");
        var confirmada = Exigir(await partida.Api.Enviar("POST", "/auth/hub/confirmar-senha",
            new JsonObject { ["comprovante"] = Texto(conta, "comprovante") }.ToJsonString()), 200, "preparo: POST /auth/hub/confirmar-senha da EBFGA");
        var master = Texto(confirmada, "tokenAcesso");
        var maria = Exigir(await partida.Api.Enviar("POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF",
            ["nome"] = "Maria Souza",
            ["nomeCurto"] = "maria",
            ["matricula"] = "20260087",
        }.ToJsonString(), token: master), 201, "preparo: POST /usuarios da Maria");
        ultimaDaPartida = await partida.Api.Enviar("GET", $"/usuarios/{Texto(maria, "id")}/permissoes", token: master);
    }

    // a EBFGA não contratou nenhum módulo contratável: nenhum deles pode vir da inicialização
    [Then(@"^concediveis traz só os módulos contratados pela escola$")]
    public void SoOsContratados()
    {
        var r = ultimaDaPartida ?? throw new InvalidOperationException("nenhum pedido à instância do cenário");
        Respostas.Confere(r, 200);
        string[] contrataveis = ["MOD-05", "MOD-06", "MOD-07", "MOD-12"];
        var vieram = Concediveis(r).Keys.Intersect(contrataveis).ToList();
        Assert.True(vieram.Count == 0, $"em produção, a EBFGA não contratou {string.Join(", ", vieram)}, que veio em concediveis: {r}");
    }

    // ── contas iniciais só pelo Key Vault (RNF-225) ────────────────────────
    [Given(@"^o Key Vault de homologação com a entrada e a senha de cada conta inicial$")]
    public void KeyVaultDeHomologacao()
    {
        partida.KeyVaultCompleto("hml");
        keyVaultDoCenario = true;
    }

    // cada conta entra com o que o Key Vault entregou: a aplicação recebeu tudo
    [Then(@"^recebe cada entrada, senha e código como variável de ambiente lida do Key Vault$")]
    public async Task RecebeuTudo()
    {
        ExigirPronta();
        foreach (var conta in ContasIniciais.Todas)
        {
            var r = await EntrarNaPartida(conta);
            Assert.True(r.Status == 200, $"{conta.Nome}: a entrada com {conta.VariavelEntrada} respondeu {r}");
        }
    }

    [Then(@"^nenhuma delas está no código, no repositório, na imagem nem no pipeline$")]
    public static void ForaDoRepositorio() => throw new PendingStepException(NaInfra);

    [When(@"^a aplicação cria ou confere uma conta inicial$")]
    public async Task CriaOuConfere()
    {
        await HomologacaoPronta();
        await EntrarComTodas();
        // e confere de novo, numa segunda partida (RN-267)
        Assert.True(await partida.Iniciar(), $"a segunda partida não ficou pronta. Log:\n{partida.Log}");
        await EntrarComTodas();
    }

    // cada conta inicial entra: sem isso, não haveria senha nem código para procurar
    async Task EntrarComTodas()
    {
        foreach (var conta in ContasIniciais.Todas)
        {
            respostasDaPartida.Add(Exigir(await EntrarNaPartida(conta), 200, $"entrada de {conta.Nome}"));
        }
    }

    [Then(@"^nenhum log, registro de auditoria ou resposta traz a senha ou o código$")]
    public void SemSenhaNoLogNemNaResposta()
    {
        var senhas = ContasIniciais.Todas.Where(c => !c.Participante).Select(partida.Senha).ToList();
        foreach (var senha in senhas.Append(partida.Entrada(ContasIniciais.Participante)))
        {
            Assert.False(partida.Log.Contains(senha, StringComparison.Ordinal), "o log da partida traz a senha ou o código de uma conta inicial");
        }
        // o código do participante é o login dele e volta nas respostas; a senha, nunca
        foreach (var r in respostasDaPartida)
        {
            Assert.False(senhas.Any(s => r.Texto.Contains(s, StringComparison.Ordinal)), "uma resposta traz a senha de uma conta inicial");
        }
    }

    [Then(@"^no banco fica só o resumo irreversível da senha, como na RNF-201$")]
    public static void SoOResumo() => throw new PendingStepException(SoNoBackend);

    // ── segredo ausente ou inválido (RNF-226) ──────────────────────────────
    [Given(@"^a aplicação de (homologação|produção) sem (a senha do administrador|a entrada do administrador|a senha do administrador qa ""qaadmin""|a senha da staff ""qastaff\.qains""|o código do participante ""QAINS-QAPART""|a lista de módulos iniciais das contas qa)$")]
    public void SemOSegredo(string ambiente, string segredo)
    {
        partida.KeyVaultCompleto(Sigla(ambiente));
        variavelAusente = segredo switch
        {
            "a senha do administrador" => ContasIniciais.Admin.VariavelSenha!,
            "a entrada do administrador" => ContasIniciais.Admin.VariavelEntrada,
            "o código do participante \"QAINS-QAPART\"" => ContasIniciais.Participante.VariavelEntrada,
            "a senha do administrador qa \"qaadmin\"" => ContasIniciais.QaAdmin.VariavelSenha!,
            "a lista de módulos iniciais das contas qa" => ContasIniciais.VariavelModulos,
            _ => ContasIniciais.Staff.VariavelSenha!,
        };
        partida.KeyVault[variavelAusente] = null;
        keyVaultDoCenario = true;
    }

    [When(@"^ela inicia$")]
    public async Task ElaInicia() => pronta = await partida.Iniciar();

    [Then(@"^para antes de aceitar o primeiro pedido$")]
    public void ParaAntes()
    {
        Assert.False(pronta || partida.AtendeuPedido,
            $"a aplicação de {partida.AmbienteDaPartida} aceitou pedidos (/health 200) sem {variavelAusente}");
        Assert.True(partida.CodigoDeSaida is not (null or 0), $"esperava código de saída diferente de 0, veio {partida.CodigoDeSaida}");
    }

    [Then(@"^o log diz o nome da variável que falta, nunca um valor$")]
    public void LogComONome()
    {
        Assert.True(partida.Log.Contains(variavelAusente, StringComparison.Ordinal),
            $"o log não diz {variavelAusente}. Log:\n{partida.Log}");
        SemValorNoLog();
    }

    [Given(@"^a senha ""(.+)"" no Key Vault para ""(.+)""$")]
    public void SenhaFraca(string senha, string entrada)
    {
        partida.KeyVaultCompleto("hml");
        variavelAusente = ContasIniciais.PorEntrada(entrada).VariavelSenha!;
        partida.KeyVault[variavelAusente] = senha;
        keyVaultDoCenario = true;
    }

    [Then(@"^para antes de aceitar o primeiro pedido, porque a senha não segue a RNF-202$")]
    public void ParaPelaSenhaFraca()
    {
        Assert.False(pronta || partida.AtendeuPedido,
            $"a aplicação aceitou pedidos com a senha fraca em {variavelAusente}");
        Assert.True(partida.CodigoDeSaida is not (null or 0), $"esperava código de saída diferente de 0, veio {partida.CodigoDeSaida}");
        Assert.True(partida.Log.Contains(variavelAusente, StringComparison.Ordinal),
            $"o log não diz qual variável tem a senha fora da RNF-202 ({variavelAusente}). Log:\n{partida.Log}");
        SemValorNoLog();
    }

    // nenhum valor do Key Vault aparece no log (RNF-225, RNF-226)
    void SemValorNoLog()
    {
        foreach (var (nome, valor) in partida.KeyVault)
        {
            if (valor is null) continue;
            Assert.False(partida.Log.Contains(valor, StringComparison.Ordinal), $"o log traz o valor de {nome}");
        }
    }

    // ── apoio ──────────────────────────────────────────────────────────────
    async Task HomologacaoPronta()
    {
        partida.KeyVaultCompleto("hml");
        keyVaultDoCenario = true;
        await partida.IniciarPronta("homologação");
        pronta = true;
    }

    void ExigirPronta()
    {
        if (!pronta) throw new InvalidOperationException($"a aplicação de {partida.AmbienteDaPartida} terminou com {partida.CodigoDeSaida} antes de atender. Log:\n{partida.Log}");
    }

    // quem concede as permissões de uma conta qa: o administrador qa para o
    // membro do time; a conta master da QAINS para a staff
    static ContaInicial QuemConcede(ContaInicial conta) =>
        conta.ContaId == ContasIniciais.ContaDoTime ? ContasIniciais.QaAdmin : ContasIniciais.Master;

    async Task<Resposta> PermissoesNaPartida(ContaInicial conta) =>
        Exigir(await partida.Api.Enviar("GET", $"/usuarios/{conta.Id}/permissoes", token: await TokenNaPartida(QuemConcede(conta))),
            200, $"GET /usuarios/{{id}}/permissoes de {conta.Nome}");

    async Task<Dictionary<string, string>> ModsNaPartida(ContaInicial conta) =>
        (await PermissoesNaPartida(conta)).Corpo!["mods"]!.AsObject().ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());

    // PUT substitui todos os módulos, com a versão lida antes
    async Task SalvarNaPartida(ContaInicial conta, Dictionary<string, string> mods)
    {
        var versao = (await PermissoesNaPartida(conta)).Corpo!["versao"]!.GetValue<int>();
        var corpo = new JsonObject { ["mods"] = new JsonObject(mods.Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value))) };
        Exigir(await partida.Api.Enviar("PUT", $"/usuarios/{conta.Id}/permissoes", corpo.ToJsonString(),
            token: await TokenNaPartida(QuemConcede(conta)), ifMatch: versao.ToString()), 200, $"preparo: PUT /usuarios/{{id}}/permissoes de {conta.Nome}");
    }

    async Task EstaAtiva(ContaInicial conta)
    {
        var veio = await partida.Api.Enviar("GET", $"/usuarios/{conta.Id}", token: await TokenNaPartida(QuemConcede(conta)));
        Respostas.Confere(veio, 200);
        Assert.Equal("ATIVO", veio.Corpo?["estado"]?.GetValue<string>());
    }

    static Dictionary<string, string> Concediveis(Resposta r) =>
        r.Corpo!["concediveis"]!.AsArray().ToDictionary(c => c!["mod"]!.GetValue<string>(), c => c!["abas"]!.GetValue<string>());

    async Task<Resposta> EntrarNoHub(string entrada, string senha) =>
        await mundo.Enviar("POST", "/auth/hub/entrar", CorpoDoHub(entrada, senha));

    Task<Resposta> EntrarNaPartida(ContaInicial conta) => partida.Entrar(conta);

    Task<string> TokenNaPartida(ContaInicial conta) => partida.Token(conta);

    static string CorpoDoHub(string entrada, string senha) => Partida.CorpoDoHub(entrada, senha);

    static string CorpoDoQuest(string codigo) => Partida.CorpoDoQuest(codigo);

    static string? Papel(Resposta r) =>
        r.Corpo?["tokenAcesso"]?.GetValue<string>() is { } token ? Jwt.Carga(token)["papel"]?.GetValue<string>() : null;
}
