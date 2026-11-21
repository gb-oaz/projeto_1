using Boaz.Core.Hub.Auditoria.Saude.Domain;

namespace Boaz.Core.Hub.Auditoria.Saude.Dtos;

/// <summary>Um serviço em GET /auditoria/saude (RF-194).</summary>
/// <param name="Id">O identificador, como "banco".</param>
/// <param name="Nome">O nome.</param>
/// <param name="Estado">NO_AR, LENTO, FORA_DO_AR, SEM_VERIFICACAO ou DESLIGADO.</param>
/// <param name="Desde">Desde quando está neste estado.</param>
/// <param name="TempoMs">O tempo da resposta; null sem resposta.</param>
/// <param name="Falha">SEM_RESPOSTA, CONEXAO_RECUSADA ou RESPOSTA_COM_ERRO; null se respondeu.</param>
/// <param name="UltimaExecucao">A última rodada, nos agendamentos.</param>
public sealed record ServicoDto(string Id, string Nome, string Estado, DateTimeOffset Desde, int? TempoMs, string? Falha, DateTimeOffset? UltimaExecucao)
{
    /// <summary>Da verificação.</summary>
    public static ServicoDto De(Verificacao v) =>
        new(v.Servico.Id, v.Servico.Nome, v.Estado.Texto(), v.Desde, v.TempoMs, v.Falha?.Texto(), v.UltimaExecucao);
}

/// <summary>Um módulo em GET /auditoria/saude (RF-196).</summary>
/// <param name="Id">MOD-01…</param>
/// <param name="Nome">O nome.</param>
/// <param name="Estado">NO_AR, LENTO, FORA_DO_AR ou SEM_PEDIDOS.</param>
/// <param name="Pedidos">Pedidos nos últimos 5 min.</param>
/// <param name="Erros5xx">Com 5xx.</param>
/// <param name="TaxaErro5xx">Os 5xx sobre os pedidos, em %; null sem pedido.</param>
/// <param name="P95Ms">O p95; null sem pedido.</param>
public sealed record ModuloDto(string Id, string Nome, string Estado, long Pedidos, long Erros5xx, decimal? TaxaErro5xx, int? P95Ms)
{
    /// <summary>Da saúde do módulo.</summary>
    public static ModuloDto De(SaudeDoModulo m) => new(m.Modulo, m.Nome, m.Estado.Texto(), m.Pedidos, m.Erros5xx, m.TaxaErro5xx, m.P95Ms);
}

/// <summary>Uma instância em GET /auditoria/saude.</summary>
/// <param name="Id">O nome.</param>
/// <param name="Versao">A versão.</param>
/// <param name="Inicio">Quando subiu.</param>
/// <param name="UltimoSinal">O último sinal.</param>
public sealed record InstanciaDto(string Id, string Versao, DateTimeOffset Inicio, DateTimeOffset UltimoSinal);

/// <summary>200 de GET /auditoria/saude e o evento "saude" do ao vivo (RF-194, RF-196).</summary>
/// <param name="Ambiente">PRODUCAO ou HOMOLOGACAO.</param>
/// <param name="VerificadoEm">Quando a verificação terminou.</param>
/// <param name="Geral">FORA_DO_AR, LENTO ou NO_AR.</param>
/// <param name="ForaDoAr">Quantos serviços estão fora do ar.</param>
/// <param name="Lentos">Quantos estão lentos.</param>
/// <param name="Servicos">Os 8 serviços: FORA_DO_AR, LENTO, SEM_VERIFICACAO, NO_AR e DESLIGADO.</param>
/// <param name="Modulos">Os módulos: FORA_DO_AR, LENTO, NO_AR e SEM_PEDIDOS.</param>
/// <param name="Instancias">As instâncias no ar.</param>
public sealed record SaudeDto(
    string Ambiente, DateTimeOffset VerificadoEm, string Geral, int ForaDoAr, int Lentos, IReadOnlyList<ServicoDto> Servicos,
    IReadOnlyList<ModuloDto> Modulos, IReadOnlyList<InstanciaDto> Instancias)
{
    /// <summary>Da saúde: serviços e módulos na ordem do contrato.</summary>
    public static SaudeDto De(SaudeDoSistema s) =>
        new(s.Ambiente, s.VerificadoEm, s.Geral.Texto(), s.ForaDoAr, s.Lentos,
            [.. s.Servicos.Select((v, i) => (v, i)).OrderBy(p => p.v.Estado.Ordem()).ThenBy(p => p.i).Select(p => ServicoDto.De(p.v))],
            [.. s.Modulos.OrderBy(m => m.Ordem).ThenBy(m => m.Modulo, StringComparer.Ordinal).Select(ModuloDto.De)],
            [.. s.Instancias.Select(i => new InstanciaDto(i.Id, i.Versao, i.Inicio, i.UltimoSinal))]);
}

/// <summary>Uma queda em GET /auditoria/saude/{servico}/historico (RF-195).</summary>
/// <param name="Inicio">Quando começou.</param>
/// <param name="Fim">Quando voltou; null com a queda aberta.</param>
/// <param name="DuracaoMin">Quantos minutos durou, até agora se aberta.</param>
/// <param name="Falha">Por que caiu.</param>
public sealed record QuedaDto(DateTimeOffset Inicio, DateTimeOffset? Fim, int DuracaoMin, string Falha);

/// <summary>Um ponto da série do histórico (RF-195).</summary>
/// <param name="Inicio">O começo da hora ou do dia.</param>
/// <param name="TempoMedioMs">O tempo médio; null sem verificação com resposta.</param>
/// <param name="Falhas">Quantas verificações falharam.</param>
public sealed record PontoDaSerieDto(DateTimeOffset Inicio, int? TempoMedioMs, int Falhas);

/// <summary>200 de GET /auditoria/saude/{servico}/historico (RF-195).</summary>
/// <param name="Servico">O id.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Periodo">24h, 7d ou 30d.</param>
/// <param name="De">O começo do período.</param>
/// <param name="Ate">O fim: agora.</param>
/// <param name="Disponibilidade">Em %, com 2 casas.</param>
/// <param name="Quedas">As quedas do período, da mais nova para a mais antiga.</param>
/// <param name="Serie">24 pontos por hora, ou 7 ou 30 por dia, do mais antigo ao atual.</param>
public sealed record HistoricoDto(
    string Servico, string Nome, string Periodo, DateTimeOffset De, DateTimeOffset Ate, decimal Disponibilidade, IReadOnlyList<QuedaDto> Quedas,
    IReadOnlyList<PontoDaSerieDto> Serie);
