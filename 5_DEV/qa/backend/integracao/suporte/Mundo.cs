using System.Text.Json.Nodes;

namespace Boaz.Qa.Integracao.Suporte;

public sealed class Pessoa
{
    public required string Nome { get; init; }
    public required string Entrada { get; set; }
    public string Senha { get; set; } = "";
    public string Id { get; set; } = "";
}

public sealed class Sessao
{
    public required Pessoa Pessoa { get; init; }
    public required string Aparelho { get; init; }
    public required string Acesso { get; set; }
    // navegador: o cookie deboaz_renovacao; app: o tokenRenovacao do corpo
    public required string Renovacao { get; set; }
    public string Sid => Jwt.Carga(Acesso)["sid"]!.GetValue<string>();
    public bool Navegador => Aparelho == "NAVEGADOR";
}

// O mundo de um cenário: as pessoas dos exemplos dos requisitos, criadas pela
// API só quando um passo as cita, as sessões abertas e a última resposta. O
// Reqnroll cria um por cenário e o entrega no construtor das classes de passos.
//
// Pessoas dos exemplos (17_modulo-usuario e 17-a/17-b):
//   escola EBFGA e a conta master    POST /contas
//   Maria, João, Rui                 staff criados pela conta master (Rui com Administrar usuários)
//   Ana                              staff criada pelo João, abaixo dele
//   Lucas                            participante, com a mãe Carla e o pai Paulo
//   Caio                             membro do time, criado pelo administrador
//   Bruno                            staff da escola COLSA, outra conta
//   Pedro                            educador autônomo
public sealed class Mundo
{
    public const string Navegador = "NAVEGADOR", App = "APP";

    // nome do módulo nos cenários → ID no registro de módulos (15_modulos)
    public static readonly Dictionary<string, string> ModuloPorNome = new()
    {
        ["SAC"] = "MOD-02",
        ["Usuários"] = "MOD-03",
        ["Sugestão"] = "MOD-04",
        ["Quiz"] = "MOD-05",
        ["Presença"] = "MOD-06",
        ["Carteirinha"] = "MOD-07",
        ["Turmas"] = "MOD-08",
        ["Tarefas"] = "MOD-09",
        ["Agenda"] = "MOD-12",
        ["Perfil"] = "MOD-13",
        ["Catálogo"] = "MOD-14",
    };

    // os que a escola precisa contratar para alguém poder recebê-los (RN-166)
    static readonly HashSet<string> Contrataveis = ["MOD-05", "MOD-06", "MOD-07", "MOD-12"];

    readonly Api api;

    public Mundo(Api api)
    {
        this.api = api;
        Apelidos["conta-ebfga"] = Conta;
        Apelidos["usr-ebfga-master"] = async () => Jwt.Carga(await TokenDe(await Master()))["sub"]!.GetValue<string>();
        Apelidos["usr-ebfga-maria"] = async () => (await Maria()).Id;
        Apelidos["usr-ebfga-joao"] = async () => (await Joao()).Id;
        Apelidos["usr-ebfga-ana"] = async () => (await Ana()).Id;
        Apelidos["usr-ebfga-rui"] = async () => (await Rui()).Id;
        Apelidos["usr-ebfga-lucas"] = async () => (await Lucas()).Id;
        Apelidos["usr-dboaz-caio"] = async () => (await Caio()).Id;
        Apelidos["usr-colsa-bruno"] = async () => (await Bruno()).Id;
        Apelidos["EBFGA-LUCAS-0412"] = CodigoDoLucas;
        // sessões da Maria nos exemplos: 3f9c2a71 no navegador, 7d20b9e4 no app
        Apelidos["3f9c2a71"] = async () => (await SessaoDe(await Maria(), Navegador)).Sid;
        Apelidos["7d20b9e4"] = async () => (await SessaoDe(await Maria(), App)).Sid;
        Apelidos["3f9c2a71.Qm9h…"] = async () => (await SessaoDe(await Maria(), App)).Renovacao;
    }

