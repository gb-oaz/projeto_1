using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>
/// O fim das sessões abertas, pela autenticação (IEncerrarSessoesDoUsuario):
/// desativação, nova geração de senha, código ou nome curto novo e conta fora de
/// ATIVA (RF-141, RNF-203, RN-175, RN-245).
/// </summary>
public interface ISessoesDosUsuarios
{
    /// <summary>Encerra as sessões do usuário.</summary>
    Task EncerrarDoUsuario(string usuarioId, MotivoFim motivo, CancellationToken ct = default);

    /// <summary>Encerra as sessões de todos os usuários da conta.</summary>
    Task EncerrarDaConta(string contaId, MotivoFim motivo, CancellationToken ct = default);
}
