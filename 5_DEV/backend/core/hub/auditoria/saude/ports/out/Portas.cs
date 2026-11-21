using Boaz.Core.Hub.Auditoria.Saude.Domain;

namespace Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

/// <summary>
/// Verifica um serviço (RN-315): uma sonda por serviço, com até 3 s para responder (RNF-246). O adaptador mede o tempo e
/// diz a falha; o caso de uso só decide o estado.
/// </summary>
public interface ISondaDeServico
{
    /// <summary>O serviço que esta sonda verifica.</summary>
    Servico Servico { get; }

    /// <summary>Verifica o serviço agora.</summary>
    Task<ResultadoDaSonda> Verificar(CancellationToken ct = default);
}

/// <summary>A última verificação desta instância, na memória (RF-194): a saúde responde sem o banco (RNF-252).</summary>
public interface IUltimaVerificacao
{
    /// <summary>Quando a última verificação terminou; null antes da primeira.</summary>
    DateTimeOffset? VerificadoEm { get; }

    /// <summary>Os serviços da última verificação.</summary>
    IReadOnlyList<Verificacao> Servicos { get; }

    /// <summary>Os módulos da última verificação.</summary>
    IReadOnlyList<SaudeDoModulo> Modulos { get; }

    /// <summary>O banco de dados está Fora do ar na última verificação (RNF-252).</summary>
    bool BancoForaDoAr { get; }

    /// <summary>A última verificação do serviço; null antes da primeira.</summary>
    Verificacao? De(Servico servico);

    /// <summary>A última saúde do módulo; null antes da primeira.</summary>
    SaudeDoModulo? DoModulo(string modulo);

    /// <summary>Guarda a verificação que terminou.</summary>
    void Guardar(DateTimeOffset em, IReadOnlyList<Verificacao> servicos, IReadOnlyList<SaudeDoModulo> modulos);
}

/// <summary>Um ponto do histórico de um serviço: o tempo médio e as falhas de uma hora ou de um dia (RF-195).</summary>
/// <param name="Inicio">O começo da hora ou do dia.</param>
/// <param name="TempoMedioMs">O tempo médio das verificações com resposta; null sem nenhuma.</param>
/// <param name="Falhas">Quantas verificações falharam.</param>
public sealed record PontoDoHistorico(DateTimeOffset Inicio, int? TempoMedioMs, int Falhas);

/// <summary>As coleções verificacoesSaude e quedasServico (RF-195, RNF-248): guardadas 31 dias.</summary>
public interface IHistoricoDaSaude
{
    /// <summary>Guarda as verificações, uma por serviço.</summary>
    Task Gravar(IReadOnlyList<Verificacao> verificacoes, CancellationToken ct = default);

    /// <summary>Abre a queda de um serviço.</summary>
    Task AbrirQueda(Queda queda, CancellationToken ct = default);

    /// <summary>Fecha a queda aberta do serviço.</summary>
    Task FecharQueda(Servico servico, DateTimeOffset fim, CancellationToken ct = default);

    /// <summary>As quedas que tocam o período, da mais nova para a mais antiga.</summary>
    Task<IReadOnlyList<Queda>> Quedas(Servico servico, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default);

    /// <summary>O tempo médio e as falhas de cada passo do período, do mais antigo ao atual, com os passos sem verificação.</summary>
    Task<IReadOnlyList<PontoDoHistorico>> Serie(Servico servico, DateTimeOffset de, TimeSpan passo, int pontos, CancellationToken ct = default);
}

/// <summary>As instâncias no ar (auditoria:instancia:&lt;id&gt;, TTL 90 s): cada uma se anuncia a cada verificação.</summary>
public interface IInstancias
{
    /// <summary>Renova o sinal desta instância.</summary>
    Task Anunciar(Instancia instancia, CancellationToken ct = default);

    /// <summary>As instâncias com sinal nos últimos 90 s.</summary>
    Task<IReadOnlyList<Instancia>> Listar(CancellationToken ct = default);
}

/// <summary>A trava entre as instâncias (auditoria:agendamento): só uma guarda a verificação, fecha o card e descarta o vencido.</summary>
public interface ITravaDaAuditoria
{
    /// <summary>Tenta pegar a trava da tarefa pelo prazo; false se outra instância a tem.</summary>
    Task<bool> Tentar(string tarefa, TimeSpan prazo, CancellationToken ct = default);
}

/// <summary>Quem é esta instância: o nome, a versão e quando subiu.</summary>
/// <param name="Id">O nome (o do contêiner).</param>
/// <param name="Versao">A versão da aplicação.</param>
/// <param name="Inicio">Quando subiu.</param>
/// <param name="Ambiente">PRODUCAO ou HOMOLOGACAO.</param>
public sealed record IdentidadeDaInstancia(string Id, string Versao, DateTimeOffset Inicio, string Ambiente);
