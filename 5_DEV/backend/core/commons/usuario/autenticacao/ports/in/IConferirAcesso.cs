using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>
/// A conferência de cada pedido (RNF-216): assinatura, validade, frente, sessão
/// ativa e versão das permissões. Conta o pedido como uso da sessão (RN-226).
/// </summary>
public interface IConferirAcesso
{
    /// <summary>Confere o token e devolve quem fez o pedido.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// TOKEN_INVALIDO, SESSAO_TERMINADA ou PERMISSOES_DESATUALIZADAS.
    /// </exception>
    Task<AcessoConferido> Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default);
}
