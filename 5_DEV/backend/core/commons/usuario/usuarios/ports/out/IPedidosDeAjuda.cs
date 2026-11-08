using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>
/// Os pedidos de ajuda da conta master sem login (RF-111): viram mensagem no SAC
/// do time deboaz e contam para o limite do aparelho (RNF-205).
/// </summary>
public interface IPedidosDeAjuda
{
    /// <summary>Quando o aparelho enviou desde a hora dada.</summary>
    Task<IReadOnlyList<DateTimeOffset>> EnviosDoAparelho(string aparelho, DateTimeOffset desde, CancellationToken ct = default);

    /// <summary>Registra o envio do aparelho e põe a mensagem no SAC; devolve o id do pedido em "pedidosAjuda" (RN-274).</summary>
    Task<string> Enviar(string aparelho, string instituicao, string contato, DateTimeOffset quando, CancellationToken ct = default);
}
