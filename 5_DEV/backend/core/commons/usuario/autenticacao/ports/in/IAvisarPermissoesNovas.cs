using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>
/// Para a Autorização: as permissões do usuário mudaram para a versão
/// <c>pv</c>. As sessões continuam, mas o token antigo passa a receber
/// PERMISSOES_DESATUALIZADAS até ser renovado (RN-230).
/// </summary>
public interface IAvisarPermissoesNovas
{
    /// <summary>
    /// Grava a versão nova em todas as sessões do usuário, auditado na execução
    /// de quem chama, ação AVISAR_PERMISSOES_NOVAS e o usuário como alvo (RN-275).
    /// </summary>
    Task Executar(string usuarioId, int pv, ExecucaoDeQuemChama chamada, CancellationToken ct = default);
}
