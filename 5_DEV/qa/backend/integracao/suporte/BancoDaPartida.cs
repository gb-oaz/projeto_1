using MongoDB.Bson;
using MongoDB.Driver;
using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

// O banco da instância de Partida (suporte/Partida.cs), para a RNF-227 de
// 17_modulo-usuario.md: coleções e índices prontos na partida.
//
// Exceção à regra "o banco só prepara" (README, seção 6), e só para isto: o
// que a RNF-227 promete é o esquema do banco (que coleções e índices existem),
// e não há endereço na API que o mostre. A suíte lê só os metadados (nomes das
// coleções, chaves e opções dos índices) e os _id dos documentos; não confere
// o conteúdo de documento nenhum. Continua caixa-preta (RN-151): não usa os
// nomes internos dos índices, só a chave e as opções que o requisito diz.
//
// É o banco próprio da instância (PARTIDA_Mongo__Banco), nunca o MONGO_BANCO
// do backend principal; Estado.Limpar o esvazia antes de cada cenário.
public static class BancoDaPartida
{
    static readonly Lazy<MongoClient> mongo = new(() => new MongoClient(Ambiente.PartidaMongoUrl));

    // as 7 coleções do modelo de documentos de 17_modulo-usuario.md (RNF-227)
    public static readonly string[] Colecoes =
        ["contas", "usuarios", "auditoria", "auditoriaErros", "geracoesSenha", "registrosPermissao", "pedidosAjuda"];

    public static IMongoDatabase Banco
    {
        get
        {
            if (Ambiente.PartidaMongoBanco.Length == 0)
            {
                throw new PendingStepException(
                    "PARTIDA_Mongo__Banco ausente: os cenários de coleções e índices (RNF-227) olham o banco próprio da instância de Partida (README, seção 5)");
            }
            return mongo.Value.GetDatabase(Ambiente.PartidaMongoBanco);
        }
    }

    public static IMongoCollection<BsonDocument> Colecao(string nome) => Banco.GetCollection<BsonDocument>(nome);

    // preparo: o banco da instância sem as coleções do modelo (o registro de
    // módulos fica, como em Estado.Limpar)
    public static async Task Esvaziar()
    {
        foreach (var nome in Colecoes) await Banco.DropCollectionAsync(nome);
    }

    public static async Task<List<string>> NomesDasColecoes() =>
        await (await Banco.ListCollectionNamesAsync()).ToListAsync();

    public static async Task<List<Indice>> Indices(string colecao)
    {
        var lista = await (await Colecao(colecao).Indexes.ListAsync()).ToListAsync();
        return [.. lista.Select(Indice.De).Where(i => i.Chave != "_id:1")];
    }

    // preparo da RNF-227: um índice da suíte na mesma chave, com outro nome,
    // faz o banco recusar o índice da aplicação nessa chave (IndexOptionsConflict),
    // sem a suíte saber o nome interno dele
    public const string IndiceQueImpede = "qa_rnf227_impede";

    public static Task CriarIndiceQueImpede(string colecao, string campo, bool unico = false) =>
        Colecao(colecao).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending(campo), new CreateIndexOptions { Name = IndiceQueImpede, Unique = unico }));

    public static async Task<List<string>> Ids(string colecao) =>
        [.. (await Colecao(colecao).Find(FilterDefinition<BsonDocument>.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync())
            .Select(d => d["_id"].ToString()!)];
}

// Um índice como o requisito o descreve: os campos em ordem, com ↓ para o
// decrescente ("usuarioId+pedidaEm↓"), se é único, se vale só com o campo
// presente (filtro parcial) e se expira documentos (TTL)
public sealed record Indice(string Chave, bool Unico, bool Parcial, bool Expira, string Nome = "")
{
    public static Indice De(BsonDocument d)
    {
        var chave = string.Join("+", d["key"].AsBsonDocument.Select(e => $"{e.Name}:{(e.Value.IsNumeric ? (e.Value.ToDouble() < 0 ? "-1" : "1") : e.Value.ToString())}"));
        return new Indice(
            chave,
            d.TryGetValue("unique", out var u) && u.ToBoolean(),
            d.Contains("partialFilterExpression"),
            d.Contains("expireAfterSeconds"),
            d.GetValue("name", "").ToString()!);
    }

    // "contaId+matricula" → "contaId:1+matricula:1"; "usuarioId+em↓+versao↓" → "usuarioId:1+em:-1+versao:-1"
    public static string ChaveDoRequisito(string campos) =>
        string.Join("+", campos.Split('+').Select(c => c.EndsWith('↓') ? $"{c.TrimEnd('↓')}:-1" : $"{c}:1"));

    public override string ToString() =>
        $"{{{Chave}}}{(Unico ? " único" : "")}{(Parcial ? " parcial" : "")}{(Expira ? " TTL" : "")}";
}
