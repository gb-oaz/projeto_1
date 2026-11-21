using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Ports.Out;
using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// A leitura das coleções auditoria e auditoriaErros (RF-199 a RF-203): só leitura, nada as altera nem apaga (RN-320, 17 RNF-206).
// Os registros das duas vêm juntos pelos índices por hora (quando↓) que a Auditoria cria na partida (RNF-251); a lista de
// execuções agrupa por traceId no banco, só com as pontas de cada uma.
public sealed class LeituraDaAuditoriaNoBanco(Armazenamento armazenamento) : ILeituraDaAuditoria
{
    static readonly SortDefinition<BsonDocument> MaisNovoPrimeiro = Builders<BsonDocument>.Sort.Descending("quando").Descending("_id");

    IMongoCollection<BsonDocument> DaAuditoria => armazenamento.Colecao(Colecoes.Auditoria);

    IMongoCollection<BsonDocument> Erros => armazenamento.Colecao(Colecoes.AuditoriaErros);

    public Task<IReadOnlyList<RegistroBruto>> Registros(ConsultaDeRegistros consulta, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<RegistroBruto>>(async () =>
        {
            var f = consulta.Filtro;
            // o nível e o código só existem nos erros
            var daAuditoria = f.Tipo != TipoDoRegistro.Erro && f.Nivel is null && f.Codigo is null;
            var dosErros = f.Tipo != TipoDoRegistro.Auditoria;
            var lidos = new List<RegistroBruto>();
            if (daAuditoria) lidos.AddRange(await Ler(DaAuditoria, TipoDoRegistro.Auditoria, Filtro(consulta, null), consulta.Limite + 1, ct));
            if (dosErros)
            {
                lidos.AddRange(await Ler(Erros, TipoDoRegistro.Erro, Filtro(consulta, f), consulta.Limite + 1, ct));
            }
            return [.. lidos.OrderByDescending(r => r.Quando).ThenByDescending(r => r.Id, StringComparer.Ordinal).Take(consulta.Limite + 1)];
        });

    // o filtro do período, da página e dos campos: erros pega também o nível e o código
    static FilterDefinition<BsonDocument> Filtro(ConsultaDeRegistros c, FiltroDosRegistros? erros)
    {
        var filtro = Bson.Filtro.Empty;
        if (c.Filtro.TraceId is { } trace) filtro &= Bson.Filtro.Eq("traceId", trace);
        if (c.Periodo is { } p) filtro &= Bson.Filtro.Gte("quando", BancoDaAuditoria.Data(p.De)) & Bson.Filtro.Lte("quando", BancoDaAuditoria.Data(p.Ate));
        if (c.Acoes is { } acoes) filtro &= Bson.Filtro.In("acao", acoes);
        if (c.ContaId is { } conta) filtro &= Bson.Filtro.Eq("contaId", conta);
        if (c.Filtro.Quem is { } quem) filtro &= Bson.Filtro.Eq("quem", quem);
        if (erros?.Nivel is { } nivel) filtro &= Bson.Filtro.Eq("nivel", nivel);
        if (erros?.Codigo is { } codigo) filtro &= Bson.Filtro.Eq("codigo", codigo);
        if (c.Depois is { } d)
        {
            var quando = BancoDaAuditoria.Data(d.Quando);
            filtro &= Bson.Filtro.Lt("quando", quando) | (Bson.Filtro.Eq("quando", quando) & Bson.Filtro.Lt("_id", d.Id));
        }
        return filtro;
    }

    static async Task<List<RegistroBruto>> Ler(IMongoCollection<BsonDocument> colecao, TipoDoRegistro tipo, FilterDefinition<BsonDocument> filtro, int limite, CancellationToken ct) =>
        [.. (await colecao.Find(filtro).Sort(MaisNovoPrimeiro).Limit(limite).ToListAsync(ct)).Select(d => Registro(d, tipo))];

    static RegistroBruto Registro(BsonDocument d, TipoDoRegistro tipo) => new(
        d["_id"].ToString()!, tipo, d.Texto("traceId") ?? "", tipo == TipoDoRegistro.Auditoria ? d.Texto("etapa") : null, tipo == TipoDoRegistro.Erro ? d.Texto("nivel") : null,
        tipo == TipoDoRegistro.Erro ? d.Texto("codigo") : null, d.Texto("acao") ?? "", d.Texto("quem"), d.Texto("contaId"), d.Texto("alvo"),
        d.Data("quando") ?? DateTimeOffset.UnixEpoch);

    public Task<IReadOnlyList<RegistroBruto>> DoTrace(string traceId, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<RegistroBruto>>(async () =>
        {
            var filtro = Bson.Filtro.Eq("traceId", traceId);
            var lidos = (await Ler(DaAuditoria, TipoDoRegistro.Auditoria, filtro, int.MaxValue, ct)).Concat(await Ler(Erros, TipoDoRegistro.Erro, filtro, int.MaxValue, ct));
            // em ordem de quando; na mesma hora, o INICIADO antes do CONCLUIDO e o erro depois deles
            return [.. lidos.OrderBy(r => r.Quando).ThenBy(r => r.Etapa == "INICIADO" ? 0 : r.Etapa == "CONCLUIDO" ? 1 : 2).ThenBy(r => r.Id, StringComparer.Ordinal)];
        });

    // uma execução por traceId: a 1ª etapa da auditoria (o INICIADO da ação principal), a última, e o último erro; juntos pelo traceId
    public Task<IReadOnlyList<PontasDaExecucao>> Execucoes(Periodo periodo, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<PontasDaExecucao>>(async () =>
        {
            var intervalo = new BsonDocument("quando", new BsonDocument { ["$gte"] = BancoDaAuditoria.Data(periodo.De), ["$lte"] = BancoDaAuditoria.Data(periodo.Ate) });
            var opcoes = new AggregateOptions { AllowDiskUse = true };
            // INICIADO antes de CONCLUIDO na mesma hora: etapa em ordem decrescente
            var ordem = new BsonDocument("$sort", new BsonDocument { ["quando"] = 1, ["etapa"] = -1 });
            var registros = await DaAuditoria.Aggregate<BsonDocument>(new[]
            {
                new BsonDocument("$match", intervalo),
                ordem,
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$traceId",
                    ["primeiro"] = new BsonDocument("$first", new BsonDocument
                    {
                        ["etapa"] = "$etapa", ["acao"] = "$acao", ["quem"] = "$quem", ["contaId"] = "$contaId", ["alvo"] = "$alvo", ["quando"] = "$quando",
                    }),
                    ["ultimo"] = new BsonDocument("$last", new BsonDocument { ["etapa"] = "$etapa", ["acao"] = "$acao", ["quando"] = "$quando" }),
                }),
            }, opcoes, ct).ToListAsync(ct);
            var erros = (await Erros.Aggregate<BsonDocument>(new[]
            {
                new BsonDocument("$match", intervalo),
                new BsonDocument("$sort", new BsonDocument("quando", 1)),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$traceId",
                    ["nivel"] = new BsonDocument("$last", "$nivel"),
                    ["codigo"] = new BsonDocument("$last", "$codigo"),
                    ["quando"] = new BsonDocument("$last", "$quando"),
                }),
            }, opcoes, ct).ToListAsync(ct)).ToDictionary(e => e["_id"].AsString);

            // a execução que começou antes do período fica de fora: o que se vê dela não tem o INICIADO
            return [.. registros.Where(r => r["primeiro"].AsBsonDocument.Texto("etapa") == "INICIADO").Select(r =>
            {
                var primeiro = r["primeiro"].AsBsonDocument;
                var ultimo = r["ultimo"].AsBsonDocument;
                var erro = erros.GetValueOrDefault(r["_id"].AsString);
                return new PontasDaExecucao(r["_id"].AsString, primeiro.Texto("acao") ?? "", primeiro.Texto("quem"), primeiro.Texto("contaId"), primeiro.Texto("alvo"),
                    primeiro.Data("quando") ?? DateTimeOffset.UnixEpoch, ultimo.Texto("etapa"), ultimo.Texto("acao"), ultimo.Data("quando"), erro?.Texto("nivel"),
                    erro?.Texto("codigo"), erro?.Data("quando"));
            })];
        });

    public Task<(int Erros, int Alertas)> ContagemDeErros(DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo(async () =>
        {
            var filtro = Bson.Filtro.Gte("quando", BancoDaAuditoria.Data(de)) & Bson.Filtro.Lte("quando", BancoDaAuditoria.Data(ate));
            var erros = await Erros.CountDocumentsAsync(filtro & Bson.Filtro.Eq("nivel", "ERRO"), cancellationToken: ct);
            var alertas = await Erros.CountDocumentsAsync(filtro & Bson.Filtro.Eq("nivel", "ALERTA"), cancellationToken: ct);
            return ((int)erros, (int)alertas);
        });
}

