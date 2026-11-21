using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Dtos;
using Boaz.Core.Hub.Auditoria.Saude.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Auditoria;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>A saúde dos serviços e dos módulos (RF-194 a RF-196), só para o administrador e o time com MOD-17 · U.</summary>
[ApiController]
[Route("auditoria/saude")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAuditoria)]
[Produces("application/json")]
public sealed class SaudeDaAuditoriaController : ControllerBase
{
    public const string Tag = "Saúde";
    public const string Descricao = "Com MOD-17 · U: a saúde dos 8 serviços e dos módulos, o histórico de um serviço e a saúde ao vivo";

    /// <summary>Ver a saúde dos serviços e dos módulos</summary>
    /// <remarks>
    /// Lida da última verificação desta instância, sem o banco de dados (RNF-252): os 8 serviços (FORA_DO_AR, LENTO,
    /// SEM_VERIFICACAO, NO_AR e DESLIGADO), os módulos pelos pedidos dos últimos 5 min e as instâncias no ar. A consulta
    /// atendida traz o X-Trace-Id e fica na auditoria como CONSULTAR_AUDITORIA (RN-325).
    /// </remarks>
    /// <param name="ver">Caso de uso VerSaude.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A saúde da última verificação.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem o papel ou sem MOD-17 · U.</response>
    [HttpGet("", Name = "verSaudeDaAuditoria")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<SaudeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ver([FromServices] IVerSaude ver, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var saude = await ver.Executar(consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(saude);
    }

    /// <summary>Ver o histórico de um serviço</summary>
    /// <remarks>
    /// As quedas, a disponibilidade e a série do período: 24h (padrão, 24 pontos por hora), 7d ou 30d (um ponto por dia de
    /// Brasília). A queda ainda aberta vem com "fim": null.
    /// </remarks>
    /// <param name="servico">O identificador de um dos 8 serviços.</param>
    /// <param name="periodo">24h (padrão), 7d ou 30d.</param>
    /// <param name="ver">Caso de uso VerHistoricoDoServico.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O histórico do serviço.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="404">SERVICO_INEXISTENTE.</response>
    /// <response code="422">DADOS_INVALIDOS no campo "periodo".</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("{servico}/historico", Name = "verHistoricoDoServico")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<HistoricoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Historico([FromRoute] string servico, [FromQuery] string? periodo, [FromServices] IVerHistoricoDoServico ver,
        CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var historico = await ver.Executar(servico, periodo, consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(historico);
    }

    /// <summary>Acompanhar a saúde ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RNF-246, RNF-249): o evento "saude" ao abrir e a cada verificação (30 s), o evento "ocorrencia"
    /// em até 5 s de cada mudança de estado e ": ping" a cada 30 s. Até 3 conexões por usuário: a 4ª fecha a mais antiga com
    /// "encerrado" LIMITE_CONEXOES. A abertura grava a consulta uma vez. As recusas saem como JSON, antes de abrir o fluxo.
    /// </remarks>
    /// <param name="acompanhar">Caso de uso AcompanharSaudeAoVivo.</param>
    /// <param name="json">O JSON das APIs.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    [HttpGet("ao-vivo", Name = "acompanharSaudeDaAuditoria")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task AoVivo([FromServices] IAcompanharSaudeAoVivo acompanhar, [FromServices] IOptions<JsonOptions> json, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var aberto = await acompanhar.Abrir(consulta, ct);
        HttpContext.ComTraceId(consulta);
        await EventosDaAuditoria.Servir(HttpContext, json.Value.JsonSerializerOptions, aberto, ct);
    }
}
