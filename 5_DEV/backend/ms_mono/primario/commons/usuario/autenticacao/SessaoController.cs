using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Primario.Commons.Usuario.Autenticacao;

/// <summary>
/// Renovação do token de acesso e saída do aparelho (17_modulo-usuario-a-autenticacao).
/// O token de sair é conferido antes do caso de uso: recusado, sai sem traceId nem registro.
/// </summary>
[ApiController]
[Route("auth")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAutenticacao)]
[Produces("application/json")]
public sealed class SessaoController : ControllerBase
{
    public const string Tag = "Sessão";
    public const string Descricao = "Renovar o token de acesso (vale 15 min) e encerrar a sessão deste aparelho";

    /// <summary>Trocar o token de acesso vencido por um novo</summary>
    /// <remarks>
    /// Com o token de renovação, não o de acesso (RF-137, RF-148, RNF-213, RNF-214): o cookie deboaz_renovacao na
    /// web, "tokenRenovacao" no corpo no app. A renovação anterior deixa de valer; usá-la de novo encerra a sessão.
    /// </remarks>
    /// <param name="renovar">Caso de uso RenovarSessao.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Sessão renovada; a renovação nova vai no cookie (web) ou no corpo (app).</response>
    /// <response code="401">Renovação ausente, inválida ou já usada.</response>
    [HttpPost("renovar", Name = "renovarSessao")]
    [ProducesResponseType<SessaoAbertaNoAppCorpo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Renovar([FromServices] IRenovarSessao renovar, CancellationToken ct)
    {
        var pedido = new RenovacaoDto(await SessaoNaResposta.TokenDeRenovacao(Request, ct));
        return Ok(SessaoNaResposta.Corpo(Response, await renovar.Executar(pedido, HttpContext.NovoTraceId(), ct)));
    }

    /// <summary>Encerrar a sessão deste aparelho</summary>
    /// <remarks>Com o token de acesso (RF-138). O cookie de renovação é apagado.</remarks>
    /// <param name="conferir">Caso de uso ConferirAcesso, para o token do pedido.</param>
    /// <param name="sair">Caso de uso SairDoAparelho.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="204">Sessão encerrada.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    [HttpPost("sair", Name = "sairDoAparelho")]
    [ComLogin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Sair([FromServices] IConferirAcesso conferir, [FromServices] ISairDoAparelho sair, CancellationToken ct)
    {
        var acesso = await conferir.Executar(Conferencia(), ct);
        await sair.Executar(acesso, HttpContext.NovoTraceId(), ct);
        SessaoNaResposta.ApagarCookie(Response);
        return NoContent();
    }

    // Sair vale nas duas frentes: o token é conferido contra a que ele diz ser.
    ConferenciaDeAcessoDto Conferencia()
    {
        var token = Http.Token(HttpContext);
        return new ConferenciaDeAcessoDto(token, Http.FrenteDoToken(token));
    }
}
