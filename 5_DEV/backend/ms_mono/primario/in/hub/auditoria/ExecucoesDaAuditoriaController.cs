using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Auditoria.Execucoes.Dtos;
using Boaz.Core.Hub.Auditoria.Execucoes.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>As execuções do começo ao fim (RF-202, RF-203), só para o administrador e o time com MOD-17 · U.</summary>
[ApiController]
[Route("auditoria/execucoes")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAuditoria)]
[Produces("application/json")]
public sealed class ExecucoesDaAuditoriaController : ControllerBase
{
    public const string Tag = "Execuções";
    public const string Descricao = "Com MOD-17 · U: as execuções com o resultado (concluída, recusada, falhou, em andamento, interrompida) e uma execução pelo traceId";

    /// <summary>Listar as execuções</summary>
    /// <remarks>
    /// Uma execução por traceId no período (até 7 dias, 24 h por padrão), com a contagem de todos os resultados e as execuções do
    /// "resultado" pedido (um ou mais, separados por vírgula). Sem o banco, 503 (RNF-252).
    /// </remarks>
    /// <param name="de">Começo do período (ISO 8601).</param>
    /// <param name="ate">Fim do período.</param>
    /// <param name="resultado">CONCLUIDA, RECUSADA, FALHOU, EM_ANDAMENTO ou INTERROMPIDA, separados por vírgula.</param>
    /// <param name="modulo">MOD-NN da ação principal.</param>
    /// <param name="acao">O código da ação principal.</param>
    /// <param name="conta">A sigla da conta.</param>
    /// <param name="depois">O "proximo" da página anterior.</param>
    /// <param name="limite">1 a 200; 50 por padrão.</param>
    /// <param name="listar">Caso de uso ListarExecucoes.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As execuções.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">DADOS_INVALIDOS, PERIODO_LONGO (7 dias) ou PERIODO_INVERTIDO.</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("", Name = "listarExecucoes")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<ExecucoesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Listar([FromQuery] string? de, [FromQuery] string? ate, [FromQuery] string? resultado, [FromQuery] string? modulo,
        [FromQuery] string? acao, [FromQuery] string? conta, [FromQuery] string? depois, [FromQuery] string? limite, [FromServices] IListarExecucoes listar,
        CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var execucoes = await listar.Executar(new PedidoDeExecucoes(de, ate, resultado, modulo, acao, conta, depois, limite), consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(execucoes);
    }

    /// <summary>Ver uma execução do começo ao fim</summary>
    /// <remarks>
    /// Os registros do traceId em ordem de quando: a ação principal é a do 1º INICIADO e as outras são internas. O resultado vem
    /// dos registros: CONCLUIDA, RECUSADA (ALERTA), FALHOU (ERRO), EM_ANDAMENTO ou INTERROMPIDA (só INICIADO há 5 min ou mais).
    /// </remarks>
    /// <param name="traceId">A execução, como trc-1d6e3f08.</param>
    /// <param name="ver">Caso de uso VerExecucao.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A execução.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="404">EXECUCAO_INEXISTENTE.</response>
    /// <response code="422">DADOS_INVALIDOS no campo "traceId".</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("{traceId}", Name = "verExecucao")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<ExecucaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ver([FromRoute] string traceId, [FromServices] IVerExecucao ver, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var execucao = await ver.Executar(traceId, consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(execucao);
    }
}
