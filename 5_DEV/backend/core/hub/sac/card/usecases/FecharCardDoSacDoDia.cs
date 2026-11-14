using Boaz.Core.Hub.Sac.Card.Domain;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.Core.Hub.Sac.Card.Ports.Out;

namespace Boaz.Core.Hub.Sac.Card.UseCases;

/// <summary>
/// O fechamento do dia (15 RN-278): à 00:00 de Brasília, o card de cada
/// escopo — o time e cada conta com chamado — guarda o fim do dia, de onde
/// saem as séries.
/// </summary>
public sealed class FecharCardDoSacDoDia(IContagemDoSac contagem, ISeriesDoSac series, TimeProvider relogio) : IFecharCardDoSacDoDia
{
    /// <inheritdoc />
    public async Task Executar(DateOnly dia, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        await series.Fechar(CardDoSac.EscopoDoTime, dia, await contagem.Contar(null, agora, ct), ct);
        foreach (var conta in await contagem.ContasComChamado(ct)) await series.Fechar(conta, dia, await contagem.Contar(conta, agora, ct), ct);
    }
}
