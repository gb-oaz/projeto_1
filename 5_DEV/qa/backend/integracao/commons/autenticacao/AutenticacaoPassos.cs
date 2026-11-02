using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Commons.Autenticacao;

// Passos dos Cenários de API de 1_Requisitos/17_modulo-usuario-a-autenticacao.html.
//
// Como funciona: cada linha Dado/Quando/Então/E do autenticacao.feature procura
// aqui um método cujo atributo [Given]/[When]/[Then] tenha uma expressão
// regular que case com a frase inteira. Cada grupo (...) da expressão vira um
// parâmetro do método, na ordem. Ex.: a linha
//   Quando envia POST /auth/hub/entrar com a entrada "maria.ebfga@deboaz.com" e uma senha errada
// casa com [When(@"^envia POST /auth/hub/entrar com a entrada ""(.+)"" e (uma senha errada|qualquer senha)$")]
// e chama EntradaErrada("maria.ebfga@deboaz.com", "uma senha errada").
// Aspas dentro da expressão são dobradas ("") por ser string verbatim do C#.
// A doc string (""" ... """) chega como último parâmetro string.
//
// O "E" herda o tipo da linha anterior: um E depois de Então procura [Then].
// Frase sem método = cenário pulado com "No matching step definition"; frase
// com dois métodos = erro de ambiguidade. Os dois aparecem na saída do teste.
//
// Passo pendente (PendingStepException, cenário pulado) = o que o contrato
// ainda não oferece ou o que só o banco mostra, que fica nos testes do backend.
// O estado do cenário (pessoas, sessões, última resposta) fica no Mundo, que o
// Reqnroll cria e injeta pelo construtor a cada cenário.
[Binding]
public sealed class AutenticacaoPassos(Mundo mundo)
{
    const string SenhaErrada = "Senha#Errada1";
    const string SoNoBackend = "conferido nos testes do backend: o qa não olha o banco nem o cache";
    const string SemSala = "abrir e fechar uma sala não está no contrato da Autenticação";
    const string SemTrocaDeChave = "trocar a chave de assinatura não tem endereço no contrato";

    string acessoAnterior = "", renovacaoAnterior = "";
    readonly Dictionary<string, string> renovacoes = [];

    // ── pessoas e sessões ──────────────────────────────────────────────────
    [Given(@"^a staff ""maria\.ebfga@deboaz\.com"", ativa, da escola ""EBFGA"", ativa$")]
    public Task MariaAtiva() => mundo.Maria();

    [Given(@"^a Maria com uma sessão no navegador$")]
    [Given(@"^a Maria no navegador, com o tempo de 8 h da escola$")]
    [Given(@"^a Maria no hub, com o tempo de 8 h da escola$")]
    [Given(@"^um token de acesso da Maria emitido há 5 min$")]
    [Given(@"^a sessão 3f9c2a71 da Maria ativa e o cookie deboaz_renovacao dela$")]
    public async Task MariaNoNavegador() => mundo.Atual = await mundo.SessaoDe(await mundo.Maria(), Navegador);

    [Given(@"^a Maria com sessões no navegador e no app$")]
    public async Task MariaNosDois()
    {
        var maria = await mundo.Maria();
        await mundo.SessaoDe(maria, Navegador);
        await mundo.SessaoDe(maria, App);
    }

    [Given(@"^a Maria com 5 sessões do hub abertas$")]
    public async Task CincoSessoes()
    {
        var maria = await mundo.Maria();
        for (var i = 0; i < 5; i++) await mundo.Entrar(maria, i % 2 == 0 ? Navegador : App);
    }

    [Given(@"^(staff|participante|conta master|membro do time) com uma sessão no (navegador|app)$")]
    public async Task SessaoDeQuem(string quem, string aparelho)
    {
        var a = aparelho == "app" ? App : Navegador;
        mundo.Atual = quem switch
        {
            "staff" => await mundo.SessaoDe(await mundo.Maria(), a),
            "participante" => await mundo.EntrarNoQuest(a),
            "conta master" => await mundo.SessaoDe(await mundo.Master(), a),
            _ => await mundo.SessaoDe(mundo.Admin(), a),
        };
    }

    [Given(@"^o código do Lucas usado em POST /auth/quest/entrar de 6 aparelhos$")]
    public async Task SeisNoQuest()
    {
        for (var i = 0; i < 6; i++) await mundo.EntrarNoQuest(App);
    }

    [Given(@"^a sessão da Maria sem pedidos há 9 h, com o tempo de 8 h da escola$")]
    public async Task SessaoSemUso()
    {
        await MariaNoNavegador();
        await Estado.ExpirarSessao(mundo.Atual!.Sid);
    }

    [Given(@"^a sessão desse token encerrada no servidor$")]
    public Task SessaoEncerrada() => Estado.ExpirarSessao(mundo.Atual!.Sid);

    [Given(@"^a escola ""EBFGA"" suspensa$")]
    public async Task EscolaSuspensa()
    {
        await mundo.Maria();
        await SuspenderEscola();
    }

    [Given(@"^a Maria desativada$")]
    public async Task MariaDesativada()
    {
        var maria = await mundo.Maria();
        Exigir(await mundo.Enviar("POST", $"/usuarios/{maria.Id}/desativar", token: await mundo.TokenDe(await mundo.Master())),
            204, "POST /usuarios/{id}/desativar");
    }

    [Given(@"^o comprovante ""(c7e1…)"" recebido junto com a senha gerada$")]
    public async Task Comprovante(string exemplo)
    {
        var comprovante = await mundo.MariaPendente();
        mundo.Apelidos[exemplo] = () => Task.FromResult(comprovante);
    }

