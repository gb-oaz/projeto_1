using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>As chaves públicas aceitas, em JWKS, para quem confere os tokens (RNF-207, RNF-218).</summary>
public sealed class LerChavesPublicas(IAssinador assinador) : ILerChavesPublicas
{
    /// <inheritdoc />
    public async Task<ChavesPublicasDto> Executar(CancellationToken ct = default) =>
        new((await assinador.ChavesAceitas(ct)).Select(c => new ChaveJwkDto(c.Kid, "RSA", "RS256", "sig", c.N, c.E)).ToList());
}
