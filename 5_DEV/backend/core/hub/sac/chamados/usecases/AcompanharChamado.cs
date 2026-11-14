using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// GET /sac/chamados/{id}/ao-vivo (RNF-230): o acesso como o GET do chamado
/// (RN-288); as mensagens novas, na ordem, assim que o canal avisa (ou na
/// conferência seguinte); a conta nunca recebe nota interna. Na reconexão, o
/// Last-Event-ID (o id da última mensagem) diz de onde seguir.
/// </summary>
public sealed class AcompanharChamado(IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas)
    : IAcompanharChamado
{
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public async Task<ConversaAoVivo> Abrir(string id, string? ultimoId, QuemPede quem, CancellationToken ct = default)
    {
        var chamado = await conversa.Carregar(id, quem, ct);
        var desde = ItemDaConversa.NumeroDe(ultimoId) ?? await chamados.UltimoItem(chamado.Dados.Id, ct);
        return new ConversaAoVivo(chamado.Dados.Id, quem.Visao, desde);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<MensagemDto> Mensagens, ConversaAoVivo Conversa)> Proximas(ConversaAoVivo conversa, TimeSpan espera,
        CancellationToken ct = default)
    {
        var itens = await chamados.Itens(conversa.Id, conversa.Desde, ct);
        if (itens.Count == 0)
        {
            await canal.Esperar(conversa.Id, espera, ct);
            itens = await chamados.Itens(conversa.Id, conversa.Desde, ct);
        }
        if (itens.Count == 0) return ([], conversa);
        var ultimo = itens.Max(i => ItemDaConversa.NumeroDe(i.Id) ?? conversa.Desde);
        var mensagens = itens
            .Where(i => i.Tipo != TipoDeItem.Evento && (conversa.Visao == Visao.Time || i.VisivelParaConta))
            .Select(MensagemDto.De)
            .ToList();
        return (mensagens, conversa with { Desde = ultimo });
    }
}
