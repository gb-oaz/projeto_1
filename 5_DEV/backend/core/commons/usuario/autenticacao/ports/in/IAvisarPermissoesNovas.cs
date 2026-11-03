namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>
/// Para a Autorização: as permissões do usuário mudaram para a versão
/// <c>pv</c>. As sessões continuam, mas o token antigo passa a receber
/// PERMISSOES_DESATUALIZADAS até ser renovado (RN-230).
/// </summary>
public interface IAvisarPermissoesNovas
{
    /// <summary>Grava a versão nova em todas as sessões do usuário.</summary>
    Task Executar(string usuarioId, int pv, CancellationToken ct = default);
}
