using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>GET /auth/chaves: as chaves públicas aceitas, sem autenticação (RNF-207, RNF-218).</summary>
public interface ILerChavesPublicas
{
    /// <summary>As chaves no formato JWKS.</summary>
    Task<ChavesPublicasDto> Executar(CancellationToken ct = default);
}
