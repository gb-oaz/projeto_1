using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Boaz.MsMono.Primario.In.Hub.Sac;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// O backend com os arquivos dos anexos do SAC no destino da configuração
// (18 v06 RNF-233 e RNF-234, 05 v13 RNF-235; sac-classes v04, sac-documentos
// v03, sac-inicializacao-sequencia v03): Sac:Arquivos:Destino BLOB grava no
// contêiner do armazenamento de objetos (ArquivosNoBlob, o Azurite de
// ConnectionStrings:Blob) e GRIDFS no balde "sacAnexos" (ArquivosNoGridFs);
// cada anexo guarda o "armazenamento" e é lido sempre de lá, sem migração
// (ArquivosDosAnexos). No Blob, o arquivo "sac/anexos/<id>" leva a tag de
// índice "uso": pendente no envio, chamado ao vincular e fechado no FECHADO,
// com o metadado "fechadoem". O expiraEm vale por ambiente (24 h sem uso; usado
// em hml, 30 dias do envio; usado em prd, nulo até o FECHADO e fechadoEm + 5
// anos depois); o DescartarAnexosVencidos (AgendamentoDoSac) apaga o arquivo
// de onde foi guardado e, no anexo usado, só marca removidoEm. Vencido, o GET
// responde 404 ANEXO_INEXISTENTE, mesmo antes de o arquivo sair.
//
// Os hosts derivam do Backend (mesmo banco) com o destino e o AMBIENTE do
// teste; cada um tem a própria chave de assinatura, por isso cada pessoa entra
// de novo pelo POST /auth/hub/entrar do host. O contêiner é próprio da
// fixture e apagado no fim.
public sealed class HostsDosAnexos : IAsyncLifetime
{
    // a conexão do Azurite dos testes (no CI, a do job backend)
    public static string ConexaoDoBlob =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Blob") is { Length: > 0 } conexao ? conexao : "UseDevelopmentStorage=true";

    readonly Dictionary<(string Destino, string Ambiente), WebApplicationFactory<Program>> hosts = [];

    public Backend Backend { get; } = new();

    public string Conteiner { get; } = $"anexos-sac-testes-{Guid.NewGuid():N}";

    public BlobContainerClient ConteinerDoBlob => new(ConexaoDoBlob, Conteiner);

    // as contas qa que a partida de homologação exige (17 RN-265, RN-268)
    static readonly Dictionary<string, string> ContasQa = new()
    {
        ["USUARIO_QA_ADMIN_ENTRADA"] = "qaadmin.dboaz@deboaz.com",
        ["USUARIO_QA_ADMIN_SENHA"] = "Qa#Admin123456",
        ["USUARIO_QA_MEMBRO_ENTRADA"] = "qamembro.dboaz@deboaz.com",
        ["USUARIO_QA_MEMBRO_SENHA"] = "Qa#Membro123456",
        ["USUARIO_QA_MASTER_ENTRADA"] = "qa_instituicao@deboaz.com",
        ["USUARIO_QA_MASTER_SENHA"] = "Qa#Master123456",
        ["USUARIO_QA_AUTONOMO_ENTRADA"] = "qa_autonomo@deboaz.com",
        ["USUARIO_QA_AUTONOMO_SENHA"] = "Qa#Autonomo123456",
        ["USUARIO_QA_STAFF_ENTRADA"] = "qastaff.qains@deboaz.com",
        ["USUARIO_QA_STAFF_SENHA"] = "Qa#Staff123456",
        ["USUARIO_QA_PARTICIPANTE_CODIGO"] = "QAINS-QAPART-0001",
        ["USUARIO_QA_MODULOS"] = """{"staff": {"mods": {"MOD-03": "CU"}}, "time": {"mods": {"MOD-14": "U"}}}""",
    };

