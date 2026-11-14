using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.MsMono.Secundario.Out.Commons;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// A partida do SAC com o banco de verdade (sac-inicializacao-sequencia v01,
// fase 1; 17 RNF-227): os índices de sac-documentos v02, com os nomes de
// Indices.cs, prontos antes do primeiro pedido, sem duplicar na segunda vez, e
// o índice único de cardSac que não pode ser criado para a partida. No padrão
// de PartidaTests (índices de identidade-acesso-documentos).
public class SacNaPartidaTests(Backend backend) : IClassFixture<Backend>
{
    Indices Indices => backend.Services.GetRequiredService<Indices>();

    async Task<List<BsonDocument>> IndicesDe(string colecao) =>
        await (await backend.Banco.GetCollection<BsonDocument>(colecao).Indexes.ListAsync()).ToListAsync();

    [Theory]
    [InlineData("chamados", "contaId_atualizadoEm")]
    [InlineData("chamados", "contaId_status")]
    [InlineData("chamados", "status_ordemDaFila")]
    [InlineData("chamados", "responsavel_status")]
    [InlineData("chamados", "status_prazoResposta")]
    [InlineData("chamados", "status_aguardandoDesde")]
    [InlineData("chamados", "status_resolvidoEm")]
    [InlineData("chamados", "avaliacao_nota")]
    [InlineData("chamados", "guardarAte")]
    [InlineData("mensagensChamado", "chamadoId_numero")]
    [InlineData("anexosSac", "chamadoId")]
    [InlineData("anexosSac", "expiraEm")]
    [InlineData("anexosSac", "enviadoPor_enviadoEm")]
    public async Task Indices_do_sac_criados_na_partida(string colecao, string indice)
    {
        _ = backend.Server;

        var indices = await IndicesDe(colecao);

        Assert.Contains(indices, i => i["name"] == indice && !(i.Contains("unique") && i["unique"].AsBoolean));
        Assert.DoesNotContain(indices, i => i.Contains("expireAfterSeconds"));
    }

    // cardSac: um documento por escopo e dia (RN-295, 15 RN-278)
    [Fact]
    public async Task RN_295_cardSac_com_escopo_e_dia_unicos()
    {
        _ = backend.Server;

        var indices = await IndicesDe("cardSac");

        Assert.Contains(indices, i => i["name"] == "escopo_dia_unico" && i["unique"].AsBoolean);
        // de novo: índice igual não muda nada
        Assert.Equal(0, await Indices.Criar());
    }

    // RNF-227: dois dias iguais do mesmo escopo antes do índice único — a
    // partida não segue, e o log cita o índice sem os valores gravados
    [Fact]
    public async Task RNF_227_indice_unico_do_cardSac_que_nao_pode_ser_criado_nao_inicia()
    {
        _ = backend.Server;
        var card = backend.Banco.GetCollection<BsonDocument>("cardSac");
        await card.Indexes.DropOneAsync("escopo_dia_unico");
        var ids = new[] { "dupla-rnf227-1", "dupla-rnf227-2" };
        foreach (var id in ids)
        {
            await card.InsertOneAsync(new BsonDocument { ["_id"] = id, ["escopo"] = "ESCOPODUPLO", ["dia"] = "2026-11-12", ["emAndamento"] = 0 });
        }
        try
        {
            var falha = await Assert.ThrowsAsync<FalhaNaInicializacao>(() => Indices.Criar());
            Assert.Contains("escopo_dia_unico em cardSac", falha.Message);
            Assert.DoesNotContain("ESCOPODUPLO", falha.Message);
        }
        finally
        {
            await card.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", ids));
        }
        Assert.Equal(0, await Indices.Criar());
        Assert.Contains(await IndicesDe("cardSac"), i => i["name"] == "escopo_dia_unico" && i["unique"].AsBoolean);
    }
}
