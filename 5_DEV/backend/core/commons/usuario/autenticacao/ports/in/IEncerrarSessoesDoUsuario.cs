using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>
/// Para os outros módulos: encerra todas as sessões de um usuário por uma mudança
/// que invalida a credencial — desativação, nova geração de senha, conta
/// indisponível, novo código ou novo nome curto (RF-141, RNF-203).
/// </summary>
public interface IEncerrarSessoesDoUsuario
{
    /// <summary>
    /// Encerra as sessões do usuário com o motivo dado, auditado na execução de
    /// quem chama, ação ENCERRAR_SESSOES_DO_USUARIO e o usuário como alvo (RN-275).
    /// </summary>
    Task Executar(string usuarioId, MotivoFim motivo, ExecucaoDeQuemChama chamada, CancellationToken ct = default);
}
