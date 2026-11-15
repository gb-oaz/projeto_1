using Boaz.Core.Hub.Pagamento.Card.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;

namespace Boaz.Core.Hub.Pagamento.Card.Ports.In;

/// <summary>GET /pagamento/card (RN-307).</summary>
public interface ILerCardDoPagamento
{
    /// <summary>O card do perfil de quem pede.</summary>
    Task<CardDoPagamentoDto> Executar(QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /pagamento/card/ao-vivo (RN-307, 15 RN-279).</summary>
public interface IAcompanharCardDoPagamento
{
    /// <summary>O intervalo conferido (5, 15, 30, 60 ou 300 s; padrão 300) e o primeiro card.</summary>
    Task<(TimeSpan Intervalo, CardDoPagamentoDto Card)> Abrir(string? intervalo, QuemPede quem, CancellationToken ct = default);

    /// <summary>O card, se a versão mudou desde a dada; null se não.</summary>
    Task<CardDoPagamentoDto?> Proximo(long versao, QuemPede quem, CancellationToken ct = default);
}
