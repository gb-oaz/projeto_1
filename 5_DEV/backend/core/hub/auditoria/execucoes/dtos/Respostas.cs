using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Dtos;

namespace Boaz.Core.Hub.Auditoria.Execucoes.Dtos;

/// <summary>Os parâmetros de GET /auditoria/execucoes como chegaram (RF-203).</summary>
/// <param name="De">Começo do período.</param>
/// <param name="Ate">Fim do período.</param>
/// <param name="Resultado">Um ou mais resultados, separados por vírgula.</param>
/// <param name="Modulo">MOD-NN.</param>
/// <param name="Acao">O código da ação principal.</param>
/// <param name="Conta">A sigla da conta.</param>
/// <param name="Depois">O cursor da página seguinte.</param>
/// <param name="Limite">Execuções por página, 1 a 200 (50 por padrão).</param>
public sealed record PedidoDeExecucoes(string? De, string? Ate, string? Resultado, string? Modulo, string? Acao, string? Conta, string? Depois, string? Limite);

/// <summary>A contagem de cada resultado no período (RF-203).</summary>
/// <param name="Concluidas">CONCLUIDA.</param>
/// <param name="Recusadas">RECUSADA.</param>
/// <param name="Falharam">FALHOU.</param>
/// <param name="Interrompidas">INTERROMPIDA.</param>
/// <param name="EmAndamento">EM_ANDAMENTO.</param>
public sealed record ContagemDto(int Concluidas, int Recusadas, int Falharam, int Interrompidas, int EmAndamento);

/// <summary>Uma execução da lista de GET /auditoria/execucoes (RF-203).</summary>
/// <param name="TraceId">A execução.</param>
/// <param name="Acao">A ação principal.</param>
/// <param name="AcaoNome">O nome dela.</param>
/// <param name="Modulo">O módulo.</param>
/// <param name="Resultado">O resultado.</param>
/// <param name="Inicio">Quando começou, com milissegundos.</param>
/// <param name="DuracaoMs">A duração; null se não terminou.</param>
/// <param name="Codigo">O código do erro.</param>
/// <param name="Quem">Quem fez.</param>
/// <param name="Conta">A conta.</param>
/// <param name="Alvo">O alvo.</param>
public sealed record ExecucaoDaListaDto(
    string TraceId, string Acao, string AcaoNome, string? Modulo, string Resultado, string Inicio, long? DuracaoMs, string? Codigo, QuemDto? Quem,
    ContaDto? Conta, string? Alvo);

/// <summary>200 de GET /auditoria/execucoes (RF-203).</summary>
/// <param name="De">O começo.</param>
/// <param name="Ate">O fim.</param>
/// <param name="Contagem">Todos os resultados do período, com os outros filtros, sem o "resultado".</param>
/// <param name="Itens">As execuções pedidas, a mais nova primeiro.</param>
/// <param name="Proximo">De onde a página seguinte começa; null na última.</param>
public sealed record ExecucoesDto(DateTimeOffset De, DateTimeOffset Ate, ContagemDto Contagem, IReadOnlyList<ExecucaoDaListaDto> Itens, string? Proximo);

/// <summary>Um passo de GET /auditoria/execucoes/{traceId} (RF-202).</summary>
/// <param name="Id">O _id do registro.</param>
/// <param name="Tipo">AUDITORIA ou ERRO.</param>
/// <param name="Acao">A ação.</param>
/// <param name="AcaoNome">O nome da ação.</param>
/// <param name="Interna">A ação não é a principal.</param>
/// <param name="Etapa">INICIADO ou CONCLUIDO.</param>
/// <param name="Nivel">ALERTA ou ERRO.</param>
/// <param name="Codigo">O código do erro.</param>
/// <param name="Quando">Quando, com milissegundos.</param>
public sealed record PassoDto(
    string Id, string Tipo, string Acao, string AcaoNome, bool Interna, string? Etapa, string? Nivel, string? Codigo, string Quando)
{
    /// <summary>Do passo.</summary>
    public static PassoDto De(Passo p) =>
        new(p.Registro.Id, p.Registro.TipoTexto, p.Registro.Acao, p.Registro.AcaoNome, p.Interna, p.Registro.Etapa, p.Registro.Nivel, p.Registro.Codigo,
            p.Registro.QuandoTexto);
}

/// <summary>200 de GET /auditoria/execucoes/{traceId} (RF-202).</summary>
/// <param name="TraceId">A execução.</param>
/// <param name="Acao">A ação principal.</param>
/// <param name="AcaoNome">O nome dela.</param>
/// <param name="Modulo">O módulo.</param>
/// <param name="Resultado">O resultado.</param>
/// <param name="Inicio">Quando começou.</param>
/// <param name="Fim">Quando terminou; null se não terminou.</param>
/// <param name="DuracaoMs">A duração; null se não terminou.</param>
/// <param name="Codigo">O código do erro.</param>
/// <param name="Quem">Quem fez.</param>
/// <param name="Conta">A conta.</param>
/// <param name="Alvo">O alvo.</param>
/// <param name="Passos">Os registros em ordem de quando.</param>
public sealed record ExecucaoDto(
    string TraceId, string Acao, string AcaoNome, string? Modulo, string Resultado, string Inicio, string? Fim, long? DuracaoMs, string? Codigo,
    QuemDto? Quem, ContaDto? Conta, string? Alvo, IReadOnlyList<PassoDto> Passos)
{
    /// <summary>Da execução.</summary>
    public static ExecucaoDto De(Execucao e) =>
        new(e.TraceId, e.Principal.Acao, e.Principal.AcaoNome, e.Principal.Modulo, e.Resultado.Texto(), e.Principal.QuandoTexto,
            e.Fim is { } fim ? Formato.ComMilissegundos(fim) : null, e.DuracaoMs, e.Codigo,
            e.Principal.Quem is { } q ? new QuemDto(q.Id, q.Nome) : null, e.Principal.Conta is { } c ? new ContaDto(c.Id, c.Sigla) : null, e.Principal.Alvo,
            [.. e.Passos.Select(PassoDto.De)]);
}
