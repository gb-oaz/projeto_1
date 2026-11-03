using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /contas/{id}/tempo-sessao: o tempo sem uso da conta (RF-114, RN-183).</summary>
public interface ILerTempoDaConta
{
    /// <summary>Lê o tempo.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">CONTA_INEXISTENTE.</exception>
    Task<TempoDaContaLidoDto> Executar(PedidoDeContaDto pedido, CancellationToken ct = default);
}