    public Resposta Ultima { get; private set; } = new(0, "", []);
    public DateTimeOffset HoraDoPedido { get; private set; }
    public (string Metodo, string Caminho, string? Corpo) UltimoPedido { get; private set; }
    public List<Sessao> Sessoes { get; } = [];
    public Sessao? Atual { get; set; }
    // de quem o cenário fala ("Dado o João …" → "Quando envia …" é o João)
    public Pessoa? Sujeito { get; set; }
    // um token guardado por um Dado ("um token de pv 6"), usado no Quando
    public string? TokenGuardado { get; set; }
    // hora citada no exemplo ("chega às 09:00"), para conferir o tentarEm pela diferença
    public TimeSpan? HoraDoExemplo { get; set; }

    // valor do exemplo do requisito → valor real, gerado quando o texto o cita
    public Dictionary<string, Func<Task<string>>> Apelidos { get; } = [];

    readonly Dictionary<string, Pessoa> pessoas = [];
    readonly Dictionary<string, (string Id, Pessoa Master)> contas = [];
    Pessoa? admin;
    string codigoLucas = "";

    public async Task<Resposta> Enviar(string metodo, string caminho, string? corpo = null,
        string? token = null, string? cookie = null, string? ifMatch = null)
    {
        UltimoPedido = (metodo, caminho, corpo);
        HoraDoPedido = DateTimeOffset.UtcNow;
        Ultima = await api.Enviar(metodo, caminho, corpo, token, cookie, ifMatch);
        return Ultima;
    }

    public async Task<string> Traduzir(string texto)
    {
        foreach (var (exemplo, real) in Apelidos.OrderByDescending(a => a.Key.Length))
        {
            if (texto.Contains(exemplo, StringComparison.Ordinal)) texto = texto.Replace(exemplo, await real());
        }
        return texto;
    }

    // ── contas ──────────────────────────────────────────────────────────────
    public Task<string> Conta() => ContaDe("EBFGA");
    public async Task<Pessoa> Master() { await Conta(); return contas["EBFGA"].Master; }

    async Task<string> ContaDe(string sigla)
    {
        if (contas.TryGetValue(sigla, out var conta)) return conta.Id;
        var corpo = sigla switch
        {
            "EBFGA" => ContaNova("INSTITUICAO", "Escola Bento Freitas", "12.345.678/0001-90", "objetivo_unidade1", sigla),
            "COLSA" => ContaNova("INSTITUICAO", "Colégio Santo Antônio", "98.765.432/0001-10", "colegio_sa", sigla),
            _ => ContaNova("AUTONOMO", "Pedro Reis", "123.456.789-09", "pedro_reis", sigla),
        };
        var r = Exigir(await api.Enviar("POST", "/contas", corpo), 201, $"POST /contas da {sigla}");
        var master = new Pessoa { Nome = $"conta master da {sigla}", Entrada = Texto(r, "login"), Senha = Texto(r, "senha") };
        contas[sigla] = (Texto(r, "contaId"), master);
        await Confirmar(Texto(r, "comprovante"));
        return contas[sigla].Id;
    }

    static string ContaNova(string tipo, string nome, string documento, string dominio, string sigla) => new JsonObject
    {
        ["tipo"] = tipo,
        ["nome"] = nome,
        ["documento"] = documento,
        ["responsavel"] = "Gustavo Boaz de Moura Souza",
        ["telefone"] = "(51) 99999-0000",
        ["dominio"] = dominio,
        ["sigla"] = sigla,
    }.ToJsonString();

    // o educador autônomo Pedro Reis é a conta master da própria conta
    public async Task<Pessoa> Pedro() { await ContaDe("PREIS"); return contas["PREIS"].Master; }

    // ── pessoas ─────────────────────────────────────────────────────────────
    public Pessoa Admin() => admin ??= new Pessoa
    {
        Nome = "administrador do deboaz",
        Entrada = Ambiente.AdminEntrada,
        Senha = Ambiente.AdminSenha,
    };

    // a Maria dos exemplos está na versão 7 das permissões (17 exemplo-entidades)
    public async Task<Pessoa> Maria(Pessoa? criador = null)
    {
        if (pessoas.TryGetValue("maria", out var maria)) return maria;
        maria = await Staff("maria", "Maria Souza", "20260087", criador ?? await Master());
        await Estado.DefinirVersao(maria.Id, 7);
        return maria;
    }

