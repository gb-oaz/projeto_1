using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// O pedido de senha sem login (17 RF-111, 18 RF-179): vira, na criação, o
/// chamado SENHA_CONTA_MASTER do time, Aberto, sem conta e sem conversa, com o
/// prazo da categoria. O número do chamado é o do pedido ("ajuda-000087" =
/// "SAC-000087"). Auditado pelo 17, dentro de ESQUECI_SENHA_MASTER.
/// </summary>
public sealed class RegistrarPedidoDeSenha(
    IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas, IPrazosDoSac prazos) : IRegistrarPedidoDeSenha
{
    readonly Conversa conversa = new(chamados, numeracao, canal, pessoas);

    /// <inheritdoc />
    public async Task<NumeroDoChamado> Executar(string aparelho, string instituicao, string contato, DateTimeOffset quando,
        CancellationToken ct = default)
    {
        var numero = new NumeroDoChamado(await numeracao.Proximo(INumeracao.Chamados, ct));
        var chamado = Chamado.DoPedidoDeSenha(numero, instituicao, contato, aparelho, await prazos.HorasUteis(Categoria.SenhaContaMaster, ct),
            quando);
        await conversa.Incluir(chamado, ct);
        return numero;
    }
}
