using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>PUT /usuarios/eu/preferencias (RN-283): troca a escolha inteira, que acompanha a pessoa em todos os aparelhos. Sem auditoria.</summary>
public sealed class SalvarPreferencias(IPreferencias preferencias) : ISalvarPreferencias
{
    /// <inheritdoc />
    public async Task<PreferenciasDto> Executar(NovasPreferenciasDto pedido, CancellationToken ct = default)
    {
        var escolha = pedido.Validar();
        await preferencias.Salvar(pedido.QuemPede, escolha, ct);
        return PreferenciasDto.De(escolha);
    }
}
