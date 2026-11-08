using System.Security.Cryptography;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// O identificador de uma execução que muda estado (RN-271): "trc-" e 8
/// hexadecimais, como trc-7f3a9c21. Liga os registros "INICIADO" e
/// "CONCLUIDO" da auditoria e o registro de erro, e volta no cabeçalho
/// X-Trace-Id.
/// </summary>
/// <param name="Valor">O texto, como trc-7f3a9c21.</param>
public readonly record struct TraceId(string Valor)
{
    /// <summary>O começo de todo traceId.</summary>
    public const string Prefixo = "trc-";

    /// <summary>Um traceId novo, sorteado para a execução.</summary>
    public static TraceId Novo() => new(Prefixo + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)));

    /// <inheritdoc />
    public override string ToString() => Valor;
}

/// <summary>A etapa da execução gravada na auditoria (RN-178).</summary>
public enum EtapaDaExecucao
{
    /// <summary>Ao chegar, antes de aplicar a regra.</summary>
    Iniciado,
    /// <summary>Antes de responder com sucesso.</summary>
    Concluido,
}

/// <summary>O nível de um registro de erro (RN-270).</summary>
public enum NivelDoErro
{
    /// <summary>Recusa prevista pela regra (4xx).</summary>
    Alerta,
    /// <summary>Falha inesperada (5xx).</summary>
    Erro,
}

/// <summary>Os textos do modelo de documentos para a etapa e o nível.</summary>
public static class TextosDaAuditoria
{
    /// <summary>INICIADO ou CONCLUIDO.</summary>
    public static string Texto(this EtapaDaExecucao etapa) => etapa == EtapaDaExecucao.Iniciado ? "INICIADO" : "CONCLUIDO";

    /// <summary>ALERTA ou ERRO.</summary>
    public static string Texto(this NivelDoErro nivel) => nivel == NivelDoErro.Alerta ? "ALERTA" : "ERRO";
}

/// <summary>
/// Um registro de auditoria (RN-178): a etapa da execução, com o traceId que
/// liga as etapas (RN-271), quem, a ação, o alvo e a hora, guardado 5 anos e
/// nunca alterado nem apagado (RNF-206). Sem login, vai sem quem; o que ainda
/// não existe ao chegar (a conta criada) vai só no "CONCLUIDO".
/// </summary>
/// <param name="TraceId">A execução.</param>
/// <param name="Etapa">INICIADO ou CONCLUIDO.</param>
/// <param name="ContaId">Conta do alvo, se houver.</param>
/// <param name="Quem">Quem fez, se houver login.</param>
/// <param name="Acao">O que fez.</param>
/// <param name="Alvo">Sobre quem ou o quê, se houver.</param>
/// <param name="Quando">Quando.</param>
public sealed record RegistroDeAuditoria(
    TraceId TraceId, EtapaDaExecucao Etapa, string? ContaId, string? Quem, AcaoAuditada Acao, string? Alvo, DateTimeOffset Quando)
{
    /// <summary>Por quanto tempo fica guardado.</summary>
    public static readonly int Anos = 5;

    /// <summary>Até quando fica guardado.</summary>
    public DateTimeOffset GuardarAte => Quando.AddYears(Anos);
}

/// <summary>
/// Um registro de erro (RN-270): a recusa ou a falha de uma execução, com o
/// mesmo traceId do "INICIADO", gravado antes de a recusa sair do caso de uso,
/// guardado 5 anos como a auditoria e nunca alterado nem apagado (RNF-206).
/// </summary>
/// <param name="TraceId">A execução.</param>
/// <param name="ContaId">Conta do alvo, se houver.</param>
/// <param name="Quem">Quem fez, se houver login.</param>
/// <param name="Acao">O que tentou fazer.</param>
/// <param name="Alvo">Sobre quem ou o quê, se houver.</param>
/// <param name="Codigo">O código da recusa, como SIGLA_EM_USO, ou ERRO_INESPERADO.</param>
/// <param name="Nivel">ALERTA (recusa prevista) ou ERRO (falha inesperada).</param>
/// <param name="Quando">Quando.</param>
public sealed record RegistroDeErro(
    TraceId TraceId, string? ContaId, string? Quem, AcaoAuditada Acao, string? Alvo, string Codigo, NivelDoErro Nivel, DateTimeOffset Quando)
{
    /// <summary>O código da falha inesperada (proposto no contrato v06, a confirmar no requisito).</summary>
    public const string ErroInesperado = "ERRO_INESPERADO";

    /// <summary>Até quando fica guardado: 5 anos, como a auditoria (RN-178).</summary>
    public DateTimeOffset GuardarAte => Quando.AddYears(RegistroDeAuditoria.Anos);
}
