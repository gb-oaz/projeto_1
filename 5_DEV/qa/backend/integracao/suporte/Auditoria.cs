using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.Qa.Integracao.Suporte;

// As coleções "auditoria" e "auditoriaErros" do banco do backend (MONGO_BANCO),
// para a RN-178, a RN-270 e a RN-271 de 17_modulo-usuario.md.
//
// Segunda exceção à regra "o banco só prepara" (README, seção 6), e só para
// isto: o que esses cenários prometem são os registros gravados nessas duas
// coleções, e o contrato não tem endereço que os mostre (nem deve ter: são
// do time deboaz). A suíte só lê, e só essas duas coleções, pelos campos do
// modelo de documentos do requisito (exemplo-entidades): traceId, etapa,
// contaId, quem, acao, alvo, codigo, nivel, quando e guardarAte. Continua
// caixa-preta (RN-151): nada de 5_DEV/backend/ é referenciado.
public static class Auditoria
{
    public const string Registros = "auditoria", Erros = "auditoriaErros";

    static readonly Lazy<MongoClient> mongo = new(() => new MongoClient(Ambiente.MongoUrl));

    static IMongoCollection<BsonDocument> Colecao(string nome) =>
        mongo.Value.GetDatabase(Ambiente.MongoBanco).GetCollection<BsonDocument>(nome);

    // o que já existia nas duas coleções antes do pedido do cenário: o preparo
    // (criar a Maria, conceder, entrar) também pode ser auditado e não conta
    public sealed record Marca(HashSet<string> Registros, HashSet<string> Erros)
    {
        public HashSet<string> De(string colecao) => colecao == Auditoria.Registros ? Registros : Erros;
    }

    public static async Task<Marca> Marcar() => new(await Ids(Registros), await Ids(Erros));

    // os documentos gravados depois da marca (e até a outra, se houver),
    // na ordem da hora em que foram gravados
    public static async Task<List<BsonDocument>> Novos(string colecao, Marca desde, Marca? ate = null)
    {
        var todos = await Todos(colecao);
        return [.. todos
            .Where(d => !desde.De(colecao).Contains(Id(d)) && (ate is null || ate.De(colecao).Contains(Id(d))))
            .OrderBy(d => Data(d, "quando") ?? DateTimeOffset.MaxValue)];
    }

    public static async Task<List<BsonDocument>> Todos(string colecao) =>
        await Colecao(colecao).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

    static async Task<HashSet<string>> Ids(string colecao) => [.. (await Todos(colecao)).Select(Id)];

    static string Id(BsonDocument d) => d.GetValue("_id", BsonNull.Value).ToString() ?? "";

    // o campo como texto; null se não veio ou veio null
    public static string? Texto(BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && !v.IsBsonNull ? (v.IsString ? v.AsString : v.ToString()) : null;

    // o campo de data, gravado como data do banco ou como texto ISO 8601
    public static DateTimeOffset? Data(BsonDocument d, string campo)
    {
        if (!d.TryGetValue(campo, out var v) || v.IsBsonNull) return null;
        if (v.IsValidDateTime) return new DateTimeOffset(v.ToUniversalTime(), TimeSpan.Zero);
        return v.IsString && DateTimeOffset.TryParse(v.AsString, out var data) ? data : null;
    }

    public static string Resumo(IEnumerable<BsonDocument> documentos) =>
        "[" + string.Join(", ", documentos.Select(d => d.ToJson())) + "]";
}
