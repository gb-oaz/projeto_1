using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Boaz.Qa.Integracao.Suporte;

// Preparo do estado que a API não cria (tempo sem uso, bloqueios seguidos,
// versão das permissões, módulo contratado), pelas chaves e coleções dos
// diagramas autenticacao-documentos, autorizacao-documentos e
// catalogo-documentos. Nunca confere resultado: o que só o banco mostra fica
// nos testes do backend.
public static class Estado
{
    static readonly Lazy<ConnectionMultiplexer> redis = new(() =>
        ConnectionMultiplexer.Connect($"{Ambiente.RedisUrl},allowAdmin=true"));
    static readonly Lazy<MongoClient> mongo = new(() => new MongoClient(Ambiente.MongoUrl));

    static readonly string[] BancosDoSistema = ["admin", "config", "local"];

    // o que a inicialização do backend criou (17 RN-190, RN-265): guardado na
    // primeira limpeza e devolvido depois de cada uma, como estava
    static List<(string Colecao, BsonDocument Documento)>? daInicializacao;

    // cada cenário começa com o banco e o cache vazios, menos as contas que a
    // inicialização criou. Apaga tudo o que está em MONGO_URL e REDIS_URL: só
    // roda com QA_PODE_LIMPAR=sim, para nunca atingir um banco que não seja
    // descartável (o do CI é em memória)
    public static async Task Limpar()
    {
        if (!Ambiente.PodeLimpar)
        {
            throw new InvalidOperationException(
                "QA_PODE_LIMPAR=sim ausente: a suíte apaga o banco e o cache de MONGO_URL e REDIS_URL antes de cada cenário");
        }
        daInicializacao ??= await GuardarInicializacao();
        // o registro de módulos (15_modulos) fica: é dado do sistema, não do cenário
        foreach (var nome in await (await mongo.Value.ListDatabaseNamesAsync()).ToListAsync())
        {
            if (BancosDoSistema.Contains(nome)) continue;
            var banco = mongo.Value.GetDatabase(nome);
            foreach (var colecao in await (await banco.ListCollectionNamesAsync()).ToListAsync())
            {
                if (colecao != "modulos") await banco.DropCollectionAsync(colecao);
            }
        }
        foreach (var servidor in redis.Value.GetServers()) await servidor.FlushAllDatabasesAsync();
        await SemearModulos();
        foreach (var grupo in daInicializacao.GroupBy(d => d.Colecao))
        {
            await Banco.GetCollection<BsonDocument>(grupo.Key).InsertManyAsync(grupo.Select(d => d.Documento.DeepClone().AsBsonDocument));
        }
    }

    // os documentos das contas e dos usuários iniciais (pelos _id do diagrama
    // usuario-inicializacao-sequencia), em qualquer coleção do banco do backend.
    // Só os copia: a suíte não confere o que eles têm
    static async Task<List<(string, BsonDocument)>> GuardarInicializacao()
    {
        var guardados = new List<(string, BsonDocument)>();
        var filtro = Builders<BsonDocument>.Filter.In("_id", ContasIniciais.Ids.Select(id => (BsonValue)id));
        foreach (var colecao in await (await Banco.ListCollectionNamesAsync()).ToListAsync())
        {
            if (colecao == "modulos") continue;
            foreach (var documento in await Banco.GetCollection<BsonDocument>(colecao).Find(filtro).ToListAsync())
            {
                guardados.Add((colecao, documento));
            }
        }
        return guardados;
    }

    // os módulos que os cenários citam, se o backend ainda não os registrou
    // (15_modulos): da conta, contratáveis e do time
    static readonly (string Id, string Nome, string Tipo)[] Modulos =
    [
        ("MOD-02", "SAC", "DA_CONTA"), ("MOD-03", "Usuários", "DA_CONTA"), ("MOD-04", "Sugestão", "DA_CONTA"),
        ("MOD-05", "Quiz", "CONTRATAVEL"), ("MOD-06", "Presença", "CONTRATAVEL"), ("MOD-07", "Carteirinha", "CONTRATAVEL"),
        ("MOD-08", "Turmas", "DA_CONTA"), ("MOD-09", "Tarefas", "DA_CONTA"), ("MOD-12", "Agenda", "CONTRATAVEL"),
        ("MOD-13", "Perfil", "DA_CONTA"), ("MOD-14", "Catálogo", "DO_TIME"),
    ];

