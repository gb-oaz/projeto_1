using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Dtos;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Auditoria;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>As ocorrências: as mudanças de estado dos serviços e dos módulos (RF-204, RF-205), só para o administrador e o time com MOD-17 · U.</summary>
[ApiController]
[Route("auditoria/ocorrencias")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAuditoria)]
[Produces("application/json")]
public sealed class OcorrenciasDaAuditoriaController : ControllerBase
{
    public const string Tag = "Ocorrências";
    public const string Descricao = "Com MOD-17 · U: as ocorrências abertas e as voltas recentes (a faixa), ao vivo, e a lista por período";

    /// <summary>Ver as ocorrências abertas</summary>
    /// <remarks>
    /// A faixa ao abrir: a última mudança de cada serviço ou módulo fora de NO_AR (FORA_DO_AR antes de LENTO, a mais nova primeiro)
    /// e as voltas a NO_AR dos últimos 10 min. Lida do Redis, sem o banco (RNF-252); sem X-Trace-Id e não grava a consulta.
    /// </remarks>
    /// <param name="ver">Caso de uso VerOcorrenciasAbertas.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As abertas e as voltas.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    [HttpGet("abertas", Name = "verOcorrenciasAbertas")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<AbertasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Abertas([FromServices] IVerOcorrenciasAbertas ver, CancellationToken ct) => Ok(await ver.Executar(ct));

    /// <summary>Acompanhar as ocorrências ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RF-204, RNF-249): o evento "abertas" ao abrir (o corpo de /ocorrencias/abertas), o evento "ocorrencia"
    /// em até 5 s de cada mudança de estado e ": ping" a cada 30 s. Conta entre as 3 conexões ao vivo do usuário. Sem X-Trace-Id e
    /// não grava a consulta.
    /// </remarks>
    /// <param name="acompanhar">Caso de uso AcompanharOcorrenciasAoVivo.</param>
    /// <param name="json">O JSON das APIs.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    [HttpGet("ao-vivo", Name = "acompanharOcorrencias")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task AoVivo([FromServices] IAcompanharOcorrenciasAoVivo acompanhar, [FromServices] IOptions<JsonOptions> json, CancellationToken ct)
    {
        var aberto = await acompanhar.Abrir(HttpContext.QuemPedeAAuditoria(), ct);
        await EventosDaAuditoria.Servir(HttpContext, json.Value.JsonSerializerOptions, aberto, ct);
    }

    /// <summary>Listar as ocorrências</summary>
    /// <remarks>
    /// As ocorrências do período (até 31 dias, 24 h por padrão), da mais nova para a mais antiga, 50 por página (até 200). Sem o
    /// banco, 503 (RNF-252).
    /// </remarks>
    /// <param name="de">Começo do período (ISO 8601).</param>
    /// <param name="ate">Fim do período.</param>
    /// <param name="tipo">SERVICO ou MODULO.</param>
    /// <param name="alvo">O id de um serviço ou de um módulo.</param>
    /// <param name="para">NO_AR, LENTO ou FORA_DO_AR.</param>
    /// <param name="depois">O "proximo" da página anterior.</param>
    /// <param name="limite">1 a 200; 50 por padrão.</param>
    /// <param name="listar">Caso de uso ListarOcorrencias.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As ocorrências.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">DADOS_INVALIDOS, PERIODO_LONGO ou PERIODO_INVERTIDO.</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("", Name = "listarOcorrencias")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<OcorrenciasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Listar([FromQuery] string? de, [FromQuery] string? ate, [FromQuery] string? tipo, [FromQuery] string? alvo,
        [FromQuery] string? para, [FromQuery] string? depois, [FromQuery] string? limite, [FromServices] IListarOcorrencias listar, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var ocorrencias = await listar.Executar(de, ate, tipo, alvo, para, depois, limite, consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(ocorrencias);
    }
}
