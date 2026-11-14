using Azure.Storage.Blobs;
using MongoDB.Bson;
using MongoDB.Driver;
using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

// Onde está o arquivo de um anexo do SAC (18 RNF-233, RNF-234): no
// armazenamento de objetos (contêiner privado, blob "sac/anexos/<id>") ou no
// GridFS do MongoDB do backend (balde "sacAnexos", filename "sac/anexos/<id>"),
// como o modelo sac-documentos v03 especifica.
//
// Exceção à regra "o banco só prepara" (README, seção 6), como a de
// BancoDaPartida: o que a RNF-233 e a RNF-234 prometem é onde o arquivo fica e
// que ele sai do armazenamento, e não há endereço na API que mostre isso (o
// GET responde 404 já pelo expiraEm, antes de o arquivo sair). A suíte só
// pergunta se o nome existe, sem ler documento nenhum do backend.
public sealed class ArquivosGuardados(string mongoUrl, string banco, string blobConexao, string conteiner)
{
    // os do backend de API_URL
    public static ArquivosGuardados DoBackend() =>
        new(Ambiente.MongoUrl, Ambiente.MongoBanco, Ambiente.BlobConexao, Ambiente.BlobConteiner);

    // os da instância de Partida (banco e contêiner próprios)
    public static ArquivosGuardados DaPartida() =>
        new(Ambiente.PartidaMongoUrl, Ambiente.PartidaMongoBanco, Ambiente.PartidaBlobConexao, Ambiente.PartidaBlobConteiner);

    public static string Objeto(string anexoId) => $"sac/anexos/{anexoId}";

    public async Task<bool> NoGridFs(string anexoId) =>
        await new MongoClient(mongoUrl).GetDatabase(banco).GetCollection<BsonDocument>("sacAnexos.files")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("filename", Objeto(anexoId))) > 0;

    public async Task<bool> NoBlob(string anexoId)
    {
        var conteinerDoBlob = Conteiner();
        if (!await conteinerDoBlob.ExistsAsync()) return false;
        return await conteinerDoBlob.GetBlobClient(Objeto(anexoId)).ExistsAsync();
    }

    // quantos arquivos de anexo existem nos dois lugares: para "nada é guardado"
    public async Task<long> Total()
    {
        var gridFs = await new MongoClient(mongoUrl).GetDatabase(banco).GetCollection<BsonDocument>("sacAnexos.files")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var conteinerDoBlob = Conteiner();
        long blobs = 0;
        if (await conteinerDoBlob.ExistsAsync())
        {
            await foreach (var _ in conteinerDoBlob.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, "sac/anexos/", CancellationToken.None)) blobs++;
        }
        return gridFs + blobs;
    }

    BlobContainerClient Conteiner()
    {
        if (blobConexao.Length == 0)
        {
            throw new PendingStepException(
                "BLOB_CONEXAO ausente: os cenários que olham o armazenamento de objetos dos anexos (18 RNF-233, RNF-234) precisam da conexão do Blob (README, seção 5)");
        }
        return new BlobContainerClient(blobConexao, conteiner);
    }
}
