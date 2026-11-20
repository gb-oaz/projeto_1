namespace Boaz.Core.Hub.Pagamento.Card.Dtos;

/// <summary>Uma linha do card: o nome e o número (em reais, em % ou uma contagem).</summary>
/// <param name="Nome">Vencidas.</param>
/// <param name="Valor">0.</param>
public sealed record LinhaDoCardDto(string Nome, decimal Valor);

/// <summary>O tamanho reduzido: o dado macro.</summary>
/// <param name="Valor">3698.33.</param>
/// <param name="Rotulo">a pagar.</param>
public sealed record ReduzidaDoCardDto(decimal Valor, string Rotulo);

/// <summary>O tamanho normal: o dado macro e os números de apoio.</summary>
/// <param name="Valor">3698.33.</param>
/// <param name="Rotulo">Vence em 10/12.</param>
/// <param name="Apoio">2 ou 3 linhas.</param>
public sealed record NormalDoCardDto(decimal Valor, string Rotulo, IReadOnlyList<LinhaDoCardDto> Apoio);

/// <summary>O tamanho expandido: a lista.</summary>
/// <param name="Lista">3 linhas.</param>
public sealed record ExpandidaDoCardDto(IReadOnlyList<LinhaDoCardDto> Lista);

/// <summary>As séries de dia, semana e mês, 12 números cada, do mais antigo ao atual.</summary>
/// <param name="Dia">12 dias.</param>
/// <param name="Semana">12 semanas.</param>
/// <param name="Mes">12 meses.</param>
public sealed record SeriesDoCardDto(IReadOnlyList<decimal> Dia, IReadOnlyList<decimal> Semana, IReadOnlyList<decimal> Mes);

/// <summary>200 de GET /pagamento/card e o evento "card" do ao vivo (RN-307, 15 RN-276).</summary>
/// <param name="Modulo">MOD-01.</param>
/// <param name="Perfil">DEBOAZ, INSTITUICAO ou AUTONOMO.</param>
/// <param name="Versao">Muda a cada mudança das faturas; o id do evento ao vivo.</param>
/// <param name="AtualizadoEm">A última mudança das faturas.</param>
/// <param name="Vazio">Sem nenhuma fatura.</param>
/// <param name="Selo">O número do selo.</param>
/// <param name="Reduzida">O dado macro.</param>
/// <param name="Normal">O dado macro e o apoio.</param>
/// <param name="Expandida">A lista.</param>
/// <param name="Series">As séries.</param>
public sealed record CardDoPagamentoDto(
    string Modulo, string Perfil, long Versao, DateTimeOffset AtualizadoEm, bool Vazio, int Selo,
    ReduzidaDoCardDto Reduzida, NormalDoCardDto Normal, ExpandidaDoCardDto Expandida, SeriesDoCardDto Series);
