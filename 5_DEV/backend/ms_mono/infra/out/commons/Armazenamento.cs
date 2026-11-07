using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Boaz.MsMono.Infra.Out.Commons;

// Banco (MongoDB) e cache (Redis) do backend, compartilhados pelos adaptadores
// de saída. Os endereços vêm de ConnectionStrings:MongoDb (ou :Mongo, nome
// usado no Azure) e ConnectionStrings:Redis; o banco, de Mongo:Banco
// (Configuracoes.cs). Nada conecta na partida: o primeiro uso conecta, para
// /health e /doc/api responderem mesmo sem banco.
public sealed class Armazenamento
{
    readonly Lazy<IMongoDatabase> banco;
    readonly Lazy<ConnectionMultiplexer> cache;

    public Armazenamento(IConfiguration configuracao, IOptions<OpcoesDoMongo> opcoes)
    {
        var mongo = configuracao.GetConnectionString("MongoDb") ?? configuracao.GetConnectionString("Mongo")
            ?? throw new InvalidOperationException("Defina ConnectionStrings__MongoDb (appsettings.json ou variável de ambiente).");
        var redis = configuracao.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Defina ConnectionStrings__Redis (appsettings.json ou variável de ambiente).");
        var nome = opcoes.Value.Banco;
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

    // O servidor aceita transação: replica set (setName no hello) ou mongos
    // (msg isdbgrid). Um MongoDB standalone, como o do job backend do GitHub
    // Actions, não aceita. Perguntado uma vez por processo.
    public async Task<bool> AceitaTransacao(CancellationToken ct = default)
    {
        if (aceitaTransacao is { } sabido) return sabido;
        var hello = await Banco.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        aceitaTransacao = hello.Contains("setName") || hello.GetValue("msg", "").ToString() == "isdbgrid";
        return aceitaTransacao.Value;
    }

    bool? aceitaTransacao;

    // Grava numa só operação (RNF-223): numa transação, quando o servidor
    // aceita; senão, em sequência, sem sessão. A gravação recebe a sessão (ou
    // null) e a usa em cada escrita.
    public async Task<T> NumaOperacao<T>(Func<IClientSessionHandle?, CancellationToken, Task<T>> gravar, CancellationToken ct = default)
    {
        if (!await AceitaTransacao(ct)) return await gravar(null, ct);
        using var sessao = await Banco.Client.StartSessionAsync(cancellationToken: ct);
        return await sessao.WithTransactionAsync((s, c) => gravar(s, c), cancellationToken: ct);
    }

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