// Os nomes de quem fez e a sigla das contas, lidos de usuarios e contas em lote (auditoria-documentos v02).
public sealed class NomesDaAuditoriaNoBanco(Armazenamento armazenamento) : INomesDaAuditoria
{
    public Task<IReadOnlyDictionary<string, string>> Usuarios(IEnumerable<string> ids, CancellationToken ct = default) =>
        Lidos(Colecoes.Usuarios, "nome", ids, ct);

    public Task<IReadOnlyDictionary<string, string>> Contas(IEnumerable<string> ids, CancellationToken ct = default) =>
        Lidos(Colecoes.Contas, "sigla", ids, ct);

    Task<IReadOnlyDictionary<string, string>> Lidos(string colecao, string campo, IEnumerable<string> ids, CancellationToken ct) =>
        BancoDaAuditoria.Lendo<IReadOnlyDictionary<string, string>>(async () =>
        {
            var lista = ids.ToList();
            if (lista.Count == 0) return new Dictionary<string, string>();
            var lidos = await armazenamento.Colecao(colecao).Find(Bson.Filtro.In("_id", lista))
                .Project(Builders<BsonDocument>.Projection.Include(campo)).ToListAsync(ct);
            return lidos.Where(d => d.Texto(campo) is not null).ToDictionary(d => d["_id"].ToString()!, d => d.Texto(campo)!);
        });

