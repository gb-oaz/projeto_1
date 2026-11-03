using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /contas/sigla-sugerida, sem login: as iniciais do nome (RN-179).</summary>
public interface ISugerirSigla
{
    /// <summary>Sugere a sigla.</summary>
    Task<SiglaSugeridaDto> Executar(NomeDaContaDto pedido, CancellationToken ct = default);
}
