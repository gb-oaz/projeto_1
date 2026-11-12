using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>usuarios.preferencias (RN-283).</summary>
public interface IPreferencias
{
    /// <summary>A escolha salva; null se nunca salvou.</summary>
    Task<PreferenciasDeCards?> Ler(string usuarioId, CancellationToken ct = default);

    /// <summary>Troca a escolha inteira.</summary>
    Task Salvar(string usuarioId, PreferenciasDeCards preferencias, CancellationToken ct = default);
}