    // o host com o destino (BLOB ou GRIDFS) e o AMBIENTE (hml ou prd), um por par
    public WebApplicationFactory<Program> Host(string destino, string ambiente)
    {
        lock (hosts)
        {
            if (hosts.TryGetValue((destino, ambiente), out var host)) return host;
            host = Backend.WithWebHostBuilder(b =>
            {
                b.UseSetting("AMBIENTE", ambiente)
                    .UseSetting("ConnectionStrings:Blob", ConexaoDoBlob)
                    .UseSetting("Sac:Arquivos:Destino", destino)
                    .UseSetting("Sac:Arquivos:Conteiner", Conteiner);
                if (ambiente == "hml")
                {
                    foreach (var (nome, valor) in ContasQa) b.UseSetting(nome, valor);
                }
            });
            hosts[(destino, ambiente)] = host;
            return host;
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var host in hosts.Values) await host.DisposeAsync();
        try
        {
            await ConteinerDoBlob.DeleteIfExistsAsync();
        }
        catch (Azure.RequestFailedException)
        {
            // sem Azurite, nada a apagar
        }
        await ((IAsyncLifetime)Backend).DisposeAsync();
    }
}

public class ArquivosDosAnexosTests(HostsDosAnexos hosts) : IClassFixture<HostsDosAnexos>
{
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52, 7, 7, 7];

    readonly Preparo preparo = new(hosts.Backend);


    // ── uma conta master e o time, entrando no host do teste ───────────────
    sealed record Pessoas(Pessoa Conta, string Master, Cliente Api, WebApplicationFactory<Program> Host);

    async Task<Pessoas> Entrar(string destino, string ambiente)
    {
        var host = hosts.Host(destino, ambiente);
        var (_, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var api = new Cliente(host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false }));
        return new Pessoas(master, await Token(api, master.Entrada, master.Senha), api, host);
    }

    // a mesma conta master entrando em outro host (ou de novo, com o relógio movido)
    static async Task<string> MasterEm(WebApplicationFactory<Program> host, Pessoas p)
    {
        using var api = new Cliente(host.CreateClient());
        return await Token(api, p.Conta.Entrada, p.Conta.Senha);
    }

    static async Task<string> Token(Cliente api, string entrada, string senha)
    {
        var sessao = await api.Enviar("POST", "/auth/hub/entrar", new { entrada, senha, aparelho = "APP" });
        Assert.Equal(HttpStatusCode.OK, sessao.Status);
        return sessao.Texto("tokenAcesso");
    }

    static async Task<string> Enviar(WebApplicationFactory<Program> host, string token, byte[] conteudo, string nome = "erro.png")
    {
        using var http = host.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/sac/anexos");
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        pedido.Content = new MultipartFormDataContent { { arquivo, "arquivo", nome } };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.Created, $"POST /sac/anexos: {(int)resposta.StatusCode} {texto}");
        return JsonNode.Parse(texto)!["id"]!.GetValue<string>();
    }

    static async Task<(HttpStatusCode Status, byte[] Bytes, string? Codigo)> Baixar(WebApplicationFactory<Program> host, string token, string id)
    {
        using var http = host.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"/sac/anexos/{id}");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        string? codigo = null;
        if (resposta.StatusCode != HttpStatusCode.OK)
        {
            try { codigo = JsonNode.Parse(bytes)?["codigo"]?.GetValue<string>(); }
            catch (System.Text.Json.JsonException) { }
        }
        return (resposta.StatusCode, bytes, codigo);
    }

    static async Task<string> Abrir(Pessoas p, string anexo) =>
        (await p.Api.Enviar("POST", "/sac/chamados", new
        {
            categoria = "MODULO_COM_PROBLEMA",
            assunto = "Presença não salva a chamada",
            descricao = "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.",
            relacionadoA = (string?)null,
            anexos = new[] { anexo },
        }, p.Master)).Texto("id");

    // a conta encerra: o chamado fica FECHADO (PELA_CONTA) e devolve o fechadoEm
    static async Task<DateTimeOffset> Fechar(Pessoas p, string chamado)
    {
        var encerrado = await p.Api.Enviar("POST", $"/sac/chamados/{chamado}/encerrar", token: p.Master);
        Assert.Equal("FECHADO", encerrado.Texto("status"));
        var lido = await p.Api.Enviar("GET", $"/sac/chamados/{chamado}", token: p.Master);
        return DateTimeOffset.Parse(lido.Texto("fechadoEm"), CultureInfo.InvariantCulture);
    }

    Task<BsonDocument?> Registro(string id) =>
        hosts.Backend.Banco.GetCollection<BsonDocument>("anexosSac").Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync()!;

    async Task<BsonDocument> RegistroQueExiste(string id) =>
        await Registro(id) ?? throw new Xunit.Sdk.XunitException($"o registro do {id} devia estar em anexosSac");

    async Task<bool> NoGridFs(string id) =>
        await hosts.Backend.Banco.GetCollection<BsonDocument>("sacAnexos.files")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("filename", $"sac/anexos/{id}")) > 0;

    BlobClient Blob(string id) => hosts.ConteinerDoBlob.GetBlobClient($"sac/anexos/{id}");

    async Task<bool> NoBlob(string id) => await hosts.ConteinerDoBlob.ExistsAsync() && await Blob(id).ExistsAsync();

    async Task<string?> Uso(string id) =>
        (await Blob(id).GetTagsAsync()).Value.Tags.TryGetValue("uso", out var uso) ? uso : null;

    static DateTimeOffset? Data(BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && !v.IsBsonNull ? new DateTimeOffset(v.ToUniversalTime(), TimeSpan.Zero) : null;

    // o agendamento do SAC na hora pedida, com o relógio do host; o relógio
    // fica ali até o fim do teste (Real no finally de quem chama)
    static async Task Agendamento(WebApplicationFactory<Program> host, DateTimeOffset quando)
    {
        host.Services.GetRequiredService<RelogioDaAplicacao>().Definir(quando);
        await AgendamentoDoSac.Rotinas(host.Services, CancellationToken.None);
    }

    static void RelogioReal(WebApplicationFactory<Program> host) => host.Services.GetRequiredService<RelogioDaAplicacao>().Real();

    // ── RNF-233 · onde o arquivo é guardado ────────────────────────────────
    [Fact]
    public async Task RNF_233_destino_BLOB_guarda_no_conteiner_com_o_tipo_e_a_tag_pendente()
    {
        var p = await Entrar("BLOB", "prd");

        var id = await Enviar(p.Host, p.Master, Png);

        var registro = await RegistroQueExiste(id);
        Assert.Equal(("BLOB", $"sac/anexos/{id}"), (registro.GetValue("armazenamento", "").ToString(), registro["objeto"].AsString));
        Assert.Equal((true, false), (await NoBlob(id), await NoGridFs(id)));
        var guardado = (await Blob(id).DownloadContentAsync()).Value;
        Assert.Equal(Png, guardado.Content.ToArray());
        Assert.Equal("image/png", guardado.Details.ContentType);
        Assert.Equal("pendente", await Uso(id));
        var baixado = await Baixar(p.Host, p.Master, id);
        Assert.Equal(HttpStatusCode.OK, baixado.Status);
        Assert.Equal(Png, baixado.Bytes);
    }

    [Fact]
    public async Task RNF_233_destino_GRIDFS_guarda_no_gridfs_e_nada_no_blob()
    {
        var p = await Entrar("GRIDFS", "prd");

        var id = await Enviar(p.Host, p.Master, Png);

        var registro = await RegistroQueExiste(id);
        Assert.Equal("GRIDFS", registro.GetValue("armazenamento", "").ToString());
        Assert.Equal((true, false), (await NoGridFs(id), await NoBlob(id)));
        var baixado = await Baixar(p.Host, p.Master, id);
        Assert.Equal(HttpStatusCode.OK, baixado.Status);
        Assert.Equal(Png, baixado.Bytes);
    }

    // sem migração: o anexo do GridFS continua lá e é lido de lá com o destino
    // BLOB, e o do Blob é lido do Blob com o destino GRIDFS
    [Fact]
    public async Task RNF_233_cada_anexo_e_lido_de_onde_foi_guardado_sem_migracao()
    {
        var gridFs = await Entrar("GRIDFS", "prd");
        var hostBlob = hosts.Host("BLOB", "prd");
        var tokenNoBlob = await MasterEm(hostBlob, gridFs);
        var antigo = await Enviar(gridFs.Host, gridFs.Master, Png);
        var novo = await Enviar(hostBlob, tokenNoBlob, Png);

        var antigoNoHostBlob = await Baixar(hostBlob, tokenNoBlob, antigo);
        var novoNoHostGridFs = await Baixar(gridFs.Host, gridFs.Master, novo);

        Assert.Equal(HttpStatusCode.OK, antigoNoHostBlob.Status);
        Assert.Equal(Png, antigoNoHostBlob.Bytes);
        Assert.Equal(HttpStatusCode.OK, novoNoHostGridFs.Status);
        Assert.Equal(Png, novoNoHostGridFs.Bytes);
        Assert.Equal((true, false), (await NoGridFs(antigo), await NoBlob(antigo)));
        Assert.Equal((true, false), (await NoBlob(novo), await NoGridFs(novo)));
    }

    // ── RNF-234 · tag de uso e expiraEm por ambiente ───────────────────────
    [Fact]
    public async Task RNF_234_no_blob_a_tag_uso_vai_a_chamado_ao_vincular_e_a_fechado_com_o_metadado_no_FECHADO()
    {
        var p = await Entrar("BLOB", "prd");
        var id = await Enviar(p.Host, p.Master, Png);

        var chamado = await Abrir(p, id);
        var aoVincular = await Uso(id);
        var fechadoEm = await Fechar(p, chamado);

        Assert.Equal("chamado", aoVincular);
        Assert.Equal("fechado", await Uso(id));
        var metadados = (await Blob(id).GetPropertiesAsync()).Value.Metadata;
        Assert.True(metadados.TryGetValue("fechadoem", out var valor) && valor.Length > 0, "o blob do anexo fechado devia trazer o metadado fechadoem");
        var registro = await RegistroQueExiste(id);
        Assert.Equal(fechadoEm.AddYears(5).ToUnixTimeSeconds(), Data(registro, "expiraEm")?.ToUnixTimeSeconds());
    }

    // enviado: 24 h; usado em hml: 30 dias do envio; usado em prd: nulo até o FECHADO
    [Theory]
    [InlineData("hml", 30)]
    [InlineData("prd", null)]
    public async Task RNF_234_expiraEm_do_anexo_por_ambiente(string ambiente, int? diasDepoisDeUsado)
    {
        var p = await Entrar("BLOB", ambiente);
        var id = await Enviar(p.Host, p.Master, Png);
        var enviado = await RegistroQueExiste(id);
        var enviadoEm = Data(enviado, "enviadoEm")!.Value;

        await Abrir(p, id);

        var usado = await RegistroQueExiste(id);
        Assert.Equal(enviadoEm.AddHours(24), Data(enviado, "expiraEm"));
        Assert.Equal(diasDepoisDeUsado is { } dias ? enviadoEm.AddDays(dias) : (DateTimeOffset?)null, Data(usado, "expiraEm"));
        Assert.True(usado.GetValue("removidoEm", BsonNull.Value).IsBsonNull, "o anexo usado não tem removidoEm até o arquivo sair");
    }

    // ── RNF-234 · descarte pelo expiraEm, nos dois destinos ────────────────
    // sem uso em 24 h: sai o arquivo de onde foi guardado e o registro
    [Theory]
    [InlineData("BLOB")]
    [InlineData("GRIDFS")]
    public async Task RNF_234_nao_usado_em_24_h_sai_o_arquivo_e_o_registro(string destino)
    {
        var p = await Entrar(destino, "prd");
        var id = await Enviar(p.Host, p.Master, Png);
        var enviadoEm = Data(await RegistroQueExiste(id), "enviadoEm")!.Value;

        try
        {
            await Agendamento(p.Host, enviadoEm.AddHours(24).AddMinutes(1));
        }
        finally
        {
            RelogioReal(p.Host);
        }

        Assert.Null(await Registro(id));
        Assert.Equal((false, false), (await NoBlob(id), await NoGridFs(id)));
        var baixado = await Baixar(p.Host, p.Master, id);
        Assert.Equal((HttpStatusCode.NotFound, "ANEXO_INEXISTENTE"), (baixado.Status, baixado.Codigo));
    }

    // usado em hml: 30 dias depois do envio sai só o arquivo; o registro fica
    // com removidoEm, e o chamado continua citando o anexo
    [Theory]
    [InlineData("BLOB")]
    [InlineData("GRIDFS")]
    public async Task RNF_234_usado_em_hml_perde_o_arquivo_em_30_dias_e_fica_o_registro(string destino)
    {
        var p = await Entrar(destino, "hml");
        var id = await Enviar(p.Host, p.Master, Png);
        var chamado = await Abrir(p, id);
        var enviadoEm = Data(await RegistroQueExiste(id), "enviadoEm")!.Value;
        bool antes;
        try
        {
            await Agendamento(p.Host, enviadoEm.AddDays(30).AddMinutes(-1));
            antes = destino == "BLOB" ? await NoBlob(id) : await NoGridFs(id);
            await Agendamento(p.Host, enviadoEm.AddDays(30).AddMinutes(1));
        }
        finally
        {
            RelogioReal(p.Host);
        }

        Assert.True(antes, "um minuto antes dos 30 dias o arquivo ainda devia estar guardado");
        Assert.Equal((false, false), (await NoBlob(id), await NoGridFs(id)));
        var registro = await RegistroQueExiste(id);
        Assert.False(registro.GetValue("removidoEm", BsonNull.Value).IsBsonNull, "o anexo usado sem o arquivo devia ter removidoEm");
        var token = await MasterEm(p.Host, p);
        var baixado = await Baixar(p.Host, token, id);
        Assert.Equal((HttpStatusCode.NotFound, "ANEXO_INEXISTENTE"), (baixado.Status, baixado.Codigo));
        var lido = await p.Api.Enviar("GET", $"/sac/chamados/{chamado}", token: token);
        Assert.Equal(HttpStatusCode.OK, lido.Status);
        Assert.Contains(id, lido.Corpo!["anexos"]!.ToJsonString(), StringComparison.Ordinal);
    }

    // usado em prd: o arquivo fica até 5 anos depois do FECHADO
    [Fact]
    public async Task RNF_234_fechado_em_prd_perde_o_arquivo_5_anos_depois_do_fechamento()
    {
        var p = await Entrar("BLOB", "prd");
        var id = await Enviar(p.Host, p.Master, Png);
        var fechadoEm = await Fechar(p, await Abrir(p, id));
        bool antes;
        try
        {
            await Agendamento(p.Host, fechadoEm.AddYears(5).AddDays(-1));
            antes = await NoBlob(id);
            await Agendamento(p.Host, fechadoEm.AddYears(5).AddMinutes(1));
        }
        finally
        {
            RelogioReal(p.Host);
        }

        Assert.True(antes, "um dia antes dos 5 anos o arquivo ainda devia estar guardado");
        Assert.False(await NoBlob(id), "5 anos depois do fechamento o arquivo devia ter saído do Blob");
        Assert.False((await RegistroQueExiste(id)).GetValue("removidoEm", BsonNull.Value).IsBsonNull);
    }

    // vencido pelo expiraEm, o GET responde 404 já na leitura, sem esperar o descarte
    [Fact]
    public async Task RNF_234_vencido_responde_404_pelo_expiraEm()
    {
        var p = await Entrar("BLOB", "prd");
        var id = await Enviar(p.Host, p.Master, Png);
        var enviadoEm = Data(await RegistroQueExiste(id), "enviadoEm")!.Value;
        var relogio = p.Host.Services.GetRequiredService<RelogioDaAplicacao>();
        (HttpStatusCode Status, byte[] Bytes, string? Codigo) baixado;
        relogio.Definir(enviadoEm.AddHours(24).AddMinutes(1));
        try
        {
            baixado = await Baixar(p.Host, await MasterEm(p.Host, p), id);
        }
        finally
        {
            relogio.Real();
        }

        Assert.Equal((HttpStatusCode.NotFound, "ANEXO_INEXISTENTE"), (baixado.Status, baixado.Codigo));
    }
}
