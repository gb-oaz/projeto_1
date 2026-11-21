using System.Net;
using System.Security.Cryptography;
using System.Text;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.In;
using Boaz.MsMono.Secundario.Out.Commons;
using Microsoft.Extensions.DependencyInjection;

namespace Boaz.MsMono.Tests.Integracao;

// Endereços da Auditoria (20_modulo-auditoria v05, auditoria-contrato-api v04) com banco e cache reais: a saúde das verificações,
// as leituras de auditoria e auditoriaErros, as medições, as ocorrências, o card, a consulta auditada com o X-Trace-Id e o ao vivo.
// O comportamento de cada cenário é coberto pelo qa (hub/auditoria); aqui, a ligação de ponta a ponta.
public class AuditoriaApiTests(Backend backend) : IClassFixture<Backend>
{
    readonly Preparo preparo = new(backend);

    [Fact]
    public async Task RF_194_saude_dos_8_servicos_com_o_x_trace_id_e_a_consulta_gravada()
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();
        // O Redis é o de verdade e o admin é sempre o mesmo: a consulta de uma rodada anterior, há menos de 5 min,
        // tornaria esta repetida (RN-325) e nada seria gravado. Apaga só a chave que os próprios testes criaram.
        var alvo = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("/auditoria/saude")))[..16];
        await backend.Services.GetRequiredService<Armazenamento>().Cache.KeyDeleteAsync($"auditoria:consulta:{admin.Id}:{alvo}");

        var saude = await cliente.Enviar("GET", "/auditoria/saude", token: admin.Token);

        Assert.Equal(HttpStatusCode.OK, saude.Status);
        Assert.Matches("^trc-[0-9a-f]{8}$", saude.TraceId);
        Assert.Equal(8, saude.Corpo!["servicos"]!.AsArray().Count);
        Assert.Equal("NO_AR", saude.Corpo["geral"]!.GetValue<string>());
        Assert.Equal(5, saude.Corpo["modulos"]!.AsArray().Count);
        Assert.NotEmpty(saude.Corpo["instancias"]!.AsArray());

        var registros = await cliente.Enviar("GET", "/auditoria/registros?acao=CONSULTAR_AUDITORIA", token: admin.Token);
        Assert.Equal(HttpStatusCode.OK, registros.Status);
        var itens = registros.Corpo!["itens"]!.AsArray();
        Assert.Contains(itens, i => i!["traceId"]!.GetValue<string>() == saude.TraceId && i["etapa"]!.GetValue<string>() == "INICIADO");
        var concluido = itens.First(i => i!["traceId"]!.GetValue<string>() == saude.TraceId && i["etapa"]!.GetValue<string>() == "CONCLUIDO")!;
        Assert.Equal(("Consultar a Auditoria", "MOD-17", "/auditoria/saude", admin.Id),
            (concluido["acaoNome"]!.GetValue<string>(), concluido["modulo"]!.GetValue<string>(), concluido["alvo"]!.GetValue<string>(),
                concluido["quem"]!["id"]!.GetValue<string>()));
    }

    [Fact]
    public async Task RN_319_acoes_card_e_ocorrencias_abertas_nao_gravam_nem_trazem_o_x_trace_id()
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();

        var acoes = await cliente.Enviar("GET", "/auditoria/acoes", token: admin.Token);
        var card = await cliente.Enviar("GET", "/auditoria/card", token: admin.Token);
        var abertas = await cliente.Enviar("GET", "/auditoria/ocorrencias/abertas", token: admin.Token);

        Assert.All(new[] { acoes, card, abertas }, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.All(new[] { acoes, card, abertas }, r => Assert.Null(r.TraceId));
        Assert.Equal(41, acoes.Corpo!.AsArray().Count);
        Assert.Equal(("MOD-17", "DEBOAZ"), (card.Corpo!["modulo"]!.GetValue<string>(), card.Corpo["perfil"]!.GetValue<string>()));
        Assert.Equal(12, card.Corpo["series"]!["dia"]!.AsArray().Count);
        Assert.NotNull(abertas.Corpo!["abertas"]);
        Assert.NotNull(abertas.Corpo["voltaram"]);
    }

    [Fact]
    public async Task RN_314_so_o_time_com_a_auditoria_usa_e_o_resto_e_recusado()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var staff = await preparo.Usuario(master, "rui");
        using var cliente = backend.Cliente();

        foreach (var pessoa in new[] { master, staff })
        {
            var recusada = await cliente.Enviar("GET", "/auditoria/saude", token: pessoa.Token);
            Assert.Equal((HttpStatusCode.Forbidden, "ACESSO_NEGADO", null), (recusada.Status, recusada.Codigo, recusada.TraceId));
        }
        var sem = await cliente.Enviar("GET", "/auditoria/saude");
        Assert.Equal((HttpStatusCode.Unauthorized, "SESSAO_TERMINADA"), (sem.Status, sem.Codigo));
    }

    [Theory]
    [InlineData("/auditoria/metricas?janela=2h", "DADOS_INVALIDOS", "janela")]
    [InlineData("/auditoria/metricas/enderecos?ordem=nome", "DADOS_INVALIDOS", "ordem")]
    [InlineData("/auditoria/registros?de=2026-01-01T00:00:00Z&ate=2026-12-31T00:00:00Z", "PERIODO_LONGO", "ate")]
    [InlineData("/auditoria/registros?de=2026-12-31T00:00:00Z&ate=2026-12-30T00:00:00Z", "PERIODO_INVERTIDO", "ate")]
    [InlineData("/auditoria/registros?nivel=GRAVE", "DADOS_INVALIDOS", "nivel")]
    [InlineData("/auditoria/execucoes?resultado=PERDIDA", "DADOS_INVALIDOS", "resultado")]
    [InlineData("/auditoria/execucoes/abc", "DADOS_INVALIDOS", "traceId")]
    [InlineData("/auditoria/ocorrencias?tipo=FILA", "DADOS_INVALIDOS", "tipo")]
    [InlineData("/auditoria/card/ao-vivo?intervalo=10", "DADOS_INVALIDOS", "intervalo")]
    [InlineData("/auditoria/saude/banco/historico?periodo=2h", "DADOS_INVALIDOS", "periodo")]
    public async Task RF_197_a_RF_205_pedido_invalido_responde_422_com_o_campo_e_sem_registro(string caminho, string codigo, string campo)
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();

        var r = await cliente.Enviar("GET", caminho, token: admin.Token);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, codigo, campo), (r.Status, r.Codigo, r.Corpo!["campo"]!.GetValue<string>()));
        Assert.Null(r.TraceId);
    }

    [Theory]
    [InlineData("/auditoria/saude/fila/historico", "SERVICO_INEXISTENTE")]
    [InlineData("/auditoria/execucoes/trc-00000000", "EXECUCAO_INEXISTENTE")]
    public async Task RF_195_RF_202_o_que_nao_existe_responde_404(string caminho, string codigo)
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();

        var r = await cliente.Enviar("GET", caminho, token: admin.Token);

        Assert.Equal((HttpStatusCode.NotFound, codigo), (r.Status, r.Codigo));
    }

    [Fact]
    public async Task RN_320_nenhum_endereco_muda_a_auditoria()
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();

        foreach (var (metodo, caminho) in new[] { ("POST", "/auditoria/saude"), ("DELETE", "/auditoria/registros"), ("PUT", "/auditoria/registros"), ("POST", "/auditoria/execucoes/trc-1d6e3f08") })
        {
            var r = await cliente.Enviar(metodo, caminho, token: admin.Token);
            Assert.Equal((HttpStatusCode.MethodNotAllowed, null), (r.Status, r.Corpo));
        }
    }

    [Fact]
    public async Task RF_198_RNF_247_pedido_medido_pelo_modelo_da_rota_aparece_nos_enderecos()
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();
        await cliente.Enviar("GET", "/auditoria/acoes", token: admin.Token);

        var enderecos = await cliente.Enviar("GET", "/auditoria/metricas/enderecos?janela=1h&modulo=MOD-17&limite=100", token: admin.Token);

        Assert.Equal(HttpStatusCode.OK, enderecos.Status);
        var itens = enderecos.Corpo!["itens"]!.AsArray();
        var acoes = itens.First(i => i!["endereco"]!.GetValue<string>() == "/auditoria/acoes")!;
        Assert.Equal(("GET", "MOD-17"), (acoes["metodo"]!.GetValue<string>(), acoes["modulo"]!.GetValue<string>()));
        Assert.True(acoes["pedidos"]!.GetValue<int>() >= 1);

        var metricas = await cliente.Enviar("GET", "/auditoria/metricas?janela=24h&modulo=MOD-17", token: admin.Token);
        Assert.Equal(HttpStatusCode.OK, metricas.Status);
        Assert.Equal((24, "MOD-17"), (metricas.Corpo!["serie"]!.AsArray().Count, metricas.Corpo["modulo"]!.GetValue<string>()));
        foreach (var janela in new[] { "1h", "7d", "30d" })
        {
            var r = await cliente.Enviar("GET", $"/auditoria/metricas?janela={janela}", token: admin.Token);
            Assert.Equal(janela switch { "1h" => 60, "7d" => 7, _ => 30 }, r.Corpo!["serie"]!.AsArray().Count);
        }
        await backend.Services.GetRequiredService<IGravarMedicoes>().Executar(true);
    }

    [Fact]
    public async Task RF_199_RF_203_listas_de_registros_execucoes_e_ocorrencias_respondem_com_a_forma_do_contrato()
    {
        var admin = await preparo.Admin();
        using var cliente = backend.Cliente();

        var registros = await cliente.Enviar("GET", "/auditoria/registros?limite=5", token: admin.Token);
        var execucoes = await cliente.Enviar("GET", "/auditoria/execucoes", token: admin.Token);
        var ocorrencias = await cliente.Enviar("GET", "/auditoria/ocorrencias", token: admin.Token);
        var historico = await cliente.Enviar("GET", "/auditoria/saude/banco/historico?periodo=7d", token: admin.Token);

        Assert.All(new[] { registros, execucoes, ocorrencias, historico }, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.NotNull(registros.Corpo!["de"]);
        Assert.NotNull(execucoes.Corpo!["contagem"]!["concluidas"]);
        Assert.NotNull(ocorrencias.Corpo!["itens"]);
        Assert.Equal((7, "banco"), (historico.Corpo!["serie"]!.AsArray().Count, historico.Corpo["servico"]!.GetValue<string>()));
        Assert.True(historico.Corpo["disponibilidade"]!.GetValue<decimal>() >= 0);
    }

    [Fact]
    public async Task RN_315_ganchos_de_teste_da_auditoria_nao_existem_sem_o_relogio_de_teste()
    {
        using var cliente = backend.Cliente();

        foreach (var (metodo, caminho) in new[] { ("PUT", "/_teste/auditoria/sondas/cache"), ("POST", "/_teste/auditoria/verificacao"), ("POST", "/_teste/auditoria/agendamento"),
            ("POST", "/_teste/auditoria/registros"), ("POST", "/_teste/auditoria/medicoes"), ("POST", "/_teste/auditoria/reiniciar") })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await cliente.Enviar(metodo, caminho, "{}")).Status);
        }
    }

    [Fact]
    public async Task RF_204_RNF_249_ocorrencias_ao_vivo_abre_o_fluxo_com_o_evento_abertas()
    {
        var admin = await preparo.Admin();
        using var http = backend.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/auditoria/ocorrencias/ao-vivo");
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);
        using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, tempo.Token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/event-stream", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", resposta.Headers.CacheControl?.ToString());
        Assert.False(resposta.Headers.Contains("X-Trace-Id"));
        using var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync(tempo.Token));
        var linhas = new List<string>();
        while (linhas.Count < 3 && await leitor.ReadLineAsync(tempo.Token) is { } linha) linhas.Add(linha);
        Assert.Equal("event: abertas", linhas[0]);
        Assert.StartsWith("data: ", linhas[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF_194_saude_ao_vivo_abre_com_o_evento_saude_e_grava_a_consulta()
    {
        var admin = await preparo.Admin();
        using var http = backend.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/auditoria/saude/ao-vivo");
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);
        using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, tempo.Token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(resposta.Headers.Contains("X-Trace-Id"));
        using var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync(tempo.Token));
        var linhas = new List<string>();
        while (linhas.Count < 3 && await leitor.ReadLineAsync(tempo.Token) is { } linha) linhas.Add(linha);
        Assert.Equal("event: saude", linhas.First(l => l.StartsWith("event:", StringComparison.Ordinal)));
    }
}
