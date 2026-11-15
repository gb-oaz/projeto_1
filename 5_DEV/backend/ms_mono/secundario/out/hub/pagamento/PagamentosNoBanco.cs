using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// dentro do namespace: "Pagamento" sozinho seria o namespace deste adaptador
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

// As coleções pagamentos e estornos (pagamento-documentos v02). Cada documento
// guarda a versão (+1 a cada gravação): Salvar só vale se a versão guardada
// ainda é a lida. Nunca número, validade nem CVV do cartão (RNF-239).
public sealed class PagamentosNoBanco(Armazenamento armazenamento) : IPagamentos, IEstornos
{
    static readonly string[] Abertos = [StatusDoPagamento.Aguardando.Texto(), StatusDoPagamento.Processando.Texto()];

    IMongoCollection<BsonDocument> Pagamentos => armazenamento.Colecao(Colecoes.Pagamentos);

    IMongoCollection<BsonDocument> Estornos => armazenamento.Colecao(Colecoes.Estornos);

    public async Task<Pagamento?> Obter(string id, CancellationToken ct = default)
    {
        var d = await Pagamentos.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return d is null ? null : Documentos.PagamentoDe(d);
    }

    public async Task<Pagamento?> PorIdExterno(string idExterno, CancellationToken ct = default)
    {
        var d = await Pagamentos.Find(Bson.Filtro.Eq("idExterno", idExterno)).FirstOrDefaultAsync(ct);
        return d is null ? null : Documentos.PagamentoDe(d);
    }

    public async Task<IReadOnlyList<Pagamento>> DasFaturas(IEnumerable<string> faturaIds, CancellationToken ct = default)
    {
        var ids = faturaIds.Distinct().ToList();
        return ids.Count == 0 ? []
            : [.. (await Pagamentos.Find(Bson.Filtro.In("faturaId", ids)).ToListAsync(ct)).Select(Documentos.PagamentoDe)];
    }

    public async Task<IReadOnlyList<Pagamento>> EmAberto(CancellationToken ct = default) =>
        [.. (await Pagamentos.Find(Bson.Filtro.In("status", Abertos)).ToListAsync(ct)).Select(Documentos.PagamentoDe)];

    public Task Incluir(Pagamento pagamento, CancellationToken ct = default) =>
        Pagamentos.InsertOneAsync(Documentos.Pagamento(pagamento, pagamento.Versao), cancellationToken: ct);

    public async Task<bool> Salvar(Pagamento pagamento, CancellationToken ct = default)
    {
        var trocado = await Pagamentos.ReplaceOneAsync(Bson.Filtro.Eq("_id", pagamento.Id) & Bson.Filtro.Eq("versao", pagamento.Versao),
            Documentos.Pagamento(pagamento, pagamento.Versao + 1), cancellationToken: ct);
        if (trocado.MatchedCount == 0) return false;
        pagamento.Versionar();
        return true;
    }

    public Task Remover(string id, CancellationToken ct = default) => Pagamentos.DeleteOneAsync(Bson.Filtro.Eq("_id", id), ct);

    async Task<Estorno?> IEstornos.PorIdExterno(string idExterno, CancellationToken ct)
    {
        var d = await Estornos.Find(Bson.Filtro.Eq("idExterno", idExterno)).FirstOrDefaultAsync(ct);
        return d is null ? null : Documentos.EstornoDe(d);
    }

    public Task Incluir(Estorno estorno, CancellationToken ct = default) =>
        Estornos.InsertOneAsync(Documentos.Estorno(estorno, estorno.Versao), cancellationToken: ct);

    public async Task<bool> Salvar(Estorno estorno, CancellationToken ct = default)
    {
        var trocado = await Estornos.ReplaceOneAsync(Bson.Filtro.Eq("_id", estorno.Id) & Bson.Filtro.Eq("versao", estorno.Versao),
            Documentos.Estorno(estorno, estorno.Versao + 1), cancellationToken: ct);
        if (trocado.MatchedCount == 0) return false;
        estorno.Versionar();
        return true;
    }

    Task IEstornos.Remover(string id, CancellationToken ct) => Estornos.DeleteOneAsync(Bson.Filtro.Eq("_id", id), ct);
}

// A coleção avisosDoBanco (RN-305, RNF-244): o _id é o id do aviso no
// intermediador, e é ele que garante a idempotência (o repetido não grava).
public sealed class AvisosDoBancoNoBanco(Armazenamento armazenamento) : IAvisosDoBanco
{
    IMongoCollection<BsonDocument> Avisos => armazenamento.Colecao(Colecoes.AvisosDoBanco);

    public async Task<bool> Registrar(RegistroDoAviso aviso, CancellationToken ct = default)
    {
        try
        {
            await Avisos.InsertOneAsync(new BsonDocument
            {
                ["_id"] = aviso.Id,
                ["idExterno"] = Bson.Valor(aviso.IdExterno),
                ["tipo"] = Bson.Valor(aviso.Tipo),
                ["recebidoEm"] = Bson.Valor(aviso.RecebidoEm),
                ["assinaturaValida"] = aviso.AssinaturaValida,
                ["resultado"] = aviso.Resultado,
                ["pagamentoId"] = Bson.Valor(aviso.PagamentoId),
                ["traceId"] = aviso.TraceId,
                ["traceIdOrigem"] = Bson.Valor(aviso.TraceIdOrigem),
            }, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException erro) when (erro.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task Concluir(string id, string resultado, string? pagamentoId, CancellationToken ct = default) =>
        Avisos.UpdateOneAsync(Bson.Filtro.Eq("_id", id),
            Bson.Mudar.Set("resultado", resultado).Set("pagamentoId", Bson.Valor(pagamentoId)), cancellationToken: ct);

    public Task Remover(string id, CancellationToken ct = default) => Avisos.DeleteOneAsync(Bson.Filtro.Eq("_id", id), ct);
}

// A coleção configuracaoPagamento (RF-193): uma versão por documento, nunca
// apagada; a vigente num instante é a de maior vigenteDesde até ele (RN-310).
public sealed class ConfiguracoesDoPagamentoNoBanco(Armazenamento armazenamento) : IConfiguracoesDoPagamento
{
    IMongoCollection<BsonDocument> Configuracoes => armazenamento.Colecao(Colecoes.ConfiguracaoPagamento);

    public async Task<ConfiguracaoDoPagamento?> Atual(CancellationToken ct = default)
    {
        var d = await Configuracoes.Find(Bson.Filtro.Empty).Sort(Builders<BsonDocument>.Sort.Descending("versao")).FirstOrDefaultAsync(ct);
        return d is null ? null : Documentos.ConfiguracaoDe(d);
    }

    public async Task<ConfiguracaoDoPagamento?> VigenteEm(DateTimeOffset quando, CancellationToken ct = default)
    {
        var d = await Configuracoes.Find(Bson.Filtro.Lte("vigenteDesde", Bson.Valor(quando)))
            .Sort(Builders<BsonDocument>.Sort.Descending("vigenteDesde").Descending("versao")).FirstOrDefaultAsync(ct);
        return d is null ? null : Documentos.ConfiguracaoDe(d);
    }

    public async Task<bool> Incluir(ConfiguracaoDoPagamento configuracao, CancellationToken ct = default)
    {
        // a versão é o _id: dois PUT com o mesmo If-Match, só um grava (412 no outro)
        try
        {
            await Configuracoes.InsertOneAsync(Documentos.Configuracao(configuracao), cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException erro) when (erro.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
