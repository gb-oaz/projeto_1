using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /usuarios/eu/preferencias (RN-283): a escolha salva ou NA_ABERTURA e 300. Sem auditoria.</summary>
public sealed class ObterPreferencias(IPreferencias preferencias) : IObterPreferencias
{
    /// <inheritdoc />
    public async Task<PreferenciasDto> Executar(QuemPedeDto pedido, CancellationToken ct = default) =>
        PreferenciasDto.De(await preferencias.Ler(pedido.QuemPede, ct) ?? PreferenciasDeCards.Padrao);
}
