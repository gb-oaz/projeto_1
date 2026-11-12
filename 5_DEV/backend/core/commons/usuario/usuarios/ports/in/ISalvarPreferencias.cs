using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>PUT /usuarios/eu/preferencias (RN-283): troca a escolha inteira. Sem auditoria.</summary>
public interface ISalvarPreferencias
{
    /// <summary>Salva e devolve o corpo salvo.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    Task<PreferenciasDto> Executar(NovasPreferenciasDto pedido, CancellationToken ct = default);
}
