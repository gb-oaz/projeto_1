using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /usuarios/eu/preferencias (RN-283): sem escolha salva, NA_ABERTURA e 300. Sem auditoria.</summary>
public interface IObterPreferencias
{
    /// <summary>Lê a escolha de quem pede.</summary>
    Task<PreferenciasDto> Executar(QuemPedeDto pedido, CancellationToken ct = default);
}
