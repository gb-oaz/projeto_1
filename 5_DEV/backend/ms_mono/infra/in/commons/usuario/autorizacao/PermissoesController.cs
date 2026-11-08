using System.Globalization;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;

namespace Boaz.MsMono.Infra.In.Commons.Usuario.Autorizacao;

/// <summary>O corpo do PUT: o conjunto inteiro de módulos e abas.</summary>
/// <param name="Mods">Cada módulo (MOD-03…) com as abas concedidas ("CU", "U"…).</param>
public sealed record PermissoesCorpo(IReadOnlyDictionary<string, string?>? Mods);

/// <summary>
/// As permissões de uma pessoa (17_modulo-usuario-b-autorizacao): ler, salvar o
/// conjunto inteiro com a versão lida no If-Match e o registro de mudanças.
/// Exige MOD-03 · U e ser superior da pessoa.
/// </summary>
[ApiController]
[Route("usuarios/{id}/permissoes")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAutorizacao)]
[Produces("application/json")]
public sealed class PermissoesController : ControllerBase
{
    public const string Tag = "Permissões";
    public const string Descricao =
        "Permissões por módulo e aba: ler, salvar o conjunto inteiro com a versão lida no If-Match e o registro de mudanças. " +
        "Exige MOD-03 · U e ser superior da pessoa";

    const string Usuarios = CalculoDePermissoes.Usuarios;

    /// <summary>Abrir as permissões da pessoa e o que ela pode receber</summary>
    /// <remarks>
    /// Exige MOD-03 · U e ser superior (RF-160, RN-252). Administrador para membro do time; conta master ou
    /// staff com Administrar usuários para o staff abaixo. Id de outra conta responde 404 (RN-235).
    /// </remarks>
    /// <param name="id">O id da pessoa.</param>
    /// <param name="ler">Caso de uso LerPermissoes.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As permissões, o que a pessoa pode receber e a versão (para o If-Match).</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior da pessoa.</response>
    /// <response code="404">Pessoa inexistente ou de outra conta.</response>
    [HttpGet("", Name = "lerPermissoes")]
    [ComAcesso(Usuarios, nameof(Abas.U))]
    [ProducesResponseType<PermissoesLidasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissoesLidasDto>> Ler([FromRoute] string id, [FromServices] ILerPermissoes ler, CancellationToken ct) =>
        Ok(await ler.Executar(new PedidoDePermissoesDto(HttpContext.QuemPede(), id), ct));

    /// <summary>Salvar o conjunto inteiro de permissões da pessoa</summary>
    /// <remarks>
    /// Exige MOD-03 · U, ser superior e o If-Match com a versão lida (RF-161, RF-162, RN-241, RN-254, RN-255).
    /// Módulo fora do mapa é retirado; o pv da pessoa sobe e as sessões dela renovam.
    /// </remarks>
    /// <param name="id">O id da pessoa.</param>
    /// <param name="corpo">O conjunto inteiro de módulos e abas.</param>
    /// <param name="salvar">Caso de uso SalvarPermissoes.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Permissões salvas, com a versão nova.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior da pessoa.</response>
    /// <response code="404">Pessoa inexistente ou de outra conta.</response>
    /// <response code="412">A versão do If-Match não é a atual.</response>
    /// <response code="422">Dados inválidos.</response>
    /// <response code="428">Falta o cabeçalho If-Match.</response>
    [HttpPut("", Name = "salvarPermissoes")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.EditarPermissoes, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<PermissoesSalvasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<PermissoesSalvasDto>> Salvar(
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PermissoesCorpo? corpo,
        [FromServices] ISalvarPermissoes salvar,
        CancellationToken ct) =>
        Ok(await salvar.Executar(new SalvamentoDePermissoesDto(HttpContext.QuemPede(), id, IfMatch(), corpo?.Mods), HttpContext.NovoTraceId(), ct));

    /// <summary>Mostrar o registro de mudanças das permissões</summary>
    /// <remarks>Exige MOD-03 · U e ser superior (RN-256): 50 por página, da mais nova para a mais antiga; ?ate= pega as anteriores.</remarks>
    /// <param name="id">O id da pessoa.</param>
    /// <param name="ate">Data e hora (UTC) da última mudança já vista: traz as anteriores a ela.</param>
    /// <param name="ler">Caso de uso LerHistoricoDePermissoes.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Até 50 mudanças, da mais nova para a mais antiga.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior da pessoa.</response>
    /// <response code="404">Pessoa inexistente ou de outra conta.</response>
    /// <response code="422">"ate" não é uma data e hora.</response>
    [HttpGet("historico", Name = "lerHistoricoDePermissoes")]
    [ComAcesso(Usuarios, nameof(Abas.U))]
    [ProducesResponseType<IReadOnlyList<MudancaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<MudancaDto>>> LerHistorico(
        [FromRoute] string id,
        [FromQuery] string? ate,
        [FromServices] ILerHistoricoDePermissoes ler,
        CancellationToken ct) =>
        Ok(await ler.Executar(new PedidoDePermissoesDto(HttpContext.QuemPede(), id, Ate(ate)), ct));

    // A versão lida, do cabeçalho If-Match; vazio é como ausente.
    string? IfMatch() => Request.Headers.IfMatch.ToString() is { Length: > 0 } versao ? versao : null;

    static DateTimeOffset? Ate(string? ate) =>
        ate is null ? null
        : DateTimeOffset.TryParse(ate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var data) ? data
        : throw FalhaDeAutorizacao.DadosInvalidos("ate");
}
