using Boaz.Core.Hub.Auditoria.Registros.Domain;

namespace Boaz.Core.Hub.Auditoria.Registros.Dtos;

/// <summary>Os parâmetros de GET /auditoria/registros como chegaram, antes de conferir (RF-199, RF-200).</summary>
/// <param name="De">Começo do período (ISO 8601).</param>
/// <param name="Ate">Fim do período.</param>
/// <param name="Tipo">AUDITORIA ou ERRO.</param>
/// <param name="Nivel">ALERTA ou ERRO.</param>
/// <param name="Modulo">MOD-NN.</param>
/// <param name="Acao">O código da ação.</param>
/// <param name="Conta">A sigla da conta.</param>
/// <param name="Quem">O id de quem fez.</param>
/// <param name="Codigo">O código do erro.</param>
/// <param name="TraceId">A execução; vale sem período.</param>
/// <param name="Depois">O cursor da página seguinte.</param>
/// <param name="Limite">Registros por página, 1 a 200.</param>
public sealed record PedidoDeRegistros(
    string? De, string? Ate, string? Tipo, string? Nivel, string? Modulo, string? Acao, string? Conta, string? Quem, string? Codigo, string? TraceId,
    string? Depois, string? Limite);

/// <summary>Quem fez, em um registro.</summary>
/// <param name="Id">O id do usuário.</param>
/// <param name="Nome">O nome.</param>
public sealed record QuemDto(string Id, string Nome);

/// <summary>A conta, em um registro.</summary>
/// <param name="Id">O id da conta.</param>
/// <param name="Sigla">A sigla.</param>
public sealed record ContaDto(string Id, string Sigla);

/// <summary>Um registro em GET /auditoria/registros e no evento "registro" do ao vivo (RF-199).</summary>
/// <param name="Id">O _id.</param>
/// <param name="Tipo">AUDITORIA ou ERRO.</param>
/// <param name="TraceId">A execução.</param>
/// <param name="Etapa">INICIADO ou CONCLUIDO; null nos erros.</param>
/// <param name="Nivel">ALERTA ou ERRO; null na auditoria.</param>
/// <param name="Codigo">O código do erro; null na auditoria.</param>
/// <param name="Acao">O código da ação.</param>
/// <param name="AcaoNome">O nome da ação.</param>
/// <param name="Modulo">O módulo da ação; null fora do catálogo.</param>
/// <param name="Quem">Quem fez; null sem login.</param>
/// <param name="Conta">A conta; null se não há.</param>
/// <param name="Alvo">O alvo.</param>
/// <param name="Quando">Quando, com milissegundos.</param>
public sealed record RegistroDto(
    string Id, string Tipo, string TraceId, string? Etapa, string? Nivel, string? Codigo, string Acao, string AcaoNome, string? Modulo, QuemDto? Quem,
    ContaDto? Conta, string? Alvo, string Quando)
{
    /// <summary>Do registro lido.</summary>
    public static RegistroDto De(RegistroLido r) =>
        new(r.Id, r.TipoTexto, r.TraceId, r.Etapa, r.Nivel, r.Codigo, r.Acao, r.AcaoNome, r.Modulo,
            r.Quem is { } q ? new QuemDto(q.Id, q.Nome) : null, r.Conta is { } c ? new ContaDto(c.Id, c.Sigla) : null, r.Alvo, r.QuandoTexto);
}

/// <summary>200 de GET /auditoria/registros (RF-199).</summary>
/// <param name="De">O começo; null com o traceId.</param>
/// <param name="Ate">O fim; null com o traceId.</param>
/// <param name="Itens">Os registros, o mais novo primeiro.</param>
/// <param name="Proximo">De onde a página seguinte começa; null na última.</param>
public sealed record RegistrosDto(DateTimeOffset? De, DateTimeOffset? Ate, IReadOnlyList<RegistroDto> Itens, string? Proximo);

/// <summary>Uma ação em GET /auditoria/acoes (RN-319).</summary>
/// <param name="Acao">O código.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Modulo">O módulo.</param>
/// <param name="SemQuem">SEM_LOGIN, SISTEMA ou BANCO.</param>
public sealed record AcaoDto(string Acao, string Nome, string? Modulo, string SemQuem)
{
    /// <summary>Da ação do catálogo.</summary>
    public static AcaoDto De(AcaoDoCatalogo a) => new(a.Acao, a.Nome, a.Modulo, a.SemQuemTexto);
}
