using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Domain;

namespace Boaz.Core.Hub.Auditoria.Ocorrencias.Dtos;

/// <summary>Uma ocorrência (RF-204, RF-205): a mudança de estado de um serviço ou de um módulo.</summary>
/// <param name="Id">O id.</param>
/// <param name="Tipo">SERVICO ou MODULO.</param>
/// <param name="Alvo">O id do serviço ou do módulo.</param>
/// <param name="Nome">O nome.</param>
/// <param name="De">O estado de antes.</param>
/// <param name="Para">O estado de agora.</param>
/// <param name="Inicio">Quando o alvo saiu de NO_AR.</param>
/// <param name="Quando">Quando a mudança foi vista.</param>
/// <param name="Falha">Por que falhou.</param>
/// <param name="DuracaoMin">Quanto durou, só na volta a NO_AR.</param>
public sealed record OcorrenciaDto(
    string Id, string Tipo, string Alvo, string Nome, string De, string Para, DateTimeOffset Inicio, DateTimeOffset Quando, string? Falha, int? DuracaoMin)
{
    /// <summary>Da ocorrência.</summary>
    public static OcorrenciaDto Da(Ocorrencia o) =>
        new(o.Id, o.Tipo.Texto(), o.Alvo, o.Nome, o.De.Texto(), o.Para.Texto(), o.Inicio, o.Quando, o.Falha?.Texto(), o.DuracaoMin);
}

/// <summary>200 de GET /auditoria/ocorrencias/abertas e o evento "abertas" do ao vivo (RF-204).</summary>
/// <param name="Abertas">A última mudança de cada alvo fora de NO_AR, FORA_DO_AR primeiro e a mais nova primeiro.</param>
/// <param name="Voltaram">As voltas a NO_AR dos últimos 10 min, a mais nova primeiro.</param>
public sealed record AbertasDto(IReadOnlyList<OcorrenciaDto> Abertas, IReadOnlyList<OcorrenciaDto> Voltaram);

/// <summary>200 de GET /auditoria/ocorrencias (RF-205).</summary>
/// <param name="De">O começo.</param>
/// <param name="Ate">O fim.</param>
/// <param name="Itens">As ocorrências, da mais nova para a mais antiga.</param>
/// <param name="Proximo">De onde a página seguinte começa; null na última.</param>
public sealed record OcorrenciasDto(DateTimeOffset De, DateTimeOffset Ate, IReadOnlyList<OcorrenciaDto> Itens, string? Proximo);
