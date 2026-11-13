using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Boaz.MsMono.Primario.In.Hub.Sac;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using Boaz.MsMono.Sistema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Endereços do SAC (18_modulo-sac v04, sac-contrato-api v01) com banco e cache
// reais: os adaptadores de chamados, anexos (GridFS e verificação local),
// prazos, card, canal ao vivo e o agendamento. O comportamento de cada cenário
// é coberto pelo qa (hub/sac); aqui, a ligação de ponta a ponta.
public class SacApiTests(Backend backend) : IClassFixture<Backend>
{
    readonly Preparo preparo = new(backend);

    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    static object Corpo(params string[] anexos) => new
    {
        categoria = "MODULO_COM_PROBLEMA",
        assunto = "Presença não salva a chamada",
        descricao = "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.",
        relacionadoA = (string?)null,
        anexos,
    };

    async Task<Resposta> EnviarArquivo(string token, string nome, byte[] conteudo)
    {
        using var http = backend.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/sac/anexos");
        var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(arquivo, "arquivo", nome);
        pedido.Content = form;
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        return new Resposta(resposta.StatusCode, texto.Length > 0 ? JsonNode.Parse(texto) : null, []);
    }

    [Fact]
    public async Task RF_171_a_RF_178_o_chamado_do_comeco_ao_fim()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var time = await preparo.Admin();
        using var api = backend.Cliente();

