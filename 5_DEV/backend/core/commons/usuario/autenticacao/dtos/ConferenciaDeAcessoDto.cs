using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>
/// O token de acesso de um pedido (cabeçalho Authorization: Bearer) e a frente
/// do endereço que o recebeu: um token do hub é recusado no quest e vice-versa (RN-227).
/// </summary>
/// <param name="Token">O JWT, sem o prefixo "Bearer ".</param>
/// <param name="Frente">Hub ou quest; a sala é do quest.</param>
public sealed record ConferenciaDeAcessoDto(string? Token, Frente Frente)
{
    /// <summary>O token presente; ausente é TOKEN_INVALIDO.</summary>
    /// <exception cref="FalhaDeAutenticacao">TOKEN_INVALIDO.</exception>
    public string Validar() => string.IsNullOrWhiteSpace(Token) ? throw FalhaDeAutenticacao.TokenInvalido() : Token.Trim();
}
