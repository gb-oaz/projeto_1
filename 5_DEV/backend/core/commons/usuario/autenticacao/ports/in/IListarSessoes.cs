using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>GET /auth/sessoes: os aparelhos conectados de quem pede (RF-156).</summary>
public interface IListarSessoes
{
    /// <summary>As sessões ativas da mesma frente, a de última ação mais recente primeiro.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">As da conferência do token.</exception>
    Task<IReadOnlyList<SessaoListadaDto>> Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default);
}
