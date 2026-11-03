using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /contas/{id}: só o cadastro da conta (RN-187).</summary>
public interface ILerConta
{
    /// <summary>Lê a conta.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO ou CONTA_INEXISTENTE.</exception>
    Task<ContaLidaDto> Executar(PedidoDeContaDto pedido, CancellationToken ct = default);
}
