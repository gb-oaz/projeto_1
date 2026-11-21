using Boaz.Core.Hub.Auditoria.Card.Domain;

namespace Boaz.Core.Hub.Auditoria.Saude.Domain;

/// <summary>
/// Um dos 8 serviços que a Auditoria verifica a cada 30 s (RF-194, RN-315). Os agendamentos não respondem a uma
/// chamada: dizem a hora da última rodada.
/// </summary>
/// <param name="Id">O identificador, como "banco".</param>
/// <param name="Nome">O nome mostrado, como "Banco de dados".</param>
/// <param name="Agendamento">É um agendamento: o estado vem da idade da última rodada.</param>
public sealed record Servico(string Id, string Nome, bool Agendamento = false)
{
    /// <summary>A própria aplicação.</summary>
    public static readonly Servico Aplicacao = new("aplicacao", "Aplicação");

    /// <summary>O banco de dados.</summary>
    public static readonly Servico Banco = new("banco", "Banco de dados");

    /// <summary>O cache.</summary>
    public static readonly Servico Cache = new("cache", "Cache");

    /// <summary>O armazenamento dos anexos.</summary>
    public static readonly Servico ArmazenamentoAnexos = new("armazenamento-anexos", "Armazenamento de anexos");

    /// <summary>A verificação de segurança dos anexos.</summary>
    public static readonly Servico VerificacaoAnexos = new("verificacao-anexos", "Verificação de anexos");

    /// <summary>O intermediador de pagamento.</summary>
    public static readonly Servico IntermediadorPagamento = new("intermediador-pagamento", "Intermediador de pagamento");

    /// <summary>O agendamento do SAC.</summary>
    public static readonly Servico AgendamentoSac = new("agendamento-sac", "Agendamento do SAC", true);

    /// <summary>O agendamento do Pagamento.</summary>
    public static readonly Servico AgendamentoPagamento = new("agendamento-pagamento", "Agendamento do Pagamento", true);

    /// <summary>Os 8 serviços, na ordem do requisito.</summary>
    public static IReadOnlyList<Servico> Todos { get; } =
        [Aplicacao, Banco, Cache, ArmazenamentoAnexos, VerificacaoAnexos, IntermediadorPagamento, AgendamentoSac, AgendamentoPagamento];

    /// <summary>O serviço pelo id; 404 SERVICO_INEXISTENTE se não for um dos 8.</summary>
    public static Servico DeId(string? id) =>
        Todos.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal)) ?? throw FalhaDaAuditoria.ServicoInexistente();
}

/// <summary>O estado de um serviço depois de uma verificação (RN-315).</summary>
public enum EstadoDoServico
{
    /// <summary>Respondeu em até 1 s.</summary>
    NoAr,
    /// <summary>Respondeu em até 3 s ou falhou a 1ª e a 2ª vez seguidas.</summary>
    Lento,
    /// <summary>Sem resposta em 3 s ou a 3ª falha seguida.</summary>
    ForaDoAr,
    /// <summary>Ainda sem verificação (o agendamento que nunca rodou).</summary>
    SemVerificacao,
    /// <summary>Desligado neste ambiente.</summary>
    Desligado,
}

/// <summary>Por que a verificação falhou (RF-194).</summary>
public enum FalhaDaVerificacao
{
    /// <summary>Nada respondeu em 3 s.</summary>
    SemResposta,
    /// <summary>A conexão foi recusada.</summary>
    ConexaoRecusada,
    /// <summary>Respondeu com erro.</summary>
    RespostaComErro,
}

/// <summary>O que uma sonda viu num serviço: o tempo, a falha, a última rodada (agendamentos) ou que ele está desligado.</summary>
/// <param name="TempoMs">O tempo da resposta; null sem resposta e nos agendamentos.</param>
/// <param name="Falha">Por que falhou; null se respondeu.</param>
/// <param name="UltimaExecucao">A última rodada, nos agendamentos; null se nunca rodou.</param>
/// <param name="Desligado">O serviço está desligado neste ambiente.</param>
public sealed record ResultadoDaSonda(int? TempoMs = null, FalhaDaVerificacao? Falha = null, DateTimeOffset? UltimaExecucao = null, bool Desligado = false)
{
    /// <summary>Respondeu no tempo dado.</summary>
    public static ResultadoDaSonda Respondeu(int tempoMs) => new(tempoMs);

    /// <summary>Falhou.</summary>
    public static ResultadoDaSonda Falhou(FalhaDaVerificacao falha, int? tempoMs = null) => new(tempoMs, falha);
}

/// <summary>Os textos dos estados e das falhas, como no contrato.</summary>
public static class TextosDaSaude
{
    /// <summary>NO_AR, LENTO, FORA_DO_AR, SEM_VERIFICACAO ou DESLIGADO.</summary>
    public static string Texto(this EstadoDoServico estado) => estado switch
    {
        EstadoDoServico.NoAr => "NO_AR",
        EstadoDoServico.Lento => "LENTO",
        EstadoDoServico.ForaDoAr => "FORA_DO_AR",
        EstadoDoServico.SemVerificacao => "SEM_VERIFICACAO",
        _ => "DESLIGADO",
    };

    /// <summary>SEM_RESPOSTA, CONEXAO_RECUSADA ou RESPOSTA_COM_ERRO.</summary>
    public static string Texto(this FalhaDaVerificacao falha) => falha switch
    {
        FalhaDaVerificacao.SemResposta => "SEM_RESPOSTA",
        FalhaDaVerificacao.ConexaoRecusada => "CONEXAO_RECUSADA",
        _ => "RESPOSTA_COM_ERRO",
    };

    /// <summary>A falha pelo texto gravado; null se não for nenhuma.</summary>
    public static FalhaDaVerificacao? FalhaDe(string? texto) =>
        Enum.GetValues<FalhaDaVerificacao>().Cast<FalhaDaVerificacao?>().FirstOrDefault(f => f!.Value.Texto() == texto);

    /// <summary>O estado pelo texto gravado; null se não for nenhum.</summary>
    public static EstadoDoServico? EstadoDe(string? texto) =>
        Enum.GetValues<EstadoDoServico>().Cast<EstadoDoServico?>().FirstOrDefault(e => e!.Value.Texto() == texto);

    /// <summary>A posição na lista de serviços: FORA_DO_AR, LENTO, SEM_VERIFICACAO, NO_AR e DESLIGADO (RF-194).</summary>
    public static int Ordem(this EstadoDoServico estado) => estado switch
    {
        EstadoDoServico.ForaDoAr => 0,
        EstadoDoServico.Lento => 1,
        EstadoDoServico.SemVerificacao => 2,
        EstadoDoServico.NoAr => 3,
        _ => 4,
    };
}
