using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using MongoDB.Bson;

namespace Boaz.MsMono.Secundario.Out.Commons.Auditoria;

// A auditoria comum (commons/auditoria) no MongoDB: as coleções auditoria e
// auditoriaErros (17 RN-178, RN-270; 18 RN-297), guardadas 5 anos, só com
// inclusão: nada é alterado nem apagado (RNF-206).
public sealed class AuditoriaNoBanco(Armazenamento armazenamento) : IAuditoria, IAuditoriaDeErros
{
    public Task Registrar(RegistroDeAuditoria registro, CancellationToken ct = default) =>
        armazenamento.Colecao(Colecoes.Auditoria).InsertOneAsync(new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString("N"),
            ["contaId"] = Bson.Valor(registro.ContaId),
            ["quem"] = Bson.Valor(registro.Quem),
            ["acao"] = registro.Acao.Texto(),
            ["alvo"] = Bson.Valor(registro.Alvo),
            ["traceId"] = registro.TraceId.Valor,
            ["etapa"] = registro.Etapa.Texto(),
            ["quando"] = Bson.Valor(registro.Quando),
            ["guardarAte"] = Bson.Valor(registro.GuardarAte),
        }, cancellationToken: ct);

    public Task Registrar(RegistroDeErro registro, CancellationToken ct = default) =>
        armazenamento.Colecao(Colecoes.AuditoriaErros).InsertOneAsync(new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString("N"),
            ["traceId"] = registro.TraceId.Valor,
            ["contaId"] = Bson.Valor(registro.ContaId),
            ["quem"] = Bson.Valor(registro.Quem),
            ["acao"] = registro.Acao.Texto(),
            ["alvo"] = Bson.Valor(registro.Alvo),
            ["codigo"] = registro.Codigo,
            ["nivel"] = registro.Nivel.Texto(),
            ["quando"] = Bson.Valor(registro.Quando),
            ["guardarAte"] = Bson.Valor(registro.GuardarAte),
        }, cancellationToken: ct);
}
