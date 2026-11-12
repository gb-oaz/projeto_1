using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>
/// Reconta e grava os cards afetados por uma mudança no cadastro, na mesma
/// execução do caso de uso que mudou (RN-278, RN-280): o da conta, o da staff
/// que criou o usuário e, quando a conta mudou, o DEBOAZ.
/// </summary>
public interface IAtualizarCardUsuarios
{
    /// <summary>Reconta e grava.</summary>
    Task Executar(MudancaNoCadastroDto mudanca, CancellationToken ct = default);
}
