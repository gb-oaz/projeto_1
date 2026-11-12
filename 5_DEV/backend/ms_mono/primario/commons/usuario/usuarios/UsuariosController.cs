using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.Commons.Usuario.Usuarios;

/// <summary>
/// O staff, os participantes e os membros do time (17_modulo-usuario): gerar a
/// senha, sem login; listar, abrir, cadastrar, editar, novo código, desativar e
/// reativar, com MOD-03 · U.
/// </summary>
[ApiController]
[Route("usuarios")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoUsuarios)]
[Produces("application/json")]
public sealed class UsuariosController : ControllerBase
{
    public const string Tag = "Usuários";
    public const string Descricao =
        "Staff, participantes e membros do time: gerar a senha com o código de liberação (público) e, com MOD-03 · U, " +
        "listar, abrir, cadastrar, editar, gerar códigos, desativar, reativar e o card";

    const string Usuarios = CalculoDePermissoes.Usuarios;

    // ── sem login ──────────────────────────────────────────────────────────────

    /// <summary>Mostrar a senha nova a quem tem o código de liberação</summary>
    /// <remarks>
    /// Endereço público (RF-109, RF-110, RN-181, RN-182): a senha sai uma vez, com o comprovante para confirmar.
    /// A 5ª tentativa errada seguida bloqueia (RNF-204); o código vale 72 h.
    /// </remarks>
    /// <param name="corpo">A entrada e o código de liberação.</param>
    /// <param name="gerar">Caso de uso GerarSenha.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A senha gerada (mostrada uma vez) e o comprovante.</response>
    /// <response code="401">Entrada ou código de liberação não conferem.</response>
    /// <response code="410">Código de liberação vencido.</response>
    /// <response code="422">Dados inválidos.</response>
    /// <response code="429">Bloqueado por tentativas erradas seguidas.</response>
    [HttpPost("gerar-senha", Name = "gerarSenha")]
    [ProducesResponseType<SenhaGeradaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status410Gone)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SenhaGeradaDto>> GerarSenha(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GeracaoDeSenhaDto? corpo,
        [FromServices] IGerarSenha gerar,
        CancellationToken ct) =>
        Ok(await gerar.Executar(corpo ?? new(null, null), HttpContext.NovoTraceId(), ct));

    // ── com login ──────────────────────────────────────────────────────────────

