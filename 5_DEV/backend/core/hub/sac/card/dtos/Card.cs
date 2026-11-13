using Boaz.Core.Hub.Sac.Card.Domain;

namespace Boaz.Core.Hub.Sac.Card.Dtos;

/// <summary>Uma linha do card.</summary>
/// <param name="Nome">O que conta.</param>
/// <param name="Valor">O número.</param>
public sealed record LinhaDto(string Nome, decimal Valor);

/// <summary>O tamanho reduzido: o dado macro.</summary>
/// <param name="Valor">O número.</param>
/// <param name="Rotulo">O que ele conta.</param>
public sealed record ReduzidaDto(int Valor, string Rotulo);

/// <summary>O tamanho normal: o dado macro e os números de apoio.</summary>
/// <param name="Valor">O número.</param>
/// <param name="Rotulo">O que ele conta.</param>
/// <param name="Apoio">3 linhas.</param>
public sealed record NormalDto(int Valor, string Rotulo, IReadOnlyList<LinhaDto> Apoio);

/// <summary>O tamanho expandido: a lista.</summary>
/// <param name="Lista">3 linhas.</param>
public sealed record ExpandidaDto(IReadOnlyList<LinhaDto> Lista);

/// <summary>As séries de dia, semana e mês, 12 números cada, do mais antigo ao atual.</summary>
/// <param name="Dia">12 dias.</param>
/// <param name="Semana">12 semanas.</param>
/// <param name="Mes">12 meses.</param>
public sealed record SeriesDto(IReadOnlyList<int> Dia, IReadOnlyList<int> Semana, IReadOnlyList<int> Mes);

/// <summary>200 de GET /sac/card e o evento "card" do ao vivo (RN-295, 15 RN-276).</summary>
/// <param name="Modulo">MOD-02.</param>
/// <param name="Perfil">DEBOAZ, INSTITUICAO ou AUTONOMO.</param>
/// <param name="Versao">Sobe a cada mudança; o id do evento ao vivo.</param>
/// <param name="AtualizadoEm">A última mudança.</param>
/// <param name="Vazio">Sem nenhum chamado.</param>
/// <param name="Selo">O número do selo.</param>
/// <param name="Reduzida">O dado macro.</param>
/// <param name="Normal">O dado macro e o apoio.</param>
/// <param name="Expandida">A lista.</param>
/// <param name="Series">As séries.</param>
public sealed record CardDoSacDto(
    string Modulo, string Perfil, long Versao, DateTimeOffset AtualizadoEm, bool Vazio, int Selo,
    ReduzidaDto Reduzida, NormalDto Normal, ExpandidaDto Expandida, SeriesDto Series)
{
    /// <summary>Do card.</summary>
    public static CardDoSacDto De(CardDoSac card) =>
        new(CardDoSac.Modulo, card.PerfilTexto, card.Contagem.Versao, card.AtualizadoEm, card.Vazio, card.Selo,
            new ReduzidaDto(card.Contagem.EmAndamento, CardDoSac.Rotulo),
            new NormalDto(card.Contagem.EmAndamento, CardDoSac.Rotulo, Linhas(card.Apoio)),
            new ExpandidaDto(Linhas(card.Lista)),
            new SeriesDto(card.Dia, card.Semana, card.Mes));

    static IReadOnlyList<LinhaDto> Linhas(IReadOnlyList<LinhaDoCard> linhas) => [.. linhas.Select(l => new LinhaDto(l.Nome, l.Valor))];
}
