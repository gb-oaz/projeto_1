using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Sac.Card.Dtos;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Sac;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>O card do SAC (RN-295, 15 RN-276 e RN-279), com MOD-02 · U; sem sessão válida, só o 401 SESSAO_TERMINADA.</summary>
[ApiController]
[Route("sac/card")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoSac)]
[Produces("application/json")]
public sealed class CardDoSacController : ControllerBase
{
    public const string Tag = "Card do SAC";
    public const string Descricao = "Com MOD-02 · U: o card do SAC na tela Módulos do hub e o card ao vivo";

    /// <summary>Montar o card do SAC</summary>
    /// <remarks>
    /// O card do perfil de quem pede: DEBOAZ (o time), INSTITUICAO ou AUTONOMO (RN-295, 15_modulos RN-276), com as
    /// séries de 12 dias, 12 semanas e 12 meses. A versão é a soma das versões dos chamados do escopo.
    /// </remarks>
    /// <param name="ler">Caso de uso LerCardDoSac.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O card no formato comum dos módulos.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada; um só código no card.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    [HttpGet("", Name = "lerCardDoSac")]
    [ComAcesso(Sac.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<CardDoSacDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ler([FromServices] ILerCardDoSac ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Acompanhar o card do SAC ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RN-295, 15_modulos RN-279): o evento "card" ao abrir e, a cada intervalo (5, 15, 30, 60
    /// ou 300 s; padrão 300), se a versão mudou; ": ping" a cada 30 s. As recusas saem como JSON, antes de abrir o fluxo.
    /// </remarks>
    /// <param name="intervalo">Segundos entre as conferências: 5, 15, 30, 60 ou 300.</param>
    /// <param name="acompanhar">Caso de uso AcompanharCardDoSac.</param>
    /// <param name="json">O JSON das APIs, igual ao de GET /sac/card.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada; um só código no card.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    /// <response code="422">Intervalo fora da lista.</response>
    [HttpGet("ao-vivo", Name = "acompanharCardDoSac")]
    [ComAcesso(Sac.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task AoVivo([FromQuery] string? intervalo, [FromServices] IAcompanharCardDoSac acompanhar,
        [FromServices] IOptions<JsonOptions> json, CancellationToken ct)
    {
        var quem = HttpContext.QuemPedeAoSac();
        var (passo, card) = await acompanhar.Abrir(intervalo, quem, ct);
        var fluxo = new EventosDoSac(HttpContext, json.Value.JsonSerializerOptions);
        try
        {
            await fluxo.Abrir(ct);
            await fluxo.Evento("card", card.Versao.ToString(System.Globalization.CultureInfo.InvariantCulture), card, ct);
            var versao = card.Versao;
            var espera = passo < Sac.Ping ? passo : Sac.Ping;
            var desdeACarga = TimeSpan.Zero;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(espera, ct);
                desdeACarga += espera;
                if (desdeACarga < passo)
                {
                    await fluxo.Ping(ct);
                    continue;
                }
                desdeACarga = TimeSpan.Zero;
                if (await acompanhar.Proximo(versao, quem, ct) is { } novo)
                {
                    versao = novo.Versao;
                    await fluxo.Evento("card", novo.Versao.ToString(System.Globalization.CultureInfo.InvariantCulture), novo, ct);
                }
                else await fluxo.Ping(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a conexão fechou do outro lado
        }
    }
}
