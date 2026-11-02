using MongoDB.Driver;
using StackExchange.Redis;

namespace Boaz.Qa.Integracao.Suporte;

// Preparo do estado que a API não cria (tempo sem uso, bloqueios seguidos),
// pelas chaves do diagrama autenticacao-documentos. Nunca confere resultado:
// o que só o banco mostra fica nos testes do backend.
public static class Estado
{
    static readonly Lazy<ConnectionMultiplexer> redis = new(() =>
        ConnectionMultiplexer.Connect($"{Ambiente.RedisUrl},allowAdmin=true"));
    static readonly Lazy<MongoClient> mongo = new(() => new MongoClient(Ambiente.MongoUrl));

    static readonly string[] BancosDoSistema = ["admin", "config", "local"];

    // cada cenário começa com o banco e o cache vazios. Apaga tudo o que está
    // em MONGO_URL e REDIS_URL: só roda com QA_PODE_LIMPAR=sim, para nunca
    // atingir um banco que não seja descartável (o do CI é em memória)
    public static async Task Limpar()
    {
        if (!Ambiente.PodeLimpar)
        {
            throw new InvalidOperationException(
                "QA_PODE_LIMPAR=sim ausente: a suíte apaga o banco e o cache de MONGO_URL e REDIS_URL antes de cada cenário");
        }
        foreach (var banco in await (await mongo.Value.ListDatabaseNamesAsync()).ToListAsync())
        {
            if (!BancosDoSistema.Contains(banco)) await mongo.Value.DropDatabaseAsync(banco);
        }
        foreach (var servidor in redis.Value.GetServers()) await servidor.FlushAllDatabasesAsync();
    }

    // a sessão passou do tempo sem uso: a chave expira (RN-226, RNF-217)
    public static Task ExpirarSessao(string sid) => redis.Value.GetDatabase().KeyDeleteAsync($"sessao:{sid}");

    // falta pouco para o tempo sem uso terminar
    public static Task EncurtarSessao(string sid, TimeSpan resta) =>
        redis.Value.GetDatabase().KeyExpireAsync($"sessao:{sid}", resta);

    // bloqueios anteriores, para o tempo dobrar (RNF-204)
    public static Task BloqueiosSeguidos(string alvo, string chave, int quantos) =>
        redis.Value.GetDatabase().HashSetAsync($"tentativas:{alvo}:{chave}", "bloqueiosSeguidos", quantos);
}
