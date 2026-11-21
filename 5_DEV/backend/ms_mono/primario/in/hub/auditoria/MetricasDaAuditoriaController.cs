using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Dtos;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>O tempo de resposta dos módulos e de cada endereço (RF-197, RF-198), só para o administrador e o time com MOD-17 · U.</summary>
[ApiController]
[Route("auditoria/metricas")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAuditoria)]
[Produces("application/json")]
public sealed class MetricasDaAuditoriaController : ControllerBase
{
    public const string Tag = "Métricas";
    public const string Descricao = "Com MOD-17 · U: o tempo de resposta (p50, p95, p99) e os erros de todos os módulos, de um módulo e de cada endereço";

    /// <summary>Ver o tempo de resposta</summary>
    /// <remarks>
    /// O resumo e a série de pedidos da janela (1h, 24h, 7d ou 30d), de todos os módulos ou de um. Os percentis são o fim da faixa
    /// do tempo em que caem (50 a 10000 ms); acima de 10 s, null. Sem o banco, 503 BANCO_DE_DADOS_FORA (RNF-252).
    /// </remarks>
    /// <param name="janela">1h (padrão), 24h, 7d ou 30d.</param>
    /// <param name="modulo">MOD-NN: só os pedidos desse módulo.</param>
    /// <param name="ver">Caso de uso VerMetricas.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As métricas da janela.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">DADOS_INVALIDOS nos campos "janela" ou "modulo".</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("", Name = "verMetricasDaAuditoria")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<MetricasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ver([FromQuery] string? janela, [FromQuery] string? modulo, [FromServices] IVerMetricas ver, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var metricas = await ver.Executar(janela, modulo, consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(metricas);
    }

    /// <summary>Ver o tempo de cada endereço</summary>
    /// <remarks>
    /// Os endereços da janela, sempre pelo modelo da rota (/pagamento/faturas/{id}/pagamentos), com "lento" quando o p95 passa de
    /// 1 s ou está acima de 10 s. Ordenados por p95 (padrão), pedidos ou erros; até 20 itens (1 a 100).
    /// </remarks>
    /// <param name="janela">1h (padrão), 24h, 7d ou 30d.</param>
    /// <param name="modulo">MOD-NN: só os endereços desse módulo.</param>
    /// <param name="ordem">p95 (padrão), pedidos ou erros.</param>
    /// <param name="limite">1 a 100; 20 por padrão.</param>
    /// <param name="ver">Caso de uso VerTempoDosEnderecos.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os endereços da janela.</response>
    /// <response code="401">SESSAO_TERMINADA.</response>
    /// <response code="403">ACESSO_NEGADO.</response>
    /// <response code="422">DADOS_INVALIDOS nos campos "janela", "modulo", "ordem" ou "limite".</response>
    /// <response code="503">BANCO_DE_DADOS_FORA.</response>
    [HttpGet("enderecos", Name = "verTempoDosEnderecos")]
    [ComAcesso(ModuloDaAuditoria.Modulo, nameof(Abas.U), SemSessaoValida = true)]
    [ProducesResponseType<EnderecosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Enderecos([FromQuery] string? janela, [FromQuery] string? modulo, [FromQuery] string? ordem,
        [FromQuery] string? limite, [FromServices] IVerTempoDosEnderecos ver, CancellationToken ct)
    {
        var consulta = HttpContext.NovaConsulta();
        var enderecos = await ver.Executar(janela, modulo, ordem, limite, consulta, ct);
        HttpContext.ComTraceId(consulta);
        return Ok(enderecos);
    }
}
