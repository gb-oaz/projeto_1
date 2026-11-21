using System.Collections.Concurrent;
using System.Text.Json;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// A coleção ocorrencias (auditoria-documentos v02): uma por mudança de estado, guardada 31 dias (RNF-248). Com o banco fora do
// ar, a ocorrência fica pendente na memória e entra na volta, com a hora em que aconteceu (RNF-252).
public sealed class OcorrenciasNoBanco(Armazenamento armazenamento) : IOcorrencias
{
    readonly ConcurrentQueue<Ocorrencia> pendentes = new();

    public async Task Gravar(Ocorrencia ocorrencia, CancellationToken ct = default)
    {
        pendentes.Enqueue(ocorrencia);
        try
        {
            while (pendentes.TryPeek(out var proxima))
            {
                await armazenamento.Colecao(Colecoes.Ocorrencias).InsertOneAsync(Documento(proxima), cancellationToken: ct);
                pendentes.TryDequeue(out _);
            }
        }
        catch (Exception erro) when (erro is MongoException or TimeoutException)
        {
            // o banco fora: a ocorrência fica pendente e a próxima verificação tenta de novo
        }
    }

    static BsonDocument Documento(Ocorrencia o) => new()
    {
        ["_id"] = o.Id,
        ["tipo"] = o.Tipo.Texto(),
        ["alvo"] = o.Alvo,
        ["nome"] = o.Nome,
        ["de"] = o.De.Texto(),
        ["para"] = o.Para.Texto(),
        ["inicio"] = BancoDaAuditoria.Data(o.Inicio),
        ["quando"] = BancoDaAuditoria.Data(o.Quando),
        ["falha"] = o.Falha is { } f ? f.Texto() : BsonNull.Value,
        ["duracaoMin"] = o.DuracaoMin is { } d ? d : BsonNull.Value,
        ["guardarAte"] = BancoDaAuditoria.Data(o.GuardarAte),
    };

    static Ocorrencia DoDocumento(BsonDocument d) => new(
        d["_id"].ToString()!, TextosDaOcorrencia.TipoDe(d.Texto("tipo")) ?? TipoDoAlvo.Servico, d.Texto("alvo") ?? "", d.Texto("nome") ?? "",
        TextosDaOcorrencia.EstadoDe(d.Texto("de")) ?? EstadoDeOcorrencia.NoAr, TextosDaOcorrencia.EstadoDe(d.Texto("para")) ?? EstadoDeOcorrencia.NoAr,
        d.Data("inicio") ?? DateTimeOffset.UnixEpoch, d.Data("quando") ?? DateTimeOffset.UnixEpoch, TextosDaSaude.FalhaDe(d.Texto("falha")),
        d.TryGetValue("duracaoMin", out var m) && m.IsNumeric ? m.ToInt32() : null);

    public Task<PaginaDeOcorrencias> Listar(
        Periodo periodo, TipoDoAlvo? tipo, string? alvo, EstadoDeOcorrencia? para, Cursor? depois, int limite, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo(async () =>
        {
            var filtro = Bson.Filtro.Gte("quando", BancoDaAuditoria.Data(periodo.De)) & Bson.Filtro.Lte("quando", BancoDaAuditoria.Data(periodo.Ate));
            if (tipo is { } t) filtro &= Bson.Filtro.Eq("tipo", t.Texto());
            if (alvo is not null) filtro &= Bson.Filtro.Eq("alvo", alvo);
            if (para is { } p) filtro &= Bson.Filtro.Eq("para", p.Texto());
            if (depois is { } c)
            {
                var quando = BancoDaAuditoria.Data(c.Quando);
                filtro &= Bson.Filtro.Lt("quando", quando) | (Bson.Filtro.Eq("quando", quando) & Bson.Filtro.Lt("_id", c.Id));
            }
            var lidas = await armazenamento.Colecao(Colecoes.Ocorrencias).Find(filtro)
                .Sort(Builders<BsonDocument>.Sort.Descending("quando").Descending("_id")).Limit(limite + 1).ToListAsync(ct);
            var itens = lidas.Take(limite).Select(DoDocumento).ToList();
            var proximo = lidas.Count > limite ? new Cursor(itens[^1].Quando, itens[^1].Id) : (Cursor?)null;
            return new PaginaDeOcorrencias(itens, proximo);
        });
}

// As ocorrências abertas e as voltas recentes no Redis (auditoria-documentos v02): o hash auditoria:ocorrencias:abertas (alvo →
// a última mudança fora de No ar) e o conjunto ordenado auditoria:ocorrencias:voltaram (as voltas a No ar, pela hora, dos
// últimos 10 min). A faixa abre sem o banco (RNF-252), e a verificação parte do que está aberto.
public sealed class OcorrenciasAbertasNoRedis(Armazenamento armazenamento) : IOcorrenciasAbertas
{
    static readonly RedisKey ChaveAbertas = "auditoria:ocorrencias:abertas";
    static readonly RedisKey ChaveVoltaram = "auditoria:ocorrencias:voltaram";
    static readonly TimeSpan JanelaDasVoltas = TimeSpan.FromMinutes(Ocorrencia.MinutosDasVoltas);

    public async Task Mudar(Ocorrencia ocorrencia, CancellationToken ct = default)
    {
        var cache = armazenamento.Cache;
        var json = JsonSerializer.Serialize(ocorrencia);
        if (ocorrencia.Para != EstadoDeOcorrencia.NoAr)
        {
            await cache.HashSetAsync(ChaveAbertas, ocorrencia.Alvo, json);
            return;
        }
        await cache.HashDeleteAsync(ChaveAbertas, ocorrencia.Alvo);
        await cache.SortedSetAddAsync(ChaveVoltaram, json, ocorrencia.Quando.ToUnixTimeMilliseconds());
        await cache.SortedSetRemoveRangeByScoreAsync(ChaveVoltaram, double.NegativeInfinity, (ocorrencia.Quando - JanelaDasVoltas).ToUnixTimeMilliseconds());
    }

    public async Task<IReadOnlyList<Ocorrencia>> Abertas(CancellationToken ct = default) =>
        [.. (await armazenamento.Cache.HashValuesAsync(ChaveAbertas)).Select(v => Ler(v.ToString())).OfType<Ocorrencia>()];

    public async Task<IReadOnlyList<Ocorrencia>> Voltaram(DateTimeOffset agora, CancellationToken ct = default) =>
        [.. (await armazenamento.Cache.SortedSetRangeByScoreAsync(ChaveVoltaram, (agora - JanelaDasVoltas).ToUnixTimeMilliseconds(), double.PositiveInfinity))
            .Select(v => Ler(v.ToString())).OfType<Ocorrencia>()];

    static Ocorrencia? Ler(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Ocorrencia>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
