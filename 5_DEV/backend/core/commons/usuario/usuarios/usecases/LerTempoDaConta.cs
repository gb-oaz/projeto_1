using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /contas/{id}/tempo-sessao (RF-114, RN-183): a conta master ou o staff da própria conta.</summary>
public sealed class LerTempoDaConta(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio) : ILerTempoDaConta
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<TempoDaContaLidoDto> Executar(PedidoDeContaDto pedido, CancellationToken ct = default)
    {
        var conta = await TempoDaPropriaConta.Conta(apoio, pedido.QuemPede, pedido.ContaId, ct);
        return TempoDaContaLidoDto.De(await contas.TempoDaConta(conta.Id, ct) ?? TempoDaConta.Padrao);
    }
}
