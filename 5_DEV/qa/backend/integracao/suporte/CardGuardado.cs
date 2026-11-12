using System.Globalization;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Boaz.Qa.Integracao.Suporte;

// O card guardado na coleção "cardUsuarios" (17 RN-280, 15 RN-278), pelos
// campos do modelo de documentos (identidade-acesso-documentos v18). Terceira
// exceção à regra de não conferir pelo banco (README, seção 6): o exemplo
// "card guardado" e o corte em 400 dias, 106 semanas e 24 meses prometem o
// próprio documento, sem endereço na API. Lê pelo contaId e o perfil, no
// MONGO_BANCO do backend principal.
public static class CardGuardado
{
    static readonly Lazy<MongoClient> mongo = new(() => new MongoClient(Ambiente.MongoUrl));

    static IMongoCollection<BsonDocument> Colecao =>
        mongo.Value.GetDatabase(Ambiente.MongoBanco).GetCollection<BsonDocument>("cardUsuarios");

    static FilterDefinition<BsonDocument> Do(string contaId, string perfil) =>
        Builders<BsonDocument>.Filter.Eq("contaId", contaId) & Builders<BsonDocument>.Filter.Eq("perfil", perfil)
            & Builders<BsonDocument>.Filter.Eq("usuarioId", BsonNull.Value);

    // o documento em JSON (datas como {"$date": …}); falha se não existe
    public static async Task<JsonObject> Ler(string contaId, string perfil)
    {
        var documento = await Colecao.Find(Do(contaId, perfil)).FirstOrDefaultAsync()
            ?? throw new Xunit.Sdk.XunitException($"\"cardUsuarios\" não tem o card {perfil} da conta {contaId}");
        return JsonNode.Parse(documento.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson }))!.AsObject();
    }

    // histórico inteiro, preparado: dias, semanas e meses terminando no dia,
    // na semana e no mês de "ultimoDia". Os valores (1000+, 2000+, 3000+) são
    // diferentes entre si, para a série mostrar de onde veio cada ponto
    public static async Task<(List<string> Dias, List<string> Semanas, List<string> Meses)> Semear(
        string contaId, string perfil, DateOnly ultimoDia, int dias, int semanas, int meses)
    {
        var d = Enumerable.Range(0, dias).Select(i => ultimoDia.AddDays(i - dias + 1).ToString("yyyy-MM-dd")).ToList();
        var s = Enumerable.Range(0, semanas).Select(i => Semana(ultimoDia.AddDays(7 * (i - semanas + 1)))).ToList();
        var m = Enumerable.Range(0, meses).Select(i => ultimoDia.AddMonths(i - meses + 1).ToString("yyyy-MM")).ToList();
        var r = await Colecao.UpdateOneAsync(Do(contaId, perfil), Builders<BsonDocument>.Update
            .Set("dias", Lista("dia", d, 1000))
            .Set("semanas", Lista("semana", s, 2000))
            .Set("meses", Lista("mes", m, 3000)));
        if (r.MatchedCount != 1) throw new InvalidOperationException($"preparo: \"cardUsuarios\" sem o card {perfil} da conta {contaId}");
        return (d, s, m);
    }

    public static int Valor(string campo, int i) => campo switch { "dia" => 1000, "semana" => 2000, _ => 3000 } + i;

    // semana ISO, como no exemplo: "2026-W45"
    public static string Semana(DateOnly dia)
    {
        var data = dia.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(data)}-W{ISOWeek.GetWeekOfYear(data):D2}";
    }

    static BsonArray Lista(string campo, List<string> chaves, int base_) =>
        new(chaves.Select((chave, i) => new BsonDocument { [campo] = chave, ["valor"] = base_ + i }));
}
