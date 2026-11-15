using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Boaz.MsMono.Secundario.Out.Hub.Pagamento;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace Boaz.MsMono.Tests.Integracao;

// Endereços do Pagamento (19_modulo-pagamento v04, pagamento-contrato-api) com
// banco e cache reais e o intermediador falso: os adaptadores de faturas,
// pagamentos, estornos, configuração e avisos do banco, o canal no Redis e o
// X-Trace-Id. O comportamento de cada cenário é coberto pelo qa
// (hub/pagamento); aqui, a ligação de ponta a ponta.
public class PagamentoApiTests(Backend backend) : IClassFixture<Backend>
{
    readonly Preparo preparo = new(backend);

    // a competência fechada pelo agendamento de hoje: a anterior, em Brasília
    static string CompetenciaAnterior()
    {
        var hoje = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3));
        return new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    // o aviso assinado como o intermediador falso espera (RN-305)
    async Task<HttpStatusCode> Avisar(string corpo, string? assinatura = null)
    {
        using var http = backend.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/pagamento/notificacoes")
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        pedido.Headers.Add("x-assinatura", assinatura ?? "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(IntermediadorFalso.SegredoPadrao), Encoding.UTF8.GetBytes(corpo))));
        using var resposta = await http.SendAsync(pedido);
        return resposta.StatusCode;
    }

    async Task<(HttpStatusCode Status, string? Tipo, byte[] Corpo)> Baixar(string caminho, string token)
    {
        using var http = backend.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, caminho);
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        return (resposta.StatusCode, resposta.Content.Headers.ContentType?.MediaType, await resposta.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task RF_182_a_RF_193_a_fatura_do_fechamento_ao_estorno()
    {
        var (contaId, master) = await preparo.Conta(Preparo.SiglaSorteada());
        var admin = await preparo.Admin();
        var competencia = CompetenciaAnterior();
        await backend.Banco.GetCollection<BsonDocument>("cobrancas").InsertOneAsync(new BsonDocument
        {
            ["_id"] = $"cob-{contaId}-MOD-03-{competencia}",
            ["competencia"] = competencia,
            ["contaId"] = contaId,
            ["moduloId"] = "MOD-03",
            ["total"] = new BsonDecimal128(147.00m),
        });
        await backend.Services.GetRequiredService<Core.Hub.Pagamento.Faturas.Ports.In.IRodarAgendamentoDoPagamento>().Executar();
        using var api = backend.Cliente();

        // RF-182, RF-183: a lista e o detalhe da conta, com o X-Trace-Id
        var lista = await api.Enviar("GET", "/pagamento/faturas?situacao=TODAS", token: master.Token);
        Assert.Equal(HttpStatusCode.OK, lista.Status);
        Assert.NotNull(lista.TraceId);
        var faturaId = lista.Corpo!["itens"]![0]!["id"]!.GetValue<string>();
        var fatura = await api.Enviar("GET", $"/pagamento/faturas/{faturaId}", token: master.Token);
        Assert.Equal((HttpStatusCode.OK, competencia), (fatura.Status, fatura.Texto("competencia")));
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar("GET", "/pagamento/avisos", token: master.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar("GET", "/pagamento/formas", token: master.Token)).Status);

        // erro com o traceId no corpo, igual ao do cabeçalho (RNF-244)
        var inexistente = await api.Enviar("GET", "/pagamento/faturas/FAT-999999", token: master.Token);
        Assert.Equal((HttpStatusCode.NotFound, "FATURA_INEXISTENTE"), (inexistente.Status, inexistente.Codigo));
        Assert.Equal(inexistente.TraceId, inexistente.Texto("traceId"));

        // RF-185: o PIX e o aviso do banco que confirma (RN-305)
        var pix = await api.Enviar("POST", $"/pagamento/faturas/{faturaId}/pagamentos", new { forma = "PIX" }, master.Token);
        Assert.Equal((HttpStatusCode.Created, "AGUARDANDO"), (pix.Status, pix.Texto("status")));
        var pagamentoId = pix.Texto("id");
        var emAberto = await api.Enviar("POST", $"/pagamento/faturas/{faturaId}/pagamentos", new { forma = "PIX" }, master.Token);
        Assert.Equal((HttpStatusCode.Conflict, "PAGAMENTO_EM_ABERTO"), (emAberto.Status, emAberto.Codigo));
        var idExterno = backend.Services.GetRequiredService<IntermediadorFalso>().Pedidos.Last(p => p.Pagamento == pagamentoId).IdExterno;

        var aviso = $$$"""{"id":"evt_{{{pagamentoId}}}","tipo":"PAGAMENTO_CONFIRMADO","idExterno":"{{{idExterno}}}","metadados":{"traceId":"{{{pix.TraceId}}}"}}""";
        Assert.Equal(HttpStatusCode.Unauthorized, await Avisar(aviso, "sha256=00"));
        Assert.Equal(HttpStatusCode.OK, await Avisar(aviso));
        Assert.Equal(HttpStatusCode.OK, await Avisar(aviso));
        var pago = await api.Enviar("GET", $"/pagamento/pagamentos/{pagamentoId}", token: master.Token);
        Assert.Equal("PAGO", pago.Texto("status"));

        // RF-189: o comprovante em PDF; o boleto de um PIX não existe
        var comprovante = await Baixar($"/pagamento/pagamentos/{pagamentoId}/comprovante", master.Token);
        Assert.Equal((HttpStatusCode.OK, "application/pdf"), (comprovante.Status, comprovante.Tipo));
        Assert.Equal((byte)'%', comprovante.Corpo[0]);
        Assert.Equal(HttpStatusCode.NotFound, (await Baixar($"/pagamento/pagamentos/{pagamentoId}/boleto", master.Token)).Status);

        // RN-307: o card de quem paga e o de quem recebe
        Assert.Equal("INSTITUICAO", (await api.Enviar("GET", "/pagamento/card", token: master.Token)).Texto("perfil"));
        Assert.Equal("DEBOAZ", (await api.Enviar("GET", "/pagamento/card", token: admin.Token)).Texto("perfil"));

        // RF-190, RF-191: o lado de quem recebe
        var resumo = await api.Enviar("GET", $"/pagamento/recebimentos/resumo?competencia={competencia}", token: admin.Token);
        Assert.Equal((HttpStatusCode.OK, competencia), (resumo.Status, resumo.Texto("competencia")));
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar("GET", "/pagamento/recebimentos/competencias", token: admin.Token)).Status);
        var doRecebedor = await api.Enviar("GET", $"/pagamento/recebimentos/faturas?competencia={competencia}&situacao=PAGAS", token: admin.Token);
        Assert.Equal(faturaId, doRecebedor.Corpo!["itens"]![0]!["id"]!.GetValue<string>());
        var negado = await api.Enviar("GET", "/pagamento/recebimentos/resumo", token: master.Token);
        Assert.Equal((HttpStatusCode.Forbidden, "ACESSO_NEGADO"), (negado.Status, negado.Codigo));

        // RF-192: o estorno parcial fica pendente até o aviso
        var estorno = await api.Enviar("POST", $"/pagamento/recebimentos/pagamentos/{pagamentoId}/estornos",
            new { valor = 47.00m, motivo = "Módulo cancelado" }, admin.Token);
        Assert.Equal((HttpStatusCode.Created, "PENDENTE"), (estorno.Status, estorno.Texto("status")));
        Assert.Equal("ESTORNO_PENDENTE", (await api.Enviar("GET", $"/pagamento/pagamentos/{pagamentoId}", token: master.Token)).Texto("status"));
    }

    [Fact]
    public async Task RF_193_configuracao_com_if_match()
    {
        var admin = await preparo.Admin();
        using var api = backend.Cliente();
        var lida = await api.Enviar("GET", "/pagamento/recebimentos/configuracao", token: admin.Token);
        Assert.Equal(HttpStatusCode.OK, lida.Status);
        var versao = lida.Corpo!["versao"]!.GetValue<int>();
        var corpo = new
        {
            formas = new Dictionary<string, bool> { ["PIX"] = true, ["CARTAO_CREDITO"] = true, ["CARTAO_DEBITO"] = true, ["BOLETO"] = true },
            jurosAoDia = 0.033m,
            toleranciaAvisoDias = 0,
            diaVencimento = 10,
            prazos = new { pixMinutos = 30, debitoMinutos = 10, boletoDiasUteis = 3 },
        };
        var salva = await api.Enviar("PUT", "/pagamento/recebimentos/configuracao", corpo, admin.Token, ifMatch: $"\"{versao}\"");
        Assert.Equal((HttpStatusCode.OK, versao + 1), (salva.Status, salva.Corpo!["versao"]!.GetValue<int>()));
        var mudou = await api.Enviar("PUT", "/pagamento/recebimentos/configuracao", corpo, admin.Token, ifMatch: $"\"{versao}\"");
        Assert.Equal((HttpStatusCode.PreconditionFailed, "CONFIGURACAO_MUDOU"), (mudou.Status, mudou.Codigo));
        var semIfMatch = await api.Enviar("PUT", "/pagamento/recebimentos/configuracao", corpo, admin.Token);
        Assert.Equal(HttpStatusCode.PreconditionFailed, semIfMatch.Status);
    }
}
