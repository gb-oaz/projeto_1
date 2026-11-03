using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>GET /usuarios/{id}/permissoes/historico: o registro de mudanças, página de 50 (RN-256).</summary>
public interface ILerHistoricoDePermissoes
{
    /// <summary>As mudanças, da mais nova para a mais antiga.</summary>
    /// <exception cref="Domain.FalhaDeAutorizacao">USUARIO_INEXISTENTE ou ACESSO_NEGADO.</exception>
    Task<IReadOnlyList<MudancaDto>> Executar(PedidoDePermissoesDto pedido, CancellationToken ct = default);
}