    /// <summary>Listar e buscar o staff, os participantes ou os membros do time</summary>
    /// <remarks>
    /// Exige MOD-03 · U (RF-105, RN-185, RN-186, RN-188). 50 por página, de A a Z; a busca acha nome, entrada, matrícula
    /// e código. Sem tipo, busca em todos os tipos que quem pede pode listar, e cada item diz o seu.
    /// </remarks>
    /// <param name="tipo">Só usuários deste tipo; ausente, todos os que quem pede pode listar.</param>
    /// <param name="busca">Texto buscado no nome, na entrada, na matrícula e no código.</param>
    /// <param name="estado">Só os usuários neste estado.</param>
    /// <param name="pagina">A página, a partir de 1; ausente é a primeira.</param>
    /// <param name="listar">Caso de uso ListarUsuarios.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A página de usuários, de A a Z.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U.</response>
    /// <response code="422">Filtro ou página inválidos.</response>
    [HttpGet("", Name = "listarUsuarios")]
    [ComAcesso(Usuarios, nameof(Abas.U))]
    [ProducesResponseType<PaginaDto<UsuarioNaListaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaginaDto<UsuarioNaListaDto>>> Listar(
        [FromQuery] string? tipo,
        [FromQuery] string? busca,
        [FromQuery] string? estado,
        [FromQuery] string? pagina,
        [FromServices] IListarUsuarios listar,
        CancellationToken ct) =>
        Ok(await listar.Executar(new ListaDeUsuariosDto(HttpContext.QuemPede(), tipo, busca, estado, Paginacao.Pagina(pagina)), ct));

    /// <summary>Montar o card Usuários</summary>
    /// <remarks>
    /// Exige MOD-03 · U (RN-189, RN-280, 15_modulos RN-276 a RN-278). O card guardado do perfil de quem pede (DEBOAZ,
    /// INSTITUICAO, STAFF ou AUTONOMO), com os 4 tamanhos e as 3 séries; o selo, "Sem acesso há 30 dias" e "Códigos
    /// vencidos" são refeitos na leitura. O participante não tem o card.
    /// </remarks>
    /// <param name="obter">Caso de uso ObterCardUsuarios.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O card no formato comum dos módulos.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido ou sessão que terminou.</response>
    /// <response code="403">Participante, ou staff sem MOD-03 · U.</response>
    [HttpGet("card", Name = "obterCardUsuarios")]
    [ComAcesso(Usuarios, nameof(Abas.U), FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<CardDoModuloDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CardDoModuloDto>> ObterCard([FromServices] IObterCardUsuarios obter, CancellationToken ct) =>
        Ok(await obter.Executar(new QuemPedeDto(HttpContext.QuemPede()), ct));

    /// <summary>Acompanhar o card Usuários ao vivo</summary>
    /// <remarks>
    /// Exige MOD-03 · U (RN-281, 15_modulos RN-279). Fluxo text/event-stream: o evento "card" ao abrir e, a cada
    /// intervalo (5, 15, 30, 60 ou 300 s; padrão 300), se a versao mudou; ": ping" a cada 30 s; "encerrado" com
    /// LIMITE_CONEXOES, SESSAO_TERMINADA ou ACESSO_NEGADO antes de fechar. Até 3 conexões por usuário.
    /// </remarks>
    /// <param name="intervalo">Segundos entre as conferências: 5, 15, 30, 60 ou 300.</param>
    /// <param name="acompanhar">Caso de uso AcompanharCardAoVivo.</param>
    /// <param name="json">O JSON das APIs, igual ao de GET /usuarios/card.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido ou sessão que terminou.</response>
    /// <response code="403">Participante, ou staff sem MOD-03 · U.</response>
    /// <response code="422">Intervalo fora da lista.</response>
    [HttpGet("card/ao-vivo", Name = "acompanharCardAoVivo")]
    [ComAcesso(Usuarios, nameof(Abas.U), FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task AcompanharAoVivo(
        [FromQuery] string? intervalo,
        [FromServices] IAcompanharCardAoVivo acompanhar,
        [FromServices] IOptions<JsonOptions> json,
        CancellationToken ct)
    {
        var acesso = HttpContext.Acesso();
        var (conexao, card) = await acompanhar.Abrir(new AberturaAoVivoDto(acesso.Sub, acesso.Sid, intervalo), ct);
        try
        {
            var fluxo = new FluxoDeEventos(HttpContext, json.Value.JsonSerializerOptions);
            await fluxo.Abrir(ct);
            await fluxo.Card(card, ct);
            while (true)
            {
                var passo = await acompanhar.Proximo(conexao, ct);
                if (passo.Ping) await fluxo.Ping(ct);
                else if (passo.Card is { } novo) await fluxo.Card(novo, ct);
                else if (passo.Encerrado is { } motivo)
                {
                    await fluxo.Encerrado(motivo, ct);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a conexão fechou do outro lado
        }
        finally
        {
            await acompanhar.Fechar(conexao, CancellationToken.None);
        }
    }

    /// <summary>Ler a escolha de atualização dos cards</summary>
    /// <remarks>
    /// Com login, sem módulo exigido (RN-283): de quem pede, pelo token. Sem escolha salva, NA_ABERTURA e 300. Sem
    /// auditoria nem X-Trace-Id.
    /// </remarks>
    /// <param name="obter">Caso de uso ObterPreferencias.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A escolha.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido ou sessão que terminou.</response>
    [HttpGet("eu/preferencias", Name = "obterPreferencias")]
    [ComSessao]
    [ProducesResponseType<PreferenciasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PreferenciasDto>> ObterPreferencias([FromServices] IObterPreferencias obter, CancellationToken ct) =>
        Ok(await obter.Executar(new QuemPedeDto(HttpContext.QuemPede()), ct));

    /// <summary>Salvar a escolha de atualização dos cards</summary>
    /// <remarks>
    /// Com login, sem módulo exigido (RN-283): troca a escolha inteira, que acompanha a pessoa em todos os aparelhos.
    /// Sem auditoria nem X-Trace-Id.
    /// </remarks>
    /// <param name="corpo">A escolha: atualizacaoCards e intervaloAoVivo.</param>
    /// <param name="salvar">Caso de uso SalvarPreferencias.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A escolha salva.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido ou sessão que terminou.</response>
    /// <response code="422">Campo inválido.</response>
    [HttpPut("eu/preferencias", Name = "salvarPreferencias")]
    [ComSessao]
    [ProducesResponseType<PreferenciasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PreferenciasDto>> SalvarPreferencias(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PreferenciasCorpo? corpo,
        [FromServices] ISalvarPreferencias salvar,
        CancellationToken ct) =>
        Ok(await salvar.Executar(new NovasPreferenciasDto(HttpContext.QuemPede(), corpo?.AtualizacaoCards, corpo?.IntervaloAoVivo), ct));

    /// <summary>Abrir o usuário selecionado</summary>
    /// <remarks>Exige MOD-03 · U (RF-153, RN-186). Id de outra conta responde 404 (RN-235).</remarks>
    /// <param name="id">O id do usuário.</param>
    /// <param name="ler">Caso de uso LerUsuario.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O usuário.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U.</response>
    /// <response code="404">Usuário inexistente ou de outra conta.</response>
    [HttpGet("{id}", Name = "lerUsuario")]
    [ComAcesso(Usuarios, nameof(Abas.U))]
    [ProducesResponseType<UsuarioLidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioLidoDto>> Ler([FromRoute] string id, [FromServices] ILerUsuario ler, CancellationToken ct) =>
        Ok(await ler.Executar(new PedidoDeUsuarioDto(HttpContext.QuemPede(), id), ct));

    /// <summary>Cadastrar um staff, um participante ou um membro do time</summary>
    /// <remarks>
    /// Exige MOD-03 · U (RF-106, RF-108, RN-164, RN-165, RN-188, RN-246). O participante recebe o código de acesso
    /// como login, sem senha; staff e membros recebem o código de liberação, mostrado uma vez, e começam sem módulos.
    /// </remarks>
    /// <param name="corpo">O cadastro do usuário.</param>
    /// <param name="criar">Caso de uso CriarUsuario.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">Usuário cadastrado, com o código de acesso ou de liberação.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U.</response>
    /// <response code="409">Conflito com um usuário existente.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost("", Name = "criarUsuario")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.CriarUsuario)]
    [ProducesResponseType<UsuarioCriadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UsuarioCriadoDto>> Criar(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NovoUsuarioCorpo? corpo,
        [FromServices] ICriarUsuario criar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await criar.Executar(new NovoUsuarioDto(HttpContext.QuemPede(), corpo?.Tipo, corpo?.Nome,
            corpo?.NomeCurto, corpo?.Matricula, corpo?.Responsaveis), HttpContext.NovoTraceId(), ct));

    /// <summary>Editar nome, nome curto, matrícula e responsáveis</summary>
    /// <remarks>
    /// Exige MOD-03 · U e ser superior (RF-153, RN-244). Nome curto novo: o staff mantém a senha e as sessões
    /// terminam; o participante recebe código novo. "responsaveis" vai inteiro.
    /// </remarks>
    /// <param name="id">O id do usuário.</param>
    /// <param name="corpo">Os campos editados.</param>
    /// <param name="editar">Caso de uso EditarUsuario.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O usuário editado.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior do usuário.</response>
    /// <response code="404">Usuário inexistente ou de outra conta.</response>
    /// <response code="409">Conflito com um usuário existente.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPatch("{id}", Name = "editarUsuario")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.EditarUsuario, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<UsuarioEditadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UsuarioEditadoDto>> Editar(
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EdicaoDeUsuarioCorpo? corpo,
        [FromServices] IEditarUsuario editar,
        CancellationToken ct) =>
        Ok(await editar.Executar(new EdicaoDeUsuarioDto(HttpContext.QuemPede(), id, corpo?.Nome, corpo?.NomeCurto, corpo?.Matricula,
            corpo?.Responsaveis), HttpContext.NovoTraceId(), ct));

    /// <summary>Gerar o código de liberação para quem esqueceu a senha</summary>
    /// <remarks>Exige MOD-03 · U e ser superior (RF-110, RN-169, RN-190, RNF-203). A senha antiga e as sessões terminam.</remarks>
    /// <param name="id">O id do usuário.</param>
    /// <param name="pedir">Caso de uso PedirNovaGeracaoDeSenha.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">O código de liberação novo, mostrado uma vez.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior do usuário.</response>
    /// <response code="404">Usuário inexistente ou de outra conta.</response>
    /// <response code="422">Pedido inválido para este usuário.</response>
    [HttpPost("{id}/nova-geracao-senha", Name = "pedirNovaGeracaoDeSenha")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.NovaGeracaoSenha, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<CodigoDeLiberacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CodigoDeLiberacaoDto>> PedirNovaGeracaoDeSenha(
        [FromRoute] string id,
        [FromServices] IPedirNovaGeracaoDeSenha pedir,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await pedir.Executar(new PedidoDeUsuarioDto(HttpContext.QuemPede(), id), HttpContext.NovoTraceId(), ct));

    /// <summary>Trocar o código de acesso perdido de um participante</summary>
    /// <remarks>Exige MOD-03 · U (RF-154, RN-184, RN-245). O código anterior e as sessões dele terminam na hora.</remarks>
    /// <param name="id">O id do participante.</param>
    /// <param name="gerar">Caso de uso GerarNovoCodigoDeAcesso.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O código de acesso novo.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U.</response>
    /// <response code="404">Usuário inexistente ou de outra conta.</response>
    /// <response code="422">Pedido inválido para este usuário.</response>
    [HttpPost("{id}/novo-codigo-acesso", Name = "gerarNovoCodigoDeAcesso")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.NovoCodigoAcesso, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<CodigoDeAcessoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CodigoDeAcessoDto>> GerarNovoCodigoDeAcesso(
        [FromRoute] string id,
        [FromServices] IGerarNovoCodigoDeAcesso gerar,
        CancellationToken ct) =>
        Ok(await gerar.Executar(new PedidoDeUsuarioDto(HttpContext.QuemPede(), id), HttpContext.NovoTraceId(), ct));

    /// <summary>Tirar o acesso de quem saiu, sem apagar</summary>
    /// <remarks>Exige MOD-03 · U e ser superior (RF-112, RF-141, RN-169, RN-174, RN-175). Todas as sessões terminam.</remarks>
    /// <param name="id">O id do usuário.</param>
    /// <param name="desativar">Caso de uso DesativarUsuario.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="204">Usuário desativado.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior do usuário.</response>
    /// <response code="404">Usuário inexistente ou de outra conta.</response>
    /// <response code="409">Conflito com o estado atual do usuário.</response>
    [HttpPost("{id}/desativar", Name = "desativarUsuario")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.DesativarUsuario, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar([FromRoute] string id, [FromServices] IDesativarUsuario desativar, CancellationToken ct)
    {
        await desativar.Executar(new PedidoDeUsuarioDto(HttpContext.QuemPede(), id), HttpContext.NovoTraceId(), ct);
        return NoContent();
    }

    /// <summary>Devolver o acesso, com as mesmas permissões</summary>
    /// <remarks>Exige MOD-03 · U e ser superior (RF-112). Volta PENDENTE se nunca gerou a senha nem entrou; senão ATIVO.</remarks>
    /// <param name="id">O id do usuário.</param>
    /// <param name="reativar">Caso de uso ReativarUsuario.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="204">Usuário reativado.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-03 · U ou sem ser superior do usuário.</response>
    /// <response code="404">Usuário inexistente ou de outra conta.</response>
    /// <response code="409">Conflito com o estado atual do usuário.</response>
    [HttpPost("{id}/reativar", Name = "reativarUsuario")]
    [ComAcessoAuditado(Usuarios, nameof(Abas.U), AcaoAuditada.ReativarUsuario, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reativar([FromRoute] string id, [FromServices] IReativarUsuario reativar, CancellationToken ct)
    {
        await reativar.Executar(new PedidoDeUsuarioDto(HttpContext.QuemPede(), id), HttpContext.NovoTraceId(), ct);
        return NoContent();
    }
}
