using System.Globalization;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// A coleção faturas (pagamento-documentos v03) e a numeração em contadores.
// A fatura guarda a versão (+1 a cada gravação): Salvar só vale se a versão
// guardada ainda é a lida. "emAberto" é o pagamento em aberto que ocupa a
// fatura (RN-303): Ocupar e Liberar o mudam numa só operação, sem depender de
// índice; Salvar nunca mexe nele. A versão do card, as somas dos recebimentos
// e o recebido por dia saem de agregações no banco ($group), sem trazer fatura
// por fatura à aplicação (RNF-245).
public sealed class FaturasNoBanco(Armazenamento armazenamento) : IFaturas, INumeracaoDoPagamento
{
    const string Ocupacao = "emAberto";

    // o dia de Brasília (UTC-3, sem horário de verão), como o Calendario do core
    const string FusoDeBrasilia = "-03:00";

    IMongoCollection<BsonDocument> Faturas => armazenamento.Colecao(Colecoes.Faturas);

    public async Task<long> Proximo(string serie, CancellationToken ct = default)
    {
        var contador = await armazenamento.Colecao(Colecoes.Contadores).FindOneAndUpdateAsync(
            Bson.Filtro.Eq("_id", serie), Bson.Mudar.Inc("valor", 1L),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
        return contador.Longo("valor");
    }

    public async Task<Fatura?> Obter(string id, CancellationToken ct = default)
    {
        var d = await Faturas.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return d is null ? null : Documentos.FaturaDe(d);
    }

    public async Task<IReadOnlyList<Fatura>> Listar(FiltroDeFaturas filtro, CancellationToken ct = default)
    {
        var f = Bson.Filtro;
        var condicao = f.Empty;
        if (filtro.ContaId is { } conta) condicao &= f.Eq("conta.id", conta);
        if (filtro.Status.Count > 0) condicao &= f.In("status", filtro.Status.Select(s => s.Texto()));
        if (filtro.Competencia is { } competencia) condicao &= f.Eq("competencia", competencia.ToString());
        if (filtro.Busca is { } busca)
        {
            var contem = Bson.Contem(busca);
            condicao &= f.Regex("conta.sigla", contem) | f.Regex("conta.nome", contem) | f.Regex("_id", contem);
        }
        return [.. (await Faturas.Find(condicao).ToListAsync(ct)).Select(Documentos.FaturaDe)];
    }

    public async Task<IReadOnlyList<Fatura>> APagarVencidasAntesDe(DateOnly dia, CancellationToken ct = default) =>
        [.. (await Faturas.Find(Bson.Filtro.Eq("status", StatusDaFatura.APagar.Texto()) & Bson.Filtro.Lt("vencimento", Documentos.Dia(dia)))
            .ToListAsync(ct)).Select(Documentos.FaturaDe)];

    public Task<bool> Existe(string contaId, Competencia competencia, CancellationToken ct = default) =>
        Faturas.Find(Bson.Filtro.Eq("conta.id", contaId) & Bson.Filtro.Eq("competencia", competencia.ToString())).AnyAsync(ct);

    public async Task<bool> Incluir(Fatura fatura, CancellationToken ct = default)
    {
        var documento = Documentos.Fatura(fatura, fatura.Versao);
        documento[Ocupacao] = BsonNull.Value;
        try
        {
            await Faturas.InsertOneAsync(documento, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException erro) when (erro.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> Salvar(Fatura fatura, CancellationToken ct = default)
    {
        var documento = Documentos.Fatura(fatura, fatura.Versao + 1);
        documento.Remove("_id");
        var mudancas = Bson.Mudar.Combine(documento.Elements.Select(e => Bson.Mudar.Set(e.Name, e.Value)));
        var gravado = await Faturas.UpdateOneAsync(Bson.Filtro.Eq("_id", fatura.Id) & Bson.Filtro.Eq("versao", fatura.Versao), mudancas,
            cancellationToken: ct);
        if (gravado.MatchedCount == 0) return false;
        fatura.Versionar();
        return true;
    }

    public async Task<string?> Ocupar(string faturaId, string pagamentoId, CancellationToken ct = default)
    {
        var ocupada = await Faturas.UpdateOneAsync(Bson.Filtro.Eq("_id", faturaId) & Bson.Filtro.Eq(Ocupacao, BsonNull.Value),
            Bson.Mudar.Set(Ocupacao, pagamentoId), cancellationToken: ct);
        if (ocupada.ModifiedCount == 1) return null;
        var atual = await Faturas.Find(Bson.Filtro.Eq("_id", faturaId)).FirstOrDefaultAsync(ct);
        return atual?.Texto(Ocupacao) ?? string.Empty;
    }

    public Task Liberar(string faturaId, string pagamentoId, CancellationToken ct = default) =>
        Faturas.UpdateOneAsync(Bson.Filtro.Eq("_id", faturaId) & Bson.Filtro.Eq(Ocupacao, pagamentoId), Bson.Mudar.Set(Ocupacao, BsonNull.Value),
            cancellationToken: ct);

    public async Task<VersaoDasFaturas> Versao(string? contaId, CancellationToken ct = default)
    {
        var onde = contaId is null ? new BsonDocument() : new BsonDocument("conta.id", contaId);
        var grupo = new BsonDocument
        {
            ["_id"] = BsonNull.Value,
            ["versao"] = new BsonDocument("$sum", new BsonDocument("$add", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$versao", 0 }), 1 })),
            ["quantas"] = new BsonDocument("$sum", 1),
            ["vencidas"] = Contar(new BsonDocument("$eq", new BsonArray { "$status", StatusDaFatura.Vencida.Texto() })),
            ["emitida"] = new BsonDocument("$max", "$emitidaEm"),
            ["mudou"] = new BsonDocument("$max", new BsonDocument("$max", "$historico.em")),
        };
        var r = await Faturas.Aggregate<BsonDocument>(new BsonDocument[] { new("$match", onde), new("$group", grupo) }, cancellationToken: ct)
            .FirstOrDefaultAsync(ct);
        if (r is null) return new VersaoDasFaturas(0, 0, 0, null);
        DateTimeOffset? emitida = r.Data("emitida"), mudou = r.Data("mudou");
        var atualizadoEm = emitida is null ? mudou : mudou is null ? emitida : emitida > mudou ? emitida : mudou;
        return new VersaoDasFaturas(r.Longo("versao"), r.Inteiro("quantas"), r.Inteiro("vencidas"), atualizadoEm);
    }

    public async Task<IReadOnlyList<SomaDeFaturas>> Somas(IReadOnlyCollection<Competencia> competencias, CancellationToken ct = default)
    {
        if (competencias.Count == 0) return [];
        var etapas = new BsonDocument[]
        {
            new("$match", new BsonDocument("competencia", new BsonDocument("$in", new BsonArray(competencias.Select(c => c.ToString()))))),
            // a forma do pagamento que quitou (pelo _id, indexado), só para as pagas
            new("$lookup", new BsonDocument
            {
                ["from"] = Colecoes.Pagamentos,
                ["localField"] = "pagaPor",
                ["foreignField"] = "_id",
                ["as"] = "quitou",
            }),
            new("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument
                {
                    ["competencia"] = "$competencia",
                    ["status"] = "$status",
                    ["tipo"] = "$conta.tipo",
                    ["forma"] = new BsonDocument("$arrayElemAt", new BsonArray { "$quitou.forma", 0 }),
                },
                ["quantas"] = new BsonDocument("$sum", 1),
                ["valor"] = new BsonDocument("$sum", "$valor"),
                ["recebido"] = new BsonDocument("$sum", Recebido),
            }),
        };
        var lidas = await Faturas.Aggregate<BsonDocument>(etapas, cancellationToken: ct).ToListAsync(ct);
        return [.. lidas.Select(r =>
        {
            var chave = r.Documento("_id") ?? [];
            return new SomaDeFaturas(Competencia.Ler(chave.Texto("competencia")) ?? default, TextosDoPagamento.LerStatusDaFatura(chave.Texto("status")),
                chave.Texto("tipo") ?? "INSTITUICAO", TextosDoPagamento.LerForma(chave.Texto("forma")), r.Inteiro("quantas"), r.DinheiroEm("valor"),
                r.DinheiroEm("recebido"));
        })];
    }

    public async Task<IReadOnlyDictionary<DateOnly, Dinheiro>> RecebidoPorDia(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var etapas = new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                ["status"] = StatusDaFatura.Paga.Texto(),
                ["pagaEm"] = new BsonDocument
                {
                    ["$gte"] = Bson.Valor(Calendario.InicioDoDia(de)),
                    ["$lt"] = Bson.Valor(Calendario.InicioDoDia(ate.AddDays(1))),
                },
            }),
            new("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument("$dateToString", new BsonDocument
                {
                    ["format"] = "%Y-%m-%d",
                    ["date"] = "$pagaEm",
                    ["timezone"] = FusoDeBrasilia,
                }),
                ["recebido"] = new BsonDocument("$sum", Recebido),
            }),
        };
        var lidas = await Faturas.Aggregate<BsonDocument>(etapas, cancellationToken: ct).ToListAsync(ct);
        return lidas.ToDictionary(
            r => DateOnly.ParseExact(r["_id"].AsString, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            r => r.DinheiroEm("recebido"));
    }

    // o pago com juros menos o estornado (RN-308)
    static BsonDocument Recebido => new("$subtract", new BsonArray
    {
        new BsonDocument("$ifNull", new BsonArray { "$valorPago", 0 }),
        new BsonDocument("$ifNull", new BsonArray { "$devolvido", 0 }),
    });

    static BsonDocument Contar(BsonDocument condicao) => new("$sum", new BsonDocument("$cond", new BsonArray { condicao, 1, 0 }));
}

// O que o Pagamento lê de outros módulos, só leitura (pagamento-documentos v03):
// as cobranças fechadas do Catálogo (cobrancas), a conta (contas) com o
// documento, o nome de quem pagou e o login da conta master ou do autônomo
// (usuarios) e o nome dos módulos (modulos).
public sealed class LeiturasDoPagamentoNoBanco(Armazenamento armazenamento) : ICobrancasDoMes, ICadastroDoPagamento
{
    public async Task<IReadOnlyList<CobrancaFechada>> Fechadas(Competencia competencia, CancellationToken ct = default) =>
        [.. (await armazenamento.Colecao(Colecoes.Cobrancas).Find(Bson.Filtro.Eq("competencia", competencia.ToString())).ToListAsync(ct))
            .Select(d => new CobrancaFechada(d["_id"].ToString() ?? "", d.Texto("contaId") ?? "", d.Texto("moduloId") ?? "", d.DinheiroEm("total")))
            .Where(c => c.ContaId.Length > 0 && c.ModuloId.Length > 0)];

