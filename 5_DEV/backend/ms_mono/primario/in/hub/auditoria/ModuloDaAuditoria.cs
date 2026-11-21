using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.MsMono.Primario.In.Commons;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

// O que os controllers da Auditoria repetem (20_modulo-auditoria): o módulo, quem consulta e a consulta com o endereço lido.
static class ModuloDaAuditoria
{
    // o MOD-17 (15_modulos)
    public const string Modulo = "MOD-17";

    // quem usa a Auditoria é o ADMINISTRADOR ou o MEMBRO_TIME com MOD-17 · U (RN-314): [ComAcesso] já conferiu
    public static QuemPede QuemPedeAAuditoria(this HttpContext http)
    {
        var acesso = http.Acesso();
        return new QuemPede(acesso.Sub, acesso.Sid, acesso.Papel);
    }

    // A consulta que fica registrada (RN-325): o alvo é o endereço com os parâmetros em ordem alfabética, sem depois nem limite, e o
    // traceId é o do X-Trace-Id, que só vai na resposta que atendeu (ver ComTraceId): a recusa não o traz.
    public static ConsultaPedida NovaConsulta(this HttpContext http)
    {
        var quem = http.QuemPedeAAuditoria();
        var parametros = http.Request.Query.Select(p => new KeyValuePair<string, string>(p.Key, string.Join(',', p.Value.OfType<string>())));
        var consulta = ConsultaDaAuditoria.De(quem.UsuarioId, http.Request.Path.Value ?? "", parametros);
        return new ConsultaPedida(quem, consulta.Alvo, TraceId.Novo());
    }

    // O X-Trace-Id da consulta atendida, na resposta.
    public static void ComTraceId(this HttpContext http, ConsultaPedida consulta) =>
        http.Response.Headers[Http.CabecalhoDoTraceId] = consulta.TraceId.Valor;
}
