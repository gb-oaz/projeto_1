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

    /// <summary>Grava o último acesso do usuário, mostrado em Usuários.</summary>
    Task RegistrarUltimoAcesso(string usuarioId, DateTimeOffset quando, CancellationToken ct = default);
}
