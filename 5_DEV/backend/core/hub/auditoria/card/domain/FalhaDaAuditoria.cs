using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Hub.Auditoria.Card.Domain;

/// <summary>Grupo de uma recusa da Auditoria, para o adaptador de entrada escolher o status HTTP.</summary>
public enum GrupoDaFalhaDaAuditoria
{
    /// <summary>403: só o administrador e o membro do time com MOD-17 · U usam a Auditoria (RN-314).</summary>
    Proibido,
    /// <summary>404: o serviço ou a execução não existe.</summary>
    NaoEncontrado,
    /// <summary>422: a forma do pedido.</summary>
    DadosInvalidos,
    /// <summary>503: o banco de dados está fora e a leitura depende dele (RNF-252).</summary>
    Indisponivel,
}

/// <summary>
/// Uma recusa da Auditoria, com o código e a mensagem do contrato (auditoria-contrato-api v04, formato-comum).
/// O adaptador de entrada a transforma em { codigo, mensagem, campo }.
/// </summary>
public sealed class FalhaDaAuditoria : Exception, IRecusaPrevista
{
    FalhaDaAuditoria(GrupoDaFalhaDaAuditoria grupo, string codigo, string mensagem) : base(mensagem) => (Grupo, Codigo) = (grupo, codigo);

    /// <summary>O grupo, que decide o status HTTP.</summary>
    public GrupoDaFalhaDaAuditoria Grupo { get; }

    /// <summary>O código do contrato, como PERIODO_LONGO.</summary>
    public string Codigo { get; }

    /// <summary>O campo errado, em DADOS_INVALIDOS, PERIODO_LONGO e PERIODO_INVERTIDO.</summary>
    public string? Campo { get; private init; }

    /// <summary>Sem o papel, sem o módulo ou sem a aba (RN-314).</summary>
    public static FalhaDaAuditoria AcessoNegado() =>
        new(GrupoDaFalhaDaAuditoria.Proibido, "ACESSO_NEGADO", "Você não tem permissão para esta ação.");

    /// <summary>O serviço pedido não é um dos 8 (RF-195).</summary>
    public static FalhaDaAuditoria ServicoInexistente() =>
        new(GrupoDaFalhaDaAuditoria.NaoEncontrado, "SERVICO_INEXISTENTE", "Serviço não encontrado.");

    /// <summary>Nenhum registro tem o traceId pedido (RF-202).</summary>
    public static FalhaDaAuditoria ExecucaoInexistente() =>
        new(GrupoDaFalhaDaAuditoria.NaoEncontrado, "EXECUCAO_INEXISTENTE", "Execução não encontrada.");

    /// <summary>Um campo do pedido está faltando ou é inválido.</summary>
    public static FalhaDaAuditoria DadosInvalidos(string campo) =>
        new(GrupoDaFalhaDaAuditoria.DadosInvalidos, "DADOS_INVALIDOS", $"O campo \"{campo}\" está faltando ou é inválido.") { Campo = campo };

    /// <summary>O período passa do máximo da consulta: 31 dias nos registros e nas ocorrências, 7 nas execuções (RN-321).</summary>
    public static FalhaDaAuditoria PeriodoLongo(int maximoDeDias) =>
        new(GrupoDaFalhaDaAuditoria.DadosInvalidos, "PERIODO_LONGO", $"Escolha um período de até {maximoDeDias} dias.") { Campo = "ate" };

    /// <summary>O fim não é depois do começo (RN-321).</summary>
    public static FalhaDaAuditoria PeriodoInvertido() =>
        new(GrupoDaFalhaDaAuditoria.DadosInvalidos, "PERIODO_INVERTIDO", "O fim deve ser depois do começo.") { Campo = "ate" };

    /// <summary>O banco de dados está fora do ar e a leitura depende dele (RNF-252).</summary>
    public static FalhaDaAuditoria BancoDeDadosFora() =>
        new(GrupoDaFalhaDaAuditoria.Indisponivel, "BANCO_DE_DADOS_FORA", "Os registros dependem do banco de dados, fora do ar.");
}
