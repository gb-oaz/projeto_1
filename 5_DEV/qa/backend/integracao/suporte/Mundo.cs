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

// O mundo de um cenário: as pessoas dos exemplos dos requisitos (a escola
// EBFGA, a Maria, o João, o Lucas), criadas pela API só quando um passo as
// cita, as sessões abertas e a última resposta. O Reqnroll cria um por cenário.
public sealed class Mundo
{
    public const string Navegador = "NAVEGADOR", App = "APP";

    readonly Api api;

    public Mundo(Api api)
    {
        this.api = api;
        Apelidos["conta-ebfga"] = Conta;
        Apelidos["usr-ebfga-maria"] = async () => (await Maria()).Id;
        Apelidos["usr-ebfga-joao"] = async () => (await Joao()).Id;
        Apelidos["usr-ebfga-lucas"] = async () => (await Lucas()).Id;
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
    // hora citada no exemplo ("chega às 09:00"), para conferir o tentarEm pela diferença
    public TimeSpan? HoraDoExemplo { get; set; }

    // valor do exemplo do requisito → valor real, gerado quando o texto o cita
    public Dictionary<string, Func<Task<string>>> Apelidos { get; } = [];

    Pessoa? master, maria, joao, lucas, admin;
    string contaId = "", codigoLucas = "";

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

    // ── pessoas ─────────────────────────────────────────────────────────────
    public async Task<string> Conta()
    {
        if (master is not null) return contaId;
        var r = Exigir(await api.Enviar("POST", "/contas", """
            {
              "tipo": "INSTITUICAO",
              "nome": "Escola Bento Freitas",
              "documento": "12.345.678/0001-90",
              "responsavel": "Gustavo Boaz de Moura Souza",
              "telefone": "(51) 99999-0000",
              "dominio": "objetivo_unidade1",
              "sigla": "EBFGA"
            }
            """), 201, "POST /contas");
        contaId = Texto(r, "contaId");
        master = new Pessoa { Nome = "conta master", Entrada = Texto(r, "login"), Senha = Texto(r, "senha") };
        await Confirmar(Texto(r, "comprovante"));
        return contaId;
    }

    public async Task<Pessoa> Master() { await Conta(); return master!; }
    public async Task<Pessoa> Maria() => maria ??= await Staff("Maria Souza", "maria", "20260087");
    public async Task<Pessoa> Joao() => joao ??= await Staff("João Lima", "joao", "20260088");

    public Pessoa Admin() => admin ??= new Pessoa
    {
        Nome = "administrador do deboaz", Entrada = Ambiente.AdminEntrada, Senha = Ambiente.AdminSenha,
    };

    public async Task<string> CodigoDoLucas() { await Lucas(); return codigoLucas; }

    public async Task<Pessoa> Lucas()
    {
        if (lucas is not null) return lucas;
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
        return lucas = new Pessoa { Nome = "Lucas Andrade", Entrada = codigoLucas, Id = Texto(r, "id") };
    }

    // staff criado pela conta master, com a senha vista e ainda não confirmada
    public async Task<(Pessoa Pessoa, string Comprovante)> StaffPendente(string nome, string nomeCurto, string matricula)
    {
        var criado = Exigir(await api.Enviar("POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF", ["nome"] = nome, ["nomeCurto"] = nomeCurto, ["matricula"] = matricula,
        }.ToJsonString(), await TokenDe(await Master())), 201, $"POST /usuarios de {nome}");
        var pessoa = new Pessoa { Nome = nome, Entrada = Texto(criado, "login"), Id = Texto(criado, "id") };
        var senha = Exigir(await api.Enviar("POST", "/usuarios/gerar-senha", new JsonObject
        {
            ["entrada"] = pessoa.Entrada, ["codigoLiberacao"] = Texto(criado, "codigoLiberacao"),
        }.ToJsonString()), 200, $"POST /usuarios/gerar-senha de {nome}");
        pessoa.Senha = Texto(senha, "senha");
        return (pessoa, Texto(senha, "comprovante"));
    }

    // a Maria só com a senha gerada, sem a confirmação (RF-143); devolve o comprovante
    public async Task<string> MariaPendente()
    {
        var (pessoa, comprovante) = await StaffPendente("Maria Souza", "maria", "20260087");
        maria = pessoa;
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

    public Pessoa? JaCriada(string entrada) =>
        new[] { master, maria, joao, admin }.FirstOrDefault(p => p?.Entrada == entrada);

    async Task<Pessoa> Staff(string nome, string nomeCurto, string matricula)
    {
        var (pessoa, comprovante) = await StaffPendente(nome, nomeCurto, matricula);
        await Confirmar(comprovante);
        return pessoa;
    }

    // confirma a senha anotada e sai, para a sessão do primeiro acesso não contar
    async Task Confirmar(string comprovante)
    {
        var r = Exigir(await api.Enviar("POST", "/auth/hub/confirmar-senha",
            new JsonObject { ["comprovante"] = comprovante }.ToJsonString()), 200, "POST /auth/hub/confirmar-senha");
        await api.Enviar("POST", "/auth/sair", token: Texto(r, "tokenAcesso"));
    }

    // ── sessões ─────────────────────────────────────────────────────────────
    public async Task<string> TokenDe(Pessoa pessoa) => (await SessaoDe(pessoa, Navegador)).Acesso;

    public async Task<Sessao> SessaoDe(Pessoa pessoa, string aparelho) =>
        Sessoes.LastOrDefault(s => s.Pessoa == pessoa && s.Aparelho == aparelho) ?? await Entrar(pessoa, aparelho);

    public async Task<Sessao> Entrar(Pessoa pessoa, string aparelho)
    {
        var r = Exigir(await Enviar("POST", "/auth/hub/entrar", new JsonObject
        {
            ["entrada"] = pessoa.Entrada, ["senha"] = pessoa.Senha, ["aparelho"] = aparelho,
        }.ToJsonString()), 200, $"POST /auth/hub/entrar de {pessoa.Nome}");
        return Registrar(pessoa, aparelho, r)!;
    }

    public async Task<Sessao> EntrarNoQuest(string aparelho, JsonObject? quem = null)
    {
        var corpo = new JsonObject { ["codigo"] = await CodigoDoLucas(), ["aparelho"] = aparelho };
        if (quem is not null) corpo["quem"] = quem;
        var r = Exigir(await Enviar("POST", "/auth/quest/entrar", corpo.ToJsonString()), 200, "POST /auth/quest/entrar");
        return Registrar(lucas!, aparelho, r)!;
    }

    // guarda a sessão aberta por uma resposta 200 de entrada ou confirmação
    public Sessao? Registrar(Pessoa pessoa, string aparelho, Resposta r)
    {
        if (r.Status != 200) return null;
        var sessao = new Sessao
        {
            Pessoa = pessoa, Aparelho = aparelho, Acesso = Texto(r, "tokenAcesso"),
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
