using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.Qa.Integracao.Suporte;

// As coleções "auditoria" e "auditoriaErros" do banco do backend (MONGO_BANCO),
// para a RN-178, a RN-270, a RN-271 e a RN-274 de 17_modulo-usuario.md e a
// RN-272 e a RN-273 de 17_modulo-usuario-a-autenticacao.md.
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

    // os casos de uso internos (RN-275 do 17): chamados de dentro de outra
    // execução, gravam com o traceId dela. As frases que contam os registros
    // "da execução do pedido" (RN-178) os deixam de fora
    public static readonly HashSet<string> Internas = ["ENCERRAR_SESSOES_DO_USUARIO", "AVISAR_PERMISSOES_NOVAS", "AVISAR_CONTRATACAO_MUDOU"];

    public static bool Interna(BsonDocument d) => Internas.Contains(Texto(d, "acao") ?? "");

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

    // só os _id: a marca é tirada antes e depois de cada pedido auditado
    static async Task<HashSet<string>> Ids(string colecao) =>
        [.. (await Colecao(colecao).Find(FilterDefinition<BsonDocument>.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync()).Select(Id)];

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

    // o traceId do registro, que tem de vir preenchido
    public static string TraceId(BsonDocument d)
    {
        var traceId = Texto(d, "traceId");
        Assert.False(string.IsNullOrWhiteSpace(traceId), $"registro sem traceId: {d.ToJson()}");
        return traceId!;
    }

    public static string MesmoTraceId(IReadOnlyCollection<BsonDocument> registros)
    {
        var traceIds = registros.Select(TraceId).Distinct().ToList();
        Assert.True(traceIds.Count == 1, $"esperava um só traceId, vieram {string.Join(", ", traceIds)}");
        return traceIds[0];
    }

    public static string TraceIdDoCabecalho(Resposta r)
    {
        var traceId = r.Cabecalho("X-Trace-Id");
        Assert.False(string.IsNullOrWhiteSpace(traceId), $"a resposta veio sem o cabeçalho X-Trace-Id: {r}");
        return traceId!;
    }

    public static void GuardadoCincoAnos(BsonDocument d)
    {
        var quando = Data(d, "quando");
        var guardarAte = Data(d, "guardarAte");
        Assert.True(quando is { } q && guardarAte is { } g && (g - q.AddYears(5)).Duration() < TimeSpan.FromSeconds(1),
            $"esperava guardarAte 5 anos depois de quando, veio {d.ToJson()}");
    }
}

// As execuções auditadas do cenário, comuns aos passos de auditoria de todas as
// features (o Reqnroll cria uma por cenário e a injeta em quem a pede). Cada
// pedido auditado passa por Executar: o preparo vem antes, fora dele, e só o
// que apareceu nas coleções entre a marca de antes e a de depois conta como
// registro "ganho" por aquela execução.
public sealed class Execucoes(Mundo mundo)
{
    static readonly TimeSpan Folga = TimeSpan.FromMinutes(1);

    readonly List<(Auditoria.Marca Antes, Auditoria.Marca Depois)> marcas = [];

    // as respostas de cada execução, na ordem
    public List<Resposta> Resultados { get; } = [];
    // o registro de "auditoria" em conferência (o INICIADO, em geral) e o de
    // "auditoriaErros", que as frases seguintes citam como "esse registro"
    public BsonDocument? Registro { get; set; }
    public BsonDocument? Erro { get; set; }

    public async Task<Resposta> Executar(Func<Task<Resposta>> pedido)
    {
        var antes = await Auditoria.Marcar();
        var r = await pedido();
        Resultados.Add(r);
        marcas.Add((antes, await Auditoria.Marcar()));
        return r;
    }

    // os registros da última execução (até agora), ou da execução i (entre as
    // marcas de antes e de depois dela)
    public Task<List<BsonDocument>> Novos(string colecao) => Novos(colecao, marcas.Count - 1);

    public Task<List<BsonDocument>> Novos(string colecao, int i)
    {
        Assert.True(marcas.Count > i && i >= 0, "nenhum pedido auditado marcado neste cenário");
        return Auditoria.Novos(colecao, marcas[i].Antes, i + 1 < marcas.Count ? marcas[i].Depois : null);
    }

    // os registros de "auditoria" do próprio pedido, sem os dos casos de uso internos (RN-275)
    public async Task<List<BsonDocument>> Proprios() =>
        [.. (await Novos(Auditoria.Registros)).Where(d => !Auditoria.Interna(d))];

    public async Task<BsonDocument> UmNovoErro()
    {
        var erros = await Novos(Auditoria.Erros);
        Assert.True(erros.Count == 1, $"esperava 1 registro novo em \"auditoriaErros\", vieram {erros.Count}: {Auditoria.Resumo(erros)}");
        return erros[0];
    }

    // o registro da etapa e da ação entre os da última execução
    public async Task<BsonDocument> Etapa(string etapa, string acao)
    {
        var novos = await Novos(Auditoria.Registros);
        var d = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == etapa && Auditoria.Texto(d, "acao") == acao);
        Assert.True(d is not null, $"esperava o registro {etapa} de {acao} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        return d;
    }

    public void NaHoraDoPedido(BsonDocument d)
    {
        var quando = Auditoria.Data(d, "quando");
        Assert.True(quando is { } q && q >= mundo.HoraDoPedido - Folga && q <= DateTimeOffset.UtcNow + Folga,
            $"esperava a hora do pedido ({mundo.HoraDoPedido:O}), veio {d.ToJson()}");
    }
}
