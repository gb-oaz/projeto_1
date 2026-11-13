using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Sac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>O card do SAC (RN-295, 15 RN-276 e RN-279), com MOD-02 · U.</summary>
[ApiController]
[Route("sac/card")]
[ApiExplorerSettings(IgnoreApi = true)]
[Produces("application/json")]
public sealed class CardDoSacController : ControllerBase
{
    /// <summary>O card do perfil de quem pede.</summary>
    [HttpGet]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    public async Task<IActionResult> Ler([FromServices] ILerCardDoSac ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoSac(), ct));

    /// <summary>
    /// O card ao vivo: o evento "card" ao abrir e, a cada intervalo (5, 15,
    /// 30, 60 ou 300 s), se a versão mudou; ": ping" a cada 30 s.
    /// </summary>
    [HttpGet("ao-vivo")]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
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