    public async Task<Pessoa> Joao() =>
        pessoas.GetValueOrDefault("joao") ?? await Staff("joao", "João Lima", "20260088", await Master());

    // par do João: também com Administrar usuários
    public async Task<Pessoa> Rui()
    {
        if (pessoas.TryGetValue("rui", out var rui)) return rui;
        rui = await Staff("rui", "Rui Alves", "20260090", await Master());
        await Conceder(rui, new() { ["MOD-03"] = "CU" });
        return rui;
    }

    // abaixo do João: criada por ele, que precisa de Administrar usuários; versão 2 como no exemplo
    public async Task<Pessoa> Ana()
    {
        if (pessoas.TryGetValue("ana", out var ana)) return ana;
        var joao = await Joao();
        await Conceder(joao, new() { ["MOD-03"] = "CU" });
        ana = await Staff("ana", "Ana Costa", "20260089", joao);
        await Estado.DefinirVersao(ana.Id, 2);
        return ana;
    }

    public async Task<Pessoa> Bruno()
    {
        await ContaDe("COLSA");
        return pessoas.GetValueOrDefault("bruno") ?? await Staff("bruno", "Bruno Dias", "20260100", contas["COLSA"].Master);
    }

    public async Task<Pessoa> Caio()
    {
        if (pessoas.TryGetValue("caio", out var caio)) return caio;
        var criado = Exigir(await api.Enviar("POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "MEMBRO_TIME",
            ["nome"] = "Caio Lima",
            ["nomeCurto"] = "caio",
        }.ToJsonString(), await TokenDe(Admin())), 201, "POST /usuarios do Caio");
        return pessoas["caio"] = await Ativar("Caio Lima", criado);
    }

    public async Task<string> CodigoDoLucas() { await Lucas(); return codigoLucas; }

    public async Task<Pessoa> Lucas()
    {
        if (pessoas.TryGetValue("lucas", out var lucas)) return lucas;
        var r = Exigir(await api.Enviar("POST", "/usuarios", """
            {
              "tipo": "PARTICIPANTE",
              "nome": "Lucas Andrade",
              "nomeCurto": "lucas",
              "matricula": null,
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
            """, await TokenDe(await Master())), 201, "POST /usuarios do Lucas");
        codigoLucas = Texto(r, "login");
        return pessoas["lucas"] = new Pessoa { Nome = "Lucas Andrade", Entrada = codigoLucas, Id = Texto(r, "id") };
    }

    // staff criado por quem tem Administrar usuários, com a senha vista e ainda não confirmada
    public async Task<(Pessoa Pessoa, string Comprovante)> StaffPendente(string nome, string nomeCurto, string matricula,
        Pessoa? criador = null)
    {
        var criado = Exigir(await EnviarComo(criador ?? await Master(), "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF",
            ["nome"] = nome,
            ["nomeCurto"] = nomeCurto,
            ["matricula"] = matricula,
        }.ToJsonString()), 201, $"POST /usuarios de {nome}");
        return await SenhaGerada(nome, criado);
    }

    // a Maria só com a senha gerada, sem a confirmação (RF-143); devolve o comprovante
    public async Task<string> MariaPendente()
    {
        var (pessoa, comprovante) = await StaffPendente("Maria Souza", "maria", "20260087");
        pessoas["maria"] = pessoa;
        return comprovante;
    }

    // entradas dos exemplos, criadas quando um pedido as usa
    public async Task<Pessoa?> PorEntrada(string entrada) => entrada switch
    {
        "maria.ebfga@deboaz.com" => await Maria(),
        "joao.ebfga@deboaz.com" => await Joao(),
        "objetivo_unidade1@deboaz.com" => await Master(),
        _ when entrada == Ambiente.AdminEntrada => Admin(),
        _ => null,
    };

    async Task<Pessoa> Staff(string chave, string nome, string matricula, Pessoa criador)
    {
        var (pessoa, comprovante) = await StaffPendente(nome, chave, matricula, criador);
        await Confirmar(comprovante);
        return pessoas[chave] = pessoa;
    }

    // usuário recém-criado (201 com o código de liberação): vê a senha e confirma
    async Task<Pessoa> Ativar(string nome, Resposta criado)
    {
        var (pessoa, comprovante) = await SenhaGerada(nome, criado);
        await Confirmar(comprovante);
        return pessoa;
    }

    async Task<(Pessoa, string)> SenhaGerada(string nome, Resposta criado)
    {
        var pessoa = new Pessoa { Nome = nome, Entrada = Texto(criado, "login"), Id = Texto(criado, "id") };
        var senha = Exigir(await api.Enviar("POST", "/usuarios/gerar-senha", new JsonObject
        {
            ["entrada"] = pessoa.Entrada,
            ["codigoLiberacao"] = Texto(criado, "codigoLiberacao"),
        }.ToJsonString()), 200, $"POST /usuarios/gerar-senha de {nome}");
        pessoa.Senha = Texto(senha, "senha");
        return (pessoa, Texto(senha, "comprovante"));
    }

    // confirma a senha anotada e sai, para a sessão do primeiro acesso não contar
    async Task Confirmar(string comprovante)
    {
        var r = Exigir(await api.Enviar("POST", "/auth/hub/confirmar-senha",
            new JsonObject { ["comprovante"] = comprovante }.ToJsonString()), 200, "POST /auth/hub/confirmar-senha");
        await api.Enviar("POST", "/auth/sair", token: Texto(r, "tokenAcesso"));
    }

    // ── permissões e módulos ───────────────────────────────────────────────
    // a escola EBFGA contrata o módulo hoje (preparo pelo banco: o contrato do
    // catálogo ainda não tem o endereço)
    public async Task Contratar(string moduloId) => await Estado.Contratar(await Conta(), "EBFGA", moduloId);
    public async Task Descontratar(string moduloId) => await Estado.Descontratar(await Conta(), moduloId);

    // GET /usuarios/{id}/permissoes por quem concede (a conta master, ou o
    // administrador para os membros do time)
    public async Task<JsonObject> Permissoes(Pessoa pessoa, Pessoa? por = null)
    {
        por ??= pessoa == pessoas.GetValueOrDefault("caio") ? Admin() : await Master();
        var r = Exigir(await EnviarComo(por, "GET", $"/usuarios/{pessoa.Id}/permissoes"),
            200, $"GET /usuarios/{{id}}/permissoes de {pessoa.Nome}");
        return r.Corpo!.AsObject();
    }

    // soma os módulos aos que a pessoa já tem e salva (PUT substitui tudo)
    public async Task Conceder(Pessoa pessoa, Dictionary<string, string> mods, Pessoa? por = null)
    {
        var atuais = (await Permissoes(pessoa, por))["mods"]!.AsObject();
        var todos = atuais.ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());
        foreach (var (mod, abas) in mods) todos[mod] = abas;
        await Definir(pessoa, todos, por);
    }

    // salva exatamente estes módulos; contrata antes os contratáveis
    public async Task<Resposta> Definir(Pessoa pessoa, Dictionary<string, string> mods, Pessoa? por = null, bool exigir = true)
    {
        foreach (var mod in mods.Keys.Where(Contrataveis.Contains)) await Contratar(mod);
        por ??= pessoa == pessoas.GetValueOrDefault("caio") ? Admin() : await Master();
        var versao = (await Permissoes(pessoa, por))["versao"]!.GetValue<int>();
        var corpo = new JsonObject { ["mods"] = new JsonObject(mods.Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value))) };
        var r = await EnviarComo(por, "PUT", $"/usuarios/{pessoa.Id}/permissoes", corpo.ToJsonString(), versao.ToString());
        return exigir ? Exigir(r, 200, $"PUT /usuarios/{{id}}/permissoes de {pessoa.Nome}") : r;
    }

    // ── sessões ─────────────────────────────────────────────────────────────
    // pedido com a sessão da pessoa no navegador. Se as permissões dela mudaram
    // (401 PERMISSOES_DESATUALIZADAS), renova uma vez e repete, como a web faz (RNF-224)
    public async Task<Resposta> EnviarComo(Pessoa pessoa, string metodo, string caminho, string? corpo = null, string? ifMatch = null)
    {
        var sessao = await SessaoDe(pessoa, Navegador);
        var r = await Enviar(metodo, caminho, corpo, sessao.Acesso, ifMatch: ifMatch);
        if (r.Status == 401 && r.Corpo?["codigo"]?.GetValue<string>() == "PERMISSOES_DESATUALIZADAS")
        {
            Exigir(await RenovarEGuardar(sessao), 200, $"POST /auth/renovar de {pessoa.Nome}");
            r = await Enviar(metodo, caminho, corpo, sessao.Acesso, ifMatch: ifMatch);
        }
        return r;
    }

    public async Task<string> TokenDe(Pessoa pessoa) => (await SessaoDe(pessoa, Navegador)).Acesso;

    public async Task<Sessao> SessaoDe(Pessoa pessoa, string aparelho) =>
        Sessoes.LastOrDefault(s => s.Pessoa == pessoa && s.Aparelho == aparelho) ?? await Entrar(pessoa, aparelho);

    public async Task<Sessao> Entrar(Pessoa pessoa, string aparelho)
    {
        var r = Exigir(await Enviar("POST", "/auth/hub/entrar", new JsonObject
        {
            ["entrada"] = pessoa.Entrada,
            ["senha"] = pessoa.Senha,
            ["aparelho"] = aparelho,
        }.ToJsonString()), 200, $"POST /auth/hub/entrar de {pessoa.Nome}");
        return Registrar(pessoa, aparelho, r)!;
    }

    public async Task<Sessao> EntrarNoQuest(string aparelho, JsonObject? quem = null)
    {
        var corpo = new JsonObject { ["codigo"] = await CodigoDoLucas(), ["aparelho"] = aparelho };
        if (quem is not null) corpo["quem"] = quem;
        var r = Exigir(await Enviar("POST", "/auth/quest/entrar", corpo.ToJsonString()), 200, "POST /auth/quest/entrar");
        return Registrar(pessoas["lucas"], aparelho, r)!;
    }

    // guarda a sessão aberta por uma resposta 200 de entrada ou confirmação
    public Sessao? Registrar(Pessoa pessoa, string aparelho, Resposta r)
    {
        if (r.Status != 200) return null;
        var sessao = new Sessao
        {
            Pessoa = pessoa,
            Aparelho = aparelho,
            Acesso = Texto(r, "tokenAcesso"),
            Renovacao = aparelho == Navegador ? CookieDeRenovacao(r) ?? "" : Texto(r, "tokenRenovacao"),
        };
        Sessoes.Add(sessao);
        return sessao;
    }

    public Task<Resposta> Renovar(Sessao sessao, string? renovacao = null)
    {
        renovacao ??= sessao.Renovacao;
        return sessao.Navegador
            ? Enviar("POST", "/auth/renovar", cookie: $"deboaz_renovacao={renovacao}")
            : Enviar("POST", "/auth/renovar", new JsonObject { ["tokenRenovacao"] = renovacao }.ToJsonString());
    }

    // renova e, se aceitou, a sessão passa a usar os tokens novos
    public async Task<Resposta> RenovarEGuardar(Sessao sessao)
    {
        var r = await Renovar(sessao);
        if (r.Status == 200)
        {
            sessao.Acesso = Texto(r, "tokenAcesso");
            sessao.Renovacao = sessao.Navegador ? CookieDeRenovacao(r) ?? "" : Texto(r, "tokenRenovacao");
        }
        return r;
    }

    // um pedido qualquer com o token, para ver se ele ainda vale
    public async Task<int> ProximoPedido(string token) => (await Enviar("GET", "/auth/sessoes", token: token)).Status;

    public static string? CookieDeRenovacao(Resposta r) =>
        r.Cookie("deboaz_renovacao")?.Split(';')[0]["deboaz_renovacao=".Length..];

    public static string Texto(Resposta r, string campo) =>
        r.Corpo?[campo]?.GetValue<string>() ?? throw new InvalidOperationException($"sem \"{campo}\" na resposta {r}");

    // passo de preparo que falhou: o cenário para aqui, dizendo qual pedido
    public static Resposta Exigir(Resposta r, int status, string pedido) =>
        r.Status == status ? r : throw new InvalidOperationException($"preparo: {pedido} respondeu {r}, esperava {status}");
}
