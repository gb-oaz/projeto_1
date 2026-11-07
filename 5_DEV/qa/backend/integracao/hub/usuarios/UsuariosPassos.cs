using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos dos Cenários de API de 1_Requisitos/17_modulo-usuario.md (contas,
// staff, participantes e membros do time). Como os passos casam com as frases:
// comentário no topo de AutenticacaoPassos.cs e README.
//
// Reaproveitados das outras features: "envia POST /contas/… com:" (pedido com
// corpo, com o token de quem o Dado citou), "a conta master envia POST
// /usuarios/…", "envia GET …", "a escola descontrata a Presença" e os passos de
// resposta de suporte/Respostas.cs.
//
// Quem um pedido do cenário cria fica anotado no Mundo: depois de "envia POST
// /usuarios com:" criar a Maria, "usr-ebfga-maria" é ela. Valores gerados
// (senha, código de liberação, comprovante) valem pelo formato.
[Binding]
public sealed partial class UsuariosPassos(Mundo mundo, Partida partida)
{
    const string SoNoBackend = "conferido nos testes do backend: o qa não olha o banco";
    const string SemSala = "abrir e fechar uma sala não está no contrato";
    const string NaInfra = "segredo no Key Vault: conferido na infraestrutura, sem endereço na API";
    const string ForaDeEscopo = "fora de escopo: nada a testar";

    // a resposta do Quando, para os Então que fazem outros pedidos antes de lê-la
    Resposta acao = new(0, "", []);
    // o alvo e o que ele era antes do Quando ("nada muda")
    string alvo = "";
    JsonObject? antes;
    string loginAntes = "", codigo = "";
    // a sigla do último POST /contas do cenário (RN-179)
    string siglaEnviada = "";
    Pessoa? leitor;
    Dictionary<string, string> modsAntes = [];
    readonly List<string> senhas = [];
    readonly List<Resposta> respostas = [];
    DateTimeOffset primeiroEnvio;
    // tempo de sessão (RN-183, RN-243)
    string tempoCaminho = "";
    string[] tempoCampos = [];
    Pessoa? tempoPor;
    JsonObject? tempoAntes;

    // ── contas (RF-104, RN-164, RN-179, RN-242) ────────────────────────────
    [When(@"^envia POST (/\S+), sem login, com:$")]
    public async Task SemLogin(string caminho, string json)
    {
        if (caminho == "/contas/esqueci-senha-master") await mundo.Conta();
        acao = await mundo.Enviar("POST", caminho, await mundo.Traduzir(json));
    }

    [Then(@"^POST /auth/hub/confirmar-senha com o comprovante abre a sessão da conta master$")]
    public async Task ConfirmaAConta()
    {
        var r = await mundo.Enviar("POST", "/auth/hub/confirmar-senha",
            new JsonObject { ["comprovante"] = Texto(acao, "comprovante") }.ToJsonString());
        Respostas.Confere(r, 200);
        Assert.Equal("MASTER", Jwt.Carga(Texto(r, "tokenAcesso"))["papel"]?.GetValue<string>());
    }

    [Given(@"^uma conta com a sigla ""EBFGA""$")]
    [Given(@"^a sigla ""EBFGA"" já usada$")]
    public Task SiglaUsada() => mundo.Conta();

    [When(@"^envia POST /contas com a sigla ""(\w+)""$")]
    public async Task ContaComSigla(string sigla)
    {
        siglaEnviada = sigla;
        await mundo.Conta();
        await NovaConta("INSTITUICAO", sigla, $"dominio_{sigla.ToLowerInvariant()}", Cnpj(20000001));
    }

    [When(@"^envia POST /contas com o domínio ""(\w+)"", já usado$")]
    public async Task ContaComDominio(string dominio)
    {
        await mundo.Conta();
        await NovaConta("INSTITUICAO", "EBFGB", dominio, Cnpj(20000002));
    }

    [When(@"^envia POST /contas com o tipo ""(\w+)""$")]
    public Task ContaDoTipo(string tipo) => NovaConta(tipo, "NOVAC", "nova_conta", Cnpj(20000003));

    [When(@"^envia POST /contas com o tipo ""(\w+)"" e o documento (?:CNPJ|CPF) (\S+)$")]
    public Task ContaComDocumento(string tipo, string documento) => NovaConta(tipo, "NOVAC", "nova_conta", documento);

    async Task NovaConta(string tipo, string sigla, string dominio, string documento) =>
        acao = await mundo.Enviar("POST", "/contas", ContaNova(tipo, tipo == "AUTONOMO" ? "Pedro Reis" : "Escola Nova", documento, dominio, sigla));

    // a sigla em minúsculas é gravada em maiúsculas (RN-179): conferida pela
    // leitura da conta na API, nunca pelo banco
    [Then(@"^a resposta é 201, conta ""(.+)"" com a sigla ""(\w+)""$")]
    public async Task ContaCriadaComASigla(string id, string sigla)
    {
        Respostas.Confere(mundo.Ultima, 201);
        var r = Exigir(await mundo.EnviarComo(mundo.Admin(), "GET", $"/contas/{id}"), 200, $"GET /contas/{id}");
        Assert.Equal(sigla, r.Corpo?["sigla"]?.GetValue<string>());
    }

    [Then(@"^a resposta é 409 SIGLA_EM_USO e nenhuma conta é criada$")]
    public async Task NenhumaConta()
    {
        Respostas.Confere(mundo.Ultima, 409, "SIGLA_EM_USO");
        var r = Exigir(await mundo.EnviarComo(mundo.Admin(), "GET", "/contas?busca=EBFGA"), 200, "GET /contas");
        Assert.Equal(1, r.Corpo?["total"]?.GetValue<int>());
    }

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com o campo ""(\w+)""$")]
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+), campo ""(\w+)""$")]
    public void ComOCampo(int status, string codigo, string campo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        Assert.Equal(campo, mundo.Ultima.Corpo?["campo"]?.GetValue<string>());
    }

    // o motivo da tabela é para quem lê; a resposta é o status e o código
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+), (?:reservada ao time|precisa de 5 letras)$")]
    public void SiglaInvalida(int status, string codigo) => Respostas.Confere(mundo.Ultima, status, codigo);

    // as siglas "QA…" são das contas qa de homologação: a recusa é em produção,
    // numa instância própria (suporte/Partida.cs), com o mesmo pedido
    [Then(@"^a resposta é em produção, (\d{3}) ([A-Z_]+), reservada ao time$")]
    public async Task SiglaQaEmProducao(int status, string codigo)
    {
        partida.KeyVaultCompleto("prd");
        await partida.IniciarPronta("produção");
        var r = await partida.Api.Enviar("POST", "/contas",
            ContaNova("INSTITUICAO", "Escola Nova", Cnpj(20000001), $"dominio_{siglaEnviada.ToLowerInvariant()}", siglaEnviada));
        Respostas.Confere(r, status, codigo);
    }

    [When(@"^envia GET /contas/sigla-sugerida\?nome=(.+)$")]
    public Task SiglaSugerida(string nome) =>
        mundo.Enviar("GET", $"/contas/sigla-sugerida?nome={Uri.EscapeDataString(nome)}");

    // ── estado da conta (RF-113, RN-173, RN-174, RN-175) ──────────────────
    // a Maria e o Lucas existem antes de a escola sair do ar, para tentarem entrar depois
    [BeforeScenario("RF-113", Order = 20000)]
    public async Task MariaELucas()
    {
        await mundo.Maria();
        await mundo.Lucas();
    }

    [Then(@"^POST /auth/hub/entrar da Maria e POST /auth/quest/entrar do Lucas recebem (\d{3})$")]
    public async Task NinguemEntra(int status)
    {
        var maria = await mundo.Maria();
        Respostas.Confere(await mundo.Enviar("POST", "/auth/hub/entrar", EntradaDe(maria)), status);
        Respostas.Confere(await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" }), status);
    }

    [Then(@"^GET (/contas\?estado=\w+) traz a EBFGA$")]
    public async Task ListaTrazAConta(string caminho)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.SujeitoOu(caminho), "GET", caminho), 200, caminho);
        Assert.Contains(await mundo.Conta(), Ids(r));
    }

    [When(@"^o Caio envia POST (/contas/\S+/estado) com o estado ""(\w+)""$")]
    public async Task CaioReativa(string caminho, string estado)
    {
        var caio = await CaioComUsuarios();
        modsAntes = Mods(Jwt.Carga((await mundo.Entrar(await mundo.Maria(), Navegador)).Acesso));
        var conta = await mundo.Traduzir(caminho);
        Exigir(await mundo.EnviarComo(caio, "POST", conta, """{ "estado": "DESATIVADA" }"""), 200, "POST /contas/{id}/estado");
        acao = await mundo.EnviarComo(caio, "POST", conta, new JsonObject { ["estado"] = estado }.ToJsonString());
    }

    [Then(@"^a resposta é 200 e a Maria e o Lucas voltam a entrar com as mesmas permissões$")]
    public async Task VoltamAEntrar()
    {
        Respostas.Confere(acao, 200);
        var sessao = await mundo.Entrar(await mundo.Maria(), Navegador);
        Assert.Equal(modsAntes, Mods(Jwt.Carga(sessao.Acesso)));
        Respostas.Confere(await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" }), 200);
    }

    [When(@"^o time deboaz envia POST (/contas/\S+/estado) com ""(\w+)""$")]
    public async Task TimeMudaOEstado(string caminho, string estado) =>
        acao = await mundo.EnviarComo(mundo.Admin(), "POST", await mundo.Traduzir(caminho), new JsonObject { ["estado"] = estado }.ToJsonString());

    // o motivo é a regra do estado; a API mostra o estado gravado
    [Then(@"^a resposta é 200 e o estado significa .+$")]
    public void EstadoGravado()
    {
        Respostas.Confere(acao, 200);
        var pedido = JsonNode.Parse(mundo.UltimoPedido.Corpo!)!["estado"]!.GetValue<string>();
        Assert.Equal(pedido, acao.Corpo?["estado"]?.GetValue<string>());
    }

    [Given(@"^a conta da EBFGA encerrada$")]
    public Task Encerrada() => MudarEstado("ENCERRADA");

    [Then(@"^os dados da escola são apagados em cascata$")]
    [Then(@"^fica só o registro mínimo para histórico, auditoria e cobrança$")]
    [Then(@"^esse registro é anonimizado depois de 5 anos$")]
    public static void SoNoBanco() => throw new PendingStepException(SoNoBackend);

    // RN-175: todos existem antes; depois a escola, ou o Paulo, sai do ar
    [Given(@"^(a EBFGA suspensa|a EBFGA desativada|a EBFGA encerrada|o Paulo desativado)$")]
    public async Task ForaDoAr(string situacao)
    {
        await mundo.Maria();
        await mundo.Lucas();
        var paulo = await mundo.Paulo();
        switch (situacao)
        {
            case "o Paulo desativado":
                Exigir(await mundo.EnviarComo(await mundo.Master(), "POST", $"/usuarios/{paulo.Id}/desativar"), 204, "POST /usuarios/{id}/desativar");
                break;
            default:
                await MudarEstado(situacao.Split(' ')[^1] switch { "suspensa" => "SUSPENSA", "desativada" => "DESATIVADA", _ => "ENCERRADA" });
                break;
        }
    }

    // a Carla usa o passo de RN-232 (Autorização), que também não exige 200
    [When(@"^(a staff Maria|o Lucas|o Paulo) envia POST (\S+)$")]
    public async Task TentaEntrar(string quem, string _)
    {
        if (quem == "o Lucas") await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" });
        else await mundo.Enviar("POST", "/auth/hub/entrar", EntradaDe(quem == "a staff Maria" ? await mundo.Maria() : await mundo.Paulo()));
    }

    async Task MudarEstado(string estado) =>
        Exigir(await mundo.EnviarComo(mundo.Admin(), "POST", $"/contas/{await mundo.Conta()}/estado",
            new JsonObject { ["estado"] = estado }.ToJsonString()), 200, $"POST /contas/{{id}}/estado {estado}");

    [Then(@"^a resposta é 200 com o estado ""(\w+)""$")]
    public void ComOEstado(string estado)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(estado, Texto(mundo.Ultima, "estado"));
    }

    // ── login (RN-180) ─────────────────────────────────────────────────────
    [When(@"^envia POST /auth/hub/entrar com a entrada ""(.+)""$")]
    public async Task EntradaEmMaiusculas(string entrada) =>
        await mundo.Enviar("POST", "/auth/hub/entrar", EntradaDe(entrada, (await mundo.MariaDaEejsa()).Senha));

    [Then(@"^a resposta é 200 e o token é de ""(.+)""$")]
    public async Task TokenDe(string _)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal((await mundo.MariaDaEejsa()).Id, Jwt.Carga(Texto(mundo.Ultima, "tokenAcesso"))["sub"]?.GetValue<string>());
    }

    [When(@"^(a conta master da EBFGA|a staff Maria, da EEJSA|o membro do time Gustavo) é criado$")]
    public async Task Criado(string quem) => loginAntes = quem switch
    {
        "a conta master da EBFGA" => (await mundo.Master()).Entrada,
        "a staff Maria, da EEJSA" => (await mundo.MariaDaEejsa()).Entrada,
        _ => (await mundo.Membro("Gustavo Souza", "gustavo")).Entrada,
    };

    [Then(@"^o login é ""([^""]+)""$")]
    public void OLogin(string login) => Assert.Equal(login, loginAntes);

    [Then(@"^GET (\S+) recebe (\d{3})$")]
    public async Task GetRecebe(string caminho, int status) =>
        Respostas.Confere(await mundo.EnviarComo(await mundo.SujeitoOu(caminho), "GET", await mundo.Traduzir(caminho)), status);

    // ── tokens de quem pede ────────────────────────────────────────────────
    [Given(@"^o token d[ao] (conta master da EBFGA|staff João|membro do time Caio|administrador do deboaz|Ana Lima, do time deboaz), com ""(MOD-\d+)"" com ""(\w+)""$")]
    public Task TokenCom(string quem, string mod, string abas) => TokenCom(quem, mod, abas, false);

    // o Paulo é criado pelo João, para ficar abaixo dele (RN-169)
    [Given(@"^o token do (staff João), com ""(MOD-\d+)"" com ""(\w+)"", superior do Paulo$")]
    public Task TokenDoSuperior(string quem, string mod, string abas) => TokenCom(quem, mod, abas, true);

    async Task TokenCom(string quem, string mod, string abas, bool superior)
    {
        var pessoa = quem switch
        {
            "conta master da EBFGA" => await mundo.Master(),
            "administrador do deboaz" => mundo.Admin(),
            "staff João" => await mundo.Joao(),
            "membro do time Caio" => await mundo.Caio(),
            _ => await mundo.Membro("Ana Lima", "analima"),
        };
        // a conta master e o administrador já têm tudo; os outros recebem a aba
        if (quem is "staff João" or "membro do time Caio" or "Ana Lima, do time deboaz") await mundo.Conceder(pessoa, new() { [mod] = abas });
        if (superior) await mundo.SessaoDe(await mundo.Paulo(criador: pessoa), Navegador);
        await mundo.TokenDe(pessoa);
        mundo.Sujeito = pessoa;
    }

    [Given(@"^o token do educador autônomo Pedro$")]
    public async Task TokenDoPedro() => mundo.Sujeito = await mundo.Pedro();

    // ── cadastro (RF-106, RF-108, RN-164, RN-188, RN-246) ─────────────────
    [When(@"^envia POST /usuarios com o tipo ""(\w+)""$")]
    public async Task CriaDoTipo(string tipo)
    {
        var corpo = new JsonObject { ["tipo"] = tipo, ["nome"] = "Teste Recusado", ["nomeCurto"] = "recusado" };
        if (tipo == "STAFF") corpo["matricula"] = null;
        await mundo.EnviarComo(mundo.Sujeito!, "POST", "/usuarios", corpo.ToJsonString());
    }

    [When(@"^a conta master envia POST /usuarios com o nome curto ""(\w+)"", já usado na EBFGA$")]
    public async Task NomeCurtoUsado(string nomeCurto)
    {
        await mundo.Maria();
        await mundo.EnviarComo(await mundo.Master(), "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF",
            ["nome"] = "Maria Silva",
            ["nomeCurto"] = nomeCurto,
            ["matricula"] = null,
        }.ToJsonString());
    }

    [Then(@"^a resposta é 201 com o login ""([A-Z]+-[A-Z]+-)"" seguido de 4 números, sem senha$")]
    public void LoginDoParticipante(string comeco)
    {
        Respostas.Confere(mundo.Ultima, 201);
        Assert.Matches($"^{Regex.Escape(comeco)}\\d{{4}}$", Texto(mundo.Ultima, "login"));
        Assert.Null(mundo.Ultima.Corpo?["senha"]);
    }

    [Then(@"^POST /auth/quest/entrar com esse código abre o perfil do Lucas$")]
    public async Task EntraComOCodigo()
    {
        var (login, id) = (Texto(mundo.Ultima, "login"), Texto(mundo.Ultima, "id"));
        var r = await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" }, login);
        Respostas.Confere(r, 200);
        Assert.Equal(id, Jwt.Carga(Texto(r, "tokenAcesso"))["sub"]?.GetValue<string>());
    }

    [When(@"^a conta master envia POST /usuarios (do Lucas|da Maria) com a matrícula (?:null|""\d+"")$")]
    public async Task CriaComMatricula(string quem)
    {
        if (quem == "do Lucas") await mundo.Lucas();
        else await mundo.Maria();
    }

    [Then(@"^GET /usuarios/\{id\} do Lucas traz uma matrícula de 8 números, única na escola$")]
    public async Task MatriculaGerada()
    {
        var master = await mundo.Master();
        var r = Exigir(await mundo.EnviarComo(master, "GET", $"/usuarios/{(await mundo.Lucas()).Id}"), 200, "GET /usuarios/{id}");
        var matricula = Texto(r, "matricula");
        Assert.Matches(@"^\d{8}$", matricula);
        var busca = Exigir(await mundo.EnviarComo(master, "GET", $"/usuarios?tipo=PARTICIPANTE&busca={matricula}"), 200, "GET /usuarios");
        Assert.Equal(1, busca.Corpo?["total"]?.GetValue<int>());
    }

    [Then(@"^GET (/usuarios/\S+) traz a matrícula ""(\d+)""$")]
    public async Task ComAMatricula(string caminho, string matricula) =>
        Assert.Equal(matricula, Texto(await Ler(caminho), "matricula"));

    [When(@"^o administrador envia POST /usuarios com o tipo ""MEMBRO_TIME"" para o Caio Reis$")]
    public Task CriaOCaio() => mundo.EnviarComo(mundo.Admin(), "POST", "/usuarios", new JsonObject
    {
        ["tipo"] = "MEMBRO_TIME",
        ["nome"] = "Caio Reis",
        ["nomeCurto"] = "caio",
    }.ToJsonString());

    [Then(@"^GET (/usuarios/\S+/permissoes) traz mods vazio$")]
    public async Task ModsVazio(string caminho)
    {
        var r = Exigir(await mundo.EnviarComo(mundo.Admin(), "GET", await mundo.Traduzir(caminho)), 200, caminho);
        Assert.Empty(r.Corpo!["mods"]!.AsObject());
    }

    // ── senha e código de liberação (RF-109, RF-110, RN-170, RN-171, RN-181, RN-182) ──
    [Given(@"^o staff ""gustavo\.ebfga@deboaz\.com"" pendente, com o código de liberação ""482913""$")]
    public Task GustavoPendente() => mundo.Gustavo();

    [Then(@"^o mesmo pedido, enviado de novo, recebe 401 CODIGO_INVALIDO$")]
    public async Task MesmoPedido()
    {
        var (metodo, caminho, corpo) = mundo.UltimoPedido;
        Respostas.Confere(await mundo.Enviar(metodo, caminho, corpo), 401, "CODIGO_INVALIDO");
    }

    [When(@"^envia POST /usuarios/gerar-senha com o código ""(\d+)""$")]
    public async Task CodigoErrado(string errado)
    {
        var (gustavo, _) = await mundo.Gustavo();
        await GerarSenha(gustavo.Entrada, errado);
    }

    // a tentativa contada aparece no bloqueio: com mais 3 erradas, a quinta recebe 429 (RNF-204)
    [Then(@"^a resposta é 401 CODIGO_INVALIDO, sem a senha, e conta uma tentativa errada$")]
    public async Task ContaATentativa()
    {
        Respostas.Confere(mundo.Ultima, 401, "CODIGO_INVALIDO");
        Assert.Null(mundo.Ultima.Corpo?["senha"]);
        var entrada = JsonNode.Parse(mundo.UltimoPedido.Corpo!)!["entrada"]!.GetValue<string>();
        for (var i = 0; i < 3; i++) Respostas.Confere(await GerarSenha(entrada, "111111"), 401, "CODIGO_INVALIDO");
        Respostas.Confere(await GerarSenha(entrada, "111111"), 429, "ENTRADA_BLOQUEADA");
    }

    [Given(@"^(\d) códigos errados seguidos para ""gustavo\.ebfga@deboaz\.com""$")]
    public async Task CodigosErrados(int quantos)
    {
        var (gustavo, _) = await mundo.Gustavo();
        for (var i = 0; i < quantos; i++) Exigir(await GerarSenha(gustavo.Entrada, "111111"), 401, "código errado");
    }

    [When(@"^o quinto chega a POST /usuarios/gerar-senha$")]
    public async Task QuintoCodigo() => await GerarSenha((await mundo.Gustavo()).Pessoa.Entrada, "111111");

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com tentarEm$")]
    public void ComTentarEm(int status, string codigo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        Assert.True(DateTimeOffset.Parse(Texto(mundo.Ultima, "tentarEm")) > mundo.HoraDoPedido, $"tentarEm no passado: {mundo.Ultima}");
    }

    [When(@"^envia POST (/usuarios/\S+)$")]
    public async Task SujeitoEnvia(string caminho) =>
        acao = await mundo.EnviarComo(await mundo.SujeitoOu(caminho), "POST", await mundo.Traduzir(caminho));

    [When(@"^o administrador envia POST (/usuarios/\S+)$")]
    public async Task AdminEnvia(string caminho) =>
        acao = await mundo.EnviarComo(mundo.Admin(), "POST", await mundo.Traduzir(caminho));

    [Then(@"^a resposta é 201 com codigoLiberacao e venceEm$")]
    [Then(@"^a resposta é 201 com o código de liberação para entregar ao Caio$")]
    public void CodigoDeLiberacao()
    {
        Respostas.Confere(acao, 201);
        Assert.Matches(@"^\d{6}$", Texto(acao, "codigoLiberacao"));
        Assert.True(DateTimeOffset.TryParse(Texto(acao, "venceEm"), out _), $"venceEm não é data e hora: {acao}");
    }

    [Then(@"^POST /auth/hub/entrar com a senha antiga do Paulo recebe 401 ENTRADA_INVALIDA$")]
    public async Task SenhaAntiga() =>
        Respostas.Confere(await mundo.Enviar("POST", "/auth/hub/entrar", EntradaDe(await mundo.Paulo())), 401, "ENTRADA_INVALIDA");

    [Then(@"^os tokens das sessões abertas do Paulo recebem 401$")]
    [Then(@"^o token do app do Paulo recebe 401 no próximo pedido$")]
    public async Task SessoesDoPaulo()
    {
        var paulo = await mundo.Paulo();
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa == paulo).ToList())
        {
            Assert.Equal(401, await mundo.ProximoPedido(sessao.Acesso));
        }
    }

    [When(@"^o João envia POST (\S+), de um par dele$")]
    public async Task JoaoPedeDoPar(string caminho)
    {
        var joao = await mundo.Joao();
        await mundo.Rui();
        await mundo.Conceder(joao, new() { ["MOD-03"] = "CU" });
        await mundo.EnviarComo(joao, "POST", await mundo.Traduzir(caminho));
    }

    [When(@"^a conta master envia POST /usuarios para criar a Maria$")]
    public async Task CriaAMaria()
    {
        acao = await mundo.EnviarComo(await mundo.Master(), "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF",
            ["nome"] = "Maria Souza",
            ["nomeCurto"] = "maria",
            ["matricula"] = "20260087",
        }.ToJsonString());
        codigo = acao.Corpo?["codigoLiberacao"]?.GetValue<string>() ?? "";
    }

    [Then(@"^a resposta traz o código de liberação e nenhum campo de senha$")]
    [Then(@"^a resposta 201 traz o código de liberação ""482913"" uma única vez$")]
    public void TrazOCodigo()
    {
        Respostas.Confere(acao, 201);
        Assert.Matches(@"^\d{6}$", codigo);
        Assert.Empty(CamposDeSenha(acao.Corpo));
    }

    [Then(@"^GET (/usuarios/\S+) não traz senha nem resumo da senha$")]
    public async Task SemSenha(string caminho) => Assert.Empty(CamposDeSenha((await Ler(caminho)).Corpo));

    [Then(@"^GET (/usuarios/\S+) não traz o código$")]
    public async Task SemCodigo(string caminho)
    {
        var r = await Ler(caminho);
        Assert.DoesNotContain(codigo, r.Texto);
        Assert.DoesNotContain("codigoLiberacao", r.Texto);
    }

    [Then(@"^a senha só sai em POST /usuarios/gerar-senha, para quem tem o código$")]
    public async Task SoComOCodigo()
    {
        var maria = await mundo.Maria();
        Respostas.Confere(await GerarSenha(maria.Entrada, codigo == "111111" ? "222222" : "111111"), 401, "CODIGO_INVALIDO");
        Respostas.Confere(await GerarSenha(maria.Entrada, codigo), 200);
        Assert.NotNull(mundo.Ultima.Corpo?["senha"]);
    }

    [Then(@"^POST /usuarios/gerar-senha só devolve a senha com ""(.+)"" e ""482913""$")]
    public async Task SoComAEntrada(string entrada)
    {
        Respostas.Confere(await GerarSenha("joao.ebfga@deboaz.com", codigo), 401, "CODIGO_INVALIDO");
        Respostas.Confere(await GerarSenha(entrada, codigo), 200);
        Assert.NotNull(mundo.Ultima.Corpo?["senha"]);
    }

    [Then(@"^a senha do administrador do deboaz fica no Key Vault, fora de qualquer endereço$")]
    [Then(@"^a entrada e a senha do administrador do deboaz ficam num segredo do Key Vault$")]
    [Then(@"^só são definidas e trocadas ali, sem endereço que as mude$")]
    [Then(@"^o sistema guarda só o resumo irreversível da senha$")]
    public static void KeyVault() => throw new PendingStepException(NaInfra);

    // a senha de um staff criado com esse nome
    [When(@"^POST /usuarios/gerar-senha gera a senha de ""(.+)""$")]
    public async Task SenhaDe(string nome) => senhas.Add((await SenhaNova(nome)).Pessoa.Senha);

    // o formato sai do exemplo: as mesmas letras, um caractere especial e 6 números
    [Then(@"^a senha tem o formato de ""(.+)""$")]
    public void FormatoDaSenha(string exemplo)
    {
        var partes = PartesDaSenha().Match(exemplo);
        Assert.Matches($"^{partes.Groups[1].Value}[^A-Za-z0-9]{partes.Groups[2].Value}\\d{{6}}$", senhas[^1]);
    }

    [When(@"^duas gerações de senha saem para o Gustavo Boaz de Moura Souza$")]
    public async Task DuasGeracoes()
    {
        var (gustavo, comprovante) = await SenhaNova("Gustavo Boaz de Moura Souza");
        senhas.Add(gustavo.Senha);
        Exigir(await mundo.Enviar("POST", "/auth/hub/confirmar-senha", new JsonObject { ["comprovante"] = comprovante }.ToJsonString()),
            200, "POST /auth/hub/confirmar-senha");
        var nova = Exigir(await mundo.EnviarComo(await mundo.Master(), "POST", $"/usuarios/{gustavo.Id}/nova-geracao-senha"),
            201, "POST /usuarios/{id}/nova-geracao-senha");
        var r = Exigir(await GerarSenha(gustavo.Entrada, Texto(nova, "codigoLiberacao")), 200, "POST /usuarios/gerar-senha");
        senhas.Add(Texto(r, "senha"));
    }

    [Then(@"^as duas começam com ""(\w+)"" e têm ""(\w+)""$")]
    public void ComecoIgual(string comeco, string palavra) =>
        Assert.All(senhas, s => Assert.True(s.StartsWith(comeco, StringComparison.Ordinal) && s.Contains(palavra), $"senha fora do formato: {s}"));

    // o caractere especial e os 6 números juntos repetidos são 1 chance em milhões
    [Then(@"^o caractere especial e os 6 números são sorteados de novo$")]
    public void SorteadosDeNovo() => Assert.NotEqual(Sorteado(senhas[0]), Sorteado(senhas[1]));

    [Given(@"^o código de liberação da Maria pedido há (\d+) h$")]
    public async Task CodigoAntigo(int horas)
    {
        var (maria, codigoDela) = await mundo.Pendente("Maria Souza", "maria", "20260087");
        codigo = codigoDela;
        await Estado.PedidoHa(maria.Id, TimeSpan.FromHours(horas));
    }

    [When(@"^ela envia POST /usuarios/gerar-senha com o código$")]
    public async Task ElaGeraASenha() => await GerarSenha((await mundo.Maria()).Entrada, codigo);

    [Then(@"^a resposta é 200 com a senha$")]
    public void ComASenha()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.NotNull(mundo.Ultima.Corpo?["senha"]);
    }

    // ── desativar e reativar (RF-112, RN-169, RN-174) ─────────────────────
    [Given(@"^o Paulo com uma sessão no app$")]
    public async Task PauloNoApp() => await mundo.SessaoDe(await mundo.Paulo(), App);

    [Then(@"^GET (/usuarios\?tipo=STAFF&estado=DESATIVADO) traz o Paulo$")]
    public async Task ListaTrazOPaulo(string caminho)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", caminho), 200, caminho);
        Assert.Contains((await mundo.Paulo()).Id, Ids(r));
    }

    // reativar quem foi desativado, guardando as permissões dele
    [When(@"^a conta master envia POST (/usuarios/usr-ebfga-paulo/reativar)$")]
    public async Task Reativa(string caminho)
    {
        var paulo = await mundo.Paulo();
        await mundo.Conceder(paulo, new() { ["MOD-04"] = "U" });
        modsAntes = Mods((await mundo.Permissoes(paulo))["mods"]!);
        var master = await mundo.Master();
        Exigir(await mundo.EnviarComo(master, "POST", $"/usuarios/{paulo.Id}/desativar"), 204, "POST /usuarios/{id}/desativar");
        acao = await mundo.EnviarComo(master, "POST", await mundo.Traduzir(caminho));
    }

    [Then(@"^a resposta é 204 e o Paulo volta a ATIVO com as mesmas permissões$")]
    public async Task VoltaAtivo()
    {
        Respostas.Confere(acao, 204);
        var paulo = await mundo.Paulo();
        Assert.Equal("ATIVO", Texto(await Ler($"/usuarios/{paulo.Id}"), "estado"));
        Assert.Equal(modsAntes, Mods((await mundo.Permissoes(paulo))["mods"]!));
    }

    [When(@"^a conta master envia POST (/usuarios/\S+/desativar) de novo$")]
    public async Task DesativaDeNovo(string caminho)
    {
        var master = await mundo.Master();
        var alvo = await mundo.Traduzir(caminho);
        Exigir(await mundo.EnviarComo(master, "POST", alvo), 204, "POST /usuarios/{id}/desativar");
        await mundo.EnviarComo(master, "POST", alvo);
    }

    [Then(@"^GET (/usuarios/\S+) continua com 200 e o estado ""(\w+)""$")]
    public async Task ContinuaNaLista(string caminho, string estado) => Assert.Equal(estado, Texto(await Ler(caminho), "estado"));

    // quem age sobre quem (RN-169): o João tem Administrar usuários; a Ana foi criada por ele
    // o qaadmin e o admin: os dois administradores, um sobre o outro (RN-169)
    [When(@"^(o administrador|a conta master|o João, com Adm\. usuários|a staff Ana|o qaadmin|o admin) envia POST /usuarios/\{id\}/desativar de (o membro Caio|o João, com Adm\. usuários|a staff Ana|o Lucas|o Rui, também com Adm\.|o João|o qaadmin|o admin)$")]
    public async Task Desativa(string quem, string de)
    {
        var joao = await mundo.Joao();
        if (quem.StartsWith("o João") || de.StartsWith("o João")) await mundo.Conceder(joao, new() { ["MOD-03"] = "CU" });
        var por = quem switch
        {
            "o administrador" => mundo.Admin(),
            "a conta master" => await mundo.Master(),
            "a staff Ana" => await mundo.Ana(),
            "o qaadmin" => mundo.QaAdmin(),
            "o admin" => mundo.AdminReal(),
            _ => joao,
        };
        var alvoDe = de switch
        {
            "o membro Caio" => await mundo.Caio(),
            "a staff Ana" => await mundo.Ana(),
            "o Lucas" => await mundo.Lucas(),
            "o Rui, também com Adm." => await mundo.Rui(),
            "o qaadmin" => mundo.QaAdmin(),
            "o admin" => mundo.AdminReal(),
            _ => joao,
        };
        await mundo.EnviarComo(por, "POST", $"/usuarios/{alvoDe.Id}/desativar");
    }

    // administrador sobre outro administrador (RN-169): o backend de API_URL,
    // em homologação, com as entradas e senhas das USUARIO_*
    Pessoa? quemAge;
    string estadoAntes = "";

    [Given(@"^o token de ""(.+)"" em homologação$")]
    public async Task TokenEmHomologacao(string entrada)
    {
        quemAge = PessoaDe(ContasIniciais.PorEntrada(entrada));
        await mundo.SessaoDe(quemAge, Navegador);
    }

    [When(@"^envia POST /usuarios/\{id\}/(reativar|nova-geracao-senha) de ""(.+)""$")]
    public async Task AdminSobreAdmin(string acaoNoAlvo, string entrada)
    {
        var alvoDe = ContasIniciais.PorEntrada(entrada);
        estadoAntes = await EstadoPeloProprio(alvoDe, "preparo: ");
        alvo = alvoDe.Id;
        acao = await mundo.EnviarComo(quemAge!, "POST", $"/usuarios/{alvoDe.Id}/{acaoNoAlvo}");
    }

    [Then(@"^""(.+)"" continua com o mesmo estado e a mesma senha$")]
    public async Task MesmoEstadoEMesmaSenha(string entrada)
    {
        Assert.Equal(estadoAntes, await EstadoPeloProprio(ContasIniciais.PorEntrada(entrada), ""));
    }

    // o estado do alvo lido com o token dele mesmo, entrando com a senha do Key
    // Vault: quem age não lê o outro administrador (RN-169, 403 ACESSO_NEGADO)
    async Task<string> EstadoPeloProprio(ContaInicial conta, string prefixo)
    {
        var alvoDe = PessoaDe(conta);
        var entrou = await mundo.Enviar("POST", "/auth/hub/entrar", Partida.CorpoDoHub(alvoDe.Entrada, alvoDe.Senha));
        Assert.True(entrou.Status == 200, $"{prefixo}{alvoDe.Nome} não entra com a senha do Key Vault: {entrou}");
        var token = Texto(entrou, "tokenAcesso");
        return Texto(Exigir(await mundo.Enviar("GET", $"/usuarios/{conta.Id}", token: token), 200,
            $"{prefixo}GET /usuarios/{{id}} de {alvoDe.Nome}, com o token dele"), "estado");
    }

    // ── edição (RF-153, RN-244) ────────────────────────────────────────────
    [When(@"^a conta master envia PATCH (\S+) com:$")]
    public async Task Edita(string caminho, string json) =>
        acao = await mundo.EnviarComo(await mundo.Master(), "PATCH", await mundo.Traduzir(caminho), await mundo.Traduzir(json));

    [Then(@"^a resposta é (\d{3}) com o login ""(.+)""$")]
    public void ComOLogin(int status, string login)
    {
        Respostas.Confere(acao, status);
        Assert.Equal(login, Texto(acao, "login"));
    }

    [Then(@"^o token do navegador da Maria recebe 401 no próximo pedido$")]
    public async Task TokenDaMariaAcaba()
    {
        var maria = await mundo.Maria();
        var sessao = mundo.Sessoes.Last(s => s.Pessoa == maria && s.Navegador);
        Assert.Equal(401, await mundo.ProximoPedido(sessao.Acesso));
    }

    [Then(@"^POST /auth/hub/entrar com o login novo e a mesma senha recebe 200$")]
    public async Task EntraComOLoginNovo()
    {
        var maria = await mundo.Maria();
        var r = await mundo.Enviar("POST", "/auth/hub/entrar", EntradaDe(Texto(acao, "login"), maria.Senha));
        Respostas.Confere(r, 200);
    }

    [When(@"^a conta master envia PATCH (\S+) com a mãe e o pai em responsaveis$")]
    public async Task EditaResponsaveis(string caminho)
    {
        loginAntes = await mundo.CodigoDoLucas();
        alvo = await mundo.Traduzir(caminho);
        acao = await mundo.EnviarComo(await mundo.Master(), "PATCH", alvo, """
            {
              "responsaveis": [
                {
                  "nome": "Carla Andrade",
                  "parentesco": "MAE"
                },
                {
                  "nome": "Paulo Andrade",
                  "parentesco": "PAI"
                }
              ]
            }
            """);
    }

    [Then(@"^GET (/usuarios/\S+) traz os dois responsáveis e o mesmo login$")]
    public async Task DoisResponsaveis(string caminho)
    {
        Respostas.Confere(acao, 200);
        var r = await Ler(caminho);
        Comparacao.Confere(JsonNode.Parse("""
            [
              {
                "nome": "Carla Andrade",
                "parentesco": "MAE"
              },
              {
                "nome": "Paulo Andrade",
                "parentesco": "PAI"
              }
            ]
            """), r.Corpo?["responsaveis"]);
        Assert.Equal(loginAntes, Texto(r, "login"));
    }

    [When(@"^a conta master envia PATCH (\S+) com (o nome curto|a matrícula) ""(\w+)""(?:, da Maria)?$")]
    public async Task EditaCampo(string caminho, string campo, string valor)
    {
        await mundo.Maria();
        alvo = await mundo.Traduzir(caminho);
        var lido = await Ler(alvo);
        antes = lido.Corpo?.AsObject();
        loginAntes = antes?["login"]?.GetValue<string>() ?? "";
        acao = await mundo.EnviarComo(await mundo.Master(), "PATCH", alvo,
            new JsonObject { [campo == "o nome curto" ? "nomeCurto" : "matricula"] = valor }.ToJsonString());
    }

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) e nada muda$")]
    public async Task NadaMuda(int status, string codigo)
    {
        Respostas.Confere(acao, status, codigo);
        var agora = (await Ler(alvo)).Corpo!;
        foreach (var campo in new[] { "nomeCurto", "login", "matricula" }) Assert.Equal(antes?[campo]?.ToJsonString(), agora[campo]?.ToJsonString());
    }

    [Then(@"^a resposta é 200 com um login novo, como ""(.+)""$")]
    public void LoginNovo(string exemplo)
    {
        Respostas.Confere(acao, 200);
        var login = Texto(acao, "login");
        Assert.Matches($"^{Regex.Replace(Regex.Escape(exemplo), @"\d", @"\d")}$", login);
        Assert.NotEqual(loginAntes, login);
    }

    [Then(@"^o código ""(.+)"" deixa de valer$")]
    public async Task CodigoAntigoNaoVale(string exemplo) =>
        Respostas.Confere(await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" }, await mundo.Traduzir(exemplo)), 401);

    // ── código de acesso (RF-154, RN-184, RN-245) ──────────────────────────
    [Given(@"^a Carla com uma sessão do quest pelo código ""EBFGA-LUCAS-0412""$")]
    public async Task CarlaNoQuest() => mundo.Atual = await mundo.EntrarNoQuest(App, Carla());

    [Given(@"^o Lucas no tablet e a Carla no celular com ""EBFGA-LUCAS-0412""$")]
    public async Task LucasECarla()
    {
        await mundo.EntrarNoQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" });
        await mundo.EntrarNoQuest(App, Carla());
    }

    [Then(@"^a resposta é 200 com o login ""([A-Z]+-[A-Z]+-)"" seguido de 4 números novos$")]
    public async Task CodigoNovo(string comeco)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var login = Texto(mundo.Ultima, "login");
        Assert.Matches($"^{Regex.Escape(comeco)}\\d{{4}}$", login);
        Assert.NotEqual(await mundo.CodigoDoLucas(), login);
    }

    [Then(@"^o token da Carla recebe 401 e POST /auth/quest/entrar com o código antigo recebe 401$")]
    [Then(@"^os tokens do tablet e do celular recebem 401 no próximo pedido$")]
    public async Task SessoesDoCodigoAcabam()
    {
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa.Entrada == mundo.Sessoes[0].Pessoa.Entrada && !s.Navegador).ToList())
        {
            Assert.Equal(401, await mundo.ProximoPedido(sessao.Acesso));
        }
        Respostas.Confere(await mundo.TentarQuest(App, Carla()), 401);
    }

    [Then(@"^POST /auth/quest/entrar com ""EBFGA-LUCAS-0412"" recebe 401 CODIGO_INVALIDO$")]
    public async Task CodigoAntigoInvalido() =>
        Respostas.Confere(await mundo.TentarQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" }), 401, "CODIGO_INVALIDO");

    [When(@"^a EBFGA envia POST /usuarios do participante de nome curto ""(\w+)""$")]
    public async Task CriaParticipante(string nomeCurto) =>
        await mundo.EnviarComo(await mundo.Master(), "POST", "/usuarios", Participante("Lucas Andrade", nomeCurto));

    [Then(@"^o login é ""([A-Z]+-[A-Z]+-)"" seguido de 4 números, como "".+""$")]
    public void FormatoDoCodigo(string comeco) => Assert.Matches($"^{Regex.Escape(comeco)}\\d{{4}}$", Texto(mundo.Ultima, "login"));

    [Then(@"^o login é único no sistema e vem em maiúsculas$")]
    public async Task CodigoUnico()
    {
        var login = Texto(mundo.Ultima, "login");
        Assert.Equal(login.ToUpperInvariant(), login);
        var r = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", $"/usuarios?tipo=PARTICIPANTE&busca={login}"), 200, "GET /usuarios");
        Assert.Equal(1, r.Corpo?["total"]?.GetValue<int>());
    }

    [When(@"^a Carla envia POST /auth/quest/entrar com o código ""(.+)""$")]
    public async Task CarlaEmMinusculas(string exemplo)
    {
        var real = await mundo.Traduzir(exemplo.ToUpperInvariant());
        await mundo.TentarQuest(App, Carla(), real.ToLowerInvariant());
    }

    [Then(@"^a resposta é 200 com o token do Lucas, sem senha$")]
    public async Task TokenDoLucas()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal((await mundo.Lucas()).Id, Jwt.Carga(Texto(mundo.Ultima, "tokenAcesso"))["sub"]?.GetValue<string>());
        Assert.Empty(CamposDeSenha(mundo.Ultima.Corpo));
    }

    // ── permissões no cadastro (RF-155, RN-166, RN-167, RN-168) ───────────
    [Given(@"^o membro do time Caio Lima, sem módulos$")]
    public async Task CaioSemModulos() => await mundo.SessaoDe(await mundo.Caio(), Navegador);

    [When(@"^o administrador envia PUT (/usuarios/\S+/permissoes) com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task AdminConcede(string caminho, string mod, string abas)
    {
        var alvo = await mundo.Traduzir(caminho);
        var versao = Exigir(await mundo.EnviarComo(mundo.Admin(), "GET", alvo), 200, caminho).Corpo!["versao"]!.GetValue<int>();
        await mundo.EnviarComo(mundo.Admin(), "PUT", alvo, new JsonObject { ["mods"] = new JsonObject { [mod] = abas } }.ToJsonString(), versao.ToString());
    }

    [Then(@"^POST /auth/renovar do Caio devolve um token só com ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task TokenDoCaio(string mod, string abas)
    {
        var sessao = await mundo.SessaoDe(await mundo.Caio(), Navegador);
        Respostas.Confere(await mundo.RenovarEGuardar(sessao), 200);
        Assert.Equal(new Dictionary<string, string> { [mod] = abas }, Mods(Jwt.Carga(sessao.Acesso)));
    }

    [Then(@"^quando a escola recontrata a Presença, ""(MOD-\d+)"" volta com ""(\w+)""$")]
    public async Task Recontrata(string mod, string abas)
    {
        await mundo.Contratar(mod);
        Assert.Equal(abas, Mods((await mundo.Permissoes(mundo.Sujeito!))["mods"]!).GetValueOrDefault(mod));
    }

    [When(@"^a conta master envia PUT (/usuarios/\S+/permissoes) com ""(MOD-\d+)""$")]
    public async Task SalvaSemContrato(string caminho, string mod)
    {
        var alvo = await mundo.Traduzir(caminho);
        var master = await mundo.Master();
        var versao = Exigir(await mundo.EnviarComo(master, "GET", alvo), 200, caminho).Corpo!["versao"]!.GetValue<int>();
        await mundo.EnviarComo(master, "PUT", alvo, new JsonObject { ["mods"] = new JsonObject { [mod] = "U" } }.ToJsonString(), versao.ToString());
    }

    // a escola contrata o Quiz, para "os contratados" não ser só os módulos da conta
    [When(@"^a conta master envia GET (/usuarios/\S+/permissoes)$")]
    public async Task MasterLePermissoes(string caminho)
    {
        await mundo.Contratar("MOD-05");
        await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho));
    }

    [Then(@"^concediveis traz todos os módulos contratados pela escola, com ""(\w+)""$")]
    public void TodosOsContratados(string abas)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var concediveis = Concediveis();
        Assert.Equal(abas, concediveis.GetValueOrDefault("MOD-05"));
        Assert.All(concediveis.Values, a => Assert.Equal(abas, a));
        Assert.DoesNotContain("MOD-06", concediveis.Keys);
    }

    // "concediveis traz só ""MOD-03"" com ""CU"" e ""MOD-05"" com ""U"""
    [Then(@"^concediveis traz só (""MOD-\d+"" com .+)$")]
    public void SoEstesConcediveis(string pares)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperados = Regex.Matches(pares, @"""(MOD-\d+)"" com ""(\w+)""").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.Equal(esperados, Concediveis());
    }

    [Given(@"^a conta master tendo salvado ""(MOD-\d+)"" com ""(\w+)"" para o João$")]
    public async Task MasterSalvouParaOJoao(string mod, string abas) => await mundo.Conceder(await mundo.Joao(), new() { [mod] = abas });

    [When(@"^o João envia POST /usuarios para criar a staff Ana$")]
    public async Task JoaoCriaAAna() => await mundo.EnviarComo(await mundo.Joao(), "POST", "/usuarios", new JsonObject
    {
        ["tipo"] = "STAFF",
        ["nome"] = "Ana Costa",
        ["nomeCurto"] = "ana",
        ["matricula"] = "20260089",
    }.ToJsonString());

    // o João tem só Administrar usuários: o Quiz (contratado) passa das abas dele
    [Then(@"^PUT (/usuarios/\S+/permissoes) do João aceita só as abas que ele tem$")]
    public async Task SoAsAbasDoJoao(string caminho)
    {
        var joao = await mundo.Joao();
        var alvo = await mundo.Traduzir(caminho);
        await mundo.Contratar("MOD-05");
        async Task<Resposta> Salvar(JsonObject mods)
        {
            var versao = Exigir(await mundo.EnviarComo(joao, "GET", alvo), 200, caminho).Corpo!["versao"]!.GetValue<int>();
            return await mundo.EnviarComo(joao, "PUT", alvo, new JsonObject { ["mods"] = mods }.ToJsonString(), versao.ToString());
        }
        Respostas.Confere(await Salvar(new JsonObject { ["MOD-03"] = "CU" }), 200);
        Respostas.Confere(await Salvar(new JsonObject { ["MOD-03"] = "CU", ["MOD-05"] = "U" }), 403, "PERMISSAO_ACIMA");
    }

    // ── quem é usuário (RN-163) ────────────────────────────────────────────
    [When(@"^(a conta master da EBFGA|o educador autônomo Pedro|a staff Maria|o Lucas|o membro do time Caio|a Carla, mãe do Lucas|uma pessoa sem código numa sala) entra$")]
    public async Task Entra(string quem)
    {
        switch (quem)
        {
            case "uma pessoa sem código numa sala":
                throw new PendingStepException(SemSala);
            case "o Lucas":
                mundo.Atual = await mundo.EntrarNoQuest(App, new JsonObject { ["tipo"] = "PARTICIPANTE" });
                leitor = await mundo.Master();
                return;
            case "a Carla, mãe do Lucas":
                mundo.Atual = await mundo.EntrarNoQuest(App, Carla());
                return;
        }
        var pessoa = quem switch
        {
            "a conta master da EBFGA" => await mundo.Master(),
            "o educador autônomo Pedro" => await mundo.Pedro(),
            "a staff Maria" => await mundo.Maria(),
            _ => await mundo.Caio(),
        };
        mundo.Atual = await mundo.Entrar(pessoa, Navegador);
        // a conta master e o autônomo leem o próprio cadastro; o Caio, pelo administrador
        leitor = quem switch
        {
            "o educador autônomo Pedro" => pessoa,
            "o membro do time Caio" => mundo.Admin(),
            _ => await mundo.Master(),
        };
    }

    [Then(@"^GET /usuarios/\{id\} traz o tipo ""(\w+)""$")]
    public async Task TrazOTipo(string tipo)
    {
        var id = Jwt.Carga(mundo.Atual!.Acesso)["sub"]!.GetValue<string>();
        var r = Exigir(await mundo.EnviarComo(leitor!, "GET", $"/usuarios/{id}"), 200, "GET /usuarios/{id}");
        Assert.Equal(tipo, Texto(r, "tipo"));
    }

    // a Carla entra no perfil do Lucas e só aparece nos responsáveis dele
    [Then(@"^não é usuário: fica nos responsaveis do Lucas$")]
    public async Task CarlaNaoEUsuaria()
    {
        var lucas = await mundo.Lucas();
        Assert.Equal(lucas.Id, Jwt.Carga(mundo.Atual!.Acesso)["sub"]?.GetValue<string>());
        var master = await mundo.Master();
        var r = await Ler($"/usuarios/{lucas.Id}");
        Assert.Contains(r.Corpo!["responsaveis"]!.AsArray(), x => x?["nome"]?.GetValue<string>() == "Carla Andrade");
        var busca = Exigir(await mundo.EnviarComo(master, "GET", "/usuarios?tipo=PARTICIPANTE&busca=Carla"), 200, "GET /usuarios");
        Assert.Empty(busca.Corpo!["itens"]!.AsArray());
    }

    [Then(@"^não é usuário: acesso anônimo$")]
    public static void Anonimo() => throw new PendingStepException(SemSala);

    // ── tempo de sessão (RF-114, RF-152, RN-183, RN-243) ──────────────────
    [Then(@"^a resposta é 200 com o mesmo corpo$")]
    public void MesmoCorpo()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(JsonNode.Parse(mundo.UltimoPedido.Corpo!), mundo.Ultima.Corpo);
    }

    [Then(@"^GET (/contas/\S+/tempo-sessao) devolve ""(\w+)"" no staff do navegador$")]
    public async Task TempoDoStaff(string caminho, string valor)
    {
        var r = Exigir(await mundo.EnviarComo(mundo.Sujeito!, "GET", await mundo.Traduzir(caminho)), 200, caminho);
        Assert.Equal(valor, Texto(r, "staffNavegador"));
    }

    [When(@"^a Maria, com ""(MOD-\d+)"" só com ""(\w+)"", envia GET (\S+)$")]
    public async Task MariaSemConfiguracao(string mod, string abas, string caminho)
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { [mod] = abas });
        await mundo.EnviarComo(maria, "GET", await mundo.Traduzir(caminho));
    }

    // o tempo das contas master fica em PT4H antes, para o "continua com" ter o que conferir
    [When(@"^envia PUT (/time/tempo-sessao) com ""(\w+)"" igual a ""(\w+)""$")]
    public async Task TempoForaDoLimite(string caminho, string campo, string valor)
    {
        var por = await mundo.SujeitoOu(caminho);
        var corpo = new JsonObject { ["membrosNavegador"] = "PT8H", ["membrosApp"] = "PT8H", ["mastersNavegador"] = "PT4H", ["mastersApp"] = "PT8H" };
        Exigir(await mundo.EnviarComo(por, "PUT", caminho, corpo.ToJsonString()), 200, $"PUT {caminho}");
        corpo[campo] = valor;
        tempoCampos = [campo];
        await mundo.EnviarComo(por, "PUT", caminho, corpo.ToJsonString());
    }

    [Then(@"^GET (/time/tempo-sessao) continua com ""(\w+)""$")]
    public async Task TempoContinua(string caminho, string valor)
    {
        var r = Exigir(await mundo.EnviarComo(await mundo.SujeitoOu(caminho), "GET", caminho), 200, caminho);
        Assert.Equal(valor, Texto(r, tempoCampos[0]));
    }

    [When(@"^a conta master envia PUT (/contas/\S+/tempo-sessao) com (\w+)$")]
    public async Task TempoDaConta(string caminho, string campo) => await LerTempo(caminho, [campo], await mundo.Master());

    [When(@"^o time deboaz envia PUT (/time/tempo-sessao) com (\w+) e (\w+)$")]
    public async Task TempoDoTime(string caminho, string campo1, string campo2) => await LerTempo(caminho, [campo1, campo2], mundo.Admin());

    async Task LerTempo(string caminho, string[] campos, Pessoa por)
    {
        (tempoCaminho, tempoCampos, tempoPor) = (await mundo.Traduzir(caminho), campos, por);
        tempoAntes = Exigir(await mundo.EnviarComo(por, "GET", tempoCaminho), 200, $"GET {caminho}").Corpo!.AsObject();
    }

    [Then(@"^aceita de (\w+) a (\w+), com padrão (\w+)$")]
    public async Task AceitaOsLimites(string minimo, string maximo, string padrao)
    {
        foreach (var campo in tempoCampos)
        {
            Assert.Equal(padrao, tempoAntes?[campo]?.GetValue<string>());
            Respostas.Confere(await SalvarTempo(campo, minimo), 200);
            Respostas.Confere(await SalvarTempo(campo, maximo), 200);
        }
        (tempoMinimo, tempoMaximo) = (minimo, maximo);
    }

    string tempoMinimo = "", tempoMaximo = "";

    // 1 min abaixo do mínimo e 1 min acima do máximo
    [Then(@"^fora disso a resposta é 422 FORA_DOS_LIMITES$")]
    public async Task ForaDosLimites()
    {
        var umMinuto = TimeSpan.FromMinutes(1);
        foreach (var valor in new[] { XmlConvert.ToTimeSpan(tempoMinimo) - umMinuto, XmlConvert.ToTimeSpan(tempoMaximo) + umMinuto })
        {
            var r = await SalvarTempo(tempoCampos[0], XmlConvert.ToString(valor));
            Respostas.Confere(r, 422, "FORA_DOS_LIMITES");
            Comparacao.Confere(new JsonObject { ["campo"] = tempoCampos[0], ["minimo"] = tempoMinimo, ["maximo"] = tempoMaximo }, r.Corpo);
        }
    }

    Task<Resposta> SalvarTempo(string campo, string valor)
    {
        var corpo = (JsonObject)tempoAntes!.DeepClone();
        corpo[campo] = valor;
        return mundo.EnviarComo(tempoPor!, "PUT", tempoCaminho, corpo.ToJsonString());
    }

    [When(@"^a conta master da EBFGA envia PUT (/time/tempo-sessao)$")]
    public async Task MasterMudaOTime(string caminho) => await mundo.EnviarComo(await mundo.Master(), "PUT", caminho, """
        {
          "membrosNavegador": "PT8H",
          "membrosApp": "PT8H",
          "mastersNavegador": "PT8H",
          "mastersApp": "PT8H"
        }
        """);

    // ── time deboaz (RF-111, RN-169, RN-172) ──────────────────────────────
    [Then(@"^a resposta é 202 e a mensagem entra no SAC do time deboaz$")]
    public void MensagemNoSac()
    {
        Respostas.Confere(acao, 202);
        throw new PendingStepException("a caixa do SAC ainda não tem endereço no contrato");
    }

    [When(@"^o membro do time Caio envia POST (/contas/\S+/codigo-master)$")]
    public async Task CaioPedeOCodigo(string caminho) =>
        acao = await mundo.EnviarComo(await CaioComUsuarios(), "POST", await mundo.Traduzir(caminho), Conferencia("(51) 99999-0000", null));

    [Then(@"^a nova geração da conta master da EBFGA segue a RN-172$")]
    public void SegueARn172()
    {
        Respostas.Confere(acao, 201);
        Assert.Matches(@"^\d{6}$", Texto(acao, "codigoLiberacao"));
        Assert.Equal("(51) 99999-0000", Texto(acao, "contato"));
    }

    // CNPJ certo e só 1 de 3 (o telefone): a conferência pede 2
    [When(@"^envia o CNPJ e o telefone certos e a data do último pagamento errada$")]
    public async Task ConferenciaIncompleta()
    {
        var conta = await mundo.Conta();
        var corpo = JsonNode.Parse(Conferencia("(51) 99999-0000", "2020-01-01"))!.AsObject();
        corpo["responsavel"] = null;
        acao = await mundo.EnviarComo(await CaioComUsuarios(), "POST", $"/contas/{conta}/codigo-master", corpo.ToJsonString());
    }

    [Then(@"^a resposta é 422 CONFERENCIA_INCOMPLETA e nenhum código é gerado$")]
    public void NenhumCodigo()
    {
        Respostas.Confere(acao, 422, "CONFERENCIA_INCOMPLETA");
        Assert.Null(acao.Corpo?["codigoLiberacao"]);
    }

    async Task<Pessoa> CaioComUsuarios()
    {
        var caio = await mundo.Caio();
        await mundo.Conceder(caio, new() { ["MOD-03"] = "U" });
        return caio;
    }

    static string Conferencia(string telefone, string? ultimoPagamento) => new JsonObject
    {
        ["documento"] = "12.345.678/0001-90",
        ["responsavel"] = "Gustavo Boaz de Moura Souza",
        ["telefone"] = telefone,
        ["ultimoPagamento"] = ultimoPagamento,
    }.ToJsonString();

    // ── listas e busca (RN-185, RN-186, RN-187) ───────────────────────────
    [When(@"^(a conta master|o time deboaz) envia GET (/usuarios|/contas)\?busca=(.+)$")]
    public async Task Busca(string quem, string caminho, string termo)
    {
        await mundo.Maria();
        await mundo.Lucas();
        var por = quem == "a conta master" ? await mundo.Master() : mundo.Admin();
        await mundo.EnviarComo(por, "GET", $"{caminho}?busca={Uri.EscapeDataString(await mundo.Traduzir(termo))}");
    }

    [Then(@"^itens traz (a Maria|a Maria, pela matrícula|o Lucas|a Escola Bento Freitas)$")]
    public async Task ItensTraz(string quem)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var id = quem switch
        {
            "o Lucas" => (await mundo.Lucas()).Id,
            "a Escola Bento Freitas" => await mundo.Conta(),
            _ => (await mundo.Maria()).Id,
        };
        Assert.Contains(id, Ids(mundo.Ultima));
    }

    // o João e o Rui desativados; a Maria, ativa, fica de fora
    [When(@"^a conta master envia GET (/usuarios\?tipo=STAFF&estado=DESATIVADO&pagina=1)$")]
    public async Task ListaDesativados(string caminho)
    {
        var master = await mundo.Master();
        await mundo.Maria();
        foreach (var pessoa in new[] { await mundo.Rui(), await mundo.Joao() })
        {
            Exigir(await mundo.EnviarComo(master, "POST", $"/usuarios/{pessoa.Id}/desativar"), 204, "POST /usuarios/{id}/desativar");
        }
        await mundo.EnviarComo(master, "GET", caminho);
    }

    [Then(@"^itens traz só o staff desativado, de A a Z, até 50 por página, com o total$")]
    public async Task SoDesativados()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var corpo = mundo.Ultima.Corpo!;
        Assert.Equal([(await mundo.Joao()).Id, (await mundo.Rui()).Id], Ids(mundo.Ultima));
        Assert.All(corpo["itens"]!.AsArray(), i => Assert.Equal("DESATIVADO", i?["estado"]?.GetValue<string>()));
        Assert.Equal(1, corpo["pagina"]?.GetValue<int>());
        Assert.Equal(2, corpo["total"]?.GetValue<int>());
    }

    [Given(@"^a Carla, mãe do Lucas, entrou em .+$")]
    public async Task CarlaEntrou() => await mundo.EntrarNoQuest(App, Carla());

    [When(@"^a conta master envia GET (/usuarios/[^/?\s]+)$")]
    public async Task MasterLe(string caminho) => await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho));

    // a hora do exemplo só pede uma data e hora (comparação de JSON)
    [Then(@"^ultimoAcesso é ""(.+)"" e ultimoAcessoQuem é ""(\w+)""$")]
    public void UltimoAcesso(string quando, string quem)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(new JsonObject { ["ultimoAcesso"] = quando, ["ultimoAcessoQuem"] = quem }, mundo.Ultima.Corpo);
    }

    [Then(@"^o item da EBFGA traz ultimoAcessoMaster$")]
    public async Task UltimoAcessoMaster()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var conta = await mundo.Conta();
        var item = mundo.Ultima.Corpo!["itens"]!.AsArray().FirstOrDefault(i => i?["id"]?.GetValue<string>() == conta);
        Assert.True(DateTimeOffset.TryParse(item?["ultimoAcessoMaster"]?.GetValue<string>(), out _), $"sem ultimoAcessoMaster: {mundo.Ultima}");
    }

    // ultimoPagamento vem da cobrança, que a API ainda não registra: fica fora da comparação
    [When(@"^o time deboaz envia GET (/contas/\S+)$")]
    public async Task TimeLeAConta(string caminho)
    {
        mundo.CamposSemConferir.Add("ultimoPagamento");
        await mundo.EnviarComo(mundo.Admin(), "GET", await mundo.Traduzir(caminho));
    }

    [Then(@"^a resposta não traz usuários, turmas nem conteúdos da escola$")]
    public void SoOCadastro()
    {
        var corpo = mundo.Ultima.Corpo!.AsObject();
        foreach (var campo in new[] { "usuarios", "staff", "participantes", "turmas", "conteudos" }) Assert.False(corpo.ContainsKey(campo), $"traz {campo}");
        Assert.DoesNotContain(corpo, c => c.Value is JsonArray);
    }

    // ── card (RN-189) ──────────────────────────────────────────────────────
    // selo, noMes e meses dependem do histórico de meses, que a API não cria: ficam
    // fora da comparação; o dado, o rótulo e as contagens são conferidos
    [Given(@"^a conta master da EBFGA, com (\d+) staff e (\d+) participantes ativos$")]
    public async Task EscolaComUsuarios(int staff, int participantes)
    {
        await Povoar(await mundo.Master(), staff, participantes);
        mundo.Sujeito = await mundo.Master();
        foreach (var campo in new[] { "selo", "noMes", "meses" }) mundo.CamposSemConferir.Add(campo);
    }

    [When(@"^(a conta master da EBFGA|o educador autônomo Pedro|o administrador do deboaz) envia GET /usuarios/card$")]
    public async Task PedeOCard(string quem)
    {
        Pessoa por;
        switch (quem)
        {
            case "a conta master da EBFGA":
                await Povoar(por = await mundo.Master(), 37, 298);
                break;
            case "o educador autônomo Pedro":
                await Povoar(por = await mundo.Pedro(), 0, 32);
                break;
            default:
                por = mundo.Admin();
                await mundo.Conta();
                // as contas que a inicialização criou (QAINS e QAAUT em homologação,
                // RN-265) também contam: completa até 112 ativas
                var ativas = Exigir(await mundo.EnviarComo(por, "GET", "/contas?estado=ATIVA"), 200, "GET /contas?estado=ATIVA")
                    .Corpo?["total"]?.GetValue<int>() ?? 1;
                for (var i = 1; i <= 112 - ativas; i++)
                {
                    var sigla = $"CT{Letras(i)}".ToUpperInvariant();
                    await mundo.NovaConta(sigla, ContaNova("INSTITUICAO", $"Escola {sigla}", Cnpj(30000000 + i), $"conta_{i}", sigla));
                }
                break;
        }
        await mundo.EnviarComo(por, "GET", "/usuarios/card");
    }

    [Then(@"^dado e rotulo são (\d+) e ""(.+)""$")]
    public void DadoERotulo(int dado, string rotulo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(dado, mundo.Ultima.Corpo?["dado"]?.GetValue<int>());
        Assert.Equal(rotulo, mundo.Ultima.Corpo?["rotulo"]?.GetValue<string>());
    }

    async Task Povoar(Pessoa master, int staff, int participantes)
    {
        for (var i = 0; i < staff; i++) await mundo.NovoStaff($"staff{Letras(i)}", $"Staff {Letras(i)}", master);
        for (var i = 0; i < participantes; i++)
        {
            Exigir(await mundo.EnviarComo(master, "POST", "/usuarios", Participante($"Participante {Letras(i)}", $"part{Letras(i)}")),
                201, "POST /usuarios de participante");
        }
    }

    // ── auditoria, senha guardada e o que não é da API ────────────────────
    [When(@"^a Maria envia (POST|PUT) (/usuarios\S*)(?: do participante Lucas)?$")]
    public async Task MariaAge(string metodo, string caminho)
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "CU" });
        var alvo = await mundo.Traduzir(caminho);
        if (caminho == "/usuarios") await mundo.EnviarComo(maria, "POST", alvo, Participante("Lucas Andrade", "lucas"));
        else if (metodo == "PUT") await mundo.EnviarComo(maria, "PUT", alvo, """{ "mods": {} }""", "1");
        else await mundo.EnviarComo(maria, "POST", alvo);
    }

    [Then(@"^a auditoria guarda a Maria, a ação, o alvo e a hora, até 5 anos depois$")]
    public static void Auditoria() => throw new PendingStepException("a auditoria não tem endereço no contrato: conferida nos testes do backend");

    [Given(@"^a senha da Maria guardada$")]
    public Task SenhaGuardada() => mundo.Maria();

    [When(@"^o time deboaz envia qualquer pedido sobre a Maria$")]
    public async Task TimePedeSobreAMaria()
    {
        var maria = await mundo.Maria();
        var conta = await mundo.Conta();
        foreach (var caminho in new[] { $"/contas/{conta}", "/contas?busca=EBFGA", $"/usuarios/{maria.Id}", "/usuarios?tipo=STAFF&busca=maria" })
        {
            respostas.Add(await mundo.EnviarComo(mundo.Admin(), "GET", caminho));
        }
    }

    [Then(@"^nenhuma resposta traz a senha$")]
    public async Task NenhumaSenha()
    {
        var maria = await mundo.Maria();
        Assert.All(respostas, r =>
        {
            Assert.DoesNotContain(maria.Senha, r.Texto);
            Assert.Empty(CamposDeSenha(r.Corpo));
        });
    }

    [Then(@"^no banco existe só o resumo irreversível, de onde a senha não pode ser lida$")]
    [Then(@"^o banco recusa a alteração e a remoção$")]
    public static void SoNoBancoTambem() => throw new PendingStepException(SoNoBackend);

    // a regra da senha é do gerador: nenhum endereço recebe uma senha escolhida
    [When(@"^o servidor confere a senha ""(.+)""$")]
    public static void RegraDaSenha(string _) => SemSenhaEscolhida();

    [Then(@"^ela é .+$")]
    public static void SemSenhaEscolhida() => throw new PendingStepException("sem endereço que receba uma senha escolhida: conferido nos testes do backend");

    [Given(@"^(\d) pedidos POST /contas/esqueci-senha-master do mesmo aparelho nas últimas 24 h$")]
    public async Task PedidosAoSac(int quantos)
    {
        await mundo.Conta();
        for (var i = 0; i < quantos; i++)
        {
            Exigir(await PedirAjuda(), 202, "POST /contas/esqueci-senha-master");
            if (i == 0) primeiroEnvio = mundo.HoraDoPedido;
        }
    }

    [When(@"^chega o quarto$")]
    public Task Quarto() => PedirAjuda();

    [Then(@"^a resposta é 429 LIMITE_ATENDIMENTO com tentarEm, 24 h depois do primeiro$")]
    public void LimiteDoDia()
    {
        Respostas.Confere(mundo.Ultima, 429, "LIMITE_ATENDIMENTO");
        var tentarEm = DateTimeOffset.Parse(Texto(mundo.Ultima, "tentarEm"));
        Assert.True((tentarEm - primeiroEnvio.AddHours(24)).Duration() < TimeSpan.FromMinutes(1), $"tentarEm {tentarEm}, primeiro {primeiroEnvio}");
    }

    Task<Resposta> PedirAjuda() => mundo.Enviar("POST", "/contas/esqueci-senha-master", """
        {
          "instituicao": "Escola Bento Freitas",
          "contato": "(51) 99999-0000"
        }
        """);

    [Given(@"^o registro ""aud-ebfga-000154"" da criação do Lucas$")]
    [When(@"^o time deboaz procura alterar ou apagar o registro$")]
    [Then(@"^nenhum endereço da API faz isso$")]
    public static void RegistroDeAuditoria() => throw new PendingStepException("a auditoria não tem endereço no contrato: conferida nos testes do backend");

    [Then(@"^(?:a conta|a conta master|a staff|o participante|a geração de senha da Maria|o acesso anônimo da sala 482107|o registro de auditoria) é:$")]
    public static void Documento(string _) => Documentos();

    [Given(@"^a Escola Bento Freitas, a conta master, a Maria e o Lucas, com datas em UTC$")]
    public static void Documentos() => throw new PendingStepException("documentos do banco: conferidos nos testes do backend");

    [Then(@"^login por Google ou Microsoft .+$")]
    [Then(@"^senha escolhida pela pessoa .+$")]
    [Then(@"^recuperação por perguntas de segurança .+$")]
    [Then(@"^turma e ano do participante .+$")]
    [Then(@"^o formato do código da sala .+$")]
    [Then(@"^usuário em várias unidades .+$")]
    public static void NadaATestar() => throw new PendingStepException(ForaDeEscopo);

    // ── apoio ───────────────────────────────────────────────────────────────
    Task<Resposta> GerarSenha(string entrada, string codigoLiberacao) => mundo.Enviar("POST", "/usuarios/gerar-senha",
        new JsonObject { ["entrada"] = entrada, ["codigoLiberacao"] = codigoLiberacao }.ToJsonString());

    // staff novo com esse nome e a senha que o código de liberação dele gera
    async Task<(Pessoa Pessoa, string Comprovante)> SenhaNova(string nome) =>
        await mundo.StaffPendente(nome, new string(nome.Split(' ')[0].ToLowerInvariant().Where(char.IsAsciiLetter).ToArray()), null);

    async Task<Resposta> Ler(string caminho) =>
        Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", await mundo.Traduzir(caminho)), 200, $"GET {caminho}");

    static string EntradaDe(Pessoa pessoa) => EntradaDe(pessoa.Entrada, pessoa.Senha);

    static string EntradaDe(string entrada, string senha) =>
        new JsonObject { ["entrada"] = entrada, ["senha"] = senha, ["aparelho"] = Navegador }.ToJsonString();

    static string Participante(string nome, string nomeCurto) => new JsonObject
    {
        ["tipo"] = "PARTICIPANTE",
        ["nome"] = nome,
        ["nomeCurto"] = nomeCurto,
        ["matricula"] = null,
        ["responsaveis"] = new JsonArray(),
    }.ToJsonString();

    static List<string> Ids(Resposta r) =>
        r.Corpo?["itens"]?.AsArray().Select(i => i?["id"]?.GetValue<string>() ?? "").ToList() ?? [];

    static Dictionary<string, string> Mods(JsonNode carga) =>
        (carga["mods"] ?? carga).AsObject().ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());

    Dictionary<string, string> Concediveis() =>
        mundo.Ultima.Corpo!["concediveis"]!.AsArray().ToDictionary(c => c!["mod"]!.GetValue<string>(), c => c!["abas"]!.GetValue<string>());

    // campos cujo nome fala de senha, em qualquer nível (senha, resumoSenha…)
    static List<string> CamposDeSenha(JsonNode? no) => no switch
    {
        JsonObject o => o.SelectMany(c => (c.Key.Contains("senha", StringComparison.OrdinalIgnoreCase) ? [c.Key] : new List<string>())
            .Concat(CamposDeSenha(c.Value))).ToList(),
        JsonArray a => a.SelectMany(CamposDeSenha).ToList(),
        _ => [],
    };

    // "Gu#Boaz507318" → o caractere especial e os 6 números
    static string Sorteado(string senha) => senha[2] + senha[^6..];

    [GeneratedRegex(@"^([A-Za-z]+)[^A-Za-z0-9]([A-Za-z]+)\d{6}$")]
    private static partial Regex PartesDaSenha();
}
