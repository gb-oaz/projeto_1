using System.Diagnostics;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Primario.Out.Hub.Sac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>
/// Os chamados do SAC (18_modulo-sac, sac-contrato-api v01), com MOD-02 · U:
/// abrir, listar, ler, a conversa (e ao vivo), assumir, mudar o status,
/// reabrir, encerrar e avaliar. O papel decide o lado (conta ou time) no caso de uso.
/// </summary>
[ApiController]
[Route("sac/chamados")]
[ApiExplorerSettings(IgnoreApi = true)]
[Produces("application/json")]
public sealed class ChamadosController : ControllerBase
{
    const string U = nameof(Abas.U);

    /// <summary>Abrir um chamado (RF-171).</summary>
    [HttpPost]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.AbrirChamado)]
    public async Task<IActionResult> Abrir([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoAbrirChamado? corpo,
        [FromServices] IAbrirChamado abrir, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await abrir.Executar(corpo ?? new(null, null, null, null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>Listar os chamados do lado de quem pede (RF-172, RF-174).</summary>
    [HttpGet]
    [ComAcesso(Sac.Modulo, U)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? situacao, [FromQuery] string? status, [FromQuery] string? categoria, [FromQuery] string? busca,
        [FromQuery] string? pagina, [FromQuery] string? responsavel, [FromQuery] string? atrasado, [FromQuery] string? notaMaxima,
        [FromServices] IListarChamados listar, CancellationToken ct) =>
        Ok(await listar.Executar(new FiltroDeChamadosDto(situacao, status, categoria, busca, Paginacao.Pagina(pagina), responsavel, atrasado,
            notaMaxima), HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Ler o chamado, as mensagens e a linha do tempo; marca como lido (RF-173).</summary>
    [HttpGet("{id}")]
    [ComAcesso(Sac.Modulo, U)]
    public async Task<IActionResult> Ler([FromRoute] string id, [FromServices] ILerChamado ler, CancellationToken ct) =>
        Ok(await ler.Executar(id, HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Enviar uma mensagem ou nota interna (RF-173, RF-175).</summary>
    [HttpPost("{id}/mensagens")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.EnviarMensagem, Alvo = AlvoAuditado.IdDaRota)]
    public async Task<IActionResult> Mensagem([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoMensagem? corpo, [FromServices] IEnviarMensagem enviar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await enviar.Executar(id, corpo ?? new(null, null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>Mensagem, nota e evento nunca mudam (RN-293): 405, sem corpo.</summary>
    [HttpPatch("{id}/mensagens/{mensagem}")]
    [HttpPut("{id}/mensagens/{mensagem}")]
    [HttpDelete("{id}/mensagens/{mensagem}")]
    public IActionResult MensagemNaoMuda()
    {
        Response.Headers.Allow = "";
        return StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    /// <summary>O time assume (RF-175).</summary>
    [HttpPost("{id}/assumir")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.AssumirChamado, Alvo = AlvoAuditado.IdDaRota)]
    public async Task<IActionResult> Assumir([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoAssumir? corpo, [FromServices] IAssumirChamado assumir,
        CancellationToken ct) =>
        Ok(await assumir.Executar(id, corpo ?? new(null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>O time pede informação ou resolve (RF-176).</summary>
    [HttpPost("{id}/status")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.MudarStatus, Alvo = AlvoAuditado.IdDaRota)]
    public async Task<IActionResult> Status([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoStatus? corpo, [FromServices] IMudarStatus mudar,
        CancellationToken ct) =>
        Ok(await mudar.Executar(id, corpo ?? new(null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>A conta reabre em até 7 dias (RF-177).</summary>
    [HttpPost("{id}/reabrir")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.ReabrirChamado, Alvo = AlvoAuditado.IdDaRota)]
    public async Task<IActionResult> Reabrir([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoReabrir? corpo, [FromServices] IReabrirChamado reabrir,
        CancellationToken ct) =>
        Ok(await reabrir.Executar(id, corpo ?? new(null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>A conta encerra (RF-177).</summary>
    [HttpPost("{id}/encerrar")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.EncerrarChamado, Alvo = AlvoAuditado.IdDaRota)]
    public async Task<IActionResult> Encerrar([FromRoute] string id, [FromServices] IEncerrarChamado encerrar, CancellationToken ct) =>
        Ok(await encerrar.Executar(id, HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>A conta avalia e confirma a solução (RF-178).</summary>
    [HttpPost("{id}/avaliacao")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.AvaliarChamado, Alvo = AlvoAuditado.IdDaRota)]
    public async Task<IActionResult> Avaliar([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoAvaliacao? corpo, [FromServices] IAvaliarChamado avaliar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await avaliar.Executar(id, corpo ?? new(null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>
    /// A conversa ao vivo (RNF-230): text/event-stream com o evento "mensagem"
    /// (id = o da mensagem) assim que chega, e ": ping" a cada 30 s. O
    /// Last-Event-ID da reconexão diz de onde seguir.
    /// </summary>
    [HttpGet("{id}/ao-vivo")]
    [ComAcesso(Sac.Modulo, U)]
    public async Task AoVivo([FromRoute] string id, [FromServices] IAcompanharChamado acompanhar, [FromServices] IOptions<JsonOptions> json,
        CancellationToken ct)
    {
        var quem = HttpContext.QuemPedeAoSac();
        var conversa = await acompanhar.Abrir(id, Request.Headers["Last-Event-ID"].FirstOrDefault(), quem, ct);
        var fluxo = new EventosDoSac(HttpContext, json.Value.JsonSerializerOptions);
        try
        {
            await fluxo.Abrir(ct);
            var desdeOPing = Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                var (mensagens, seguinte) = await acompanhar.Proximas(conversa, Sac.Conferencia, ct);
                conversa = seguinte;
                foreach (var mensagem in mensagens) await fluxo.Evento("mensagem", mensagem.Id, mensagem, ct);
                if (desdeOPing.Elapsed < Sac.Ping) continue;
                await fluxo.Ping(ct);
                desdeOPing.Restart();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a conexão fechou do outro lado
        }
    }
}
