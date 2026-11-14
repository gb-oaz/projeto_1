using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Domain;

namespace Boaz.Core.Hub.Sac.Configuracao.Dtos;

/// <summary>O prazo de uma categoria.</summary>
/// <param name="Categoria">A categoria.</param>
/// <param name="HorasUteis">De 1 a 80.</param>
public sealed record PrazoDto(string Categoria, int HorasUteis);

/// <summary>200 de GET e PUT /sac/configuracao (RF-181).</summary>
/// <param name="Prazos">As 6 categorias.</param>
/// <param name="SalvaEm">Quando foram salvos; null no padrão.</param>
/// <param name="SalvaPor">Quem salvou; null no padrão.</param>
public sealed record PrazosDto(IReadOnlyList<PrazoDto> Prazos, DateTimeOffset? SalvaEm, string? SalvaPor)
{
    /// <summary>Dos prazos.</summary>
    public static PrazosDto De(PrazosDoSac prazos) =>
        new([.. Enum.GetValues<Categoria>().Select(c => new PrazoDto(c.Texto(), prazos.HorasDe(c)))], prazos.SalvaEm, prazos.SalvaPor);
}

/// <summary>Um prazo no corpo de PUT /sac/configuracao.</summary>
/// <param name="Categoria">A categoria.</param>
/// <param name="HorasUteis">De 1 a 80.</param>
public sealed record PedidoPrazo(string? Categoria, int? HorasUteis);

/// <summary>O corpo de PUT /sac/configuracao (RF-181): só as categorias que mudam.</summary>
/// <param name="Prazos">Ao menos um.</param>
public sealed record PedidoPrazos(IReadOnlyList<PedidoPrazo?>? Prazos)
{
    /// <summary>Confere a forma: ao menos um prazo, cada um com categoria e horas.</summary>
    public IReadOnlyList<(Categoria Categoria, int Horas)> Validar()
    {
        if (Prazos is not { Count: > 0 }) throw FalhaDoSac.DadosInvalidos("prazos");
        return [.. Prazos.Select(p => (
            Textos.CategoriaDe(p?.Categoria) ?? throw FalhaDoSac.DadosInvalidos("categoria"),
            p!.HorasUteis ?? throw FalhaDoSac.DadosInvalidos("horasUteis")))];
    }
}
