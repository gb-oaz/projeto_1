using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /contas/sigla-sugerida (RN-179): as iniciais do nome e se estão livres.</summary>
public sealed class SugerirSigla(IContas contas) : ISugerirSigla
{
    /// <inheritdoc />
    public async Task<SiglaSugeridaDto> Executar(NomeDaContaDto pedido, CancellationToken ct = default)
    {
        var sigla = Sigla.Sugerir(pedido.Nome);
        return new(sigla, sigla is not null && sigla != Sigla.DoTime && !await contas.SiglaEmUso(sigla, ct));
    }
}
