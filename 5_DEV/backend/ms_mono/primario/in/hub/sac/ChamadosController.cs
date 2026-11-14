using Boaz.Core.Commons.Auditoria.Domain;
using System.Diagnostics;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Primario.Out.Hub.Sac;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>
/// Os chamados do SAC (18_modulo-sac, sac-contrato-api v02), com MOD-02 · U:
/// abrir, listar, ler, a conversa (e ao vivo), assumir, mudar o status,
/// reabrir, encerrar e avaliar. O papel decide o lado (conta ou time) no caso de uso.
/// </summary>
[ApiController]
[Route("sac/chamados")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoSac)]
[Produces("application/json")]
public sealed class ChamadosController : ControllerBase
{
    public const string Tag = "Chamados";
    public const string Descricao =
        "Com MOD-02 · U: a conta abre, acompanha, responde, reabre, encerra e avalia; o time vê a fila, assume, responde " +
        "(com nota interna), pede informação e resolve. A conversa ao vivo é um fluxo text/event-stream";

    const string U = nameof(Abas.U);

    /// <summary>Abrir um chamado</summary>
    /// <remarks>
    /// Só a conta master e o autônomo (RF-171, RN-291, RN-294); o time recebe 403. Os anexos já foram enviados em
    /// POST /sac/anexos. Até 10 chamados em andamento por conta. Auditado como ABRIR_CHAMADO (RN-297).
    /// </remarks>
    /// <param name="corpo">Categoria, assunto, descrição, o chamado relacionado e os ids dos anexos.</param>
    /// <param name="abrir">Caso de uso AbrirChamado.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">O chamado aberto, com o prazo de primeira resposta.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é do time.</response>
    /// <response code="409">LIMITE_CHAMADOS: 10 em andamento na conta.</response>
    /// <response code="422">Dados inválidos ou mais de 5 anexos.</response>
    [HttpPost("", Name = "abrirChamado")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.AbrirChamado)]
    [ProducesResponseType<ChamadoAbertoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Abrir([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoAbrirChamado? corpo,
        [FromServices] IAbrirChamado abrir, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await abrir.Executar(corpo ?? new(null, null, null, null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>Listar os chamados do lado de quem pede</summary>
    /// <remarks>
    /// A conta vê os dela, em Meus chamados, do mais novo (RF-172); o time vê a fila de todas as contas, quem espera há
    /// mais tempo primeiro (RF-174). 50 por página. Os filtros de responsável, atrasado e nota máxima são só do time.
    /// </remarks>
    /// <param name="situacao">EM_ANDAMENTO ou ENCERRADOS.</param>
    /// <param name="status">Só os chamados neste status.</param>
    /// <param name="categoria">Só os chamados desta categoria.</param>
    /// <param name="busca">Texto buscado no número, no assunto e na conta.</param>
    /// <param name="pagina">A página, a partir de 1; ausente é a primeira.</param>
    /// <param name="responsavel">nenhum: os sem responsável; eu: os de quem pede.</param>
    /// <param name="atrasado">true: só os que passaram do prazo.</param>
    /// <param name="notaMaxima">Só os avaliados com nota até esta, de 1 a 5.</param>
    /// <param name="listar">Caso de uso ListarChamados.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A página de chamados.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    /// <response code="422">Filtro ou página inválidos.</response>
    [HttpGet("", Name = "listarChamados")]
    [ComAcesso(Sac.Modulo, U)]
    [ProducesResponseType<ListaDeChamadosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? situacao, [FromQuery] string? status, [FromQuery] string? categoria, [FromQuery] string? busca,
        [FromQuery] string? pagina, [FromQuery] string? responsavel, [FromQuery] string? atrasado, [FromQuery] string? notaMaxima,
        [FromServices] IListarChamados listar, CancellationToken ct) =>
        Ok(await listar.Executar(new FiltroDeChamadosDto(situacao, status, categoria, busca, Paginacao.Pagina(pagina), responsavel, atrasado,
            notaMaxima), HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Ler o chamado, as mensagens e a linha do tempo</summary>
    /// <remarks>
    /// Marca o chamado como lido por quem pede (RF-173). A nota interna só vai para o time; o chamado de outra conta
    /// responde 404, como o que não existe (RN-288).
    /// </remarks>
    /// <param name="id">O número do chamado, como SAC-000042.</param>
    /// <param name="ler">Caso de uso LerChamado.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O chamado com a conversa.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    /// <response code="404">CHAMADO_INEXISTENTE: não existe ou é de outra conta.</response>
    [HttpGet("{id}", Name = "lerChamado")]
    [ComAcesso(Sac.Modulo, U)]
    [ProducesResponseType<ChamadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ler([FromRoute] string id, [FromServices] ILerChamado ler, CancellationToken ct) =>
        Ok(await ler.Executar(id, HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Enviar uma mensagem ou uma nota interna</summary>
    /// <remarks>
    /// A conta responde; o time responde ou escreve a nota interna, que a conta nunca vê (RF-173, RF-175). Os anexos são
    /// do próprio usuário (RN-292). Auditado como ENVIAR_MENSAGEM, com o chamado como alvo (RN-297).
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="corpo">O texto, os ids dos anexos e, no time, se é nota interna.</param>
    /// <param name="enviar">Caso de uso EnviarMensagem.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">A mensagem gravada e o status do chamado.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou a conta pedindo nota interna.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    /// <response code="409">Chamado fechado, sem conversa ou transição inválida.</response>
    /// <response code="422">Dados inválidos ou mais de 5 anexos.</response>
    [HttpPost("{id}/mensagens", Name = "enviarMensagem")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.EnviarMensagem, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<MensagemEnviadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Mensagem([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoMensagem? corpo, [FromServices] IEnviarMensagem enviar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await enviar.Executar(id, corpo ?? new(null, null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>Mensagem, nota e evento nunca mudam</summary>
    /// <remarks>
    /// PATCH, PUT e DELETE de uma mensagem respondem 405, sem corpo e com o cabeçalho Allow vazio, sem conferir o token
    /// (RN-293). Correção é uma nova mensagem.
    /// </remarks>
    /// <response code="405">Sem corpo: nada na conversa muda.</response>
    [HttpPatch("{id}/mensagens/{m}")]
    [HttpPut("{id}/mensagens/{m}")]
    [HttpDelete("{id}/mensagens/{m}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult MensagemNaoMuda()
    {
        Response.Headers.Allow = "";
        return StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    /// <summary>O time assume o chamado</summary>
    /// <remarks>
    /// Só o time (RF-175). Se outro já tem o chamado, responde 409 JA_ASSUMIDO com o responsável, a menos que o pedido
    /// peça para substituir. Auditado como ASSUMIR_CHAMADO (RN-297).
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="corpo">substituir: true para tirar o chamado de quem o tem.</param>
    /// <param name="assumir">Caso de uso AssumirChamado.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O chamado com o responsável novo.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é da conta.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    /// <response code="409">JA_ASSUMIDO ou CHAMADO_FECHADO.</response>
    [HttpPost("{id}/assumir", Name = "assumirChamado")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.AssumirChamado, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<ResponsavelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assumir([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoAssumir? corpo, [FromServices] IAssumirChamado assumir,
        CancellationToken ct) =>
        Ok(await assumir.Executar(id, corpo ?? new(null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>O time pede informação ou resolve</summary>
    /// <remarks>
    /// Só o time (RF-176): AGUARDANDO_CLIENTE ou RESOLVIDO, com a mensagem para a conta, pela tabela da RN-290.
    /// Auditado como MUDAR_STATUS (RN-297).
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="corpo">O status novo e a mensagem.</param>
    /// <param name="mudar">Caso de uso MudarStatus.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O chamado com o status novo.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é da conta.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    /// <response code="409">TRANSICAO_INVALIDA ou CHAMADO_FECHADO.</response>
    /// <response code="422">Status ou mensagem inválidos.</response>
    [HttpPost("{id}/status", Name = "mudarStatus")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.MudarStatus, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<StatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Status([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoStatus? corpo, [FromServices] IMudarStatus mudar,
        CancellationToken ct) =>
        Ok(await mudar.Executar(id, corpo ?? new(null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>A conta reabre o chamado resolvido</summary>
    /// <remarks>
    /// Só a conta, em até 7 dias do RESOLVIDO (RF-177, RN-290), com a mensagem do que ainda falta. Auditado como
    /// REABRIR_CHAMADO (RN-297).
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="corpo">A mensagem da reabertura.</param>
    /// <param name="reabrir">Caso de uso ReabrirChamado.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O chamado reaberto, com o responsável.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é do time.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    /// <response code="409">PRAZO_REABERTURA_VENCIDO, TRANSICAO_INVALIDA ou CHAMADO_FECHADO.</response>
    /// <response code="422">Mensagem inválida.</response>
    [HttpPost("{id}/reabrir", Name = "reabrirChamado")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.ReabrirChamado, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<ResponsavelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reabrir([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoReabrir? corpo, [FromServices] IReabrirChamado reabrir,
        CancellationToken ct) =>
        Ok(await reabrir.Executar(id, corpo ?? new(null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>A conta encerra o chamado</summary>
    /// <remarks>
    /// Só a conta, com o chamado em andamento (RF-177): fecha com fechamento PELA_CONTA, que não pode ser avaliado.
    /// Auditado como ENCERRAR_CHAMADO (RN-297).
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="encerrar">Caso de uso EncerrarChamado.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O chamado fechado, com resolvidoEm.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é do time.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    /// <response code="409">TRANSICAO_INVALIDA ou CHAMADO_FECHADO.</response>
    [HttpPost("{id}/encerrar", Name = "encerrarChamado")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.EncerrarChamado, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<StatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Encerrar([FromRoute] string id, [FromServices] IEncerrarChamado encerrar, CancellationToken ct) =>
        Ok(await encerrar.Executar(id, HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>A conta avalia e confirma a solução</summary>
    /// <remarks>
    /// Só a conta, uma vez por chamado (RF-178, RN-296): nota de 1 a 5 e o comentário opcional. Do RESOLVIDO, fecha com
    /// fechamento CONFIRMADO; do fechado pelo prazo há até 7 dias, só guarda a avaliação. Auditado como AVALIAR_CHAMADO (RN-297).
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="corpo">A nota e o comentário.</param>
    /// <param name="avaliar">Caso de uso AvaliarChamado.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">O chamado com a avaliação.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U, ou quem pede é do time.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    /// <response code="409">JA_AVALIADO, PRAZO_AVALIACAO_VENCIDO ou TRANSICAO_INVALIDA.</response>
    /// <response code="422">Nota ou comentário inválidos.</response>
    [HttpPost("{id}/avaliacao", Name = "avaliarChamado")]
    [ComAcessoAuditado(Sac.Modulo, U, AcaoAuditada.AvaliarChamado, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<AvaliadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Avaliar([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoAvaliacao? corpo, [FromServices] IAvaliarChamado avaliar,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await avaliar.Executar(id, corpo ?? new(null, null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));

    /// <summary>Acompanhar a conversa ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RNF-230): o evento "mensagem" (id = o da mensagem) assim que chega, em até 5 s, e
    /// ": ping" a cada 30 s. O Last-Event-ID da reconexão diz de onde seguir. A nota interna nunca vai para a conta.
    /// As recusas saem como JSON, antes de abrir o fluxo.
    /// </remarks>
    /// <param name="id">O número do chamado.</param>
    /// <param name="acompanhar">Caso de uso AcompanharChamado.</param>
    /// <param name="json">O JSON das APIs, igual ao de GET /sac/chamados/{id}.</param>
    /// <param name="ct">Cancelamento do pedido (a conexão fechou).</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    /// <response code="404">CHAMADO_INEXISTENTE.</response>
    [HttpGet("{id}/ao-vivo", Name = "acompanharChamado")]
    [ComAcesso(Sac.Modulo, U)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task AoVivo([FromRoute] string id, [FromServices] IAcompanharChamado acompanhar, [FromServices] IOptions<JsonOptions> json,
        CancellationToken ct)
    {
        var quem = HttpContext.QuemPedeAoSac();
        var conversa = await acompanhar.Abrir(id, Request.Headers["Last-Event-ID"].FirstOrDefault(), quem, ct);
        var fluxo = new EventosDoSac(HttpContext, json.Value.JsonSerializerOptions);
        try
        {
            await fluxo.Abrir(ct);
            var desdeOPing = Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                var (mensagens, seguinte) = await acompanhar.Proximas(conversa, Sac.Conferencia, ct);
                conversa = seguinte;
                foreach (var mensagem in mensagens) await fluxo.Evento("mensagem", mensagem.Id, mensagem, ct);
                if (desdeOPing.Elapsed < Sac.Ping) continue;
                await fluxo.Ping(ct);
                desdeOPing.Restart();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a conexão fechou do outro lado
        }
    }
}
