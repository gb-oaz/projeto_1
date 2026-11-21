using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using Boaz.MsMono.Secundario.Out.Commons.Auditoria;
using MongoDB.Bson;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// A auditoria comum com o aviso ao vivo (auditoria-classes v03): decora a AuditoriaNoBanco e, depois de gravar o registro em
// auditoria ou em auditoriaErros, publica o aviso em auditoria:registros, para a aba Registros ao vivo mostrar em até 5 s
// (RNF-249). A falha do aviso vai para o log e a ação segue: o registro já está gravado e a lista o traz de qualquer jeito.
public sealed class AuditoriaComAviso(AuditoriaNoBanco banco, IAvisosDaAuditoria avisos, ILogger<AuditoriaComAviso> log) : IAuditoria, IAuditoriaDeErros
{
    public async Task Registrar(RegistroDeAuditoria registro, CancellationToken ct = default)
    {
        var id = await banco.RegistrarComId(registro, ct);
        await Avisar(new RegistroBruto(id, TipoDoRegistro.Auditoria, registro.TraceId.Valor, registro.Etapa.Texto(), null, null, registro.Acao.Texto(),
            registro.Quem, registro.ContaId, registro.Alvo, registro.Quando));
    }

    public async Task Registrar(RegistroDeErro registro, CancellationToken ct = default)
    {
        var id = await banco.RegistrarComId(registro, ct);
        await Avisar(new RegistroBruto(id, TipoDoRegistro.Erro, registro.TraceId.Valor, null, registro.Nivel.Texto(), registro.Codigo, registro.Acao.Texto(),
            registro.Quem, registro.ContaId, registro.Alvo, registro.Quando));
    }

    async Task Avisar(RegistroBruto registro)
    {
        try
        {
            await avisos.Publicar(registro);
        }
        catch (Exception erro)
        {
            log.LogWarning("Aviso da auditoria não publicado ({Erro}); o registro está gravado", erro.GetType().Name);
        }
    }
}

// O que o gancho POST /_teste/auditoria/registros grava (só homologação com o relógio de teste): registros com qualquer ação,
// inclusive a que ainda não está no catálogo, direto em auditoria e auditoriaErros, com o mesmo aviso ao vivo.
public sealed class RegistrosDeTeste(Armazenamento armazenamento, IAvisosDaAuditoria avisos)
{
    public async Task Gravar(RegistroBruto registro, CancellationToken ct = default)
    {
        var erro = registro.Tipo == TipoDoRegistro.Erro;
        var id = registro.Id;
        var guardarAte = registro.Quando.AddYears(RegistroDeAuditoria.Anos);
        var documento = new BsonDocument
        {
            ["_id"] = id,
            ["traceId"] = registro.TraceId,
            ["contaId"] = Bson.Valor(registro.ContaId),
            ["quem"] = Bson.Valor(registro.QuemId),
            ["acao"] = registro.Acao,
            ["alvo"] = Bson.Valor(registro.Alvo),
        };
        if (erro)
        {
            documento["codigo"] = Bson.Valor(registro.Codigo);
            documento["nivel"] = registro.Nivel;
        }
        else
        {
            documento["etapa"] = registro.Etapa;
        }
        documento["quando"] = Bson.Valor(registro.Quando);
        documento["guardarAte"] = Bson.Valor(guardarAte);
        await armazenamento.Colecao(erro ? Colecoes.AuditoriaErros : Colecoes.Auditoria).InsertOneAsync(documento, cancellationToken: ct);
        try
        {
            await avisos.Publicar(registro, ct);
        }
        catch (Exception)
        {
            // o aviso é só do ao vivo
        }
    }
}
