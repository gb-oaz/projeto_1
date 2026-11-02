using Boaz.Core.Commons.Usuario.Autorizacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

/// <summary>O registro de módulos (15_modulos, coleção modulos): as chaves de mods vêm daqui.</summary>
public interface IModulos
{
    /// <summary>Todos os módulos registrados que não foram retirados.</summary>
    Task<IReadOnlyList<ModuloRegistrado>> Registrados(CancellationToken ct = default);
}
