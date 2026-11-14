using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// A coleção anexosSac (sac-documentos v03, RN-292, RNF-233, RNF-234): cada
// anexo guarda onde o arquivo ficou ("armazenamento") e até quando fica
// ("expiraEm"): 24 h sem uso; usado, 30 dias do envio em homologação ou nenhum
// em produção até o FECHADO, quando passa a fechadoEm + 5 anos. O arquivo que
// sai pelo descarte deixa o registro do anexo usado, com "removidoEm". A tag
// "uso" do arquivo no armazenamento de objetos acompanha (IArquivos.MarcarUso).
public sealed class AnexosNoBanco(Armazenamento armazenamento, IArquivos arquivos, AmbienteDaAplicacao ambiente) : IAnexos, IAnexosDoChamado
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
        ["armazenamento"] = anexo.Armazenamento.Texto(),
        ["objeto"] = anexo.Objeto,
        ["chamadoId"] = Bson.Valor(anexo.ChamadoId),
        ["mensagemId"] = Bson.Valor(anexo.MensagemId),
        ["interna"] = anexo.Interna,
        ["expiraEm"] = Bson.Valor(anexo.ExpiraEm),
        ["removidoEm"] = Bson.Valor(anexo.RemovidoEm),
    }, cancellationToken: ct);

    public async Task<Anexo?> Obter(string id, CancellationToken ct = default)
    {
        var d = await Colecao.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return d is null ? null : Ler(d);
    }

    // pelo índice expiraEm; o que já perdeu o arquivo não volta ao descarte
    public async Task<IReadOnlyList<Anexo>> VencidosAte(DateTimeOffset agora, CancellationToken ct = default)
    {
        var f = Bson.Filtro;
        var lidos = await Colecao.Find(f.Lte("expiraEm", Bson.Valor(agora)) & SemRemocao()).ToListAsync(ct);
        return [.. lidos.Select(Ler)];
    }

    public Task Apagar(string id, CancellationToken ct = default) => Colecao.DeleteOneAsync(Bson.Filtro.Eq("_id", id), ct);

    public Task MarcarRemovido(string id, DateTimeOffset agora, CancellationToken ct = default) =>
        Colecao.UpdateOneAsync(Bson.Filtro.Eq("_id", id), Bson.Mudar.Set("removidoEm", Bson.Valor(agora)), cancellationToken: ct);

    public async Task<bool> Disponiveis(IReadOnlyList<string> ids, string usuarioId, DateTimeOffset agora, CancellationToken ct = default)
    {
        var f = Bson.Filtro;
        var livres = await Colecao.CountDocumentsAsync(f.In("_id", ids) & f.Eq("enviadoPor", usuarioId) & f.Eq("chamadoId", BsonNull.Value)
            & f.Gt("expiraEm", Bson.Valor(agora)), cancellationToken: ct);
        return livres == ids.Count;
    }

    // o expiraEm de cada um vem do próprio envio (30 dias em homologação)
    public async Task Vincular(IReadOnlyList<string> ids, NumeroDoChamado chamado, string? mensagemId, bool interna, CancellationToken ct = default)
    {
        foreach (var anexo in await Lidos(Bson.Filtro.In("_id", ids), ct))
        {
            await Colecao.UpdateOneAsync(Bson.Filtro.Eq("_id", anexo.Id), Bson.Mudar
                .Set("chamadoId", chamado.Valor)
                .Set("mensagemId", Bson.Valor(mensagemId))
                .Set("interna", interna)
                .Set("expiraEm", Bson.Valor(Anexo.PrazoAoUsar(ambiente.EmHomologacao, anexo.EnviadoEm))), cancellationToken: ct);
            await arquivos.MarcarUso(anexo.Armazenamento, anexo.Objeto, UsoDoArquivo.Chamado, ct);
        }
    }

    public async Task AoFechar(NumeroDoChamado chamado, DateTimeOffset fechadoEm, CancellationToken ct = default)
    {
        foreach (var anexo in await DoChamado(chamado, ct))
        {
            await Colecao.UpdateOneAsync(Bson.Filtro.Eq("_id", anexo.Id),
                Bson.Mudar.Set("expiraEm", Bson.Valor(Anexo.PrazoAoFechar(ambiente.EmHomologacao, anexo.EnviadoEm, fechadoEm))),
                cancellationToken: ct);
            await arquivos.MarcarUso(anexo.Armazenamento, anexo.Objeto, UsoDoArquivo.Fechado, ct);
        }
    }

    public async Task AoReabrir(NumeroDoChamado chamado, CancellationToken ct = default)
    {
        foreach (var anexo in await DoChamado(chamado, ct))
        {
            await Colecao.UpdateOneAsync(Bson.Filtro.Eq("_id", anexo.Id),
                Bson.Mudar.Set("expiraEm", Bson.Valor(Anexo.PrazoAoUsar(ambiente.EmHomologacao, anexo.EnviadoEm))), cancellationToken: ct);
            await arquivos.MarcarUso(anexo.Armazenamento, anexo.Objeto, UsoDoArquivo.Chamado, ct);
        }
    }

    // os do chamado que ainda têm o arquivo
    Task<IReadOnlyList<Anexo>> DoChamado(NumeroDoChamado chamado, CancellationToken ct) =>
        Lidos(Bson.Filtro.Eq("chamadoId", chamado.Valor) & SemRemocao(), ct);

    async Task<IReadOnlyList<Anexo>> Lidos(FilterDefinition<BsonDocument> filtro, CancellationToken ct) =>
        [.. (await Colecao.Find(filtro).ToListAsync(ct)).Select(Ler)];

    // o campo ausente (anexo de antes do removidoEm) conta como sem remoção
    static FilterDefinition<BsonDocument> SemRemocao() => Bson.Filtro.Eq("removidoEm", BsonNull.Value);

    static Anexo Ler(BsonDocument d) => new(
        d.Texto("_id") ?? "", d.Texto("nome") ?? "", d.Texto("tipo") ?? "", d.Longo("tamanho"), d.Texto("sha256") ?? "",
        d.Texto("enviadoPor") ?? "", d.Texto("contaId") ?? "", d.Data("enviadoEm")!.Value, d.Data("expiraEm"), d.Texto("chamadoId"),
        d.Texto("mensagemId"), d.Logico("interna"), TextosDoAnexo.ArmazenamentoDe(d.Texto("armazenamento")), d.Data("removidoEm"));
}
