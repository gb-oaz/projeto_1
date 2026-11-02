using System.Net;

namespace Boaz.MsMono.Tests.Integracao;

// Endereços da Autenticação (autenticacao-contrato-api v02) com o Redis real
// guardando sessões e tentativas.
public class AutenticacaoApiTests(Backend backend) : IClassFixture<Backend>
{
    readonly Preparo preparo = new(backend);

    static string Sigla() => string.Concat(Guid.NewGuid().ToByteArray().Take(5).Select(b => (char)('A' + b % 26)));

    [Fact]
    public async Task RF_134_entra_no_navegador_com_o_cookie_e_renova()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();

        var entrada = await api.Enviar("POST", "/auth/hub/entrar", new { entrada = master.Entrada.ToUpperInvariant(), senha = master.Senha, aparelho = "NAVEGADOR" });
        var cookie = entrada.Cookie("deboaz_renovacao")!;
        var renovada = await api.Enviar("POST", "/auth/renovar", cookie: $"deboaz_renovacao={cookie}");
        var copia = await api.Enviar("POST", "/auth/renovar", cookie: $"deboaz_renovacao={cookie}");

        Assert.Equal(("Bearer", 900), (entrada.Texto("tipo"), entrada.Corpo!["expiraEm"]!.GetValue<int>()));
        Assert.Null(entrada.Corpo["tokenRenovacao"]);
        Assert.Contains(entrada.Cookies, c => c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("path=/auth/renovar"));
        Assert.Equal(HttpStatusCode.OK, renovada.Status);
        Assert.NotEqual(cookie, renovada.Cookie("deboaz_renovacao"));
        // a renovação já trocada é sinal de cópia: a sessão termina (RNF-213)
        Assert.Equal("SESSAO_TERMINADA", copia.Codigo);
    }

    [Fact]
    public async Task RF_137_renova_no_app_pelo_corpo()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();

        var renovada = await api.Enviar("POST", "/auth/renovar", new { tokenRenovacao = master.Renovacao });
        var semNada = await api.Enviar("POST", "/auth/renovar");
        var malformado = await api.Enviar("POST", "/auth/renovar", "{ ruim");

        Assert.Equal(HttpStatusCode.OK, renovada.Status);
        Assert.NotEqual(master.Renovacao, renovada.Texto("tokenRenovacao"));
        Assert.Equal("SESSAO_TERMINADA", semNada.Codigo);
        Assert.Equal("SESSAO_TERMINADA", malformado.Codigo);
    }

    [Fact]
    public async Task RNF_204_senha_errada_bloqueia_na_quinta()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();

        var respostas = new List<Resposta>();
        for (var i = 0; i < 5; i++)
        {
            respostas.Add(await api.Enviar("POST", "/auth/hub/entrar", new { entrada = master.Entrada, senha = "Errada#000000", aparelho = "APP" }));
        }

        Assert.All(respostas.Take(4), r => Assert.Equal((HttpStatusCode.Unauthorized, "ENTRADA_INVALIDA"), (r.Status, r.Codigo)));
        Assert.Equal((HttpStatusCode.TooManyRequests, "ENTRADA_BLOQUEADA"), (respostas[4].Status, respostas[4].Codigo));
    }

    [Fact]
    public async Task RF_156_RF_157_aparelhos_conectados()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var outra = await preparo.Entrar(master.Id, master.Entrada, master.Senha);
        using var api = backend.Cliente();

        var lista = await api.Enviar("GET", "/auth/sessoes", token: master.Token);
        var sidDaOutra = Sid(outra.Token);
        var encerrada = await api.Enviar("DELETE", $"/auth/sessoes/{sidDaOutra}", token: master.Token);
        var inexistente = await api.Enviar("DELETE", "/auth/sessoes/nao-existe", token: master.Token);
        var todas = await api.Enviar("DELETE", "/auth/sessoes", token: master.Token);

        Assert.Equal(2, lista.Corpo!.AsArray().Count);
        Assert.Single(lista.Corpo.AsArray(), s => s!["esteAparelho"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NoContent, encerrada.Status);
        Assert.Equal("SESSAO_NAO_ENCONTRADA", inexistente.Codigo);
        Assert.Equal(HttpStatusCode.NoContent, todas.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar("GET", "/auth/sessoes", token: master.Token)).Status);
    }

    [Fact]
    public async Task RF_138_sai_deste_aparelho()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();

        var saiu = await api.Enviar("POST", "/auth/sair", token: master.Token);

        Assert.Equal(HttpStatusCode.NoContent, saiu.Status);
        Assert.Contains(saiu.Cookies, c => c.StartsWith("deboaz_renovacao=;", StringComparison.Ordinal));
        Assert.Equal("SESSAO_TERMINADA", (await api.Enviar("GET", "/auth/sessoes", token: master.Token)).Codigo);
    }

    [Fact]
    public async Task RF_135_quest_e_aparelhos_so_no_hub()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var (_, codigo) = await preparo.Participante(master, "lucas");
        using var api = backend.Cliente();

        var quest = await api.Enviar("POST", "/auth/quest/entrar", new { codigo = codigo.ToLowerInvariant(), aparelho = "APP", quem = new { tipo = "PARTICIPANTE" } });
        var semQuem = await api.Enviar("POST", "/auth/quest/entrar", new { codigo, aparelho = "APP" });
        var token = quest.Texto("tokenAcesso");

        Assert.Equal(HttpStatusCode.OK, quest.Status);
        Assert.Equal(("DADOS_INVALIDOS", "quem"), (semQuem.Codigo, semQuem.Texto("campo")));
        Assert.Equal("TOKEN_INVALIDO", (await api.Enviar("GET", "/auth/sessoes", token: token)).Codigo);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Enviar("POST", "/auth/sair", token: token)).Status);
    }

    [Fact]
    public async Task RF_136_sala_fechada_e_chaves_publicas()
    {
        using var api = backend.Cliente();

        var sala = await api.Enviar("POST", "/auth/sala/entrar", new { sala = "000000" });
        var chaves = await api.Enviar("GET", "/auth/chaves");

        Assert.Equal((HttpStatusCode.NotFound, "SALA_FECHADA"), (sala.Status, sala.Codigo));
        var chave = chaves.Corpo!["keys"]![0]!;
        Assert.Equal(("RSA", "RS256", "AQAB"), (chave["kty"]!.GetValue<string>(), chave["alg"]!.GetValue<string>(), chave["e"]!.GetValue<string>()));
    }

    [Fact]
    public async Task RF_143_comprovante_vale_uma_vez()
    {
        using var api = backend.Cliente();
        var sigla = Sigla();
        var criada = await api.Enviar("POST", "/contas", new
        {
            tipo = "AUTONOMO",
            nome = "Pedro Reis",
            documento = "123.456.789-09",
            responsavel = "Pedro Reis",
            telefone = "(51) 99999-0000",
            dominio = sigla.ToLowerInvariant(),
            sigla,
        });

        var primeira = await api.Enviar("POST", "/auth/hub/confirmar-senha", new { comprovante = criada.Texto("comprovante"), aparelho = "APP" });
        var segunda = await api.Enviar("POST", "/auth/hub/confirmar-senha", new { comprovante = criada.Texto("comprovante") });

        Assert.Equal(HttpStatusCode.OK, primeira.Status);
        Assert.NotNull(primeira.Corpo!["tokenRenovacao"]);
        Assert.Equal("COMPROVANTE_INVALIDO", segunda.Codigo);
    }

    [Fact]
    public async Task Administrador_volta_mesmo_com_o_banco_vazio()
    {
        // o banco desta fixture pode já ter o time; sem ele, a entrada o cria
        await backend.Banco.DropCollectionAsync("usuarios");
        await backend.Banco.DropCollectionAsync("contas");

        var admin = await preparo.Admin();
        using var api = backend.Cliente();

        Assert.Equal(HttpStatusCode.OK, (await api.Enviar("GET", "/time/tempo-sessao", token: admin.Token)).Status);
    }

    static string Sid(string token) =>
        System.Text.Json.Nodes.JsonNode.Parse(Convert.FromBase64String(Base64(token.Split('.')[1])))!["sid"]!.GetValue<string>();

    static string Base64(string url)
    {
        var s = url.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }
}
