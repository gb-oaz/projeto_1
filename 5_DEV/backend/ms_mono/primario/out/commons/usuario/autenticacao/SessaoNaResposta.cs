using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.MsMono.Primario.Out.Commons.Usuario.Autenticacao;

/// <summary>A sessão aberta no navegador: a renovação vai no cookie deboaz_renovacao e não aparece no corpo.</summary>
/// <param name="TokenAcesso">O token de acesso (JWT), para o cabeçalho Authorization: Bearer.</param>
/// <param name="Tipo">O tipo do token, para o cabeçalho Authorization.</param>
/// <param name="ExpiraEm">Em quantos segundos o token de acesso vence.</param>
public sealed record SessaoAbertaCorpo(string TokenAcesso, string Tipo, int ExpiraEm);

/// <summary>A sessão aberta no app: o token de renovação vem no corpo.</summary>
/// <param name="TokenAcesso">O token de acesso (JWT), para o cabeçalho Authorization: Bearer.</param>
/// <param name="Tipo">O tipo do token, para o cabeçalho Authorization.</param>
/// <param name="ExpiraEm">Em quantos segundos o token de acesso vence.</param>
/// <param name="TokenRenovacao">O token de renovação, para POST /auth/renovar.</param>
public sealed record SessaoAbertaNoAppCorpo(string TokenAcesso, string Tipo, int ExpiraEm, string TokenRenovacao);

// A sessão aberta na resposta HTTP e o cookie de renovação (contrato da
// Autenticação): na web a renovação vai no cookie; no app, no corpo.
public static class SessaoNaResposta
{
    public const string Cookie = "deboaz_renovacao";
    const string CaminhoDoCookie = "/auth/renovar";

    // O corpo da sessão aberta; na web, grava o cookie de renovação.
    public static object Corpo(HttpResponse resposta, SessaoAbertaDto sessao)
    {
        if (sessao.Aparelho == Aparelho.App)
        {
            return new SessaoAbertaNoAppCorpo(sessao.TokenAcesso, sessao.Tipo, sessao.ExpiraEm, sessao.TokenRenovacao);
        }
        resposta.Cookies.Append(Cookie, sessao.TokenRenovacao, OpcoesDoCookie());
        return new SessaoAbertaCorpo(sessao.TokenAcesso, sessao.Tipo, sessao.ExpiraEm);
    }

    // Sair apaga o cookie de renovação.
    public static void ApagarCookie(HttpResponse resposta) => resposta.Cookies.Delete(Cookie, OpcoesDoCookie());

    // A renovação: o cookie na web; no app, o campo tokenRenovacao do corpo.
    public static async Task<string?> TokenDeRenovacao(HttpRequest pedido, CancellationToken ct)
    {
        if (pedido.Cookies.TryGetValue(Cookie, out var cookie) && cookie.Length > 0) return cookie;
        if (pedido.ContentLength is 0 || !pedido.HasJsonContentType()) return null;
        try
        {
            return (await pedido.ReadFromJsonAsync<RenovacaoDto>(ct))?.TokenRenovacao;
        }
        catch (System.Text.Json.JsonException)
        {
            // corpo que não é JSON: sem renovação, a sessão é dada como terminada
            return null;
        }
    }

    // HttpOnly, Secure, SameSite=Strict e só para /auth/renovar (contrato da Autenticação).
    static CookieOptions OpcoesDoCookie() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CaminhoDoCookie,
    };
}
