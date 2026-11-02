using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>GET /usuarios/{id}/permissoes: o que a pessoa tem e o que quem pede pode conceder (RF-160).</summary>
public interface ILerPermissoes
{
    /// <summary>Lê as permissões.</summary>
    /// <exception cref="Domain.FalhaDeAutorizacao">USUARIO_INEXISTENTE ou ACESSO_NEGADO.</exception>
    Task<PermissoesLidasDto> Executar(PedidoDePermissoesDto pedido, CancellationToken ct = default);
}
