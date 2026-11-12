using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Boaz.Qa.Integracao.Suporte;

// As permissões guardadas no documento do usuário (17-b RN-287, "o que fica
// guardado"), pelos campos do modelo de documentos (autorizacao-documentos
// v04): versao, pv, mods e suspensos [{mod, abas}]. Quarta exceção à regra de
// não conferir pelo banco (README, seção 6): o módulo suspenso não sai na API
// (o GET só traz mods e concediveis), e a RN-287 promete o próprio documento.
// Lê pelo _id, no MONGO_BANCO do backend principal.
public static class PermissoesGuardadas
{
    static readonly Lazy<MongoClient> mongo = new(() => new MongoClient(Ambiente.MongoUrl));

    static IMongoCollection<BsonDocument> Usuarios =>
        mongo.Value.GetDatabase(Ambiente.MongoBanco).GetCollection<BsonDocument>("usuarios");

    // "permissoes" do usuário em JSON; falha se o usuário ou o campo não existe
    public static async Task<JsonObject> Ler(string usuarioId)
    {
        var documento = await Usuarios.Find(Builders<BsonDocument>.Filter.Eq("_id", usuarioId)).FirstOrDefaultAsync()
            ?? throw new Xunit.Sdk.XunitException($"\"usuarios\" não tem {usuarioId}");
        if (!documento.TryGetValue("permissoes", out var permissoes) || !permissoes.IsBsonDocument)
        {
            throw new Xunit.Sdk.XunitException($"{usuarioId} não tem \"permissoes\": {documento.ToJson()}");
        }
        return JsonNode.Parse(permissoes.AsBsonDocument.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson }))!.AsObject();
    }

    // as abas guardadas do módulo suspenso, ou null se ele não está em "suspensos"
    public static async Task<string?> AbasSuspensas(string usuarioId, string moduloId)
    {
        var suspensos = (await Ler(usuarioId))["suspensos"] as JsonArray ?? [];
        return suspensos.FirstOrDefault(s => s?["mod"]?.GetValue<string>() == moduloId)?["abas"]?.GetValue<string>();
    }
}
