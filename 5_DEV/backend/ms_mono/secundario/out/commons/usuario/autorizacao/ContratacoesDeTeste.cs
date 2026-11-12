using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Commons.Usuario.Autorizacao;

// O papel do Catálogo no teste (POST /_teste/contratacao, contrato autorizacao
// v11, grupo TESTE): grava a contratação em contratacoes como o Catálogo grava —
// descontratar fecha o ate das em vigor; contratar abre uma nova desde agora.
// Só existe com o relógio de teste ligado (sistema/TesteController).
public sealed class ContratacoesDeTeste(Armazenamento armazenamento)
{
    IMongoCollection<BsonDocument> Contratacoes => armazenamento.Colecao(Colecoes.Contratacoes);

    // a sigla da conta; null se a conta não existe
    public async Task<string?> SiglaDaConta(string contaId, CancellationToken ct = default)
    {
        var conta = await armazenamento.Colecao(Colecoes.Contas).Find(Bson.Filtro.Eq("_id", contaId)).FirstOrDefaultAsync(ct);
        return conta is null ? null : conta.Texto("sigla") ?? "";
    }

    // grava o estado pedido; false se ele já estava gravado (nada a avisar)
    public async Task<bool> Mudar(string contaId, string sigla, string moduloId, bool contratado, DateTimeOffset agora,
        CancellationToken ct = default)
    {
        var emVigor = Bson.Filtro.Eq("contaId", contaId)
            & Bson.Filtro.Eq("moduloId", moduloId)
            & Bson.Filtro.Lte("desde", Bson.Valor(agora))
            & (Bson.Filtro.Eq("ate", BsonNull.Value) | Bson.Filtro.Gt("ate", Bson.Valor(agora)));
        if (!contratado)
        {
            var fechadas = await Contratacoes.UpdateManyAsync(emVigor, Bson.Mudar.Set("ate", Bson.Valor(agora)), cancellationToken: ct);
            return fechadas.ModifiedCount > 0;
        }
        if (await Contratacoes.Find(emVigor).AnyAsync(ct)) return false;
        await Contratacoes.InsertOneAsync(new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString("N"),
            ["contaId"] = contaId,
            ["sigla"] = sigla,
            ["moduloId"] = moduloId,
            ["desde"] = Bson.Valor(agora),
            ["ate"] = BsonNull.Value,
        }, cancellationToken: ct);
        return true;
    }
}
