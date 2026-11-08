using System.Net;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// RN-270, RN-271: a falha inesperada responde 500 ERRO_INESPERADO com a mensagem
// do contrato, sem detalhe da exceção, com o X-Trace-Id da execução, e grava
// nivel ERRO em "auditoriaErros". O banco "cai" na gravação do pedido de ajuda.
public class ErroInesperadoApiTests(Backend backend) : IClassFixture<Backend>
{
    const string Detalhe = "detalhe interno que não pode sair na resposta";

    // Os pedidos de ajuda com o banco fora do ar na gravação.
    sealed class PedidosQueCaem : IPedidosDeAjuda
    {
        public Task<IReadOnlyList<DateTimeOffset>> EnviosDoAparelho(string aparelho, DateTimeOffset desde, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DateTimeOffset>>([]);

        public Task<string> Enviar(string aparelho, string instituicao, string contato, DateTimeOffset quando, CancellationToken ct = default) =>
            throw new InvalidOperationException(Detalhe);
    }

    [Fact]
    public async Task RN_270_falha_inesperada_responde_500_com_o_traceId()
    {
        using var fora = backend.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IPedidosDeAjuda, PedidosQueCaem>()));
        using var api = new Cliente(fora.CreateClient());

        var resposta = await api.Enviar("POST", "/contas/esqueci-senha-master",
            new { instituicao = "Escola Bento Freitas", contato = "(51) 99999-0000" });

        Assert.Equal((HttpStatusCode.InternalServerError, "ERRO_INESPERADO"), (resposta.Status, resposta.Codigo));
        Assert.Equal("Algo deu errado. Tente de novo mais tarde.", resposta.Texto("mensagem"));
        Assert.DoesNotContain(Detalhe, resposta.Corpo!.ToJsonString(), StringComparison.Ordinal);
        Assert.Matches("^trc-[0-9a-f]{8}$", resposta.TraceId);

        var filtro = Builders<BsonDocument>.Filter.Eq("traceId", resposta.TraceId);
        var erro = Assert.Single(await backend.Banco.GetCollection<BsonDocument>("auditoriaErros").Find(filtro).ToListAsync());
        Assert.Equal(("ESQUECI_SENHA_MASTER", "ERRO_INESPERADO", "ERRO"), (erro["acao"].AsString, erro["codigo"].AsString, erro["nivel"].AsString));
        Assert.Equal("INICIADO", Assert.Single(await backend.Banco.GetCollection<BsonDocument>("auditoria").Find(filtro).ToListAsync())["etapa"].AsString);
    }
}
