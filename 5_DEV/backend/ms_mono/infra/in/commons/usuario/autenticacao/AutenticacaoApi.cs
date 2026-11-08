using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using static Boaz.MsMono.Infra.In.Commons.Http;

namespace Boaz.MsMono.Infra.In.Commons.Usuario.Autenticacao;

// A sessão aberta como sai no corpo: no app, com o token de renovação; na web,
// ele vai no cookie deboaz_renovacao e não aparece no corpo.
public sealed record SessaoAbertaCorpo(string TokenAcesso, string Tipo, int ExpiraEm);

public sealed record SessaoAbertaNoAppCorpo(string TokenAcesso, string Tipo, int ExpiraEm, string TokenRenovacao);

// Endereços da Autenticação (17_modulo-usuario-a-autenticacao): entrar no hub,
// no quest e na sala, confirmar a senha, renovar, sair e os aparelhos conectados.
// Os 8 que mudam a sessão são auditados com um traceId novo, no X-Trace-Id do
// sucesso e da recusa (RN-272, RN-273); o token de sair e dos aparelhos é
// conferido antes do caso de uso: recusado, sai sem traceId nem registro.
public static class AutenticacaoApi
{
    public const string Tag = "Autenticação";

    // A API deste módulo no seletor do Swagger (/doc/api/autenticacao.json).
    public const string Documento = "autenticacao";
    public const string Descricao =
        "Entrada no hub, no quest e na sala, confirmação da senha, renovação do token e aparelhos conectados " +
        "(17_modulo-usuario-a-autenticacao). O token de acesso vale 15 min; a renovação vai no cookie na web e no corpo no app";

    public const string Cookie = "deboaz_renovacao";
    const string CaminhoDoCookie = "/auth/renovar";

    public static IEndpointRouteBuilder MapAutenticacao(this IEndpointRouteBuilder rotas)
    {
        var api = rotas.MapGroup("/auth").WithTags(Tag).WithGroupName(Documento);

        api.MapPost("/hub/entrar", async (EntradaNoHubDto? corpo, HttpContext http, IEntrarNoHub entrar, CancellationToken ct) =>
                Aberta(http, await entrar.Executar(corpo ?? new(null, null, null), http.NovoTraceId(), ct)))
            .WithSummary("Abrir uma sessão no hub com a entrada e a senha")
            .WithDescription("Endereço público (RF-134, RF-139, RF-140, RN-247, RNF-204). aparelho NAVEGADOR ou APP; a 5ª tentativa errada " +
                "seguida bloqueia, e o bloqueio dobra a cada vez.")
            .Produces<SessaoAbertaNoAppCorpo>()
            .Produces<Erro>(StatusCodes.Status401Unauthorized)
            .Produces<Erro>(StatusCodes.Status403Forbidden)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity)
            .Produces<Erro>(StatusCodes.Status429TooManyRequests);

