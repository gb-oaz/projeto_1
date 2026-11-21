using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Auditoria;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>O card da Auditoria (RN-322, 15 RN-276 e RN-279), com MOD-17 · U; sem sessão válida, só o 401 SESSAO_TERMINADA.</summary>
[ApiController]
[Route("auditoria/card")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAuditoria)]
[Produces("application/json")]
public sealed class CardDaAuditoriaController : ControllerBase
{
    public const string Tag = "Card da Auditoria";
    public const string Descricao = "Com MOD-17 · U: o card da Auditoria na tela Módulos do hub e o card ao vivo";

    /// <summary>Montar o card da Auditoria</summary>
    /// <remarks>
    /// O card do time deboaz: os serviços fora do ar (o selo e a reduzida), os erros de hoje com os alertas e as execuções interrompidas
    /// (a normal), a expandida e as séries de 12 dias, 12 semanas e 12 meses. Não é auditado nem traz X-Trace-Id.
    /// </remarks>
    /// <param name="ler">Caso de uso VerCardDaAuditoria.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O card no formato comum dos módulos.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    [HttpGet("", Name = "verCardDaAuditoria")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<CardDaAuditoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ver([FromServices] IVerCardDaAuditoria ler, CancellationToken ct) => Ok(await ler.Executar(ct));

    /// <summary>Acompanhar o card da Auditoria ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RN-322, 15_modulos RN-279): o evento "card" ao abrir e, a cada intervalo (5, 15, 30, 60 ou 300 s; padrão
    /// 300), se a versão mudou; ": ping" a cada 30 s. As recusas saem como JSON, antes de abrir o fluxo.
    /// </remarks>
    /// <param name="intervalo">Segundos entre as conferências: 5, 15, 30, 60 ou 300.</param>
    /// <param name="acompanhar">Caso de uso AcompanharCardDaAuditoriaAoVivo.</param>
    /// <param name="json">O JSON das APIs, igual ao de GET /auditoria/card.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">Intervalo fora da lista.</response>
    [HttpGet("ao-vivo", Name = "acompanharCardDaAuditoria")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task AoVivo([FromQuery] string? intervalo, [FromServices] IAcompanharCardDaAuditoriaAoVivo acompanhar,
        [FromServices] IOptions<JsonOptions> json, CancellationToken ct)
    {
        var (passo, card) = await acompanhar.Abrir(intervalo, ct);
        var fluxo = new EventosDaAuditoria(HttpContext, json.Value.JsonSerializerOptions);
        try
        {
            await fluxo.Abrir(ct);
            await fluxo.Evento("card", card.Versao.ToString(System.Globalization.CultureInfo.InvariantCulture), card, ct);
            var versao = card.Versao;
            var espera = passo < Ping ? passo : Ping;
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
                if (await acompanhar.Proximo(versao, ct) is { } novo)
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

    // ": ping" a cada 30 s
    static readonly TimeSpan Ping = TimeSpan.FromSeconds(30);
}
