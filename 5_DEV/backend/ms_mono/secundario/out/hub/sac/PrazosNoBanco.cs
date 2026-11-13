using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.Core.Hub.Sac.Configuracao.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// A coleção configuracaoSac, um documento "prazos" (RF-181): sem ele, o
// padrão da RN-289. Também dá o prazo da categoria a quem abre um chamado.
public sealed class PrazosNoBanco(Armazenamento armazenamento) : IConfiguracaoDoSac, IPrazosDoSac
{
    const string Id = "prazos";

    IMongoCollection<BsonDocument> Colecao => armazenamento.Colecao(Colecoes.ConfiguracaoSac);

    public async Task<PrazosDoSac> Ler(CancellationToken ct = default)
    {
        var documento = await Colecao.Find(Bson.Filtro.Eq("_id", Id)).FirstOrDefaultAsync(ct);
        if (documento is null) return PrazosDoSac.Padrao;
        var horas = new Dictionary<Categoria, int>();
        if (documento.TryGetValue("prazos", out var prazos) && prazos.IsBsonArray)
        {
            foreach (var prazo in prazos.AsBsonArray.OfType<BsonDocument>())
            {
                if (Textos.CategoriaDe(prazo.Texto("categoria")) is { } categoria) horas[categoria] = prazo.Inteiro("horasUteis");
            }
        }
        return new PrazosDoSac(horas, documento.Data("salvaEm"), documento.Texto("salvaPor"));
    }

    public Task Salvar(PrazosDoSac prazos, CancellationToken ct = default) =>
        Colecao.UpdateOneAsync(Bson.Filtro.Eq("_id", Id), Bson.Mudar
            .Set("prazos", new BsonArray(Enum.GetValues<Categoria>()
                .Select(c => new BsonDocument { ["categoria"] = c.Texto(), ["horasUteis"] = prazos.HorasDe(c) })))
            .Set("salvaEm", Bson.Valor(prazos.SalvaEm))
            .Set("salvaPor", Bson.Valor(prazos.SalvaPor))
            .Inc("versao", 1), new UpdateOptions { IsUpsert = true }, ct);

    public async Task<int> HorasUteis(Categoria categoria, CancellationToken ct = default) => (await Ler(ct)).HorasDe(categoria);
}
