using System.Globalization;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// As leituras da Auditoria dependem do banco: com ele fora, falham com o 503 do contrato (RNF-252), e não com um erro do driver.
static class BancoDaAuditoria
{
    public static async Task<T> Lendo<T>(Func<Task<T>> leitura)
    {
        try
        {
            return await leitura();
        }
        catch (Exception erro) when (erro is MongoException or TimeoutException)
        {
            throw FalhaDaAuditoria.BancoDeDadosFora();
        }
    }

    public static string Id() => Guid.NewGuid().ToString("N");

    public static BsonDateTime Data(DateTimeOffset data) => new(data.UtcDateTime);
}

// verificacoesSaude e quedasServico (auditoria-documentos v02), guardadas 31 dias (RNF-248): cada verificação por serviço e
// cada queda, aberta ao ficar Fora do ar e fechada na volta. O histórico de um serviço sai daqui.
public sealed class HistoricoDaSaudeNoBanco(Armazenamento armazenamento) : IHistoricoDaSaude
{
    const int DiasDeGuarda = 31;

    public Task Gravar(IReadOnlyList<Verificacao> verificacoes, CancellationToken ct = default) =>
        verificacoes.Count == 0
            ? Task.CompletedTask
            : armazenamento.Colecao(Colecoes.VerificacoesSaude).InsertManyAsync(verificacoes.Select(v => new BsonDocument
            {
                ["_id"] = BancoDaAuditoria.Id(),
                ["servico"] = v.Servico.Id,
                ["quando"] = BancoDaAuditoria.Data(v.Quando),
                ["instancia"] = v.Instancia,
                ["estado"] = v.Estado.Texto(),
                ["tempoMs"] = v.TempoMs is { } t ? t : BsonNull.Value,
                ["ultimaExecucao"] = Bson.Valor(v.UltimaExecucao),
                ["falha"] = v.Falha is { } f ? f.Texto() : BsonNull.Value,
                ["guardarAte"] = BancoDaAuditoria.Data(v.Quando.AddDays(DiasDeGuarda)),
            }), cancellationToken: ct);

    public Task AbrirQueda(Queda queda, CancellationToken ct = default) =>
        armazenamento.Colecao(Colecoes.QuedasServico).InsertOneAsync(new BsonDocument
        {
            ["_id"] = queda.Id,
            ["servico"] = queda.Servico.Id,
            ["inicio"] = BancoDaAuditoria.Data(queda.Inicio),
            ["fim"] = BsonNull.Value,
            ["falha"] = queda.Falha.Texto(),
            ["guardarAte"] = BancoDaAuditoria.Data(queda.GuardarAte),
        }, cancellationToken: ct);

    public Task FecharQueda(Servico servico, DateTimeOffset fim, CancellationToken ct = default) =>
        armazenamento.Colecao(Colecoes.QuedasServico).UpdateManyAsync(
            Bson.Filtro.Eq("servico", servico.Id) & Bson.Filtro.Eq("fim", BsonNull.Value), Bson.Mudar.Set("fim", BancoDaAuditoria.Data(fim)),
            cancellationToken: ct);

