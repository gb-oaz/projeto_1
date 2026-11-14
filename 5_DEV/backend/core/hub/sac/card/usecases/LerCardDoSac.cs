using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Card.Domain;
using Boaz.Core.Hub.Sac.Card.Dtos;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.Core.Hub.Sac.Card.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Card.UseCases;

/// <summary>
/// GET /sac/card (RN-295): o card do perfil de quem pede — DEBOAZ para o time,
/// INSTITUICAO para a conta master e AUTONOMO para o autônomo — contado agora,
/// com as séries dos dias fechados.
/// </summary>
public sealed class LerCardDoSac(IContagemDoSac contagem, ISeriesDoSac series, TimeProvider relogio) : ILerCardDoSac
{
    /// <inheritdoc />
    public async Task<CardDoSacDto> Executar(QuemPede quem, CancellationToken ct = default)
    {
        var perfil = quem.Visao == Visao.Time ? PerfilDoCard.Deboaz : quem.Papel == "AUTONOMO" ? PerfilDoCard.Autonomo : PerfilDoCard.Instituicao;
        var contaId = perfil == PerfilDoCard.Deboaz ? null : quem.ContaId;
        var agora = relogio.GetUtcNow();
        var numeros = await contagem.Contar(contaId, agora, ct);
        var hoje = CalendarioDoCard.Dia(agora);
        var fechados = await series.Fechados(contaId ?? CardDoSac.EscopoDoTime, hoje.AddMonths(-CalendarioDoCard.NaSerie), ct);
        var (dia, semana, mes) = CardDoSac.Series(fechados, hoje, numeros.EmAndamento);
        return CardDoSacDto.De(new CardDoSac(perfil, numeros, dia, semana, mes, agora));
    }
}