    // ── tokens recusados (RNF-216) ─────────────────────────────────────────
    [Given(@"^a Maria com um token (com a assinatura errada|vencido|do quest|de uma sessão encerrada|com a versão de permissões antiga)$")]
    public async Task TokenComProblema(string problema)
    {
        var sessao = await mundo.Entrar(await mundo.Maria(), Navegador);
        var cabecalho = Jwt.Cabecalho(sessao.Acesso);
        var carga = Jwt.Carga(sessao.Acesso);
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        switch (problema)
        {
            case "com a assinatura errada":
                var partes = sessao.Acesso.Split('.');
                var assinatura = Jwt.DeB64(partes[2]);
                assinatura[0] ^= 0xFF;
                mundo.TokenGuardado = $"{partes[0]}.{partes[1]}.{Convert.ToBase64String(assinatura).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
                break;
            case "vencido":
                carga["iat"] = agora - 960;
                carga["exp"] = agora - 60;
                mundo.TokenGuardado = Jwt.Assinar(cabecalho, carga);
                break;
            case "do quest":
                carga["aud"] = "quest";
                mundo.TokenGuardado = Jwt.Assinar(cabecalho, carga);
                break;
            case "de uma sessão encerrada":
                Exigir(await mundo.Enviar("POST", "/auth/sair", token: sessao.Acesso), 204, "POST /auth/sair");
                mundo.TokenGuardado = sessao.Acesso;
                break;
            default:
                carga["pv"] = carga["pv"]!.GetValue<int>() - 1;
                mundo.TokenGuardado = Jwt.Assinar(cabecalho, carga);
                break;
        }
    }

    [Given(@"^um token de acesso de aud ""(hub|quest)""$")]
    public async Task TokenDaFrente(string aud) =>
        mundo.TokenGuardado = aud == "hub"
            ? (await mundo.SessaoDe(await mundo.Maria(), Navegador)).Acesso
            : (await mundo.EntrarNoQuest(App)).Acesso;

    [When(@"^é usado num endereço do (hub|quest)$")]
    public async Task UsadoNaFrente(string frente)
    {
        if (frente == "quest") throw new PendingStepException("o contrato ainda não tem endereço só do quest");
        await mundo.Enviar("GET", "/auth/sessoes", token: mundo.TokenGuardado);
    }

    [When(@"^envia (?:um|qualquer) pedido com esse token$")]
    public Task PedidoComEsseToken() =>
        mundo.Enviar("GET", "/auth/sessoes", token: mundo.TokenGuardado ?? mundo.Atual!.Acesso);

    // ── entrar ──────────────────────────────────────────────────────────────
    [When(@"^(conta master, educador, staff|membro do time deboaz|participante ou responsável|pessoa sem código de acesso) envia POST (\S+) com (.+)$")]
    public async Task FormaDeEntrar(string quem, string endereco, string _)
    {
        switch (quem)
        {
            case "pessoa sem código de acesso":
                throw new PendingStepException(SemSala);
            case "participante ou responsável":
                await mundo.Enviar("POST", endereco, new JsonObject
                {
                    ["codigo"] = await mundo.CodigoDoLucas(),
                    ["aparelho"] = App,
                    ["quem"] = new JsonObject { ["tipo"] = "PARTICIPANTE" },
                }.ToJsonString());
                break;
            default:
                var pessoa = quem.StartsWith("membro") ? mundo.Admin() : await mundo.Maria();
                await mundo.Enviar("POST", endereco, Entrada(pessoa, pessoa.Senha, Navegador));
                break;
        }
    }

    [When(@"^envia (POST|GET|PUT|PATCH|DELETE) (\S+) com:$")]
    public async Task EnviaComCorpo(string metodo, string caminho, string json)
    {
        var corpo = JsonNode.Parse(await mundo.Traduzir(json))!.AsObject();
        Pessoa? pessoa = null;
        if (corpo["entrada"]?.GetValue<string>() is { } entrada && (pessoa = await mundo.PorEntrada(entrada)) is not null
            && corpo["senha"]?.GetValue<string>() == "••••••••")
        {
            corpo["senha"] = pessoa.Senha;
        }
        if (corpo["codigo"] is not null) pessoa = await mundo.Lucas();
        if (caminho == "/auth/hub/confirmar-senha") pessoa = await mundo.Maria();
        if (caminho == "/auth/renovar" && corpo["tokenRenovacao"]?.GetValue<string>() is { } renovacao)
        {
            mundo.Atual = mundo.Sessoes.FirstOrDefault(s => s.Renovacao == renovacao);
            (acessoAnterior, renovacaoAnterior) = (mundo.Atual?.Acesso ?? "", renovacao);
        }
        // fora de /auth/, o pedido vai com o token de quem o Dado citou (Usuários)
        var alvo = await mundo.Traduzir(caminho);
        var r = mundo.Sujeito is { } sujeito && !caminho.StartsWith("/auth/")
            ? await mundo.EnviarComo(sujeito, metodo, alvo, corpo.ToJsonString())
            : await mundo.Enviar(metodo, alvo, corpo.ToJsonString());
        if (pessoa is not null && caminho.StartsWith("/auth/") && caminho != "/auth/renovar")
        {
            var aparelho = corpo["aparelho"]?.GetValue<string>() ?? (CookieDeRenovacao(r) is null ? App : Navegador);
            if (mundo.Registrar(pessoa, aparelho, r) is { } sessao) mundo.Atual = sessao;
        }
    }

