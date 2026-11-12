using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// O fechamento da 00:00 de Brasília (15 RN-278): uma réplica só (a trava do
/// dia) reconta cada card, abre o dia, a semana ISO e o mês de hoje, corta em
/// 400, 106 e 24 e sobe a versao.
/// </summary>
public sealed class FecharCardsDoDia(IRepositorioCardUsuarios cards, IContagemDoCard contagem, ITravaDoFechamento trava, TimeProvider relogio)
    : IFecharCardsDoDia
{
    readonly ContagemDosCards guardados = new(cards, contagem);

    /// <inheritdoc />
    public async Task<bool> Executar(bool sobDemanda = false, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        if (!await trava.Pegar(CalendarioDoCard.Dia(agora), sobDemanda, ct)) return false;
        foreach (var card in await cards.Todos(ct))
        {
            card.FecharDia(await guardados.Contar(card, agora, ct), agora);
            await cards.Salvar(card, ct);
        }
        return true;
    }
}
