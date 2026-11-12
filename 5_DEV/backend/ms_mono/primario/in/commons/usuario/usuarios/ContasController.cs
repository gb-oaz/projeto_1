using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;

/// <summary>
/// As contas assinantes (17_modulo-usuario): criar a conta e pedir ajuda da
/// master, sem login; listar, ver, mudar o estado e gerar o código da master,
/// só o time deboaz com MOD-03 · U.
/// </summary>
[ApiController]
[Route("contas")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoUsuarios)]
[Produces("application/json")]
public sealed class ContasController : ControllerBase
{
    public const string Tag = "Contas";
    public const string Descricao =
        "Contas assinantes: criar (público), sugerir a sigla, pedir ajuda da master e, só o time deboaz, listar, ver, " +
        "mudar o estado e gerar o código da conta master";

    const string Usuarios = CalculoDePermissoes.Usuarios;

    // ── sem login ──────────────────────────────────────────────────────────────

    /// <summary>Criar a conta de uma instituição ou de um educador autônomo</summary>
    /// <remarks>
    /// Endereço público, da tela Criar conta do hub (RF-104, RN-179, RN-180, RN-242). A senha da conta master é
    /// gerada e mostrada uma vez; a pessoa anota e confirma em POST /auth/hub/confirmar-senha com o comprovante.
    /// </remarks>
    /// <param name="corpo">O cadastro da conta.</param>
    /// <param name="criar">Caso de uso CriarConta.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">Conta criada, com a entrada da master, a senha (mostrada uma vez) e o comprovante.</response>
    /// <response code="409">Conflito com uma conta existente.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost("", Name = "criarConta")]
    [ProducesResponseType<ContaCriadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContaCriadaDto>> Criar(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NovaContaDto? corpo,
        [FromServices] ICriarConta criar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await criar.Executar(corpo ?? new(null, null, null, null, null, null, null), HttpContext.NovoTraceId(), ct));

    /// <summary>Sugerir a sigla a partir do nome</summary>
    /// <remarks>
    /// Endereço público (RN-179): iniciais sem conectores, completadas com a última palavra. Sigla null e livre false
    /// se o nome não der 5 letras.
    /// </remarks>
    /// <param name="nome">O nome da instituição ou do educador.</param>
    /// <param name="sugerir">Caso de uso SugerirSigla.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A sigla sugerida e se está livre.</response>
    [HttpGet("sigla-sugerida", Name = "sugerirSigla")]
    [ProducesResponseType<SiglaSugeridaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SiglaSugeridaDto>> SugerirSigla([FromQuery] string? nome, [FromServices] ISugerirSigla sugerir, CancellationToken ct) =>
        Ok(await sugerir.Executar(new NomeDaContaDto(nome), ct));

    /// <summary>Pedir ajuda ao SAC quando a conta master esqueceu a senha</summary>
    /// <remarks>Endereço público (RF-111, RNF-205): vira uma mensagem no SAC. Até 3 envios em 24 h do mesmo aparelho.</remarks>
    /// <param name="corpo">A instituição e o contato.</param>
    /// <param name="pedir">Caso de uso PedirAjudaDaMaster.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="202">Pedido recebido pelo SAC.</response>
    /// <response code="422">Dados inválidos.</response>
    /// <response code="429">Mais de 3 envios em 24 h do mesmo aparelho.</response>
    [HttpPost("esqueci-senha-master", Name = "pedirAjudaDaMaster")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> PedirAjudaDaMaster(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AjudaDaMasterCorpo? corpo,
        [FromServices] IPedirAjudaDaMaster pedir,
        CancellationToken ct)
    {
        await pedir.Executar(new AjudaDaMasterDto(corpo?.Instituicao, corpo?.Contato, Aparelho()), HttpContext.NovoTraceId(), ct);
        return StatusCode(StatusCodes.Status202Accepted);
    }

    // ── time deboaz ────────────────────────────────────────────────────────────

    /// <summary>Listar e buscar as contas assinantes</summary>
    /// <remarks>Só o time deboaz, com MOD-03 · U (RF-113, RN-185, RN-186). A conta do time não aparece.</remarks>
    /// <param name="busca">Texto buscado no cadastro das contas.</param>
    /// <param name="estado">Só as contas neste estado.</param>
    /// <param name="pagina">A página, a partir de 1; ausente é a primeira.</param>
    /// <param name="listar">Caso de uso ListarContas.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A página de contas.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou fora do time deboaz.</response>
    /// <response code="422">Filtro ou página inválidos.</response>
    [HttpGet("", Name = "listarContas")]
    [ComAcesso(Usuarios, nameof(Abas.U))]
    [ProducesResponseType<PaginaDto<ContaNaListaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaginaDto<ContaNaListaDto>>> Listar(
        [FromQuery] string? busca,
        [FromQuery] string? estado,
        [FromQuery] string? pagina,
        [FromServices] IListarContas listar,
        CancellationToken ct) =>
        Ok(await listar.Executar(new ListaDeContasDto(HttpContext.QuemPede(), busca, estado, Paginacao.Pagina(pagina)), ct));

    /// <summary>Ver o cadastro de uma conta</summary>
    /// <remarks>Só o time deboaz, com MOD-03 · U (RN-187): o cadastro, sem usuários nem conteúdo.</remarks>
    /// <param name="id">O id da conta.</param>
    /// <param name="ler">Caso de uso LerConta.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O cadastro da conta.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou fora do time deboaz.</response>
    /// <response code="404">Conta inexistente.</response>
    [HttpGet("{id}", Name = "lerConta")]
    [ComAcesso(Usuarios, nameof(Abas.U))]
    [ProducesResponseType<ContaLidaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContaLidaDto>> Ler([FromRoute] string id, [FromServices] ILerConta ler, CancellationToken ct) =>
        Ok(await ler.Executar(new PedidoDeContaDto(HttpContext.QuemPede(), id), ct));

    /// <summary>Suspender, desativar, reativar ou encerrar uma conta</summary>
    /// <remarks>
    /// Só o time deboaz, com MOD-03 · U (RF-113, RN-173, RN-174, RN-175, RNF-203). Fora de ATIVA ninguém da conta
    /// entra e as sessões terminam; encerrada não volta.
    /// </remarks>
    /// <param name="id">O id da conta.</param>
    /// <param name="corpo">O estado novo.</param>
    /// <param name="mudar">Caso de uso MudarEstadoDaConta.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A conta com o estado novo.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou fora do time deboaz.</response>
    /// <response code="404">Conta inexistente.</response>
    /// <response code="409">A mudança não vale a partir do estado atual.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost("{id}/estado", Name = "mudarEstadoDaConta")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.MudarEstadoConta, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<ContaComEstadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContaComEstadoDto>> MudarEstado(
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EstadoDaContaCorpo? corpo,
        [FromServices] IMudarEstadoDaConta mudar,
        CancellationToken ct) =>
        Ok(await mudar.Executar(new EstadoDaContaDto(HttpContext.QuemPede(), id, corpo?.Estado), HttpContext.NovoTraceId(), ct));

    /// <summary>Conferir o cadastro e gerar o código da conta master</summary>
    /// <remarks>
    /// Só o time deboaz, com MOD-03 · U (RN-172, RN-190): o CNPJ e 2 de 3 entre quem cadastrou, telefone e
    /// data do último pagamento.
    /// </remarks>
    /// <param name="id">O id da conta.</param>
    /// <param name="corpo">Os dados conferidos do cadastro.</param>
    /// <param name="gerar">Caso de uso GerarCodigoDaMaster.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">Código da conta master gerado.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou fora do time deboaz.</response>
    /// <response code="404">Conta inexistente.</response>
    /// <response code="422">Dados inválidos ou que não conferem com o cadastro.</response>
    [HttpPost("{id}/codigo-master", Name = "gerarCodigoDaMaster")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.CodigoMaster)]
    [ProducesResponseType<CodigoDaMasterDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CodigoDaMasterDto>> GerarCodigoDaMaster(
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ConferenciaDaMasterCorpo? corpo,
        [FromServices] IGerarCodigoDaMaster gerar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await gerar.Executar(new ConferenciaDaMasterDto(HttpContext.QuemPede(), id, corpo?.Documento,
            corpo?.Responsavel, corpo?.Telefone, corpo?.UltimoPagamento), HttpContext.NovoTraceId(), ct));

    // O aparelho do pedido de ajuda (RNF-205): o endereço de quem envia.
    string Aparelho() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
