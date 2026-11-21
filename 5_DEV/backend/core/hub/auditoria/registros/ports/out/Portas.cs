using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Domain;

namespace Boaz.Core.Hub.Auditoria.Registros.Ports.Out;

/// <summary>Um registro como está no banco, sem os nomes: da auditoria ou da auditoriaErros.</summary>
/// <param name="Id">O _id.</param>
/// <param name="Tipo">De qual coleção.</param>
/// <param name="TraceId">A execução.</param>
/// <param name="Etapa">INICIADO ou CONCLUIDO; null nos erros.</param>
/// <param name="Nivel">ALERTA ou ERRO; null na auditoria.</param>
/// <param name="Codigo">O código do erro.</param>
/// <param name="Acao">O código da ação.</param>
/// <param name="QuemId">O id de quem fez.</param>
/// <param name="ContaId">O id da conta.</param>
/// <param name="Alvo">O alvo.</param>
/// <param name="Quando">Quando.</param>
public sealed record RegistroBruto(
    string Id, TipoDoRegistro Tipo, string TraceId, string? Etapa, string? Nivel, string? Codigo, string Acao, string? QuemId, string? ContaId,
    string? Alvo, DateTimeOffset Quando);

/// <summary>O que a leitura dos registros pede ao banco: o período, o filtro já resolvido (ações e conta), a página.</summary>
/// <param name="Periodo">O período; null com o traceId, que vale sem período.</param>
/// <param name="Filtro">O filtro do pedido.</param>
/// <param name="Acoes">As ações que passam (do módulo e da ação); null sem esse filtro; vazia, nenhuma.</param>
/// <param name="ContaId">O id da conta da sigla pedida; null sem esse filtro.</param>
/// <param name="Depois">Depois de qual registro.</param>
/// <param name="Limite">Quantos registros: o adaptador devolve até um a mais, para saber se há outra página.</param>
public sealed record ConsultaDeRegistros(Periodo? Periodo, FiltroDosRegistros Filtro, IReadOnlyList<string>? Acoes, string? ContaId, Cursor? Depois, int Limite);

/// <summary>
/// A leitura das coleções auditoria e auditoriaErros (RF-199 a RF-203), só leitura: nada as altera nem apaga (RN-320,
/// 17 RNF-206). Sem o banco, lança <c>FalhaDaAuditoria.BancoDeDadosFora</c>.
/// </summary>
public interface ILeituraDaAuditoria
{
    /// <summary>Os registros das duas coleções juntos, do mais novo para o mais antigo, até Limite + 1.</summary>
    Task<IReadOnlyList<RegistroBruto>> Registros(ConsultaDeRegistros consulta, CancellationToken ct = default);

    /// <summary>Todos os registros do traceId, nas duas coleções, em ordem de quando.</summary>
    Task<IReadOnlyList<RegistroBruto>> DoTrace(string traceId, CancellationToken ct = default);

    /// <summary>As pontas de cada execução do período, uma por traceId, agrupadas no banco.</summary>
    Task<IReadOnlyList<PontasDaExecucao>> Execucoes(Periodo periodo, CancellationToken ct = default);

    /// <summary>Quantos registros de erro de cada nível entre as duas horas.</summary>
    Task<(int Erros, int Alertas)> ContagemDeErros(DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default);
}

/// <summary>Os nomes lidos de usuarios e de contas, em lote por página, nunca um a um (auditoria-documentos v02).</summary>
public interface INomesDaAuditoria
{
    /// <summary>O nome de cada usuário que existe.</summary>
    Task<IReadOnlyDictionary<string, string>> Usuarios(IEnumerable<string> ids, CancellationToken ct = default);

    /// <summary>A sigla de cada conta que existe.</summary>
    Task<IReadOnlyDictionary<string, string>> Contas(IEnumerable<string> ids, CancellationToken ct = default);

    /// <summary>O id da conta da sigla, sem diferença de caixa; null se não existe.</summary>
    Task<string?> IdDaConta(string sigla, CancellationToken ct = default);
}

/// <summary>O aviso de cada registro gravado (canal auditoria:registros): o ao vivo o recebe em até 5 s (RNF-249).</summary>
public interface IAvisosDaAuditoria
{
    /// <summary>Avisa as conexões ao vivo depois de gravar; a falha é só do aviso, o registro já está gravado.</summary>
    Task Publicar(RegistroBruto registro, CancellationToken ct = default);

    /// <summary>Assina o canal agora.</summary>
    Task<IAssinatura<RegistroBruto>> Assinar(CancellationToken ct = default);
}

/// <summary>A consulta que acabou de ser registrada (auditoria:consulta:&lt;usuarioId&gt;:&lt;hash do alvo&gt;).</summary>
public interface IConsultasRecentes
{
    /// <summary>
    /// true se a pessoa não consultou o alvo nos últimos <paramref name="janela"/> (e passa a ter consultado agora); false se a
    /// mesma consulta já foi registrada dentro da janela.
    /// </summary>
    Task<bool> Primeira(string quem, string alvo, DateTimeOffset agora, TimeSpan janela, CancellationToken ct = default);
}