    public Task<IReadOnlyList<Queda>> Quedas(Servico servico, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<Queda>>(async () =>
        {
            var filtro = Bson.Filtro.Eq("servico", servico.Id) & Bson.Filtro.Lte("inicio", BancoDaAuditoria.Data(ate))
                & (Bson.Filtro.Eq("fim", BsonNull.Value) | Bson.Filtro.Gte("fim", BancoDaAuditoria.Data(de)));
            var lidas = await armazenamento.Colecao(Colecoes.QuedasServico).Find(filtro).SortByDescending(d => d["inicio"]).ToListAsync(ct);
            return [.. lidas.Select(d => new Queda(d["_id"].ToString()!, servico, d.Data("inicio")!.Value, d.Data("fim"),
                TextosDaSaude.FalhaDe(d.Texto("falha")) ?? FalhaDaVerificacao.SemResposta))];
        });

    // o tempo médio e as falhas de cada passo, somados no banco: o índice do passo é (quando − de) ÷ passo
    public Task<IReadOnlyList<PontoDoHistorico>> Serie(Servico servico, DateTimeOffset de, TimeSpan passo, int pontos, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<PontoDoHistorico>>(async () =>
        {
            var ate = de + (passo * pontos);
            var passoMs = (long)passo.TotalMilliseconds;
            var etapas = new[]
            {
                new BsonDocument("$match", new BsonDocument { ["servico"] = servico.Id, ["quando"] = new BsonDocument { ["$gte"] = BancoDaAuditoria.Data(de), ["$lt"] = BancoDaAuditoria.Data(ate) } }),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = new BsonDocument("$floor", new BsonDocument("$divide", new BsonArray
                    {
                        new BsonDocument("$subtract", new BsonArray { "$quando", BancoDaAuditoria.Data(de) }), passoMs,
                    })),
                    ["tempo"] = new BsonDocument("$avg", "$tempoMs"),
                    ["falhas"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$ne", new BsonArray { "$falha", BsonNull.Value }), 1, 0,
                    })),
                }),
            };
            var grupos = await armazenamento.Colecao(Colecoes.VerificacoesSaude).Aggregate<BsonDocument>(etapas, cancellationToken: ct).ToListAsync(ct);
            var porPasso = grupos.ToDictionary(g => (int)g["_id"].ToInt64(), g => g);
            return [.. Enumerable.Range(0, pontos).Select(i =>
            {
                var grupo = porPasso.GetValueOrDefault(i);
                int? media = grupo is not null && grupo.TryGetValue("tempo", out var t) && t.IsNumeric
                    ? (int)Math.Round(t.ToDouble(), MidpointRounding.AwayFromZero)
                    : null;
                return new PontoDoHistorico(de + (passo * i), media, grupo?.Inteiro("falhas") ?? 0);
            })];
        });
}

// medicoesMinuto e medicoesHora (auditoria-documentos v02): cada instância soma o seu minuto e a sua hora com $inc e upsert; o _id
// é "<minuto>|<método> <modelo da rota>". As leituras somam no banco, sem trazer cada hora à aplicação (RNF-250).
public sealed class MedicoesNoBanco(Armazenamento armazenamento) : IMedicoes
{
    const int DiasDoMinuto = 2, DiasDaHora = 90;

    public async Task Somar(IReadOnlyList<Medicao> medicoes, CancellationToken ct = default)
    {
        if (medicoes.Count == 0) return;
        var minutos = medicoes.Select(m => Atualizacao(m, "minuto", m.Inicio, "yyyy-MM-dd'T'HH:mm", DiasDoMinuto)).ToList();
        var horas = medicoes.Select(m => Atualizacao(m, "hora", Janelas.Hora(m.Inicio), "yyyy-MM-dd'T'HH", DiasDaHora)).ToList();
        await armazenamento.Colecao(Colecoes.MedicoesMinuto).BulkWriteAsync(minutos, new BulkWriteOptions { IsOrdered = false }, ct);
        await armazenamento.Colecao(Colecoes.MedicoesHora).BulkWriteAsync(horas, new BulkWriteOptions { IsOrdered = false }, ct);
    }

    static UpdateOneModel<BsonDocument> Atualizacao(Medicao m, string campo, DateTimeOffset inicio, string formato, int dias)
    {
        var inc = new BsonDocument
        {
            ["pedidos"] = m.Pedidos,
            ["ok"] = m.Ok,
            ["erros4xx"] = m.Erros4xx,
            ["erros5xx"] = m.Erros5xx,
        };
        var tempos = m.Tempos.Contagens;
        for (var i = 0; i < tempos.Count; i++)
        {
            if (tempos[i] > 0) inc[((FaixaDeTempo)i).Campo()] = tempos[i];
        }
        var id = $"{inicio.ToString(formato, CultureInfo.InvariantCulture)}|{m.Metodo} {m.Endereco}";
        return new UpdateOneModel<BsonDocument>(Bson.Filtro.Eq("_id", id), new BsonDocument
        {
            ["$inc"] = inc,
            ["$setOnInsert"] = new BsonDocument
            {
                [campo] = BancoDaAuditoria.Data(inicio),
                ["metodo"] = m.Metodo,
                ["endereco"] = m.Endereco,
                ["modulo"] = Bson.Valor(m.Modulo),
                ["guardarAte"] = BancoDaAuditoria.Data(inicio.AddDays(dias)),
            },
        })
        { IsUpsert = true };
    }

    static (string Colecao, string Campo) De(Janela janela) => janela.PorHora() ? (Colecoes.MedicoesHora, "hora") : (Colecoes.MedicoesMinuto, "minuto");

