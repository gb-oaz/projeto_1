using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>
/// Resposta das entradas, da confirmação de senha e da renovação. No navegador o
/// adaptador põe <see cref="TokenRenovacao"/> no cookie deboaz_renovacao (HttpOnly,
/// Secure, SameSite=Strict, só /auth/renovar) e o tira do corpo; no app ele vai no
/// corpo (RNF-214).
/// </summary>
/// <param name="TokenAcesso">O JWT RS256.</param>
/// <param name="Tipo">Sempre "Bearer".</param>
/// <param name="ExpiraEm">Segundos de validade do token de acesso: 900.</param>
/// <param name="TokenRenovacao">O token de renovação de uso único.</param>
/// <param name="Aparelho">Aparelho da sessão, para o adaptador decidir entre cookie e corpo.</param>
public sealed record SessaoAbertaDto(string TokenAcesso, string Tipo, int ExpiraEm, string TokenRenovacao, Aparelho Aparelho);

/// <summary>Uma linha de GET /auth/sessoes (RF-156).</summary>
/// <param name="Sid">Id da sessão.</param>
/// <param name="Aparelho">NAVEGADOR ou APP.</param>
/// <param name="UltimaAcao">Último pedido com a sessão.</param>
/// <param name="EsteAparelho">A sessão do token que pediu a lista.</param>
public sealed record SessaoListadaDto(string Sid, string Aparelho, DateTimeOffset UltimaAcao, bool EsteAparelho);

/// <summary>Uma chave de GET /auth/chaves, no formato JWK (RNF-207).</summary>
/// <param name="Kid">Id da chave.</param>
/// <param name="Kty">Sempre "RSA".</param>
/// <param name="Alg">Sempre "RS256".</param>
/// <param name="Use">Sempre "sig".</param>
/// <param name="N">Módulo em base64url.</param>
/// <param name="E">Expoente em base64url.</param>
public sealed record ChaveJwkDto(string Kid, string Kty, string Alg, string Use, string N, string E);

/// <summary>Corpo de GET /auth/chaves: todas as chaves aceitas, a atual e as que ainda valem (RNF-218).</summary>
/// <param name="Keys">As chaves, no campo "keys" do JWKS.</param>
public sealed record ChavesPublicasDto(IReadOnlyList<ChaveJwkDto> Keys);

/// <summary>
/// Quem fez o pedido, depois de o token e a sessão serem conferidos (RNF-216):
/// o que os outros módulos usam para autorizar.
/// </summary>
/// <param name="Sid">Sessão do pedido.</param>
/// <param name="Sub">Usuário, ou o acesso anônimo.</param>
/// <param name="UsuarioId">Usuário; null no acesso anônimo.</param>
/// <param name="ContaId">Conta.</param>
/// <param name="Sigla">Sigla da conta.</param>
/// <param name="Tipo">Tipo do usuário ou ANONIMO.</param>
/// <param name="Frente">Frente da sessão.</param>
/// <param name="Aparelho">Aparelho da sessão.</param>
/// <param name="Quem">No quest, quem entrou.</param>
/// <param name="Papel">Papel da autorização.</param>
/// <param name="Mods">Módulos e abas liberados.</param>
/// <param name="Sala">Sala do acesso anônimo.</param>
public sealed record AcessoConferido(
    string Sid, string Sub, string? UsuarioId, string ContaId, string Sigla, string Tipo, Frente Frente,
    Aparelho Aparelho, QuemEntrou? Quem, string Papel, IReadOnlyDictionary<string, string> Mods, string? Sala);
