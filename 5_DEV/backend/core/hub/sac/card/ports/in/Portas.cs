using Boaz.Core.Hub.Sac.Card.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Card.Ports.In;

/// <summary>GET /sac/card (RN-295).</summary>
public interface ILerCardDoSac
{
    /// <summary>O card do perfil de quem pede.</summary>
    Task<CardDoSacDto> Executar(QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /sac/card/ao-vivo (RN-295, 15 RN-279).</summary>
public interface IAcompanharCardDoSac
{
    /// <summary>O intervalo conferido (5, 15, 30, 60 ou 300 s; padrão 300) e o primeiro card.</summary>
    Task<(TimeSpan Intervalo, CardDoSacDto Card)> Abrir(string? intervalo, QuemPede quem, CancellationToken ct = default);

    /// <summary>O card, se a versão mudou desde a dada; null se não.</summary>
    Task<CardDoSacDto?> Proximo(long versao, QuemPede quem, CancellationToken ct = default);
}

/// <summary>O fechamento do dia do card do SAC à 00:00 de Brasília (15 RN-278), pelo AgendamentoDoSac.</summary>
public interface IFecharCardDoSacDoDia
{
    /// <summary>Guarda o fim do dia dado de cada escopo.</summary>
    Task Executar(DateOnly dia, CancellationToken ct = default);
}