    public async Task<ContaDaFatura?> Conta(string contaId, CancellationToken ct = default)
    {
        var d = await armazenamento.Colecao(Colecoes.Contas).Find(Bson.Filtro.Eq("_id", contaId)).FirstOrDefaultAsync(ct);
        return d is null ? null : new ContaDaFatura(contaId, d.Texto("sigla") ?? "", d.Texto("nome") ?? "", d.Texto("tipo") ?? "INSTITUICAO");
    }

    // o boleto sai em nome e documento da conta; o e-mail do pagador é o login
    // da conta master ou do autônomo (o usuário "MASTER" da conta)
    public async Task<Pagador?> Pagador(string contaId, CancellationToken ct = default)
    {
        var conta = await armazenamento.Colecao(Colecoes.Contas).Find(Bson.Filtro.Eq("_id", contaId)).FirstOrDefaultAsync(ct);
        if (conta is null) return null;
        var master = await armazenamento.Colecao(Colecoes.Usuarios).Find(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.Eq("tipo", "MASTER"))
            .FirstOrDefaultAsync(ct);
        return new Pagador(conta.Texto("nome") ?? "", conta.Texto("documento") ?? "", master?.Texto("login") ?? "");
    }

    public async Task<IReadOnlyDictionary<string, string>> Nomes(IEnumerable<string> usuarioIds, CancellationToken ct = default)
    {
        var ids = usuarioIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();
        var achados = await armazenamento.Colecao(Colecoes.Usuarios).Find(Bson.Filtro.In("_id", ids)).ToListAsync(ct);
        return achados.ToDictionary(d => d["_id"].ToString() ?? "", d => d.Texto("nome") ?? "");
    }

