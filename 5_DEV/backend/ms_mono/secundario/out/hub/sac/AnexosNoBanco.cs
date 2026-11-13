using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// A coleção anexosSac (RN-292): o anexo enviado vence em 24 h (expiraEm) até
// ser ligado a um chamado ou mensagem; ligado, não vence mais.
public sealed class AnexosNoBanco(Armazenamento armazenamento) : IAnexos, IAnexosDoChamado
{
    IMongoCollection<BsonDocument> Colecao => armazenamento.Colecao(Colecoes.AnexosSac);

    public Task Incluir(Anexo anexo, CancellationToken ct = default) => Colecao.InsertOneAsync(new BsonDocument
    {
        ["_id"] = anexo.Id,
        ["nome"] = anexo.Nome,
        ["tipo"] = anexo.Tipo,
        ["tamanho"] = anexo.Tamanho,
        ["sha256"] = anexo.Sha256,
        ["enviadoPor"] = anexo.EnviadoPor,
        ["contaId"] = anexo.ContaId,
        ["enviadoEm"] = Bson.Valor(anexo.EnviadoEm),
        ["verificacao"] = new BsonDocument { ["resultado"] = "LIMPO", ["em"] = Bson.Valor(anexo.EnviadoEm) },
        ["objeto"] = anexo.Objeto,
        ["chamadoId"] = Bson.Valor(anexo.ChamadoId),
        ["mensagemId"] = Bson.Valor(anexo.MensagemId),
        ["interna"] = anexo.Interna,
        ["expiraEm"] = Bson.Valor(anexo.ExpiraEm),
    }, cancellationToken: ct);

    public async Task<Anexo?> Obter(string id, CancellationToken ct = default)
    {
        var d = await Colecao.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return d is null ? null : Ler(d);
    }

    public async Task<IReadOnlyList<Anexo>> NaoUsadosAte(DateTimeOffset agora, CancellationToken ct = default)
    {
        var lidos = await Colecao.Find(Bson.Filtro.Eq("chamadoId", BsonNull.Value) & Bson.Filtro.Lte("expiraEm", Bson.Valor(agora)))
            .ToListAsync(ct);
        return [.. lidos.Select(Ler)];
    }

    public Task Apagar(string id, CancellationToken ct = default) => Colecao.DeleteOneAsync(Bson.Filtro.Eq("_id", id), ct);

    public async Task<bool> Disponiveis(IReadOnlyList<string> ids, string usuarioId, DateTimeOffset agora, CancellationToken ct = default)
    {
        var f = Bson.Filtro;
        var livres = await Colecao.CountDocumentsAsync(f.In("_id", ids) & f.Eq("enviadoPor", usuarioId) & f.Eq("chamadoId", BsonNull.Value)
            & f.Gt("expiraEm", Bson.Valor(agora)), cancellationToken: ct);
        return livres == ids.Count;
    }

    public Task Vincular(IReadOnlyList<string> ids, NumeroDoChamado chamado, string? mensagemId, bool interna, CancellationToken ct = default) =>
        Colecao.UpdateManyAsync(Bson.Filtro.In("_id", ids), Bson.Mudar
            .Set("chamadoId", chamado.Valor)
            .Set("mensagemId", Bson.Valor(mensagemId))
            .Set("interna", interna)
            .Set("expiraEm", BsonNull.Value), cancellationToken: ct);

    static Anexo Ler(BsonDocument d) => new(
        d.Texto("_id") ?? "", d.Texto("nome") ?? "", d.Texto("tipo") ?? "", d.Longo("tamanho"), d.Texto("sha256") ?? "",
        d.Texto("enviadoPor") ?? "", d.Texto("contaId") ?? "", d.Data("enviadoEm")!.Value, d.Data("expiraEm"), d.Texto("chamadoId"),
        d.Texto("mensagemId"), d.Logico("interna"));
}
