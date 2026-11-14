using Boaz.Core.Hub.Sac.Card.Dtos;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Card.UseCases;

/// <summary>
/// GET /sac/card/ao-vivo (RN-295, 15 RN-279): o card ao abrir e, a cada
/// intervalo, só se a versão mudou.
/// </summary>
public sealed class AcompanharCardDoSac(ILerCardDoSac ler) : IAcompanharCardDoSac
{
    static readonly int[] Intervalos = [5, 15, 30, 60, 300];

    /// <inheritdoc />
    public async Task<(TimeSpan Intervalo, CardDoSacDto Card)> Abrir(string? intervalo, QuemPede quem, CancellationToken ct = default)
    {
        var segundos = intervalo is null ? 300
            : int.TryParse(intervalo, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var s)
                && Intervalos.Contains(s) ? s : throw FalhaDoSac.DadosInvalidos("intervalo");
        return (TimeSpan.FromSeconds(segundos), await ler.Executar(quem, ct));
    }

    /// <inheritdoc />
    public async Task<CardDoSacDto?> Proximo(long versao, QuemPede quem, CancellationToken ct = default)
    {
        var card = await ler.Executar(quem, ct);
        return card.Versao == versao ? null : card;
    }
}
