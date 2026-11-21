using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Auditoria;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>Os registros da auditoria e das recusas (RF-199 a RF-201) e o catálogo das ações (RN-319), só para o administrador e o time com MOD-17 · U.</summary>
[ApiController]
[Route("auditoria")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAuditoria)]
[Produces("application/json")]
public sealed class RegistrosDaAuditoriaController : ControllerBase
{
    public const string Tag = "Registros";
    public const string Descricao = "Com MOD-17 · U: os registros de auditoria e de auditoriaErros por período, com filtros, e ao vivo; o catálogo das ações";

    /// <summary>Ver os registros</summary>
    /// <remarks>
    /// "auditoria" e "auditoriaErros" juntos, o mais novo primeiro, 50 por página (até 200) com o cursor "proximo". As últimas 24 h
    /// por padrão, até 31 dias; o traceId traz todos os registros da execução, sem período. Sem o banco, 503 (RNF-252).
    /// </remarks>
    /// <param name="de">Começo do período (ISO 8601).</param>
    /// <param name="ate">Fim do período.</param>
    /// <param name="tipo">AUDITORIA ou ERRO.</param>
    /// <param name="nivel">ALERTA ou ERRO.</param>
    /// <param name="modulo">MOD-NN.</param>
    /// <param name="acao">O código da ação.</param>
    /// <param name="conta">A sigla da conta, sem diferença de caixa.</param>
    /// <param name="quem">O id de quem fez.</param>
    /// <param name="codigo">O código do erro.</param>
    /// <param name="traceId">A execução, como trc-7f3a9c21.</param>
    /// <param name="depois">O "proximo" da página anterior.</param>
    /// <param name="limite">1 a 200; 50 por padrão.</param>
    /// <param name="ver">Caso de uso ConsultarRegistros.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os registros.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">DADOS_INVALIDOS, PERIODO_LONGO ou PERIODO_INVERTIDO.</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("registros", Name = "consultarRegistros")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<RegistrosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Consultar([FromQuery] string? de, [FromQuery] string? ate, [FromQuery] string? tipo, [FromQuery] string? nivel,
        [FromQuery] string? modulo, [FromQuery] string? acao, [FromQuery] string? conta, [FromQuery] string? quem, [FromQuery] string? codigo,
        [FromQuery] string? traceId, [FromQuery] string? depois, [FromQuery] string? limite, [FromServices] IConsultarRegistros ver, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var registros = await ver.Executar(new PedidoDeRegistros(de, ate, tipo, nivel, modulo, acao, conta, quem, codigo, traceId, depois, limite), consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(registros);
    }

    /// <summary>Acompanhar os registros ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RF-201, RNF-249): o evento "registro" (id = o do registro, o corpo igual a um item de /registros) em até
    /// 5 s depois de gravado, pelos mesmos filtros de /registros, sem de, ate, traceId, depois nem limite; o evento "ocorrencia" a cada
    /// mudança de estado; ": ping" a cada 30 s. Até 3 conexões por usuário. A abertura grava a consulta uma vez.
    /// </remarks>
    /// <param name="tipo">AUDITORIA ou ERRO.</param>
    /// <param name="nivel">ALERTA ou ERRO.</param>
    /// <param name="modulo">MOD-NN.</param>
    /// <param name="acao">O código da ação.</param>
    /// <param name="conta">A sigla da conta.</param>
    /// <param name="quem">O id de quem fez.</param>
    /// <param name="codigo">O código do erro.</param>
    /// <param name="de">Não vale no ao vivo: 422.</param>
    /// <param name="ate">Não vale no ao vivo: 422.</param>
    /// <param name="traceId">Não vale no ao vivo: 422.</param>
    /// <param name="depois">Não vale no ao vivo: 422.</param>
    /// <param name="limite">Não vale no ao vivo: 422.</param>
    /// <param name="acompanhar">Caso de uso AcompanharRegistrosAoVivo.</param>
    /// <param name="json">O JSON das APIs.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">DADOS_INVALIDOS (de, traceId…), antes de abrir o fluxo.</response>
    [HttpGet("registros/ao-vivo", Name = "acompanharRegistros")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task AoVivo([FromQuery] string? tipo, [FromQuery] string? nivel, [FromQuery] string? modulo, [FromQuery] string? acao,
        [FromQuery] string? conta, [FromQuery] string? quem, [FromQuery] string? codigo, [FromQuery] string? de, [FromQuery] string? ate,
        [FromQuery] string? traceId, [FromQuery] string? depois, [FromQuery] string? limite, [FromServices] IAcompanharRegistrosAoVivo acompanhar,
        [FromServices] IOptions<JsonOptions> json, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var aberto = await acompanhar.Abrir(new PedidoDeRegistros(de, ate, tipo, nivel, modulo, acao, conta, quem, codigo, traceId, depois, limite), consulta, ct);
        HttpContext.ComTraceId(consulta);
        await EventosDaAuditoria.Servir(HttpContext, json.Value.JsonSerializerOptions, aberto, ct);
    }

    /// <summary>Ver o catálogo das ações</summary>
    /// <remarks>
    /// As 41 ações auditadas com o nome, o módulo e o que quer dizer a falta de quem (SEM_LOGIN, SISTEMA ou BANCO), para os
    /// filtros e os nomes. Uma ação fora do catálogo aparece nos registros com o próprio código e "modulo": null. Não é auditado.
    /// </remarks>
    /// <param name="listar">Caso de uso ListarAcoes.</param>
    /// <response code="200">O catálogo.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    [HttpGet("acoes", Name = "listarAcoes")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<IReadOnlyList<AcaoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public IActionResult Acoes([FromServices] IListarAcoes listar) => Ok(listar.Executar());
}