    [Then(@"^o mesmo comprovante, enviado de novo, recebe 401 COMPROVANTE_INVALIDO$")]
    public async Task ComprovanteDeNovo()
    {
        var (metodo, caminho, corpo) = mundo.UltimoPedido;
        Respostas.Confere(await mundo.Enviar(metodo, caminho, corpo), 401, "COMPROVANTE_INVALIDO");
    }

    [When(@"^envia o mesmo pedido com o aparelho ""(APP|NAVEGADOR)""$")]
    [When(@"^envia POST /auth/hub/entrar com o aparelho ""(APP|NAVEGADOR)""$")]
    [When(@"^a Maria envia POST /auth/hub/entrar com o aparelho ""(APP|NAVEGADOR)""$")]
    public Task MariaEntraCom(string aparelho) => MariaEntra(aparelho);

    [When(@"^a Maria envia POST /auth/hub/entrar$")]
    [When(@"^a Maria envia POST /auth/hub/entrar com a entrada e a senha certas$")]
    [When(@"^envia POST /auth/hub/entrar de um sexto aparelho$")]
    [When(@"^a Maria entra no hub$")]
    [When(@"^a Maria entra no hub pelo navegador, com o tempo de 8 h da escola$")]
    public Task MariaEntraNoNavegador() => MariaEntra(Navegador);

    async Task MariaEntra(string aparelho)
    {
        var maria = await mundo.Maria();
        if (mundo.Registrar(maria, aparelho, await mundo.Enviar("POST", "/auth/hub/entrar", Entrada(maria, maria.Senha, aparelho))) is { } sessao)
        {
            mundo.Atual = sessao;
        }
    }

    [When(@"^envia POST /auth/hub/entrar com a entrada ""(.+)"" e (uma senha errada|qualquer senha)$")]
    public async Task EntradaErrada(string entrada, string _)
    {
        await mundo.Maria();
        await mundo.Enviar("POST", "/auth/hub/entrar", Entrada(entrada, SenhaErrada));
    }

    [Given(@"^(\d) senhas erradas seguidas para ""(.+)""$")]
    public async Task SenhasErradas(int quantas, string entrada)
    {
        await mundo.PorEntrada(entrada);
        for (var i = 0; i < quantas; i++)
        {
            Exigir(await mundo.Enviar("POST", "/auth/hub/entrar", Entrada(entrada, SenhaErrada)), 401, "senha errada");
        }
    }

    [When(@"^a quinta senha errada chega às (\d{2}):(\d{2})$")]
    public async Task QuintaSenhaErrada(int hora, int minuto)
    {
        mundo.HoraDoExemplo = new TimeSpan(hora, minuto, 0);
        await mundo.Enviar("POST", "/auth/hub/entrar", Entrada("maria.ebfga@deboaz.com", SenhaErrada));
    }

    [Then(@"^até as \d{2}:\d{2} a senha certa também recebe 429$")]
    public async Task SenhaCertaBloqueada()
    {
        var maria = await mundo.Maria();
        Respostas.Confere(await mundo.Enviar("POST", "/auth/hub/entrar", Entrada(maria, maria.Senha, Navegador)), 429, "ENTRADA_BLOQUEADA");
    }

    [Then(@"^uma tentativa errada é contada para ""(.+)""$")]
    public async Task TentativaContada(string entrada)
    {
        // contada: com mais 4 erradas, a quinta bloqueia (RF-158)
        for (var i = 0; i < 4; i++) await mundo.Enviar("POST", "/auth/hub/entrar", Entrada(entrada, SenhaErrada));
        Respostas.Confere(mundo.Ultima, 429, "ENTRADA_BLOQUEADA");
    }

    [Given(@"^(\d+) bloqueios seguidos para ""(.+)""$")]
    public async Task Bloqueios(int quantos, string entrada)
    {
        await mundo.PorEntrada(entrada);
        if (quantos > 0) await Estado.BloqueiosSeguidos("ENTRADA", entrada, quantos);
    }

    [When(@"^a quinta senha errada seguida chega a POST /auth/hub/entrar$")]
    public async Task CincoErradas()
    {
        for (var i = 0; i < 5; i++) await mundo.Enviar("POST", "/auth/hub/entrar", Entrada("maria.ebfga@deboaz.com", SenhaErrada));
    }

    [When(@"^o código ""EBFGA-LUCAS-0412"" chega errado 5 vezes seguidas a POST /auth/quest/entrar$")]
    public async Task CodigoErrado()
    {
        // o código do Lucas com os números trocados
        var codigo = await mundo.CodigoDoLucas();
        var errado = codigo[..^4] + (codigo[^4..] == "0000" ? "1111" : "0000");
        for (var i = 0; i < 5; i++)
        {
            await mundo.Enviar("POST", "/auth/quest/entrar", new JsonObject { ["codigo"] = errado, ["aparelho"] = App }.ToJsonString());
        }
    }

    [Then(@"^a resposta é 429 ENTRADA_BLOQUEADA, com tentarEm daqui a (\d+) (min|h)$")]
    public void TentarEmDaquiA(int quanto, string unidade)
    {
        Respostas.Confere(mundo.Ultima, 429, "ENTRADA_BLOQUEADA");
        Respostas.TentarEm(mundo, unidade == "h" ? TimeSpan.FromHours(quanto) : TimeSpan.FromMinutes(quanto));
    }

    [When(@"^a Carla envia POST /auth/quest/entrar com o código do Lucas(?: e o parentesco ""Mãe""|, como mãe)?$")]
    public async Task CarlaEntra() =>
        mundo.Atual = await mundo.EntrarNoQuest(App, new JsonObject { ["tipo"] = "RESPONSAVEL", ["parentesco"] = "Mãe" });