        var anexo = await EnviarArquivo(master.Token, "erro.png", Png);
        Assert.Equal(HttpStatusCode.Created, anexo.Status);
        var aberto = await api.Enviar("POST", "/sac/chamados", Corpo(anexo.Texto("id")), master.Token);
        Assert.Equal((HttpStatusCode.Created, "ABERTO"), (aberto.Status, aberto.Texto("status")));
        var id = aberto.Texto("id");
        Assert.Matches("^SAC-[0-9]{6}$", id);
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar("GET", $"/sac/anexos/{anexo.Texto("id")}", token: master.Token)).Status);

        var assumido = await api.Enviar("POST", $"/sac/chamados/{id}/assumir", new { substituir = true }, time.Token);
        Assert.Equal("EM_ATENDIMENTO", assumido.Texto("status"));
        var nota = await api.Enviar("POST", $"/sac/chamados/{id}/mensagens", new { texto = "Conferir o log", interna = true }, time.Token);
        Assert.Equal(HttpStatusCode.Created, nota.Status);
        var pergunta = await api.Enviar("POST", $"/sac/chamados/{id}/status", new { status = "AGUARDANDO_CLIENTE", mensagem = "Qual a turma?" }, time.Token);
        Assert.Equal("AGUARDANDO_CLIENTE", pergunta.Texto("status"));

        var lista = await api.Enviar("GET", "/sac/chamados?situacao=EM_ANDAMENTO&pagina=1", token: master.Token);
        var item = lista.Corpo!["itens"]!.AsArray().Single()!;
        Assert.True(item["novaResposta"]!.GetValue<bool>());
        var lido = await api.Enviar("GET", $"/sac/chamados/{id}", token: master.Token);
        Assert.DoesNotContain("Conferir o log", lido.Corpo!.ToJsonString());
        Assert.NotEmpty(lido.Corpo!["linhaDoTempo"]!.AsArray());
        var fila = await api.Enviar("GET", $"/sac/chamados?busca={id}&categoria=MODULO_COM_PROBLEMA&responsavel=eu&atrasado=false", token: time.Token);
        Assert.Equal(1, fila.Corpo!["total"]!.GetValue<int>());
        Assert.Equal(0, (await api.Enviar("GET", "/sac/chamados?responsavel=nenhum&atrasado=true&notaMaxima=2", token: time.Token)).Corpo!["itens"]!
            .AsArray().Count(i => i!["id"]!.GetValue<string>() == id));

        var resposta = await api.Enviar("POST", $"/sac/chamados/{id}/mensagens", new { texto = "Turma 7A" }, master.Token);
        Assert.Equal("EM_ATENDIMENTO", resposta.Texto("statusChamado"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await api.Enviar("DELETE", $"/sac/chamados/{id}/mensagens/{resposta.Texto("id")}", token: time.Token)).Status);
        await api.Enviar("POST", $"/sac/chamados/{id}/status", new { status = "RESOLVIDO", mensagem = "Corrigido." }, time.Token);
        Assert.Equal("EM_ATENDIMENTO", (await api.Enviar("POST", $"/sac/chamados/{id}/reabrir", new { mensagem = "Ainda aparece" }, master.Token)).Texto("status"));
        await api.Enviar("POST", $"/sac/chamados/{id}/status", new { status = "RESOLVIDO", mensagem = "Agora sim." }, time.Token);
        var avaliado = await api.Enviar("POST", $"/sac/chamados/{id}/avaliacao", new { nota = 5, comentario = "Obrigado" }, master.Token);
        Assert.Equal((HttpStatusCode.Created, "FECHADO"), (avaliado.Status, avaliado.Texto("status")));
        var ja = await api.Enviar("POST", $"/sac/chamados/{id}/avaliacao", new { nota = 4 }, master.Token);
        Assert.Equal((HttpStatusCode.Conflict, "JA_AVALIADO"), (ja.Status, ja.Codigo));

        var outro = (await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token)).Texto("id");
        Assert.Equal("FECHADO", (await api.Enviar("POST", $"/sac/chamados/{outro}/encerrar", token: master.Token)).Texto("status"));
        var auditados = await backend.Banco.GetCollection<BsonDocument>("auditoria")
            .Find(Builders<BsonDocument>.Filter.Eq("alvo", id) & Builders<BsonDocument>.Filter.Eq("acao", "AVALIAR_CHAMADO")).ToListAsync();
        Assert.Equal(3, auditados.Count);
    }

    [Fact]
    public async Task RN_292_anexos_recusados_e_descartados()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        using var api = backend.Cliente();
        var eicar = Encoding.ASCII.GetBytes("%PDF-1.4\n" + string.Concat(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$", "EICAR-STANDARD-", "ANTIVIRUS-TEST-FILE!$H+H*"));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ANEXO_RECUSADO"), Par(await EnviarArquivo(master.Token, "nota.pdf", eicar)));
        Assert.Equal((HttpStatusCode.UnsupportedMediaType, "TIPO_NAO_ACEITO"), Par(await EnviarArquivo(master.Token, "tela.mp4", [0, 0, 0, 0x18])));
        var interruptor = backend.Services.GetRequiredService<InterruptorDaVerificacao>();
        interruptor.Disponivel = false;
        try
        {
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "VERIFICACAO_INDISPONIVEL"), Par(await EnviarArquivo(master.Token, "erro.png", Png)));
        }
        finally
        {
            interruptor.Disponivel = true;
        }
        var anexo = (await EnviarArquivo(master.Token, "erro.png", Png)).Texto("id");
        var relogio = backend.Services.GetRequiredService<RelogioDaAplicacao>();
        relogio.Definir(DateTimeOffset.UtcNow.AddHours(25));
        try
        {
            await AgendamentoDoSac.Rotinas(backend.Services, CancellationToken.None);
        }
        finally
        {
            relogio.Real();
        }
        Assert.Equal((HttpStatusCode.NotFound, "ANEXO_INEXISTENTE"), Par(await api.Enviar("GET", $"/sac/anexos/{anexo}", token: master.Token)));
    }

    static (HttpStatusCode, string?) Par(Resposta r) => (r.Status, r.Codigo);

    [Fact]
    public async Task RN_290_sete_dias_sem_resposta_pelo_agendamento()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var time = await preparo.Admin();
        using var api = backend.Cliente();
        var id = (await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token)).Texto("id");
        await api.Enviar("POST", $"/sac/chamados/{id}/assumir", token: time.Token);
        await api.Enviar("POST", $"/sac/chamados/{id}/status", new { status = "AGUARDANDO_CLIENTE", mensagem = "Qual a turma?" }, time.Token);
        var relogio = backend.Services.GetRequiredService<RelogioDaAplicacao>();
        relogio.Definir(DateTimeOffset.UtcNow.AddDays(7).AddMinutes(1));
        try
        {
            await AgendamentoDoSac.Rotinas(backend.Services, CancellationToken.None);
        }
        finally
        {
            relogio.Real();
        }
        var chamado = await backend.Banco.GetCollection<BsonDocument>("chamados").Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();
        Assert.Equal("RESOLVIDO", chamado["status"].AsString);
    }

    [Fact]
    public async Task RF_181_RN_295_prazos_e_card()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var admin = await preparo.Admin();
        using var api = backend.Cliente();
        var salvos = await api.Enviar("PUT", "/sac/configuracao", new { prazos = new[] { new { categoria = "DUVIDA_DE_USO", horasUteis = 12 } } }, admin.Token);
        Assert.Equal(HttpStatusCode.OK, salvos.Status);
        var lidos = await api.Enviar("GET", "/sac/configuracao", token: admin.Token);
        Assert.Contains(lidos.Corpo!["prazos"]!.AsArray(), p => p!["categoria"]!.GetValue<string>() == "DUVIDA_DE_USO" && p["horasUteis"]!.GetValue<int>() == 12);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "FORA_DOS_LIMITES"),
            Par(await api.Enviar("PUT", "/sac/configuracao", new { prazos = new[] { new { categoria = "COBRANCA", horasUteis = 81 } } }, admin.Token)));

        await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token);
        var card = await api.Enviar("GET", "/sac/card", token: master.Token);
        Assert.Equal(("INSTITUICAO", 1), (card.Texto("perfil"), card.Corpo!["reduzida"]!["valor"]!.GetValue<int>()));
        Assert.Equal("DEBOAZ", (await api.Enviar("GET", "/sac/card", token: admin.Token)).Texto("perfil"));

        await backend.Services.GetRequiredService<Boaz.Core.Hub.Sac.Card.Ports.In.IFecharCardDoSacDoDia>()
            .Executar(Boaz.Core.Commons.Usuario.Usuarios.Domain.CalendarioDoCard.Dia(DateTimeOffset.UtcNow).AddDays(-1));
        var depois = await api.Enviar("GET", "/sac/card", token: master.Token);
        Assert.Equal(1, depois.Corpo!["series"]!["dia"]!.AsArray()[10]!.GetValue<int>());
    }

    [Fact]
    public async Task RNF_230_conversa_e_card_ao_vivo()
    {
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var time = await preparo.Admin();
        using var api = backend.Cliente();
        var id = (await api.Enviar("POST", "/sac/chamados", Corpo(), master.Token)).Texto("id");

        using var http = backend.CreateClient();
        using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"/sac/chamados/{id}/ao-vivo");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", master.Token);
        using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, tempo.Token);
        Assert.Equal("text/event-stream", resposta.Content.Headers.ContentType?.MediaType);
        using var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync(tempo.Token));
        var enviada = (await api.Enviar("POST", $"/sac/chamados/{id}/mensagens", new { texto = "Qual a turma?" }, time.Token)).Texto("id");
        Assert.Equal($"id: {enviada}", await leitor.ReadLineAsync(tempo.Token));
        Assert.Equal("event: mensagem", await leitor.ReadLineAsync(tempo.Token));
        Assert.Contains("Qual a turma?", await leitor.ReadLineAsync(tempo.Token));

        using var card = new HttpRequestMessage(HttpMethod.Get, "/sac/card/ao-vivo?intervalo=5");
        card.Headers.Authorization = new AuthenticationHeaderValue("Bearer", master.Token);
        using var fluxo = await http.SendAsync(card, HttpCompletionOption.ResponseHeadersRead, tempo.Token);
        using var cartao = new StreamReader(await fluxo.Content.ReadAsStreamAsync(tempo.Token));
        Assert.StartsWith("id: ", await cartao.ReadLineAsync(tempo.Token));
        Assert.Equal("event: card", await cartao.ReadLineAsync(tempo.Token));
        await cartao.ReadLineAsync(tempo.Token);
        await cartao.ReadLineAsync(tempo.Token);
        await api.Enviar("POST", $"/sac/chamados/{id}/mensagens", new { texto = "Turma 7A" }, master.Token);
        Assert.StartsWith("id: ", await cartao.ReadLineAsync(tempo.Token));
    }
}
