using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Infra.In.Commons.Usuario.Autenticacao;

/// <summary>
/// Entrada no hub, no quest e na sala e a confirmação da senha
/// (17_modulo-usuario-a-autenticacao): endereços públicos, auditados com um
/// traceId novo no X-Trace-Id do sucesso e da recusa (RN-272, RN-273).
/// </summary>
[ApiController]
[Route("auth")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAutenticacao)]
[Produces("application/json")]
public sealed class EntradaController : ControllerBase
{
    public const string Tag = "Entrada";
    public const string Descricao = "Abrir a sessão no hub, no quest e na sala, e confirmar a senha gerada. Endereços públicos";

    /// <summary>Abrir uma sessão no hub com a entrada e a senha</summary>
    /// <remarks>
    /// Endereço público (RF-134, RF-139, RF-140, RN-247, RNF-204). aparelho NAVEGADOR ou APP; a 5ª tentativa errada
    /// seguida bloqueia, e o bloqueio dobra a cada vez.
    /// </remarks>
    /// <param name="corpo">A entrada, a senha e o aparelho (NAVEGADOR ou APP).</param>
    /// <param name="entrar">Caso de uso EntrarNoHub.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Sessão aberta; no navegador a renovação vai no cookie deboaz_renovacao, no app vem no corpo.</response>
    /// <response code="401">Entrada ou senha erradas.</response>
    /// <response code="403">Acesso recusado à conta ou ao usuário.</response>
    /// <response code="422">Dados inválidos.</response>
    /// <response code="429">Bloqueado por tentativas erradas seguidas.</response>
    [HttpPost("hub/entrar", Name = "entrarNoHub")]
    [ProducesResponseType<SessaoAbertaNoAppCorpo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EntrarNoHub(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EntradaNoHubDto? corpo,
        [FromServices] IEntrarNoHub entrar,
        CancellationToken ct) =>
        Ok(SessaoNaResposta.Corpo(Response, await entrar.Executar(corpo ?? new(null, null, null), HttpContext.NovoTraceId(), ct)));

    /// <summary>Abrir uma sessão no quest com o código de acesso</summary>
    /// <remarks>Endereço público (RF-135, RF-139, RF-140, RN-228): o participante, ou o responsável com o código dele.</remarks>
    /// <param name="corpo">O código de acesso, quem entra e o aparelho.</param>
    /// <param name="entrar">Caso de uso EntrarNoQuest.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Sessão aberta; no navegador a renovação vai no cookie deboaz_renovacao, no app vem no corpo.</response>
    /// <response code="401">Código de acesso errado.</response>
    /// <response code="403">Acesso recusado à conta ou ao participante.</response>
    /// <response code="422">Dados inválidos.</response>
    /// <response code="429">Bloqueado por tentativas erradas seguidas.</response>
    [HttpPost("quest/entrar", Name = "entrarNoQuest")]
    [ProducesResponseType<SessaoAbertaNoAppCorpo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EntrarNoQuest(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EntradaNoQuestDto? corpo,
        [FromServices] IEntrarNoQuest entrar,
        CancellationToken ct) =>
        Ok(SessaoNaResposta.Corpo(Response, await entrar.Executar(corpo ?? new(null, null, null), HttpContext.NovoTraceId(), ct)));

    /// <summary>Confirmar que a senha gerada foi anotada e já entrar no hub</summary>
    /// <remarks>Endereço público (RF-143): o comprovante entregue com a senha vale uma vez, por 72 h.</remarks>
    /// <param name="corpo">O comprovante entregue com a senha.</param>
    /// <param name="confirmar">Caso de uso ConfirmarSenha.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Senha confirmada e sessão aberta.</response>
    /// <response code="401">Comprovante inválido, já usado ou vencido.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost("hub/confirmar-senha", Name = "confirmarSenha")]
    [ProducesResponseType<SessaoAbertaNoAppCorpo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarSenha(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ConfirmacaoDeSenhaDto? corpo,
        [FromServices] IConfirmarSenha confirmar,
        CancellationToken ct) =>
        Ok(SessaoNaResposta.Corpo(Response, await confirmar.Executar(corpo ?? new(null), HttpContext.NovoTraceId(), ct)));

    /// <summary>Participar de uma sala ao vivo sem código de acesso</summary>
    /// <remarks>Endereço público (RF-136, RN-231): papel ANONIMO, só com o módulo e o id da sala no token.</remarks>
    /// <param name="corpo">O código da sala.</param>
    /// <param name="entrar">Caso de uso EntrarNaSala.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Sessão anônima aberta na sala.</response>
    /// <response code="404">Sala não encontrada.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost("sala/entrar", Name = "entrarNaSala")]
    [ProducesResponseType<SessaoAbertaCorpo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EntrarNaSala(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EntradaNaSalaDto? corpo,
        [FromServices] IEntrarNaSala entrar,
        CancellationToken ct) =>
        Ok(SessaoNaResposta.Corpo(Response, await entrar.Executar(corpo ?? new(null), HttpContext.NovoTraceId(), ct)));
}
