using Boaz.Core.Commons.Auditoria.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Os adaptadores primários do SAC pelo bloco comum de sac-contrato-api v03
// (18 RN-297; 17 RN-270, RN-271): ChamadosController, AnexosController,
// ConfiguracaoDoSacController, CardDoSacController e EventosDoSac. O que o
// requisito do SAC não traz como cenário e o Usuários prova em
// UsuariosApiTests e ErroInesperadoApiTests: o X-Trace-Id e os registros da
// execução auditada, o 403 auditado, o 401 antes de tudo, o corpo que não é
// JSON, o 500 sem detalhe, o 405 sem login e o fluxo SSE sem cache.
public class SacContratoApiTests(Backend backend) : IClassFixture<Backend>
{
    const string Detalhe = "detalhe interno que não pode sair na resposta";

    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    readonly Preparo preparo = new(backend);

    static object Corpo(string assunto = "Presença não salva a chamada") => new
    {
        categoria = "MODULO_COM_PROBLEMA",
        assunto,
        descricao = "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.",
        relacionadoA = (string?)null,
        anexos = Array.Empty<string>(),
    };

    Task<List<BsonDocument>> Registros(string colecao, string traceId) =>
        backend.Banco.GetCollection<BsonDocument>(colecao).Find(Builders<BsonDocument>.Filter.Eq("traceId", traceId)).ToListAsync();

    Task<long> Contar(string colecao) =>
        backend.Banco.GetCollection<BsonDocument>(colecao).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    // RN-297, 17 RN-178, RN-270, RN-271: abrir é auditado; o X-Trace-Id vai no
    // sucesso e na recusa, com "INICIADO" e "CONCLUIDO" (alvo: o chamado) no
    // sucesso e só "INICIADO" mais o ALERTA em "auditoriaErros" na recusa
    [Fact]
    public async Task RN_297_abrir_auditado_pelo_traceId_no_sucesso_e_na_recusa()
    {
        var (contaId, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var api = backend.Cliente();

        var aberto = await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token);
        var curto = await api.Enviar("POST", "/sac/chamados", Corpo("Erro"), master.Token);

        Assert.Equal(HttpStatusCode.Created, aberto.Status);
        Assert.Matches("^trc-[0-9a-f]{8}$", aberto.TraceId);
        var etapas = (await Registros("auditoria", aberto.TraceId!)).ToDictionary(d => d["etapa"].AsString);
        Assert.Equal(new[] { "CONCLUIDO", "INICIADO" }, etapas.Keys.Order().ToArray());
        Assert.All(etapas.Values, d => Assert.Equal(("ABRIR_CHAMADO", master.Id, contaId),
            (d["acao"].AsString, d["quem"].AsString, d["contaId"].AsString)));
        Assert.Equal(aberto.Texto("id"), etapas["CONCLUIDO"]["alvo"].AsString);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DADOS_INVALIDOS", "assunto"), (curto.Status, curto.Codigo, curto.Texto("campo")));
        Assert.Matches("^trc-[0-9a-f]{8}$", curto.TraceId);
        Assert.Equal("INICIADO", Assert.Single(await Registros("auditoria", curto.TraceId!))["etapa"].AsString);
        var erro = Assert.Single(await Registros("auditoriaErros", curto.TraceId!));
        Assert.Equal(("ABRIR_CHAMADO", "DADOS_INVALIDOS", "ALERTA"), (erro["acao"].AsString, erro["codigo"].AsString, erro["nivel"].AsString));
    }

