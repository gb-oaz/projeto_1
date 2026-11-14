using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>Os prazos do SAC por categoria (RF-181): o time lê (MOD-02 · U); o administrador salva (MOD-02 · CU).</summary>
[ApiController]
[Route("sac/configuracao")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoSac)]
[Produces("application/json")]
public sealed class ConfiguracaoDoSacController : ControllerBase
{
    public const string Tag = "Prazos";
    public const string Descricao =
        "Os prazos de primeira resposta por categoria, em horas úteis: o time lê com MOD-02 · U; o administrador salva " +
        "com MOD-02 · CU (aba Configuração)";

    /// <summary>Ler os prazos</summary>
    /// <remarks>
    /// Só o time (RF-181, RN-289): as 6 categorias, sempre; sem nada salvo, o padrão de 4, 8, 8, 16, 40 e 4 horas úteis,
    /// com salvaEm e salvaPor nulos.
    /// </remarks>
    /// <param name="ler">Caso de uso LerPrazos.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os prazos de cada categoria.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é da conta.</response>
    [HttpGet("", Name = "lerPrazosDoSac")]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    [ProducesResponseType<PrazosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ler([FromServices] ILerPrazos ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Salvar os prazos que mudam</summary>
    /// <remarks>
    /// Só o administrador do time, com MOD-02 · CU (RF-181, RN-289, RNF-229): só as categorias que mudam, de 1 a 80 horas
    /// úteis; vale para os chamados abertos daqui em diante. Auditado como SALVAR_PRAZOS_SAC (RN-297).
    /// </remarks>
    /// <param name="corpo">As categorias que mudam, com as horas úteis.</param>
    /// <param name="salvar">Caso de uso SalvarPrazos.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os prazos de todas as categorias, com salvaEm e salvaPor.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · CU, ou quem pede é da conta.</response>
    /// <response code="422">FORA_DOS_LIMITES ou dados inválidos.</response>
    [HttpPut("", Name = "salvarPrazosDoSac")]
    [ComAcessoAuditado(Sac.Modulo, nameof(Abas.CU), AcaoAuditada.SalvarPrazosSac)]
    [ProducesResponseType<PrazosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Salvar([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoPrazos? corpo,
        [FromServices] ISalvarPrazos salvar, CancellationToken ct) =>
        Ok(await salvar.Executar(corpo ?? new(null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));
}
