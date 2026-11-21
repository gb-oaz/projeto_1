using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;

namespace Boaz.Core.Hub.Auditoria.Execucoes.Domain;

/// <summary>O resultado de uma execução, pelos registros do mesmo traceId (RN-318).</summary>
public enum ResultadoDaExecucao
{
    /// <summary>O CONCLUIDO da ação principal foi gravado.</summary>
    Concluida,
    /// <summary>Um ALERTA: a regra recusou.</summary>
    Recusada,
    /// <summary>Um ERRO: a execução falhou.</summary>
    Falhou,
    /// <summary>Só o INICIADO, há menos de 5 min.</summary>
    EmAndamento,
    /// <summary>Só o INICIADO, há 5 min ou mais: a execução não terminou.</summary>
    Interrompida,
}

/// <summary>Os textos do resultado, como no contrato.</summary>
public static class TextosDaExecucao
{
    /// <summary>CONCLUIDA, RECUSADA, FALHOU, EM_ANDAMENTO ou INTERROMPIDA.</summary>
    public static string Texto(this ResultadoDaExecucao resultado) => resultado switch
    {
        ResultadoDaExecucao.Concluida => "CONCLUIDA",
        ResultadoDaExecucao.Recusada => "RECUSADA",
        ResultadoDaExecucao.Falhou => "FALHOU",
        ResultadoDaExecucao.EmAndamento => "EM_ANDAMENTO",
        _ => "INTERROMPIDA",
    };

    /// <summary>O resultado pelo texto; null se não for nenhum.</summary>
    public static ResultadoDaExecucao? ResultadoDe(string? texto) =>
        Enum.GetValues<ResultadoDaExecucao>().Cast<ResultadoDaExecucao?>().FirstOrDefault(r => r!.Value.Texto() == texto);
}

/// <summary>
/// O que a leitura em lote de uma execução traz (um por traceId, só as pontas): a ação principal (a do 1º INICIADO)
/// com quem, a conta e o alvo; a última etapa gravada na auditoria; e o erro, se há. Dá para dizer o resultado sem
/// trazer cada registro.
/// </summary>
/// <param name="TraceId">A execução.</param>
/// <param name="Acao">A ação principal.</param>
/// <param name="QuemId">Quem fez.</param>
/// <param name="ContaId">A conta.</param>
/// <param name="Alvo">O alvo.</param>
/// <param name="Inicio">Quando o 1º INICIADO foi gravado.</param>
/// <param name="UltimaEtapa">A etapa do último registro da auditoria (INICIADO ou CONCLUIDO).</param>
/// <param name="UltimaAcao">A ação do último registro da auditoria.</param>
/// <param name="UltimoQuando">Quando foi gravado.</param>
/// <param name="NivelDoErro">ALERTA ou ERRO, se há registro de erro.</param>
/// <param name="CodigoDoErro">O código do erro.</param>
/// <param name="QuandoDoErro">Quando o erro foi gravado.</param>
public sealed record PontasDaExecucao(
    string TraceId, string Acao, string? QuemId, string? ContaId, string? Alvo, DateTimeOffset Inicio, string? UltimaEtapa, string? UltimaAcao,
    DateTimeOffset? UltimoQuando, string? NivelDoErro, string? CodigoDoErro, DateTimeOffset? QuandoDoErro);

/// <summary>Uma execução da lista (RF-203): o resumo, sem os passos.</summary>
/// <param name="Pontas">O que a leitura trouxe.</param>
/// <param name="Resultado">O resultado.</param>
/// <param name="Fim">Quando terminou; null se não terminou.</param>
public sealed record ResumoDaExecucao(PontasDaExecucao Pontas, ResultadoDaExecucao Resultado, DateTimeOffset? Fim)
{
    /// <summary>Há quanto tempo o INICIADO sem fim vira "Interrompida": 5 min.</summary>
    public static readonly TimeSpan PrazoDaInterrupcao = TimeSpan.FromMinutes(5);

