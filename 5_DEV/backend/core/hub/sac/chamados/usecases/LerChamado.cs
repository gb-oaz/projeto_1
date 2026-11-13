using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// GET /sac/chamados/{id} (RF-173, RN-288): o chamado do lado de quem pede
/// (a conta sem as notas internas) e, ao abrir, a resposta nova fica lida.
/// Leitura: sem auditoria (17 RN-178).
/// </summary>
public sealed class LerChamado(IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, TimeProvider relogio)
    : ILerChamado
{
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public async Task<ChamadoDto> Executar(string id, QuemPede quem, CancellationToken ct = default)
    {
        var chamado = await conversa.Carregar(id, quem, ct);
        var visao = quem.Visao;
        var naoLido = visao == Visao.Conta ? chamado.Dados.NaoLidoPelaConta : chamado.Dados.NaoLidoPeloTime;
        if (naoLido) await chamados.MarcarLido(chamado.Dados.Id, visao, ct);
        return ChamadoDto.De(chamado, await chamados.Itens(chamado.Dados.Id, null, ct), visao, relogio.GetUtcNow());
    }
}
