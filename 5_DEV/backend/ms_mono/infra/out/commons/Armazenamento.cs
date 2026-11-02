using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Boaz.MsMono.Infra.Out.Commons;

// Banco (MongoDB) e cache (Redis) do backend, compartilhados pelos adaptadores
// de saída. Os endereços vêm de ConnectionStrings:MongoDb (ou :Mongo, nome
// usado no Azure) e ConnectionStrings:Redis; o banco, de Mongo:Banco (padrão
// "boaz", o mesmo do qa). Nada conecta na partida: o primeiro uso conecta, para
// /health e /doc/api responderem mesmo sem banco.
public sealed class Armazenamento
{
    public const string BancoPadrao = "boaz";

    readonly Lazy<IMongoDatabase> banco;
    readonly Lazy<ConnectionMultiplexer> cache;

    public Armazenamento(IConfiguration configuracao)
    {
        var mongo = configuracao.GetConnectionString("MongoDb") ?? configuracao.GetConnectionString("Mongo") ?? "mongodb://localhost:27017";
        var redis = configuracao.GetConnectionString("Redis") ?? "localhost:6379";
        var nome = configuracao["Mongo:Banco"] is { Length: > 0 } n ? n : BancoPadrao;
        banco = new(() => new MongoClient(mongo).GetDatabase(nome));
        cache = new(() =>
        {
            var opcoes = ConfigurationOptions.Parse(redis);
            opcoes.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(opcoes);
        });
    }

    // Banco de dados do backend.
    public IMongoDatabase Banco => banco.Value;

    // Cache das sessões e das tentativas.
    public IDatabase Cache => cache.Value.GetDatabase();

    // Uma coleção como documentos soltos: cada módulo grava só os próprios
    // campos de um documento compartilhado (usuarios), com $set campo a campo.
    public IMongoCollection<BsonDocument> Colecao(string nome) => Banco.GetCollection<BsonDocument>(nome);
}

// Nomes das coleções (diagramas *-documentos).
public static class Colecoes
{
    public const string Usuarios = "usuarios";
    public const string Contas = "contas";
    public const string GeracoesSenha = "geracoesSenha";
    public const string Auditoria = "auditoria";
    public const string PedidosAjuda = "pedidosAjuda";
    public const string RegistrosPermissao = "registrosPermissao";
    public const string Modulos = "modulos";
    public const string Contratacoes = "contratacoes";
}

// Leitura e escrita de campos de BsonDocument com nulos e datas.
public static class Bson
{
    public static string? Texto(this BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && v.IsString ? v.AsString : null;

    public static int Inteiro(this BsonDocument d, string campo, int padrao = 0) =>
        d.TryGetValue(campo, out var v) && v.IsNumeric ? v.ToInt32() : padrao;

    public static DateTimeOffset? Data(this BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && v.IsValidDateTime ? new DateTimeOffset(v.ToUniversalTime(), TimeSpan.Zero) : null;

    public static BsonDocument? Documento(this BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && v.IsBsonDocument ? v.AsBsonDocument : null;

    public static BsonValue Valor(string? texto) => texto is null ? BsonNull.Value : texto;

    public static BsonValue Valor(DateTimeOffset? data) => data is { } d ? new BsonDateTime(d.UtcDateTime) : BsonNull.Value;

    public static FilterDefinitionBuilder<BsonDocument> Filtro => Builders<BsonDocument>.Filter;

    public static UpdateDefinitionBuilder<BsonDocument> Mudar => Builders<BsonDocument>.Update;

    // Texto da busca como expressão regular literal, sem diferenciar maiúsculas.
    public static BsonRegularExpression Contem(string busca) =>
        new(System.Text.RegularExpressions.Regex.Escape(busca), "i");
}
