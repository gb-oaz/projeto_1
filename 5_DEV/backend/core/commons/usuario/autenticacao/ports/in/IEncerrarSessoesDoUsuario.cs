using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>
/// Para os outros módulos: encerra todas as sessões de um usuário por uma mudança
/// que invalida a credencial — desativação, nova geração de senha, conta
/// indisponível, novo código ou novo nome curto (RF-141, RNF-203).
/// </summary>
public interface IEncerrarSessoesDoUsuario
{
    /// <summary>Encerra as sessões do usuário com o motivo dado.</summary>
    Task Executar(string usuarioId, MotivoFim motivo, CancellationToken ct = default);
}