    public async Task<IReadOnlyDictionary<string, string>> Modulos(CancellationToken ct = default) =>
        (await armazenamento.Colecao(Colecoes.Modulos).Find(Bson.Filtro.Empty).ToListAsync(ct))
            .ToDictionary(d => d["_id"].ToString() ?? "", d => d.Texto("nome") ?? d["_id"].ToString() ?? "");
}

// A coleção avisosPagamento (RN-311): um aviso de atraso por fatura vencida,
// aberto pelo agendamento depois da tolerância e encerrado quando a fatura
// deixa de estar vencida; juros e total são calculados na leitura.
public sealed class AvisosDeAtrasoNoBanco(Armazenamento armazenamento) : IAvisosDeAtraso
{
    const string Tipo = "FATURA_EM_ATRASO";

    IMongoCollection<BsonDocument> Avisos => armazenamento.Colecao(Colecoes.AvisosPagamento);

    static string Id(string faturaId) => $"avs-{faturaId}-{Tipo}";

    public async Task<bool> Abrir(AvisoDeAtraso aviso, CancellationToken ct = default)
    {
        var documento = new BsonDocument
        {
            ["_id"] = Id(aviso.FaturaId),
            ["tipo"] = Tipo,
            ["faturaId"] = aviso.FaturaId,
            ["contaId"] = aviso.ContaId,
            ["vencimento"] = Documentos.Dia(aviso.Vencimento),
            ["desde"] = Bson.Valor(aviso.Desde),
            ["encerradoEm"] = BsonNull.Value,
        };
        // o encerrado volta a abrir; o aberto fica como está
        var gravado = await Avisos.ReplaceOneAsync(Bson.Filtro.Eq("_id", documento["_id"]) & Bson.Filtro.Ne("encerradoEm", BsonNull.Value),
            documento, cancellationToken: ct);
        if (gravado.MatchedCount == 1) return true;
        try
        {
            await Avisos.InsertOneAsync(documento, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException erro) when (erro.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task Encerrar(string faturaId, DateTimeOffset em, CancellationToken ct = default) =>
        Avisos.UpdateOneAsync(Bson.Filtro.Eq("_id", Id(faturaId)) & Bson.Filtro.Eq("encerradoEm", BsonNull.Value),
            Bson.Mudar.Set("encerradoEm", Bson.Valor(em)), cancellationToken: ct);

    public async Task<IReadOnlyList<AvisoDeAtraso>> Abertos(string contaId, CancellationToken ct = default) =>
        await Ler(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.Eq("encerradoEm", BsonNull.Value), ct);

    public async Task<IReadOnlyList<AvisoDeAtraso>> TodosAbertos(CancellationToken ct = default) =>
        await Ler(Bson.Filtro.Eq("encerradoEm", BsonNull.Value), ct);

    async Task<IReadOnlyList<AvisoDeAtraso>> Ler(FilterDefinition<BsonDocument> filtro, CancellationToken ct) =>
        [.. (await Avisos.Find(filtro).ToListAsync(ct)).Select(d => new AvisoDeAtraso(d.Texto("faturaId") ?? "", d.Texto("contaId") ?? "",
            d.DiaDe("vencimento") ?? DateOnly.MinValue, d.Data("desde") ?? DateTimeOffset.MinValue))];
}
