using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Primario.Commons.Usuario.Autenticacao;

/// <summary>
/// Os aparelhos conectados da própria pessoa (RF-156, RF-157), só no hub: o token
/// do quest é recusado. O token é conferido antes do caso de uso: recusado, sai
/// sem traceId nem registro.
/// </summary>
[ApiController]
[Route("auth/sessoes")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAutenticacao)]
[Produces("application/json")]
public sealed class AparelhosController : ControllerBase
{
    public const string Tag = "Aparelhos conectados";
    public const string Descricao = "As sessões abertas da própria pessoa no hub: listar, sair de uma ou de todas";

    /// <summary>Listar os aparelhos conectados</summary>
    /// <remarks>Com o token de acesso (RF-156): as sessões da própria pessoa, a mais recente primeiro.</remarks>
    /// <param name="listar">Caso de uso ListarSessoes.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As sessões abertas, a mais recente primeiro.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    [HttpGet("", Name = "listarSessoes")]
    [ComLogin]
    [ProducesResponseType<IReadOnlyList<SessaoListadaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SessaoListadaDto>>> Listar([FromServices] IListarSessoes listar, CancellationToken ct) =>
        Ok(await listar.Executar(DoHub(), ct));

    /// <summary>Sair de um aparelho escolhido na lista</summary>
    /// <remarks>Com o token de acesso (RF-157): só as próprias sessões; a de outra pessoa responde 404.</remarks>
    /// <param name="sid">O id da sessão, como veio na lista.</param>
    /// <param name="conferir">Caso de uso ConferirAcesso, para o token do pedido.</param>
    /// <param name="encerrar">Caso de uso EncerrarSessao.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="204">Sessão encerrada.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="404">Sessão inexistente ou de outra pessoa.</response>
    [HttpDelete("{sid}", Name = "encerrarSessao")]
    [ComLogin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Encerrar(
        [FromRoute] string sid,
        [FromServices] IConferirAcesso conferir,
        [FromServices] IEncerrarSessao encerrar,
        CancellationToken ct)
    {
        var acesso = await conferir.Executar(DoHub(), ct);
        await encerrar.Executar(acesso, sid, HttpContext.NovoTraceId(), ct);
        return NoContent();
    }

    /// <summary>Sair de todos os aparelhos de uma vez, inclusive este</summary>
    /// <remarks>Com o token de acesso (RF-157).</remarks>
    /// <param name="conferir">Caso de uso ConferirAcesso, para o token do pedido.</param>
    /// <param name="encerrar">Caso de uso EncerrarTodasAsSessoes.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="204">Todas as sessões encerradas.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    [HttpDelete("", Name = "encerrarTodasAsSessoes")]
    [ComLogin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EncerrarTodas(
        [FromServices] IConferirAcesso conferir,
        [FromServices] IEncerrarTodasAsSessoes encerrar,
        CancellationToken ct)
    {
        var acesso = await conferir.Executar(DoHub(), ct);
        await encerrar.Executar(acesso, HttpContext.NovoTraceId(), ct);
        return NoContent();
    }

    // Aparelhos conectados são do hub (RF-156, RF-157): o token do quest é recusado.
    ConferenciaDeAcessoDto DoHub() => new(Http.Token(HttpContext), Frente.Hub);
}
