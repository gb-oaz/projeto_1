using Boaz.Core.Hub.Auditoria.Card.Domain;

namespace Boaz.Core.Hub.Auditoria.Card.Dtos;

/// <summary>Uma linha do card.</summary>
/// <param name="Nome">O que conta.</param>
/// <param name="Valor">O número.</param>
public sealed record LinhaDto(string Nome, int Valor);

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

/// <summary>200 de GET /auditoria/card e o evento "card" do ao vivo (RN-322, 15 RN-276).</summary>
/// <param name="Modulo">MOD-17.</param>
/// <param name="Perfil">DEBOAZ.</param>
/// <param name="Versao">Sobe a cada mudança; o id do evento ao vivo.</param>
/// <param name="AtualizadoEm">A hora da leitura.</param>
/// <param name="Vazio">Tudo no ar e nenhum erro hoje.</param>
/// <param name="Selo">Os serviços fora do ar.</param>
/// <param name="Reduzida">O dado macro.</param>
/// <param name="Normal">Os erros de hoje e o apoio.</param>
/// <param name="Expandida">A lista.</param>
/// <param name="Series">As séries.</param>
public sealed record CardDaAuditoriaDto(
    string Modulo, string Perfil, long Versao, DateTimeOffset AtualizadoEm, bool Vazio, int Selo, ReduzidaDto Reduzida, NormalDto Normal,
    ExpandidaDto Expandida, SeriesDto Series)
{
    /// <summary>Do card.</summary>
    public static CardDaAuditoriaDto De(CardDaAuditoria card)
    {
        var hoje = card.DeHoje;
        var foraDoAr = new LinhaDto("Fora do ar", card.ForaDoAr);
        var interrompidas = new LinhaDto("Interrompidas", hoje.Interrompidas);
        return new CardDaAuditoriaDto(CardDaAuditoria.Modulo, CardDaAuditoria.Perfil, card.Versao, card.Agora, card.Vazio, card.ForaDoAr,
            new ReduzidaDto(card.ForaDoAr, card.Rotulo),
            new NormalDto(hoje.Erros, "erros hoje", [foraDoAr, new LinhaDto("Alertas", hoje.Alertas), interrompidas]),
            new ExpandidaDto([foraDoAr, new LinhaDto("Erros hoje", hoje.Erros), interrompidas]),
            new SeriesDto(card.SerieDoDia, card.SerieDaSemana, card.SerieDoMes));
    }
}