    // RN-297: as ações sobre o chamado gravam o {id} da rota como alvo, e salvar
    // os prazos grava SALVAR_PRAZOS_SAC, todas com o X-Trace-Id
    [Fact]
    public async Task RN_297_acoes_sobre_o_chamado_com_o_id_como_alvo()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var admin = await preparo.Admin();
        using var api = backend.Cliente();
        var id = (await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token)).Texto("id");

        var acoes = new (string Acao, Resposta Resposta)[]
        {
            ("ASSUMIR_CHAMADO", await api.Enviar("POST", $"/sac/chamados/{id}/assumir", new { substituir = false }, admin.Token)),
            ("ENVIAR_MENSAGEM", await api.Enviar("POST", $"/sac/chamados/{id}/mensagens", new { texto = "Qual a turma?" }, admin.Token)),
            ("MUDAR_STATUS", await api.Enviar("POST", $"/sac/chamados/{id}/status", new { status = "RESOLVIDO", mensagem = "Corrigido." }, admin.Token)),
            ("REABRIR_CHAMADO", await api.Enviar("POST", $"/sac/chamados/{id}/reabrir", new { mensagem = "Ainda aparece" }, master.Token)),
            ("ENCERRAR_CHAMADO", await api.Enviar("POST", $"/sac/chamados/{id}/encerrar", token: master.Token)),
        };
        var prazos = await api.Enviar("PUT", "/sac/configuracao", new { prazos = new[] { new { categoria = "COBRANCA", horasUteis = 8 } } },
            admin.Token);

        foreach (var (acao, resposta) in acoes)
        {
            Assert.True(resposta.TraceId is { } trace && trace.StartsWith("trc-", StringComparison.Ordinal), $"{acao} sem X-Trace-Id ({resposta.Status})");
            var concluido = Assert.Single(await Registros("auditoria", resposta.TraceId!), d => d["etapa"].AsString == "CONCLUIDO");
            Assert.Equal((acao, id), (concluido["acao"].AsString, concluido["alvo"].AsString));
        }
        Assert.Matches("^trc-[0-9a-f]{8}$", prazos.TraceId);
        Assert.Contains(await Registros("auditoria", prazos.TraceId!), d => d["acao"].AsString == "SALVAR_PRAZOS_SAC" && d["etapa"].AsString == "CONCLUIDO");
    }

    // 17 RN-270: sem o MOD-02, o 403 de quem muda estado é auditado com o
    // X-Trace-Id; o da leitura sai sem cabeçalho e sem registro
    [Fact]
    public async Task RN_288_staff_sem_o_sac_recebe_403_auditado_so_ao_mudar_estado()
    {
        var (contaId, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var maria = await preparo.Usuario(master, "maria");
        using var api = backend.Cliente();

        var abrir = await api.Enviar("POST", "/sac/chamados", Corpo(), maria.Token);
        var listar = await api.Enviar("GET", "/sac/chamados", token: maria.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "ACESSO_NEGADO"), (abrir.Status, abrir.Codigo));
        Assert.Matches("^trc-[0-9a-f]{8}$", abrir.TraceId);
        var iniciado = Assert.Single(await Registros("auditoria", abrir.TraceId!));
        Assert.Equal(("INICIADO", "ABRIR_CHAMADO", maria.Id, contaId),
            (iniciado["etapa"].AsString, iniciado["acao"].AsString, iniciado["quem"].AsString, iniciado["contaId"].AsString));
        var erro = Assert.Single(await Registros("auditoriaErros", abrir.TraceId!));
        Assert.Equal(("ACESSO_NEGADO", "ALERTA"), (erro["codigo"].AsString, erro["nivel"].AsString));
        Assert.Equal((HttpStatusCode.Forbidden, "ACESSO_NEGADO", (string?)null), (listar.Status, listar.Codigo, listar.TraceId));
    }

    // o 401 sai antes de tudo: sem X-Trace-Id e sem registro
    [Theory]
    [InlineData("POST", "/sac/chamados", null)]
    [InlineData("POST", "/sac/chamados/SAC-000001/assumir", null)]
    [InlineData("PUT", "/sac/configuracao", "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJxYSJ9.invalida")]
    [InlineData("GET", "/sac/chamados/SAC-000001/ao-vivo", null)]
    public async Task Sem_token_valido_o_401_sai_antes_de_tudo(string metodo, string caminho, string? token)
    {
        _ = backend.Server;
        var auditoria = await Contar("auditoria");
        var erros = await Contar("auditoriaErros");
        using var api = backend.Cliente();

        var resposta = await api.Enviar(metodo, caminho, metodo == "GET" ? null : new { }, token);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.Status);
        Assert.Contains(resposta.Codigo ?? "", new[] { "TOKEN_INVALIDO", "SESSAO_TERMINADA" });
        Assert.Null(resposta.TraceId);
        Assert.Equal((auditoria, erros), (await Contar("auditoria"), await Contar("auditoriaErros")));
    }

    // RN-295 v06, contrato v03: o card e o card ao vivo respondem 401 só
    // SESSAO_TERMINADA, seja qual for o problema do token, sem X-Trace-Id e
    // sem abrir o fluxo
    [Theory]
    [InlineData("/sac/card", "sem token")]
    [InlineData("/sac/card", "token adulterado")]
    [InlineData("/sac/card", "token vencido")]
    [InlineData("/sac/card", "sessão encerrada")]
    [InlineData("/sac/card/ao-vivo?intervalo=30", "sem token")]
    [InlineData("/sac/card/ao-vivo?intervalo=30", "token adulterado")]
    [InlineData("/sac/card/ao-vivo?intervalo=30", "token vencido")]
    [InlineData("/sac/card/ao-vivo?intervalo=30", "sessão encerrada")]
    public async Task RN_295_card_sem_sessao_valida_responde_so_SESSAO_TERMINADA(string caminho, string problema)
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var relogio = backend.Services.GetRequiredService<Boaz.MsMono.Sistema.RelogioDaAplicacao>();
        string? token = problema switch
        {
            "sem token" => null,
            "token adulterado" => Adulterado(master.Token),
            _ => master.Token,
        };
        if (problema == "sessão encerrada")
        {
            var acesso = await backend.Services.GetRequiredService<Boaz.Core.Commons.Usuario.Autenticacao.Ports.In.IConferirAcesso>()
                .Executar(new Boaz.Core.Commons.Usuario.Autenticacao.Dtos.ConferenciaDeAcessoDto(master.Token,
                    Boaz.Core.Commons.Usuario.Autenticacao.Domain.Frente.Hub));
            await backend.Services.GetRequiredService<Boaz.Core.Commons.Usuario.Autenticacao.Ports.In.ISairDoAparelho>()
                .Executar(acesso, TraceId.Novo());
        }
        // o token vencido: o relógio da aplicação um dia à frente
        if (problema == "token vencido") relogio.Definir(DateTimeOffset.UtcNow.AddDays(1));
        try
        {
            using var http = backend.CreateClient();
            using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var pedido = new HttpRequestMessage(HttpMethod.Get, caminho);
            if (token is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(caminho.Contains("ao-vivo", StringComparison.Ordinal) ? "text/event-stream" : "application/json"));
            using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, tempo.Token);

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
            Assert.NotEqual("text/event-stream", resposta.Content.Headers.ContentType?.MediaType);
            var corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(tempo.Token));
            Assert.Equal("SESSAO_TERMINADA", corpo?["codigo"]?.GetValue<string>());
            Assert.False(resposta.Headers.Contains("X-Trace-Id"));
        }
        finally
        {
            relogio.Real();
        }
    }

    // a carga muda e a assinatura fica a do token original
    static string Adulterado(string token)
    {
        var partes = token.Split('.');
        var carga = Encoding.UTF8.GetString(Convert.FromBase64String(partes[1].Replace('-', '+').Replace('_', '/')
            .PadRight(partes[1].Length + (4 - partes[1].Length % 4) % 4, '=')));
        var outra = JsonNode.Parse(carga)!;
        outra["sub"] = "usr-adulterado";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(outra.ToJsonString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{partes[0]}.{b64}.{partes[2]}";
    }

    // 17 RN-271: fora do Development, o corpo que não é JSON responde 422
    // DADOS_INVALIDOS no campo "corpo", sem X-Trace-Id e sem registro
    [Theory]
    [InlineData("POST", "/sac/chamados", "{ \"categoria\": ")]
    [InlineData("PUT", "/sac/configuracao", "prazos=8")]
    public async Task Corpo_que_nao_e_json_nao_chega_ao_caso_de_uso(string metodo, string caminho, string corpo)
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var producao = backend.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var token = caminho == "/sac/configuracao"
            ? await TokenNo(producao, Backend.AdminEntrada, Backend.AdminSenha)
            : await TokenNo(producao, master.Entrada, master.Senha);
        using var http = producao.CreateClient();
        var auditoria = await Contar("auditoria");
        var erros = await Contar("auditoriaErros");

        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), caminho)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var json = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        Assert.Equal(("DADOS_INVALIDOS", "corpo"), (json["codigo"]!.GetValue<string>(), json["campo"]!.GetValue<string>()));
        Assert.Equal("O campo \"corpo\" está faltando ou é inválido.", json["mensagem"]!.GetValue<string>());
        Assert.False(resposta.Headers.Contains("X-Trace-Id"));
        Assert.Equal((auditoria, erros), (await Contar("auditoria"), await Contar("auditoriaErros")));
    }

    // Os prazos com o banco fora do ar na leitura de quem abre.
    sealed class PrazosQueCaem : IPrazosDoSac
    {
        public Task<int> HorasUteis(Categoria categoria, CancellationToken ct = default) => throw new InvalidOperationException(Detalhe);
    }

    // 17 RN-270: a falha inesperada ao abrir responde 500 ERRO_INESPERADO, sem
    // detalhe, com o X-Trace-Id, e grava nivel ERRO em "auditoriaErros"
    [Fact]
    public async Task Falha_inesperada_ao_abrir_responde_500_com_o_traceId()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var fora = backend.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IPrazosDoSac, PrazosQueCaem>()));
        var token = await TokenNo(fora, master.Entrada, master.Senha);
        using var api = new Cliente(fora.CreateClient());

        var resposta = await api.Enviar("POST", "/sac/chamados", Corpo(), token);

        Assert.Equal((HttpStatusCode.InternalServerError, "ERRO_INESPERADO"), (resposta.Status, resposta.Codigo));
        Assert.Equal("Algo deu errado. Tente de novo mais tarde.", resposta.Texto("mensagem"));
        Assert.DoesNotContain(Detalhe, resposta.Corpo!.ToJsonString(), StringComparison.Ordinal);
        Assert.Matches("^trc-[0-9a-f]{8}$", resposta.TraceId);
        var erro = Assert.Single(await Registros("auditoriaErros", resposta.TraceId!));
        Assert.Equal(("ABRIR_CHAMADO", "ERRO_INESPERADO", "ERRO"), (erro["acao"].AsString, erro["codigo"].AsString, erro["nivel"].AsString));
        Assert.Equal("INICIADO", Assert.Single(await Registros("auditoria", resposta.TraceId!))["etapa"].AsString);
    }

    // RN-293: PATCH, PUT e DELETE de uma mensagem respondem 405 sem corpo, sem conferir o token
    [Theory]
    [InlineData("PATCH")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task RN_293_mensagem_nao_muda_405_sem_login_e_sem_corpo(string metodo)
    {
        using var http = backend.CreateClient();
        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), "/sac/chamados/SAC-000042/mensagens/msg-000301")
        {
            Content = new StringContent("{ \"texto\": \"Turma 7B\" }", Encoding.UTF8, "application/json"),
        };

        using var resposta = await http.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, resposta.StatusCode);
        Assert.Empty(await resposta.Content.ReadAsByteArrayAsync());
        Assert.False(resposta.Headers.Contains("X-Trace-Id"));
    }

    // EventosDoSac: o fluxo da conversa e o do card saem como text/event-stream
    // sem cache; as recusas saem como JSON, antes de abrir o fluxo
    [Fact]
    public async Task RNF_230_RN_295_fluxos_ao_vivo_sem_cache_e_recusas_em_json()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var api = backend.Cliente();
        var id = (await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token)).Texto("id");
        using var http = backend.CreateClient();
        using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        foreach (var caminho in new[] { $"/sac/chamados/{id}/ao-vivo", "/sac/card/ao-vivo?intervalo=30" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, caminho);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", master.Token);
            pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, tempo.Token);

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.Equal("text/event-stream", resposta.Content.Headers.ContentType?.MediaType);
            Assert.True(resposta.Headers.CacheControl?.NoStore, $"{caminho} sem Cache-Control: no-store");
        }

        var inexistente = await api.Enviar("GET", "/sac/chamados/SAC-999999/ao-vivo", token: master.Token);
        var intervalo = await api.Enviar("GET", "/sac/card/ao-vivo?intervalo=10", token: master.Token);
        Assert.Equal((HttpStatusCode.NotFound, "CHAMADO_INEXISTENTE"), (inexistente.Status, inexistente.Codigo));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DADOS_INVALIDOS"), (intervalo.Status, intervalo.Codigo));
        // RN-295 v06: 10 e 0 recusados com a mensagem e o campo do contrato v03
        var zero = await api.Enviar("GET", "/sac/card/ao-vivo?intervalo=0", token: master.Token);
        foreach (var recusa in new[] { intervalo, zero })
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "O campo \"intervalo\" está faltando ou é inválido.", "intervalo"),
                (recusa.Status, recusa.Corpo?["mensagem"]?.GetValue<string>(), recusa.Corpo?["campo"]?.GetValue<string>()));
        }
    }

    // AnexosController: o arquivo sai com o tipo do anexo e como download, com o nome enviado
    [Fact]
    public async Task RN_292_anexo_baixado_com_o_tipo_e_o_nome()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var http = backend.CreateClient();
        using var envio = new HttpRequestMessage(HttpMethod.Post, "/sac/anexos");
        var arquivo = new ByteArrayContent(Png);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        envio.Content = new MultipartFormDataContent { { arquivo, "arquivo", "erro.png" } };
        envio.Headers.Authorization = new AuthenticationHeaderValue("Bearer", master.Token);
        using var enviado = await http.SendAsync(envio);
        var id = JsonNode.Parse(await enviado.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        using var leitura = new HttpRequestMessage(HttpMethod.Get, $"/sac/anexos/{id}");
        leitura.Headers.Authorization = new AuthenticationHeaderValue("Bearer", master.Token);
        using var baixado = await http.SendAsync(leitura);

        Assert.Equal(HttpStatusCode.OK, baixado.StatusCode);
        Assert.Equal("image/png", baixado.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", baixado.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("erro.png", baixado.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(Png, await baixado.Content.ReadAsByteArrayAsync());
    }

    // RN-292: sem o campo "arquivo" no formulário, 422 DADOS_INVALIDOS no campo "arquivo"
    [Fact]
    public async Task RN_292_envio_sem_o_campo_arquivo()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var http = backend.CreateClient();
        using var envio = new HttpRequestMessage(HttpMethod.Post, "/sac/anexos");
        envio.Content = new MultipartFormDataContent { { new StringContent("sem arquivo"), "outro" } };
        envio.Headers.Authorization = new AuthenticationHeaderValue("Bearer", master.Token);

        using var resposta = await http.SendAsync(envio);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var json = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        Assert.Equal(("DADOS_INVALIDOS", "arquivo"), (json["codigo"]!.GetValue<string>(), json["campo"]!.GetValue<string>()));
    }

    // um token do host derivado: a chave de assinatura dos testes é sorteada por host
    static async Task<string> TokenNo(WebApplicationFactory<Program> host, string entrada, string senha)
    {
        using var api = new Cliente(host.CreateClient());
        var sessao = await api.Enviar("POST", "/auth/hub/entrar", new { entrada, senha, aparelho = "APP" });
        Assert.Equal(HttpStatusCode.OK, sessao.Status);
        return sessao.Texto("tokenAcesso");
    }
}
