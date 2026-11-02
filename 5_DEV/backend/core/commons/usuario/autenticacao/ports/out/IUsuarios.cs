using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>
/// Leitura dos usuários (coleção usuarios). O administrador do deboaz, cuja
/// senha fica no Key Vault (RN-190), também chega por aqui: o adaptador o monta.
/// </summary>
public interface IUsuarios
{
    /// <summary>O usuário dono da entrada, já em minúsculas; null se não existir.</summary>
    Task<UsuarioDaEntrada?> BuscarPorEntrada(string entrada, CancellationToken ct = default);

    /// <summary>O participante dono do código de acesso, já em maiúsculas; null se não existir.</summary>
    Task<UsuarioDaEntrada?> BuscarPorCodigoDeAcesso(string codigo, CancellationToken ct = default);

    /// <summary>O usuário pelo id; null se não existir.</summary>
    Task<UsuarioDaEntrada?> Buscar(string id, CancellationToken ct = default);

    /// <summary>Grava o último acesso do usuário e quem entrou, mostrados em Usuários (RN-186).</summary>
    /// <param name="usuarioId">O usuário que entrou.</param>
    /// <param name="quando">A hora da entrada.</param>
    /// <param name="quem">O parentesco do responsável que entrou no quest ("MAE"); null quando foi o próprio usuário.</param>
    /// <param name="ct">Cancelamento.</param>
    Task RegistrarUltimoAcesso(string usuarioId, DateTimeOffset quando, string? quem, CancellationToken ct = default);
}
