using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;

/// <summary>
/// O tempo de sessão (17_modulo-usuario): o do staff e dos participantes de uma
/// conta e o dos membros do time e das contas master. Exige MOD-03 · C.
/// </summary>
[ApiController]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoUsuarios)]
[Produces("application/json")]
public sealed class TempoDeSessaoController : ControllerBase
{
    public const string Tag = "Tempo de sessão";
    public const string Descricao =
        "Quanto tempo cada um fica conectado, em duração ISO 8601: o staff e os participantes da conta e, só o time deboaz, " +
        "os membros do time e as contas master. Exige MOD-03 na aba Configuração (C)";

    const string Usuarios = CalculoDePermissoes.Usuarios;

    /// <summary>Ler o tempo de sessão do staff e dos participantes</summary>
    /// <remarks>Exige MOD-03 · C (RF-114, RN-183). staffNavegador em horas; staffApp e participante em dias.</remarks>
    /// <param name="id">O id da conta.</param>
    /// <param name="ler">Caso de uso LerTempoDaConta.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os tempos de sessão da conta.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · C.</response>
    /// <response code="404">Conta não encontrada.</response>
    [HttpGet("contas/{id}/tempo-sessao", Name = "lerTempoDaConta")]
    [ComAcesso(Usuarios, nameof(Abas.C))]
    [ProducesResponseType<TempoDaContaLidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TempoDaContaLidoDto>> LerDaConta([FromRoute] string id, [FromServices] ILerTempoDaConta ler, CancellationToken ct) =>
        Ok(await ler.Executar(new PedidoDeContaDto(HttpContext.QuemPede(), id), ct));

    /// <summary>Salvar o tempo de sessão do staff e dos participantes</summary>
    /// <remarks>Exige MOD-03 · C (RF-114, RN-183). Fora dos limites: FORA_DOS_LIMITES com o campo, o mínimo e o máximo.</remarks>
    /// <param name="id">O id da conta.</param>
    /// <param name="corpo">Os tempos de sessão novos.</param>
    /// <param name="salvar">Caso de uso SalvarTempoDaConta.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os tempos de sessão salvos.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · C.</response>
    /// <response code="404">Conta não encontrada.</response>
    /// <response code="422">Dados inválidos ou fora dos limites.</response>
    [HttpPut("contas/{id}/tempo-sessao", Name = "salvarTempoDaConta")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.C), AcaoAuditada.SalvarTempoSessao, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<TempoDaContaLidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TempoDaContaLidoDto>> SalvarDaConta(
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TempoDaContaCorpo? corpo,
        [FromServices] ISalvarTempoDaConta salvar,
        CancellationToken ct) =>
        Ok(await salvar.Executar(new TempoDaContaDto(HttpContext.QuemPede(), id, corpo?.StaffNavegador, corpo?.StaffApp, corpo?.Participante),
            HttpContext.NovoTraceId(), ct));

    /// <summary>Ler o tempo de sessão dos membros do time e das contas master</summary>
    /// <remarks>Só o time deboaz, com MOD-03 · C (RF-152, RN-243). Todos em horas.</remarks>
    /// <param name="ler">Caso de uso LerTempoDoTime.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os tempos de sessão do time e das contas master.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · C ou fora do time deboaz.</response>
    [HttpGet("time/tempo-sessao", Name = "lerTempoDoTime")]
    [ComAcesso(Usuarios, nameof(Abas.C))]
    [ProducesResponseType<TempoDoTimeLidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TempoDoTimeLidoDto>> LerDoTime([FromServices] ILerTempoDoTime ler, CancellationToken ct) =>
        Ok(await ler.Executar(new QuemPedeDto(HttpContext.QuemPede()), ct));

    /// <summary>Salvar o tempo de sessão dos membros do time e das contas master</summary>
    /// <remarks>Só o time deboaz, com MOD-03 · C (RF-152, RN-243). Fora dos limites: FORA_DOS_LIMITES.</remarks>
    /// <param name="corpo">Os tempos de sessão novos.</param>
    /// <param name="salvar">Caso de uso SalvarTempoDoTime.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os tempos de sessão salvos.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · C ou fora do time deboaz.</response>
    /// <response code="422">Dados inválidos ou fora dos limites.</response>
    [HttpPut("time/tempo-sessao", Name = "salvarTempoDoTime")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.C), AcaoAuditada.SalvarTempoSessao, Alvo = AlvoAuditado.Time)]
    [ProducesResponseType<TempoDoTimeLidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TempoDoTimeLidoDto>> SalvarDoTime(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TempoDoTimeCorpo? corpo,
        [FromServices] ISalvarTempoDoTime salvar,
        CancellationToken ct) =>
        Ok(await salvar.Executar(new TempoDoTimeDto(HttpContext.QuemPede(), corpo?.MembrosNavegador, corpo?.MembrosApp, corpo?.MastersNavegador,
            corpo?.MastersApp), HttpContext.NovoTraceId(), ct));
}
