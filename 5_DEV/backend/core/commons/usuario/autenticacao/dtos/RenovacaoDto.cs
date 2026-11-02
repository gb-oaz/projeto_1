using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>
/// POST /auth/renovar (RF-137): o token de renovação, vindo do cookie
/// deboaz_renovacao no navegador ou do corpo no app. O adaptador de entrada
/// escolhe a origem; o caso de uso só recebe o texto.
/// </summary>
/// <param name="TokenRenovacao">O token no formato &lt;sid&gt;.&lt;segredo&gt;.</param>
public sealed record RenovacaoDto(string? TokenRenovacao)
{
    /// <summary>
    /// Lê o token. Ausente ou fora do formato é sessão terminada, e não dado
    /// inválido: o cliente só tem de entrar de novo.
    /// </summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    public TokenDeRenovacao Validar() => TokenDeRenovacao.Ler(TokenRenovacao) ?? throw FalhaDeAutenticacao.SessaoTerminada();
}
