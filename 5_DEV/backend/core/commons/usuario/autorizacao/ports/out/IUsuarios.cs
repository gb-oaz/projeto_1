using Boaz.Core.Commons.Usuario.Autorizacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

/// <summary>Leitura dos usuários (coleção usuarios), com o papel e quem criou cada um.</summary>
public interface IUsuarios
{
    /// <summary>O usuário pelo id, de qualquer conta; null se não existir.</summary>
    Task<UsuarioAutorizado?> Buscar(string id, CancellationToken ct = default);
}