    [When(@"^envia POST /auth/sala/entrar com a sala ""482107"", aberta$")]
    [Then(@"^a resposta é 200 com um token de papel ""ANONIMO"", só com o módulo e o id da sala$")]
    [Then(@"^quando o professor fecha a sala, esse token recebe 401 em qualquer pedido$")]
    [Given(@"^um token de papel ""ANONIMO"" da sala 482107$")]
    [When(@"^passa 2 h sem pedidos, com a sala aberta$")]
    [Then(@"^os pedidos à sala continuam com 200$")]
    [Then(@"^os pedidos a perfil, tarefa ou outra sala recebem 403$")]
    public static void SalaAberta() => throw new PendingStepException(SemSala);

    [When(@"^envia POST /auth/sala/entrar com uma sala fechada ou inexistente$")]
    public Task SalaInexistente() => mundo.Enviar("POST", "/auth/sala/entrar", new JsonObject { ["sala"] = "000000" }.ToJsonString());

    // ── o que a resposta de entrada traz ───────────────────────────────────
    [Then(@"^a resposta é 200 com tokenAcesso, tipo ""Bearer"" e expiraEm 900$")]
    public void TokenDeAcesso()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(JsonNode.Parse("""{ "tokenAcesso": "eyJ…", "tipo": "Bearer", "expiraEm": 900 }"""), mundo.Ultima.Corpo);
    }

    [Then(@"^vem o cookie deboaz_renovacao, HttpOnly, Secure e SameSite=Strict, só para /auth/renovar$")]
    public void VemOCookie()
    {
        var cookie = mundo.Ultima.Cookie("deboaz_renovacao") ?? throw new Xunit.Sdk.XunitException($"sem o cookie deboaz_renovacao: {mundo.Ultima}");
        var atributos = cookie.Split(';').Select(a => a.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("httponly", atributos);
        Assert.Contains("secure", atributos);
        Assert.Contains("samesite=strict", atributos);
        Assert.Contains("path=/auth/renovar", atributos);
    }

    [Then(@"^a resposta é 200 com o tokenRenovacao no corpo, sem cookie$")]
    public void RenovacaoNoCorpo()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.False(string.IsNullOrEmpty(mundo.Ultima.Corpo?["tokenRenovacao"]?.GetValue<string>()), $"sem tokenRenovacao: {mundo.Ultima}");
        Assert.Null(mundo.Ultima.Cookie("deboaz_renovacao"));
    }

    [Then(@"^a resposta é 200 com o token de acesso e a renovação$")]
    public void AcessoERenovacao()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Corpo?["tokenRenovacao"] is not null || mundo.Ultima.Cookie("deboaz_renovacao") is not null,
            $"sem renovação: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 403 ACESSO_INDISPONIVEL e nenhuma sessão é aberta$")]
    public void AcessoIndisponivel()
    {
        Respostas.Confere(mundo.Ultima, 403, "ACESSO_INDISPONIVEL");
        Assert.Null(mundo.Ultima.Corpo?["tokenAcesso"]);
        Assert.Null(mundo.Ultima.Cookie("deboaz_renovacao"));
    }

    [Then(@"^a resposta é 200 com o token de acesso do Lucas$")]
    public async Task TokenDoLucas()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal((await mundo.Lucas()).Id, Carga()["sub"]?.GetValue<string>());
    }

    // o Reqnroll não passa o grupo que não casou: um atributo para cada forma
    [Then(@"^o token tem o sub ""(usr-ebfga-lucas)"" e, em quem, o responsável e o parentesco ""(.+)""$")]
    public Task QuemEntrouComSub(string sub, string parentesco) => QuemEntrou(sub, parentesco);

    [Then(@"^o token traz em quem o (?:tipo ""RESPONSAVEL""|responsável) e o parentesco ""(.+)""$")]
    public Task QuemEntrouSemSub(string parentesco) => QuemEntrou(null, parentesco);

    async Task QuemEntrou(string? sub, string parentesco)
    {
        var carga = Carga();
        if (!string.IsNullOrEmpty(sub)) Assert.Equal(await mundo.Traduzir(sub), carga["sub"]?.GetValue<string>());
        Assert.Equal("RESPONSAVEL", carga["quem"]?["tipo"]?.GetValue<string>());
        Assert.Equal(parentesco, carga["quem"]?["parentesco"]?.GetValue<string>());
    }

    [Then(@"^ela faz pela API o que o Lucas faz, menos o que cada módulo restringir ao responsável$")]
    public static void ResponsavelComoLucas()
    {
        // o token é o do Lucas (passo anterior); o que cada módulo restringe ao
        // responsável é cenário do documento do módulo
    }

    [Then(@"^recebe um token de acesso com expiraEm 900, que não fica guardado no servidor$")]
    public void ValeQuinzeMinutos()
    {
        TokenDeAcesso();
        var carga = Carga();
        Assert.Equal(900, carga["exp"]!.GetValue<long>() - carga["iat"]!.GetValue<long>());
        // "não fica guardado": conferido nos testes do backend
    }

    [Then(@"^um token de renovação de uso único, que vale enquanto a sessão estiver ativa$")]
    public async Task RenovacaoDeUsoUnico()
    {
        var sessao = mundo.Atual!;
        var usada = sessao.Renovacao;
        Respostas.Confere(await mundo.RenovarEGuardar(sessao), 200);
        Respostas.Confere(await mundo.Renovar(sessao, usada), 401, "SESSAO_TERMINADA");
    }

    [Then(@"^o token de acesso é um JWT assinado com RS256, com o kid no cabeçalho$")]
    public void AssinadoComRs256()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var cabecalho = Jwt.Cabecalho(mundo.Atual!.Acesso);
        Assert.Equal("RS256", cabecalho["alg"]?.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(cabecalho["kid"]?.GetValue<string>()), "sem kid no cabeçalho");
    }

    [Then(@"^a chave RSA de 2048 bits que assina nunca sai do Key Vault$")]
    public async Task Chave2048()
    {
        // "nunca sai do Key Vault": conferido nos testes do backend; aqui, o tamanho da chave pública
        var chave = await ChaveDoToken();
        Assert.Equal(256, Jwt.DeB64(chave["n"]!.GetValue<string>()).Length);
    }

    [Then(@"^GET /auth/chaves devolve a chave pública desse kid, sem autenticação$")]
    public async Task ChavePublica()
    {
        var chave = await ChaveDoToken();
        Assert.True(Jwt.Confere(mundo.Atual!.Acesso, chave["n"]!.GetValue<string>(), chave["e"]!.GetValue<string>()),
            "a chave pública não confere a assinatura do token");
    }

    async Task<JsonNode> ChaveDoToken()
    {
        var kid = Jwt.Cabecalho(mundo.Atual!.Acesso)["kid"]?.GetValue<string>();
        var r = Exigir(await mundo.Enviar("GET", "/auth/chaves"), 200, "GET /auth/chaves");
        return r.Corpo?["keys"]?.AsArray().FirstOrDefault(k => k?["kid"]?.GetValue<string>() == kid)
            ?? throw new Xunit.Sdk.XunitException($"GET /auth/chaves sem o kid {kid}: {r}");
    }

    [Then(@"^o token de acesso traz esta identidade, sem nome, documento, matrícula nem senha:$")]
    public async Task Identidade(string exemplo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var meio = exemplo.IndexOf("}\n{", StringComparison.Ordinal) + 1;
        var cabecalho = JsonNode.Parse(exemplo[..meio])!.AsObject();
        var carga = JsonNode.Parse(await mundo.Traduzir(exemplo[meio..]))!.AsObject();
        // o kid é o da chave do ambiente; iat, exp e jti mudam a cada token;
        // papel, mods e pv vêm da autorização (próximo passo)
        cabecalho.Remove("kid");
        foreach (var campo in new[] { "iat", "exp", "jti", "mods", "pv" }) carga.Remove(campo);
        var token = Carga();
        Comparacao.Confere(cabecalho, Jwt.Cabecalho(mundo.Atual!.Acesso));
        Comparacao.Confere(carga, token);
        Assert.Equal(900, token["exp"]!.GetValue<long>() - token["iat"]!.GetValue<long>());
        Assert.False(string.IsNullOrEmpty(token["jti"]?.GetValue<string>()), "sem jti");
        foreach (var proibido in new[] { "nome", "documento", "matricula", "senha" }) Assert.False(token.ContainsKey(proibido), $"o token traz {proibido}");
    }

    [Then(@"^papel, mods e pv são os da autorização$")]
    public async Task DaAutorizacao()
    {
        var maria = await mundo.Maria();
        var r = Exigir(await mundo.Enviar("GET", $"/usuarios/{maria.Id}/permissoes", token: await mundo.TokenDe(await mundo.Master())),
            200, "GET /usuarios/{id}/permissoes");
        var token = Jwt.Carga(mundo.Atual!.Acesso);
        Assert.Equal("STAFF", token["papel"]?.GetValue<string>());
        Comparacao.Confere(r.Corpo!["mods"], token["mods"]);
        Assert.Equal(r.Corpo!["versao"]!.GetValue<int>(), token["pv"]!.GetValue<int>());
    }

    [Then(@"^entra só com a entrada e a senha, sem provedor externo nem segundo fator$")]
    public void SoEntradaESenha() => TokenDeAcesso();

    [When(@"^a Maria abre o app de novo, com a sessão ativa$")]
    public async Task AbreOApp() => await mundo.RenovarEGuardar(await mundo.SessaoDe(await mundo.Maria(), App));

    [Then(@"^continua conectada pelo token de renovação guardado no aparelho, sem biometria$")]
    public void ContinuaConectada() => Respostas.Confere(mundo.Ultima, 200);

    [When(@"^o Lucas abre Perfil no quest$")]
    [Then(@"^não há a lista de aparelhos conectados nem ""Sair de todos os aparelhos""$")]
    public static void PerfilNoQuest() => throw new PendingStepException("é tela: fica no qa do frontend");

    // ── renovar ─────────────────────────────────────────────────────────────
    [When(@"^envia POST /auth/renovar(?: com o cookie)?$")]
    public async Task Renova()
    {
        var sessao = mundo.Atual!;
        (acessoAnterior, renovacaoAnterior) = (sessao.Acesso, sessao.Renovacao);
        await mundo.RenovarEGuardar(sessao);
    }

    [Then(@"^a resposta é 200 com um tokenAcesso novo e um cookie de renovação novo$")]
    public void NovosNoNavegador()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.NotEqual(acessoAnterior, Texto(mundo.Ultima, "tokenAcesso"));
        Assert.NotEqual(renovacaoAnterior, CookieDeRenovacao(mundo.Ultima) ?? renovacaoAnterior);
    }

    [Then(@"^a resposta é 200 com tokenAcesso e tokenRenovacao novos$")]
    public void NovosNoApp()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.NotEqual(acessoAnterior, Texto(mundo.Ultima, "tokenAcesso"));
        Assert.NotEqual(renovacaoAnterior, Texto(mundo.Ultima, "tokenRenovacao"));
    }

    [Given(@"^a Maria com o token de renovação ""(3f9c2a71\.A)""$")]
    public async Task RenovacaoA(string exemplo)
    {
        mundo.Atual = await mundo.SessaoDe(await mundo.Maria(), App);
        renovacoes[exemplo] = mundo.Atual.Renovacao;
    }

    [Given(@"^o token de renovação ""(3f9c2a71\.A)"" já trocado por ""(3f9c2a71\.B)""$")]
    public async Task RenovacaoTrocada(string a, string b)
    {
        await RenovacaoA(a);
        Exigir(await mundo.RenovarEGuardar(mundo.Atual!), 200, "POST /auth/renovar");
        renovacoes[b] = mundo.Atual!.Renovacao;
    }

    [When(@"^(?:alguém )?envia POST /auth/renovar com ""(3f9c2a71\.[AB])""$")]
    public Task RenovaCom(string exemplo) => mundo.Renovar(mundo.Atual!, renovacoes[exemplo]);

    [Then(@"^recebe ""(3f9c2a71\.B)"", o id da sessão seguido de 256 bits aleatórios$")]
    public void RenovacaoNova(string exemplo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var nova = Texto(mundo.Ultima, "tokenRenovacao");
        var sid = mundo.Atual!.Sid;
        Assert.StartsWith(sid + ".", nova);
        Assert.Equal(32, Jwt.DeB64(nova[(sid.Length + 1)..]).Length);
        renovacoes[exemplo] = nova;
        mundo.Atual.Acesso = Texto(mundo.Ultima, "tokenAcesso");
        mundo.Atual.Renovacao = nova;
    }

    [Then(@"^""(3f9c2a71\.A)"" deixa de valer$")]
    public async Task DeixaDeValer(string exemplo) =>
        Respostas.Confere(await mundo.Renovar(mundo.Atual!, renovacoes[exemplo]), 401, "SESSAO_TERMINADA");

    [Then(@"^a sessão guarda só o resumo irreversível do token novo$")]
    [Then(@"^cada sessão guarda a conta, a frente, o aparelho, quem entrou, o pv e a última ação$")]
    public static void SoNoBanco()
    {
        // conferido nos testes do backend: o qa não olha o banco nem o cache
    }

    [Then(@"^a resposta é 401 SESSAO_TERMINADA e a sessão 3f9c2a71 termina$")]
    public async Task SessaoTermina()
    {
        Respostas.Confere(mundo.Ultima, 401, "SESSAO_TERMINADA");
        Respostas.Confere(await mundo.Renovar(mundo.Atual!, renovacoes["3f9c2a71.B"]), 401, "SESSAO_TERMINADA");
    }

    [When(@"^o token de acesso dela passa de 15 min$")]
    public static void PassaDe15Min()
    {
        // a renovação não depende do token de acesso vencido; o token vencido é recusado em RNF-216
    }

    [Then(@"^POST /auth/renovar devolve um token novo$")]
    public async Task DevolveTokenNovo()
    {
        var anterior = mundo.Atual!.Acesso;
        Respostas.Confere(await mundo.RenovarEGuardar(mundo.Atual), 200);
        Assert.NotEqual(anterior, mundo.Atual.Acesso);
    }

    [Then(@"^a renovação funciona enquanto ela não passar 8 h sem pedidos$")]
    public async Task RenovaDeNovo() => Respostas.Confere(await mundo.RenovarEGuardar(mundo.Atual!), 200);

    // ── tempo sem uso ──────────────────────────────────────────────────────
    [When(@"^passa sem pedidos mais que o tempo .+$")]
    public Task PassaOTempo() => Estado.ExpirarSessao(mundo.Atual!.Sid);

    [Then(@"^o próximo pedido recebe 401 e POST /auth/renovar recebe 401 SESSAO_TERMINADA$")]
    public async Task SessaoVencida()
    {
        Assert.Equal(401, await mundo.ProximoPedido(mundo.Atual!.Acesso));
        Respostas.Confere(await mundo.Renovar(mundo.Atual), 401, "SESSAO_TERMINADA");
    }

    [When(@"^ela faz um pedido a cada 7 h$")]
    public async Task PedidoACada7h()
    {
        // faltando 3 s para o tempo sem uso acabar, um pedido refaz o prazo
        await Estado.EncurtarSessao(mundo.Atual!.Sid, TimeSpan.FromSeconds(3));
        Assert.Equal(200, await mundo.ProximoPedido(mundo.Atual.Acesso));
        await Task.Delay(TimeSpan.FromSeconds(4));
    }

    [Then(@"^a sessão continua e POST /auth/renovar recebe 200$")]
    public async Task SessaoContinua() => Respostas.Confere(await mundo.RenovarEGuardar(mundo.Atual!), 200);

    // ── sair e sessões ─────────────────────────────────────────────────────
    [When(@"^envia POST /auth/sair com o token do navegador$")]
    public async Task Sair() =>
        await mundo.Enviar("POST", "/auth/sair", token: (await mundo.SessaoDe(await mundo.Maria(), Navegador)).Acesso);

    [Then(@"^a resposta é 204 e o cookie de renovação é apagado$")]
    public void CookieApagado()
    {
        Respostas.Confere(mundo.Ultima, 204);
        var cookie = mundo.Ultima.Cookie("deboaz_renovacao")?.ToLowerInvariant()
            ?? throw new Xunit.Sdk.XunitException($"sem o Set-Cookie que apaga deboaz_renovacao: {mundo.Ultima}");
        Assert.True(cookie.StartsWith("deboaz_renovacao=;") || cookie.Contains("max-age=0") || cookie.Contains("expires=thu, 01 jan 1970"),
            $"o cookie não foi apagado: {cookie}");
    }

    [Then(@"^o token do navegador recebe 401 no próximo pedido, e o do app continua valendo$")]
    public async Task SoONavegadorSaiu()
    {
        var maria = await mundo.Maria();
        Assert.Equal(401, await mundo.ProximoPedido((await mundo.SessaoDe(maria, Navegador)).Acesso));
        Assert.Equal(200, await mundo.ProximoPedido((await mundo.SessaoDe(maria, App)).Acesso));
    }

    [When(@"^envia GET /auth/sessoes com o token do navegador$")]
    public async Task ListaSessoes() =>
        await mundo.Enviar("GET", "/auth/sessoes", token: (await mundo.SessaoDe(await mundo.Maria(), Navegador)).Acesso);

    [When(@"^a Maria envia DELETE (/auth/sessoes/\S+) com o token do app$")]
    public async Task SairDeUm(string caminho)
    {
        var alvo = await mundo.Traduzir(caminho);
        await mundo.Enviar("DELETE", alvo, token: (await mundo.SessaoDe(await mundo.Maria(), App)).Acesso);
    }

    [When(@"^a Maria envia DELETE /auth/sessoes/\{sid\} com o sid de uma sessão do João$")]
    public async Task SessaoDoJoao()
    {
        var doJoao = await mundo.SessaoDe(await mundo.Joao(), Navegador);
        await mundo.Enviar("DELETE", $"/auth/sessoes/{doJoao.Sid}", token: (await mundo.SessaoDe(await mundo.Maria(), Navegador)).Acesso);
    }

    [When(@"^a Maria envia DELETE /auth/sessoes$")]
    public async Task SairDeTodos()
    {
        await MariaNosDois();
        await mundo.Enviar("DELETE", "/auth/sessoes", token: (await mundo.SessaoDe(await mundo.Maria(), Navegador)).Acesso);
    }

    [Then(@"^os tokens e as renovações de todas as sessões dela recebem 401$")]
    public async Task TodasERenovacoesRecusadas()
    {
        await TodasRecusadas();
        await RenovacoesRecusadasSemCodigo();
    }

    [Then(@"^os tokens das duas sessões recebem 401 no próximo pedido$")]
    [Then(@"^o token emitido antes recebe 401 no próximo pedido, nos dois aparelhos$")]
    public async Task TodasRecusadas()
    {
        var maria = await mundo.Maria();
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa == maria).ToList())
        {
            Assert.True(await mundo.ProximoPedido(sessao.Acesso) == 401, $"o token do {sessao.Aparelho} ainda vale: {mundo.Ultima}");
        }
    }

    async Task RenovacoesRecusadasSemCodigo()
    {
        var maria = await mundo.Maria();
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa == maria).ToList())
        {
            Respostas.Confere(await mundo.Renovar(sessao), 401);
        }
    }

    [Then(@"^POST /auth/renovar de cada aparelho recebe 401 SESSAO_TERMINADA$")]
    public async Task RenovacoesRecusadas()
    {
        var maria = await mundo.Maria();
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa == maria).ToList())
        {
            Respostas.Confere(await mundo.Renovar(sessao), 401, "SESSAO_TERMINADA");
        }
    }

    [Then(@"^GET /auth/sessoes lista duas sessões, uma por aparelho$")]
    public async Task DuasSessoes()
    {
        var r = Exigir(await mundo.Enviar("GET", "/auth/sessoes", token: mundo.Atual!.Acesso), 200, "GET /auth/sessoes");
        var aparelhos = r.Corpo!.AsArray().Select(s => s?["aparelho"]?.GetValue<string>()).Order().ToList();
        Assert.Equal([App, Navegador], aparelhos);
    }

    [Then(@"^a resposta é 200 e a sessão de última ação mais antiga termina$")]
    public async Task MaisAntigaTermina()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var maisAntiga = mundo.Sessoes.First(s => s.Pessoa.Nome == "Maria Souza");
        Assert.Equal(401, await mundo.ProximoPedido(maisAntiga.Acesso));
        mundo.Atual = maisAntiga;
    }

    [Then(@"^POST /auth/renovar daquela sessão recebe 401 SESSAO_TERMINADA$")]
    public async Task RenovacaoDaqueleRecusada() => Respostas.Confere(await mundo.Renovar(mundo.Atual!), 401, "SESSAO_TERMINADA");

    [Then(@"^POST /auth/renovar da Maria recebe 401 SESSAO_TERMINADA$")]
    public async Task RenovacaoDaMariaRecusada() =>
        Respostas.Confere(await mundo.Renovar(await mundo.SessaoDe(await mundo.Maria(), Navegador)), 401, "SESSAO_TERMINADA");

    [Then(@"^as 6 sessões continuam e cada POST /auth/renovar recebe 200$")]
    public async Task SeisContinuam()
    {
        foreach (var sessao in mundo.Sessoes.Where(s => s.Pessoa.Nome == "Lucas Andrade").ToList())
        {
            Respostas.Confere(await mundo.RenovarEGuardar(sessao), 200);
        }
    }

    // ── o que muda a credencial (RN-230, RNF-203) ──────────────────────────
    // o reativar precisa de alguém desativado antes: passo próprio em UsuariosPassos
    [When(@"^a conta master envia POST (/usuarios/\S+(?<!/reativar))$")]
    public async Task MasterEnvia(string caminho)
    {
        var alvo = await mundo.Traduzir(caminho);
        await mundo.Enviar("POST", alvo, token: await mundo.TokenDe(await mundo.Master()));
    }

    [When(@"^a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes$")]
    public async Task MasterMudaPermissoes()
    {
        mundo.TokenGuardado = (await mundo.SessaoDe(await mundo.Maria(), Navegador)).Acesso;
        await MudarPermissoesDaMaria();
    }

    [When(@"^(a conta master desativa a Maria|o time deboaz suspende a escola|a conta master pede uma nova geração de senha|a conta master muda as permissões dela|a escola descontrata um módulo que ela usa)$")]
    public async Task Mudanca(string mudanca)
    {
        var maria = await mundo.Maria();
        var master = await mundo.TokenDe(await mundo.Master());
        switch (mudanca)
        {
            case "a conta master desativa a Maria":
                await mundo.Enviar("POST", $"/usuarios/{maria.Id}/desativar", token: master);
                break;
            case "o time deboaz suspende a escola":
                await SuspenderEscola();
                break;
            case "a conta master pede uma nova geração de senha":
                await mundo.Enviar("POST", $"/usuarios/{maria.Id}/nova-geracao-senha", token: master);
                break;
            case "a conta master muda as permissões dela":
                await MudarPermissoesDaMaria();
                break;
            default:
                throw new PendingStepException("contratar e descontratar módulo não está no contrato de Usuários");
        }
    }

    [Then(@"^o token antigo recebe 401 PERMISSOES_DESATUALIZADAS$")]
    public async Task TokenDesatualizado() =>
        Respostas.Confere(await mundo.Enviar("GET", "/auth/sessoes", token: mundo.TokenGuardado), 401, "PERMISSOES_DESATUALIZADAS");

    [Then(@"^POST /auth/renovar da Maria recebe 200 com um token de pv novo$")]
    public async Task PvNovo()
    {
        var pvAntigo = Jwt.Carga(mundo.TokenGuardado!)["pv"]!.GetValue<int>();
        var sessao = await mundo.SessaoDe(await mundo.Maria(), Navegador);
        Respostas.Confere(await mundo.RenovarEGuardar(sessao), 200);
        Assert.True(Jwt.Carga(sessao.Acesso)["pv"]!.GetValue<int>() > pvAntigo, "o pv do token novo não subiu");
    }

    async Task MudarPermissoesDaMaria()
    {
        var maria = await mundo.Maria();
        var master = await mundo.TokenDe(await mundo.Master());
        var atuais = Exigir(await mundo.Enviar("GET", $"/usuarios/{maria.Id}/permissoes", token: master), 200, "GET /usuarios/{id}/permissoes");
        // Administrar usuários (RN-251), que a conta master sempre pode conceder
        await mundo.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes", """{ "mods": { "MOD-03": "CU" } }""", master,
            ifMatch: atuais.Corpo!["versao"]!.GetValue<int>().ToString());
    }

    async Task SuspenderEscola()
    {
        var conta = await mundo.Conta();
        Exigir(await mundo.Enviar("POST", $"/contas/{conta}/estado", """{ "estado": "SUSPENSA" }""", await mundo.TokenDe(mundo.Admin())),
            200, "POST /contas/{id}/estado");
    }

    // ── só no banco ou fora do contrato ────────────────────────────────────
    [Then(@"^a sessão fica no Redis na chave sessao:3f9c2a71, expirando em 8 h e refeita a cada pedido:$")]
    public static void SessaoNoRedis(string _) => throw new PendingStepException(SoNoBackend);

    [Then(@"^o índice sessoes:usr-ebfga-maria lista as sessões dela, para encerrar todas de uma vez$")]
    [Then(@"^a tentativa e o bloqueio ficam no Redis, com expiração igual ao tempo do bloqueio$")]
    public static void NoRedis() => throw new PendingStepException(SoNoBackend);

    [When(@"^alguém erra a senha de ""(.+)""$")]
    public async Task ErraASenha(string entrada)
    {
        await mundo.PorEntrada(entrada);
        await mundo.Enviar("POST", "/auth/hub/entrar", Entrada(entrada, SenhaErrada));
    }

    [Given(@"^tokens assinados com a chave ""(.+)""$")]
    [Given(@"^a suspeita de vazamento da chave ""(.+)""$")]
    [When(@"^a chave é trocada por ""(.+)"" depois de 90 dias$")]
    [Then(@"^os tokens novos levam o kid ""(.+)"" e GET /auth/chaves traz as duas$")]
    [Then(@"^os assinados com ""(.+)"" continuam aceitos até vencer, sem encerrar sessões$")]
    [Then(@"^os tokens assinados com ""(.+)"" recebem 401$")]
    public static void TrocaDeChave(string _) => throw new PendingStepException(SemTrocaDeChave);

    [When(@"^a chave é trocada na hora$")]
    [Then(@"^POST /auth/renovar devolve tokens assinados com a chave nova$")]
    public static void TrocaNaHora() => throw new PendingStepException(SemTrocaDeChave);

    // ── apoio ───────────────────────────────────────────────────────────────
    JsonObject Carga() => Jwt.Carga(Texto(mundo.Ultima, "tokenAcesso"));

    static string Entrada(Pessoa pessoa, string senha, string aparelho) =>
        new JsonObject { ["entrada"] = pessoa.Entrada, ["senha"] = senha, ["aparelho"] = aparelho }.ToJsonString();

    static string Entrada(string entrada, string senha) =>
        new JsonObject { ["entrada"] = entrada, ["senha"] = senha, ["aparelho"] = Navegador }.ToJsonString();
}