    // 1h: os minutos de de até ate (exclusive; a série para aí, com os 60 pontos); as outras: as horas de de até ate, a atual inclusive.
    // Os totais da janela (resumo e endereços) incluem também o minuto de agora, para o pedido recém-feito já contar.
    static BsonDocument Periodo(string campo, bool porHora, DateTimeOffset de, DateTimeOffset ate, string? modulo, bool comAtual = false)
    {
        var filtro = new BsonDocument(campo, new BsonDocument
        {
            ["$gte"] = BancoDaAuditoria.Data(de),
            [porHora ? "$lte" : "$lt"] = BancoDaAuditoria.Data(comAtual && !porHora ? ate.AddMinutes(1) : ate),
        });
        if (modulo is not null) filtro["modulo"] = modulo;
        return filtro;
    }

    static BsonDocument Soma()
    {
        var soma = new BsonDocument
        {
            ["pedidos"] = new BsonDocument("$sum", "$pedidos"),
            ["erros4xx"] = new BsonDocument("$sum", "$erros4xx"),
            ["erros5xx"] = new BsonDocument("$sum", "$erros5xx"),
        };
        foreach (var faixa in FaixasDeTempo.Campos) soma[faixa] = new BsonDocument("$sum", $"${faixa}");
        return soma;
    }

    static ResumoDeMedicoes Resumo(BsonDocument d) =>
        new(d.Longo("pedidos"), d.Longo("erros4xx"), d.Longo("erros5xx"), new Histograma([.. FaixasDeTempo.Campos.Select(d.Longo)]));

    Task<List<BsonDocument>> Agrupar(string colecao, BsonDocument filtro, BsonValue id, CancellationToken ct, bool comModulo = false)
    {
        var soma = Soma();
        soma.InsertAt(0, new BsonElement("_id", id));
        if (comModulo) soma["modulo"] = new BsonDocument("$first", "$modulo");
        return armazenamento.Colecao(colecao)
            .Aggregate<BsonDocument>(new[] { new BsonDocument("$match", filtro), new BsonDocument("$group", soma) }, new AggregateOptions { AllowDiskUse = true },
                ct)
            .ToListAsync(ct);
    }

    public Task<IReadOnlyDictionary<string, ResumoDeMedicoes>> PorModulo(DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyDictionary<string, ResumoDeMedicoes>>(async () =>
        {
            var filtro = Periodo("minuto", false, de, ate.AddMinutes(1), null);
            filtro["modulo"] = new BsonDocument("$ne", BsonNull.Value);
            var grupos = await Agrupar(Colecoes.MedicoesMinuto, filtro, "$modulo", ct);
            return grupos.ToDictionary(g => g["_id"].AsString, Resumo);
        });

    public Task<ResumoDeMedicoes> Resumo(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo(async () =>
        {
            var (colecao, campo) = De(janela);
            var grupos = await Agrupar(colecao, Periodo(campo, janela.PorHora(), de, ate, modulo, comAtual: true), BsonNull.Value, ct);
            return grupos.Count == 0 ? ResumoDeMedicoes.Vazio : Resumo(grupos[0]);
        });

    public Task<IReadOnlyList<PontoDaMedicao>> Serie(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<PontoDaMedicao>>(async () =>
        {
            var (colecao, campo) = De(janela);
            var indice = new BsonDocument("$floor", new BsonDocument("$divide", new BsonArray
            {
                new BsonDocument("$subtract", new BsonArray { $"${campo}", BancoDaAuditoria.Data(de) }), (long)janela.Passo().TotalMilliseconds,
            }));
            var grupos = await Agrupar(colecao, Periodo(campo, janela.PorHora(), de, ate, modulo), indice, ct);
            return [.. grupos.Select(g => new PontoDaMedicao(de + (janela.Passo() * g["_id"].ToInt64()), Resumo(g)))];
        });

    public Task<IReadOnlyList<MedicaoDoEndereco>> PorEndereco(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default) =>
        BancoDaAuditoria.Lendo<IReadOnlyList<MedicaoDoEndereco>>(async () =>
        {
            var (colecao, campo) = De(janela);
            var chave = new BsonDocument { ["metodo"] = "$metodo", ["endereco"] = "$endereco" };
            var grupos = await Agrupar(colecao, Periodo(campo, janela.PorHora(), de, ate, modulo, comAtual: true), chave, ct, comModulo: true);
            return [.. grupos.Select(g => new MedicaoDoEndereco(g["_id"]["metodo"].AsString, g["_id"]["endereco"].AsString, g.Texto("modulo"), Resumo(g)))];
        });
}
