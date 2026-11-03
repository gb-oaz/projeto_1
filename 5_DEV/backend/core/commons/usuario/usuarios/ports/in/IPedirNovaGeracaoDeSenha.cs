using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/{id}/nova-geracao-senha: o superior pede uma senha nova (RF-110, RNF-203).</summary>
public interface IPedirNovaGeracaoDeSenha
{
    /// <summary>Pede a geração.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">USUARIO_INEXISTENTE, ACESSO_NEGADO ou SEM_SENHA.</exception>
    Task<CodigoDeLiberacaoDto> Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default);
}
