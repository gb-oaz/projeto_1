using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos das contas iniciais de 1_Requisitos/17_modulo-usuario.md: o
// administrador do deboaz (RN-190), as contas qa de homologação (RN-265,
// RN-266), a inicialização sem duplicar (RN-267) e os segredos do Key Vault
// (RNF-225, RNF-226).
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

    [Then(@"^existem o administrador, as 5 contas qa e as contas ""(\w+)"" e ""(\w+)""$")]
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
        var master = await TokenNaPartida(ContasIniciais.Master);
        Exigir(await partida.Api.Enviar("POST", $"/usuarios/{staff.Id}/desativar", token: master), 204, "POST /usuarios/{id}/desativar");
        citada = staff;
    }

    [Then(@"^a staff volta ativa, com a senha do Key Vault$")]
    public async Task VoltaAtiva()
    {
        ExigirPronta();
        var staff = citada!;
        var r = await EntrarNaPartida(staff);
        Assert.True(r.Status == 200, $"a staff desativada não voltou: a entrada com a senha do Key Vault respondeu {r}");
        var master = await TokenNaPartida(ContasIniciais.Master);
        var veio = await partida.Api.Enviar("GET", $"/usuarios/{staff.Id}", token: master);
        Respostas.Confere(veio, 200);
        Assert.Equal("ATIVO", veio.Corpo?["estado"]?.GetValue<string>());
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
    [Given(@"^a aplicação de (homologação|produção) sem (a senha do administrador|a entrada do administrador|a senha da staff ""qastaff\.qains""|o código do participante ""QAINS-QAPART"")$")]
    public void SemOSegredo(string ambiente, string segredo)
    {
        partida.KeyVaultCompleto(Sigla(ambiente));
        variavelAusente = segredo switch
        {
            "a senha do administrador" => ContasIniciais.Admin.VariavelSenha!,
            "a entrada do administrador" => ContasIniciais.Admin.VariavelEntrada,
            "o código do participante \"QAINS-QAPART\"" => ContasIniciais.Participante.VariavelEntrada,
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

    async Task<Resposta> EntrarNoHub(string entrada, string senha) =>
        await mundo.Enviar("POST", "/auth/hub/entrar", CorpoDoHub(entrada, senha));

    Task<Resposta> EntrarNaPartida(ContaInicial conta) => conta.Participante
        ? partida.Api.Enviar("POST", "/auth/quest/entrar", CorpoDoQuest(partida.Entrada(conta)))
        : partida.Api.Enviar("POST", "/auth/hub/entrar", CorpoDoHub(partida.Entrada(conta), partida.Senha(conta)));

    async Task<string> TokenNaPartida(ContaInicial conta) =>
        Texto(Exigir(await EntrarNaPartida(conta), 200, $"POST /auth/hub/entrar de {conta.Nome}"), "tokenAcesso");

    static string CorpoDoHub(string entrada, string senha) => new JsonObject
    {
        ["entrada"] = entrada,
        ["senha"] = senha,
        ["aparelho"] = Navegador,
    }.ToJsonString();

    static string CorpoDoQuest(string codigo) => new JsonObject
    {
        ["codigo"] = codigo,
        ["aparelho"] = Navegador,
        ["quem"] = new JsonObject { ["tipo"] = "PARTICIPANTE" },
    }.ToJsonString();

    static string? Papel(Resposta r) =>
        r.Corpo?["tokenAcesso"]?.GetValue<string>() is { } token ? Jwt.Carga(token)["papel"]?.GetValue<string>() : null;
}