    /// <summary>A duração em ms, do INICIADO ao fim; null sem fim.</summary>
    public long? DuracaoMs => Fim is { } fim ? (long)(fim - Pontas.Inicio).TotalMilliseconds : null;

    /// <summary>O módulo da ação principal; null se ela não está no catálogo.</summary>
    public string? Modulo => CatalogoDeAcoes.Modulo(Pontas.Acao);

    /// <summary>O resultado de uma execução (RN-318), agora.</summary>
    public static ResumoDaExecucao De(PontasDaExecucao p, DateTimeOffset agora)
    {
        if (p.NivelDoErro is { } nivel)
        {
            return new ResumoDaExecucao(p, nivel == "ALERTA" ? ResultadoDaExecucao.Recusada : ResultadoDaExecucao.Falhou, p.QuandoDoErro);
        }
        if (p.UltimaEtapa == "CONCLUIDO" && p.UltimaAcao == p.Acao) return new ResumoDaExecucao(p, ResultadoDaExecucao.Concluida, p.UltimoQuando);
        return new ResumoDaExecucao(p, agora - p.Inicio >= PrazoDaInterrupcao ? ResultadoDaExecucao.Interrompida : ResultadoDaExecucao.EmAndamento, null);
    }
}

/// <summary>Um passo de uma execução: um registro, com o aviso de que é de uma ação interna (RN-275).</summary>
/// <param name="Registro">O registro.</param>
/// <param name="Interna">A ação não é a principal da execução.</param>
public sealed record Passo(RegistroLido Registro, bool Interna);

/// <summary>
/// Uma execução do começo ao fim (RF-202): os registros do mesmo traceId em ordem de quando. A ação principal é a do 1º
/// INICIADO; as outras são internas.
/// </summary>
/// <param name="TraceId">A execução.</param>
/// <param name="Principal">O 1º INICIADO (ou o 1º registro, se não há INICIADO).</param>
/// <param name="Passos">Todos os registros, em ordem de quando.</param>
/// <param name="Resultado">O resultado.</param>
/// <param name="Fim">Quando terminou; null se não terminou.</param>
/// <param name="Codigo">O código do erro, se há.</param>
public sealed record Execucao(
    string TraceId, RegistroLido Principal, IReadOnlyList<Passo> Passos, ResultadoDaExecucao Resultado, DateTimeOffset? Fim, string? Codigo)
{
    /// <summary>A duração em ms, do INICIADO ao fim; null sem fim.</summary>
    public long? DuracaoMs => Fim is { } fim ? (long)(fim - Principal.Quando).TotalMilliseconds : null;

    /// <summary>Monta a execução com os registros do traceId, em ordem de quando.</summary>
    public static Execucao Montar(IReadOnlyList<RegistroLido> registros, DateTimeOffset agora)
    {
        if (registros.Count == 0) throw FalhaDaAuditoria.ExecucaoInexistente();
        var principal = registros.FirstOrDefault(r => r.Tipo == TipoDoRegistro.Auditoria && r.Etapa == "INICIADO") ?? registros[0];
        var erro = registros.LastOrDefault(r => r.Tipo == TipoDoRegistro.Erro);
        var concluido = registros.LastOrDefault(r => r.Tipo == TipoDoRegistro.Auditoria && r.Etapa == "CONCLUIDO" && r.Acao == principal.Acao);
        var passos = registros.Select(r => new Passo(r, r.Acao != principal.Acao)).ToList();
        var pontas = new PontasDaExecucao(principal.TraceId, principal.Acao, principal.Quem?.Id, principal.Conta?.Id, principal.Alvo, principal.Quando,
            concluido?.Etapa, concluido?.Acao, concluido?.Quando, erro?.Nivel, erro?.Codigo, erro?.Quando);
        var resumo = ResumoDaExecucao.De(pontas, agora);
        return new Execucao(principal.TraceId, principal, passos, resumo.Resultado, resumo.Fim, erro?.Codigo);
    }
}