    static async Task SemearModulos()
    {
        var modulos = Banco.GetCollection<BsonDocument>("modulos");
        foreach (var (id, nome, tipo) in Modulos)
        {
            await modulos.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.SetOnInsert("nome", nome).SetOnInsert("tipo", tipo),
                new UpdateOptions { IsUpsert = true });
        }
    }

    static IMongoDatabase Banco => mongo.Value.GetDatabase(Ambiente.MongoBanco);

    // versão das permissões de um usuário, como nos exemplos (versão 6, versão 7)
    public static Task DefinirVersao(string usuarioId, int versao) =>
        Banco.GetCollection<BsonDocument>("usuarios").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", usuarioId),
            Builders<BsonDocument>.Update.Set("permissoes.versao", versao).Set("permissoes.pv", versao));

    // a conta contrata um módulo hoje (16_modulo-catalogo, coleção contratacoes)
    public static Task Contratar(string contaId, string sigla, string moduloId) =>
        Banco.GetCollection<BsonDocument>("contratacoes").InsertOneAsync(new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString(),
            ["contaId"] = contaId,
            ["sigla"] = sigla,
            ["moduloId"] = moduloId,
            ["plano"] = "MENSAL",
            ["desde"] = DateTime.UtcNow.AddMinutes(-1),
            ["ate"] = BsonNull.Value,
            ["automatica"] = false,
        });

    // a contratação em vigor termina agora
    public static Task Descontratar(string contaId, string moduloId) =>
        Banco.GetCollection<BsonDocument>("contratacoes").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("contaId", contaId) & Builders<BsonDocument>.Filter.Eq("moduloId", moduloId)
                & Builders<BsonDocument>.Filter.Eq("ate", BsonNull.Value),
            Builders<BsonDocument>.Update.Set("ate", DateTime.UtcNow.AddSeconds(-1)));

    // o código de liberação foi pedido há algum tempo (RN-182: vale 72 h)
    public static Task PedidoHa(string usuarioId, TimeSpan tempo) =>
        Banco.GetCollection<BsonDocument>("geracoesSenha").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("usuarioId", usuarioId),
            Builders<BsonDocument>.Update.Set("pedidaEm", DateTime.UtcNow - tempo));

    // o código de liberação vence hoje, no horário de Brasília, e ainda não
    // venceu (selo do card, 17 RN-280): vence no meio do que resta do dia
    public static Task CodigoVenceHoje(string usuarioId)
    {
        var agora = DateTime.UtcNow;
        var fimDoDia = agora.AddHours(-3).Date.AddDays(1).AddHours(3);
        var vence = agora + (fimDoDia - agora) / 2;
        return Banco.GetCollection<BsonDocument>("geracoesSenha").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("usuarioId", usuarioId),
            Builders<BsonDocument>.Update.Set("pedidaEm", vence - TimeSpan.FromHours(72)));
    }

    // último acesso dos usuários (17 RN-280, "Sem acesso há 30 dias"): a API só
    // o grava quando a pessoa entra, e o cenário precisa dele no passado
    public static Task UltimoAcesso(IEnumerable<string> usuarioIds, DateTime quando) =>
        Banco.GetCollection<BsonDocument>("usuarios").UpdateManyAsync(
            Builders<BsonDocument>.Filter.In("_id", usuarioIds),
            Builders<BsonDocument>.Update.Set("ultimoAcesso", quando));

    // quem nunca entrou: ultimoAcesso nulo (17 RN-280, conta em "Sem acesso há 30 dias")
    public static Task UltimoAcessoNulo(string usuarioId) =>
        Banco.GetCollection<BsonDocument>("usuarios").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", usuarioId),
            Builders<BsonDocument>.Update.Set("ultimoAcesso", BsonNull.Value));

    // usuários criados há algum tempo ("ATIVO desde 2026-09-01"): "Sem acesso
    // há 30 dias" só conta quem existe há mais de 30 dias (17 RN-280)
    public static Task CriadoEm(IEnumerable<string> usuarioIds, DateTime quando) =>
        Banco.GetCollection<BsonDocument>("usuarios").UpdateManyAsync(
            Builders<BsonDocument>.Filter.In("_id", usuarioIds),
            Builders<BsonDocument>.Update.Set("criadoEm", quando));

    // master que nunca entrou: ultimoAcessoMaster nulo (17 RN-280)
    public static Task UltimoAcessoMasterNulo(string contaId) =>
        Banco.GetCollection<BsonDocument>("contas").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", contaId),
            Builders<BsonDocument>.Update.Set("ultimoAcessoMaster", BsonNull.Value));

    // conta criada há algum tempo ("Master sem acesso há 30 dias", 17 RN-280)
    public static Task CriadaEm(string contaId, DateTime quando) =>
        Banco.GetCollection<BsonDocument>("contas").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", contaId),
            Builders<BsonDocument>.Update.Set("criadaEm", quando));

    // último acesso da conta master (17 RN-280, "Master sem acesso há 30 dias")
    public static Task UltimoAcessoMaster(string contaId, DateTime quando) =>
        Banco.GetCollection<BsonDocument>("contas").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", contaId),
            Builders<BsonDocument>.Update.Set("ultimoAcessoMaster", quando));

    // meses anteriores do card guardado (17 RN-280, 15 RN-278): o histórico só
    // nasce com o tempo. Entram antes do mês atual, que o backend já gravou;
    // sem o card guardado, nada muda (o cenário falha pela resposta)
    public static Task MesesAnterioresDoCard(string contaId, string perfil, IReadOnlyList<int> valores)
    {
        var mesAtual = DateTime.UtcNow.AddHours(-3);
        var meses = new BsonArray(valores.Select((valor, i) => new BsonDocument
        {
            ["mes"] = mesAtual.AddMonths(i - valores.Count).ToString("yyyy-MM"),
            ["valor"] = valor,
        }));
        return Banco.GetCollection<BsonDocument>("cardUsuarios").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("contaId", contaId) & Builders<BsonDocument>.Filter.Eq("perfil", perfil)
                & Builders<BsonDocument>.Filter.Eq("usuarioId", BsonNull.Value),
            Builders<BsonDocument>.Update.PushEach("meses", meses, position: 0));
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