    public Task<string?> IdDaConta(string sigla, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo(async () =>
        {
            var conta = await armazenamento.Colecao(Colecoes.Contas).Find(Bson.Filtro.Eq("sigla", sigla.ToUpperInvariant()))
                .Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefaultAsync(ct);
            return conta?["_id"].ToString();
        });
}

// cardAuditoria (RN-322): um documento por dia de Brasília, "deboaz:2026-11-20", fechado a cada minuto com os números do dia;
// a versão sobe só quando eles mudam. Guardado 400 dias (15 RN-278).
public sealed class CardDaAuditoriaNoBanco(Armazenamento armazenamento) : ICardDaAuditoria
{
    IMongoCollection<BsonDocument> Colecao => armazenamento.Colecao(Colecoes.CardAuditoria);

    public async Task<DiaDoCard> Fechar(DiaDoCard dia, CancellationToken ct = default)
    {
        var guardado = await Colecao.Find(Bson.Filtro.Eq("_id", dia.Id)).FirstOrDefaultAsync(ct);
        var anterior = guardado is null ? null : DoDocumento(guardado);
        if (anterior is not null && !dia.MudouDe(anterior)) return anterior;
        var novo = dia with { Versao = (anterior?.Versao ?? 0) + 1 };
        await Colecao.ReplaceOneAsync(Bson.Filtro.Eq("_id", dia.Id), new BsonDocument
        {
            ["_id"] = novo.Id,
            ["dia"] = CalendarioDoCard.ChaveDoDia(novo.Dia),
            ["erros"] = novo.Erros,
            ["alertas"] = novo.Alertas,
            ["interrompidas"] = novo.Interrompidas,
            ["versao"] = novo.Versao,
            ["guardarAte"] = BancoDaAuditoria.Data(novo.GuardarAte),
        }, new ReplaceOptions { IsUpsert = true }, ct);
        return novo;
    }

    public Task<IReadOnlyList<DiaDoCard>> Dias(DateOnly desde, DateOnly ate, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<DiaDoCard>>(async () =>
        {
            var filtro = Bson.Filtro.Gte("dia", CalendarioDoCard.ChaveDoDia(desde)) & Bson.Filtro.Lte("dia", CalendarioDoCard.ChaveDoDia(ate));
            return [.. (await Colecao.Find(filtro).ToListAsync(ct)).Select(DoDocumento)];
        });

    static DiaDoCard DoDocumento(BsonDocument d) =>
        new(DateOnly.ParseExact(d.Texto("dia")!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), d.Inteiro("erros"), d.Inteiro("alertas"),
            d.Inteiro("interrompidas"), d.Longo("versao"));
}

// O descarte pela guarda (RNF-248): apaga o que passou do guardarAte nas 6 coleções da Auditoria, de hora em hora pelo agendamento,
// e não pelo TTL do banco, para valer no MongoDB e no Cosmos DB (RU). auditoria e auditoriaErros nunca são apagadas.
public sealed class DescarteDaAuditoriaNoBanco(Armazenamento armazenamento) : IDescarteDaAuditoria
{
    static readonly string[] AsDaGuarda =
    [
        Colecoes.VerificacoesSaude, Colecoes.QuedasServico, Colecoes.MedicoesMinuto, Colecoes.MedicoesHora, Colecoes.CardAuditoria, Colecoes.Ocorrencias,
    ];

    public async Task ApagarVencidos(DateTimeOffset agora, CancellationToken ct = default)
    {
        foreach (var colecao in AsDaGuarda)
        {
            await armazenamento.Colecao(colecao).DeleteManyAsync(Bson.Filtro.Lt("guardarAte", BancoDaAuditoria.Data(agora)), ct);
        }
    }
}