        api.MapPost("/quest/entrar", async (EntradaNoQuestDto? corpo, HttpContext http, IEntrarNoQuest entrar, CancellationToken ct) =>
                Aberta(http, await entrar.Executar(corpo ?? new(null, null, null), http.NovoTraceId(), ct)))
            .WithSummary("Abrir uma sessão no quest com o código de acesso")
            .WithDescription("Endereço público (RF-135, RF-139, RF-140, RN-228): o participante, ou o responsável com o código dele.")
            .Produces<SessaoAbertaNoAppCorpo>()
            .Produces<Erro>(StatusCodes.Status401Unauthorized)
            .Produces<Erro>(StatusCodes.Status403Forbidden)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity)
            .Produces<Erro>(StatusCodes.Status429TooManyRequests);

        api.MapPost("/hub/confirmar-senha", async (ConfirmacaoDeSenhaDto? corpo, HttpContext http, IConfirmarSenha confirmar, CancellationToken ct) =>
                Aberta(http, await confirmar.Executar(corpo ?? new(null), http.NovoTraceId(), ct)))
            .WithSummary("Confirmar que a senha gerada foi anotada e já entrar no hub")
            .WithDescription("Endereço público (RF-143): o comprovante entregue com a senha vale uma vez, por 72 h.")
            .Produces<SessaoAbertaNoAppCorpo>()
            .Produces<Erro>(StatusCodes.Status401Unauthorized)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/sala/entrar", async (EntradaNaSalaDto? corpo, HttpContext http, IEntrarNaSala entrar, CancellationToken ct) =>
                Aberta(http, await entrar.Executar(corpo ?? new(null), http.NovoTraceId(), ct)))
            .WithSummary("Participar de uma sala ao vivo sem código de acesso")
            .WithDescription("Endereço público (RF-136, RN-231): papel ANONIMO, só com o módulo e o id da sala no token.")
            .Produces<SessaoAbertaCorpo>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/renovar", async (HttpContext http, IRenovarSessao renovar, CancellationToken ct) =>
            {
                var pedido = new RenovacaoDto(await TokenDeRenovacao(http, ct));
                return Aberta(http, await renovar.Executar(pedido, http.NovoTraceId(), ct));
            })
            .WithSummary("Trocar o token de acesso vencido por um novo")
            .WithDescription("Com o token de renovação, não o de acesso (RF-137, RF-148, RNF-213, RNF-214): o cookie deboaz_renovacao na " +
                "web, \"tokenRenovacao\" no corpo no app. A renovação anterior deixa de valer; usá-la de novo encerra a sessão.")
            .Produces<SessaoAbertaNoAppCorpo>()
            .Produces<Erro>(StatusCodes.Status401Unauthorized);

        api.MapPost("/sair", async (HttpContext http, IConferirAcesso conferir, ISairDoAparelho sair, CancellationToken ct) =>
            {
                var acesso = await conferir.Executar(Conferencia(http), ct);
                await sair.Executar(acesso, http.NovoTraceId(), ct);
                http.Response.Cookies.Delete(Cookie, OpcoesDoCookie());
                return Results.NoContent();
            })
            .ComLogin()
            .WithSummary("Encerrar a sessão deste aparelho")
            .WithDescription("Com o token de acesso (RF-138). O cookie de renovação é apagado.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Erro>(StatusCodes.Status401Unauthorized);

        api.MapGet("/sessoes", (HttpContext http, IListarSessoes listar, CancellationToken ct) => listar.Executar(DoHub(http), ct))
            .ComLogin()
            .WithSummary("Listar os aparelhos conectados")
            .WithDescription("Com o token de acesso (RF-156): as sessões da própria pessoa, a mais recente primeiro.")
            .Produces<IReadOnlyList<SessaoListadaDto>>()
            .Produces<Erro>(StatusCodes.Status401Unauthorized);

        api.MapDelete("/sessoes/{sid}", async (string sid, HttpContext http, IConferirAcesso conferir, IEncerrarSessao encerrar,
                CancellationToken ct) =>
            {
                var acesso = await conferir.Executar(DoHub(http), ct);
                await encerrar.Executar(acesso, sid, http.NovoTraceId(), ct);
                return Results.NoContent();
            })
            .ComLogin()
            .WithSummary("Sair de um aparelho escolhido na lista")
            .WithDescription("Com o token de acesso (RF-157): só as próprias sessões; a de outra pessoa responde 404.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Erro>(StatusCodes.Status401Unauthorized)
            .Produces<Erro>(StatusCodes.Status404NotFound);

        api.MapDelete("/sessoes", async (HttpContext http, IConferirAcesso conferir, IEncerrarTodasAsSessoes encerrar, CancellationToken ct) =>
            {
                var acesso = await conferir.Executar(DoHub(http), ct);
                await encerrar.Executar(acesso, http.NovoTraceId(), ct);
                return Results.NoContent();
            })
            .ComLogin()
            .WithSummary("Sair de todos os aparelhos de uma vez, inclusive este")
            .WithDescription("Com o token de acesso (RF-157).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Erro>(StatusCodes.Status401Unauthorized);

        api.MapGet("/chaves", (ILerChavesPublicas ler, CancellationToken ct) => ler.Executar(ct))
            .WithSummary("Publicar a chave pública que confere a assinatura do token")
            .WithDescription("Endereço público (RNF-207, RNF-218), no formato JWKS, para as aplicações e os serviços do deboaz.")
            .Produces<ChavesPublicasDto>();

        return rotas;
    }

    // Aparelhos conectados são do hub (RF-156, RF-157): o token do quest é recusado.
    static ConferenciaDeAcessoDto DoHub(HttpContext http) => new(Token(http), Frente.Hub);

    // Sair vale nas duas frentes: o token é conferido contra a que ele diz ser.
    static ConferenciaDeAcessoDto Conferencia(HttpContext http)
    {
        var token = Token(http);
        return new ConferenciaDeAcessoDto(token, FrenteDoToken(token));
    }

    // A renovação: o cookie na web; no app, o campo tokenRenovacao do corpo.
    static async Task<string?> TokenDeRenovacao(HttpContext http, CancellationToken ct)
    {
        if (http.Request.Cookies.TryGetValue(Cookie, out var cookie) && cookie.Length > 0) return cookie;
        if (http.Request.ContentLength is 0 || !(http.Request.HasJsonContentType())) return null;
        try
        {
            return (await http.Request.ReadFromJsonAsync<RenovacaoDto>(ct))?.TokenRenovacao;
        }
        catch (System.Text.Json.JsonException)
        {
            // corpo que não é JSON: sem renovação, a sessão é dada como terminada
            return null;
        }
    }

    // A resposta da sessão aberta: na web a renovação vai no cookie; no app, no corpo.
    static IResult Aberta(HttpContext http, SessaoAbertaDto sessao)
    {
        if (sessao.Aparelho == Aparelho.App)
        {
            return Results.Ok(new SessaoAbertaNoAppCorpo(sessao.TokenAcesso, sessao.Tipo, sessao.ExpiraEm, sessao.TokenRenovacao));
        }
        http.Response.Cookies.Append(Cookie, sessao.TokenRenovacao, OpcoesDoCookie());
        return Results.Ok(new SessaoAbertaCorpo(sessao.TokenAcesso, sessao.Tipo, sessao.ExpiraEm));
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
