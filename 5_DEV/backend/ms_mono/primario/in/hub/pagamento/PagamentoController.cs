using System.Globalization;
using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Dtos;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.In;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.In;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.In;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Pagamento;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Pagamento;

// O que os controllers do Pagamento repetem: o módulo, o grupo da
// documentação e quem pede, como o token conferido diz (RN-298).
static class PagamentoHttp
{
    // o MOD-01 (15_modulos, 19 RN-298)
    public const string Modulo = "MOD-01";

    // a API do Pagamento no catálogo (/doc/api/pagamento.json)
    public const string Documento = DocumentacaoApi.DocumentoPagamento;

    // a conferência do pagamento ao vivo (RNF-238: em até 5 s) e o ": ping" a cada 30 s
    public static readonly TimeSpan Conferencia = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan Ping = TimeSpan.FromSeconds(30);

    public static QuemPede QuemPedeAoPagamento(this HttpContext http)
    {
        var acesso = http.Acesso();
        var usa = acesso.Mods.TryGetValue(Modulo, out var abas) && abas.Contains('U', StringComparison.Ordinal);
        return new QuemPede(acesso.Sub, acesso.Papel, acesso.ContaId, usa);
    }

    // a página da lista: número inteiro ≥ 1; fora disso, 422 no campo "pagina"
    public static int? Pagina(string? pagina) =>
        pagina is null ? null
        : int.TryParse(pagina, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? numero
        : throw FalhaDoPagamento.DadosInvalidos("pagina");

    // o PDF baixado, com o nome do contrato (attachment; filename="boleto-FAT-000402.pdf")
    public static FileContentResult Pdf(this ControllerBase controller, byte[] pdf, string nome)
    {
        controller.Response.Headers.ContentDisposition = $"attachment; filename=\"{nome}\"";
        return controller.File(pdf, "application/pdf");
    }
}

/// <summary>
/// O lado de quem paga (19_modulo-pagamento, pagamento-contrato-api v02), com
/// MOD-01 · U: as faturas da conta, o detalhe, o novo pagamento, os avisos de
/// atraso e as formas aceitas. O administrador do deboaz recebe 403 (RN-298),
/// e o participante também: o token do quest é conferido na frente dele e
/// cai no 403 do módulo, não no 401 da frente (FrenteDoToken, como o Usuários).
/// </summary>
[ApiController]
[Route("pagamento")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = PagamentoHttp.Documento)]
[Produces("application/json")]
public sealed class FaturasController : ControllerBase
{
    public const string Tag = "Faturas";
    public const string Descricao =
        "Com MOD-01 · U: as faturas da conta (uma por competência, vencendo no dia 10), o detalhe com juros e tentativas, " +
        "o novo pagamento por PIX, cartão ou boleto, os avisos de atraso e as formas aceitas";

    const string U = nameof(Abas.U);

    /// <summary>Listar as faturas da conta</summary>
    /// <remarks>
    /// As faturas da conta de quem pede, com o resumo do topo (RF-182, RN-298, RN-300, RN-301): VENCIDA antes de
    /// A_PAGAR, a competência mais nova primeiro, 50 por página. Cada item diz o pagamento em aberto, se houver.
    /// </remarks>
    /// <param name="situacao">A_PAGAR (padrão: a pagar e vencidas), PAGAS ou TODAS.</param>
    /// <param name="pagina">A página, a partir de 1; ausente é a primeira.</param>
    /// <param name="listar">Caso de uso ListarFaturas.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A página de faturas e o resumo (a pagar, vencidas e o próximo vencimento).</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="422">DADOS_INVALIDOS em "situacao" ou "pagina".</response>
    [HttpGet("faturas", Name = "listarFaturas")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<ListaDeFaturasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar([FromQuery] string? situacao, [FromQuery] string? pagina, [FromServices] IListarFaturas listar,
        CancellationToken ct) =>
        Ok(await listar.Executar(situacao, PagamentoHttp.Pagina(pagina), HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Ler uma fatura</summary>
    /// <remarks>
    /// O detalhe com os itens cobrados, os juros de hoje e todas as tentativas de pagamento, cada uma com o seu traceId
    /// (RF-183, RN-301, RN-304, RN-310). Juros: valor × taxa ao dia vigente no vencimento × dias desde o dia seguinte.
    /// Fatura de outra conta responde 404, como a que não existe.
    /// </remarks>
    /// <param name="id">O número da fatura, como FAT-000402.</param>
    /// <param name="ler">Caso de uso LerFatura.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A fatura.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">FATURA_INEXISTENTE, inclusive a de outra conta.</response>
    [HttpGet("faturas/{id}", Name = "lerFatura")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<FaturaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ler([FromRoute] string id, [FromServices] ILerFatura ler, CancellationToken ct) =>
        Ok(await ler.Executar(id, HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Pagar a fatura</summary>
    /// <remarks>
    /// Cria o pagamento na forma escolhida (RF-184 a RF-188, RN-303, RNF-239): PIX (AGUARDANDO, com o copia e cola e o
    /// QR em SVG), cartão de crédito (PAGO na hora, RECUSADO ou PROCESSANDO), cartão de débito (AGUARDANDO, com a
    /// confirmacaoUrl do banco) ou boleto (AGUARDANDO, com a linha digitável e o PDF). O cartão vai só como o token
    /// gerado pela tela no intermediador; número, CVV ou validade no corpo são recusados. Um pagamento em aberto por
    /// fatura: para trocar de forma, cancele o que espera. Sem resposta do banco em 15 s, 503 e nada foi cobrado
    /// (RNF-241); o traceId do corpo do erro, mandado no X-Trace-Id da nova tentativa, vira o traceIdOrigem.
    /// Auditado como PAGAMENTO_CRIADO (RN-309).
    /// </remarks>
    /// <param name="id">O número da fatura, como FAT-000402.</param>
    /// <param name="corpo">A forma e, no cartão, o token.</param>
    /// <param name="criar">Caso de uso CriarPagamento.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">O pagamento criado, com o Location dele.</response>
    /// <response code="401">TOKEN_INVALIDO, SESSAO_TERMINADA ou PERMISSOES_DESATUALIZADAS.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">FATURA_INEXISTENTE, inclusive a de outra conta.</response>
    /// <response code="409">FATURA_PAGA, FATURA_CANCELADA ou PAGAMENTO_EM_ABERTO.</response>
    /// <response code="422">DADOS_INVALIDOS em "forma", "cartao" ou "cartao.token", ou FORMA_INDISPONIVEL.</response>
    /// <response code="503">BANCO_INDISPONIVEL: o banco não respondeu em 15 s; nada foi cobrado.</response>
    [HttpPost("faturas/{id}/pagamentos", Name = "criarPagamento")]
    [ComAcessoAuditado(PagamentoHttp.Modulo, U, AcaoAuditada.PagamentoCriado, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<PagamentoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Pagar([FromRoute] string id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoDePagamento? corpo,
        [FromServices] ICriarPagamento criar, CancellationToken ct)
    {
        var pagamento = await criar.Executar(id, corpo ?? new PedidoDePagamento(null, null), HttpContext.QuemPedeAoPagamento(),
            HttpContext.NovoTraceId(), HttpContext.TraceIdDeOrigem(), ct);
        return Created($"/pagamento/pagamentos/{pagamento.Id}", pagamento);
    }

    /// <summary>Ler os avisos de atraso</summary>
    /// <remarks>
    /// O aviso FATURA_EM_ATRASO de cada fatura vencida há mais que a tolerância da configuração, com os juros de hoje
    /// (RN-311, RN-313). Só no módulo: sem e-mail, SMS ou WhatsApp, e não bloqueia a conta.
    /// </remarks>
    /// <param name="ler">Caso de uso LerAvisos.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Os avisos abertos.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    [HttpGet("avisos", Name = "lerAvisos")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<ListaDeAvisosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Avisos([FromServices] ILerAvisos ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Ler as formas de pagamento ligadas</summary>
    /// <remarks>As formas ligadas agora na configuração, na ordem da tela de pagar (RF-184, RF-193).</remarks>
    /// <param name="ler">Caso de uso LerFormas.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As formas ligadas.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    [HttpGet("formas", Name = "lerFormas")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<FormasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Formas([FromServices] ILerFormas ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoPagamento(), ct));
}

/// <summary>
/// Um pagamento de quem paga, com MOD-01 · U: ler, cancelar o que ainda espera,
/// o PDF do boleto e do comprovante e o pagamento ao vivo (RF-185 a RF-189, RNF-238).
/// </summary>
[ApiController]
[Route("pagamento/pagamentos")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = PagamentoHttp.Documento)]
[Produces("application/json")]
public sealed class PagamentosController : ControllerBase
{
    public const string Tag = "Pagamentos";
    public const string Descricao =
        "Com MOD-01 · U: o estado de um pagamento, o cancelamento do que espera para trocar de forma, os PDFs do boleto e do " +
        "comprovante e o pagamento ao vivo, um fluxo text/event-stream";

    const string U = nameof(Abas.U);

    /// <summary>Ler um pagamento</summary>
    /// <remarks>
    /// O estado atual (RN-302, RN-303, RF-187): AGUARDANDO, PROCESSANDO, PAGO, RECUSADO, EXPIRADO, CANCELADO,
    /// ESTORNO_PENDENTE, ESTORNADO_PARCIAL ou ESTORNADO. "duplicidade": true quando pagou uma fatura que já estava paga.
    /// </remarks>
    /// <param name="id">O número do pagamento, como PAG-000773.</param>
    /// <param name="ler">Caso de uso LerPagamento.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O pagamento, no mesmo corpo da criação.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE, inclusive o de outra conta.</response>
    [HttpGet("{id}", Name = "lerPagamento")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<PagamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ler([FromRoute] string id, [FromServices] ILerPagamento ler, CancellationToken ct) =>
        Ok(await ler.Executar(id, HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Cancelar o pagamento que ainda espera</summary>
    /// <remarks>
    /// Desiste do pagamento em aberto para trocar de forma (RF-184, RN-303): o banco é avisado e a fatura fica livre.
    /// O já confirmado não se cancela. Auditado como PAGAMENTO_CANCELADO (RN-309).
    /// </remarks>
    /// <param name="id">O número do pagamento, como PAG-000772.</param>
    /// <param name="cancelar">Caso de uso CancelarPagamento.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O pagamento CANCELADO.</response>
    /// <response code="401">TOKEN_INVALIDO, SESSAO_TERMINADA ou PERMISSOES_DESATUALIZADAS.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE, inclusive o de outra conta.</response>
    /// <response code="409">PAGAMENTO_CONFIRMADO: o banco já confirmou.</response>
    [HttpPost("{id}/cancelar", Name = "cancelarPagamento")]
    [ComAcessoAuditado(PagamentoHttp.Modulo, U, AcaoAuditada.PagamentoCancelado, Alvo = AlvoAuditado.IdDaRota)]
    [ProducesResponseType<PagamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar([FromRoute] string id, [FromServices] ICancelarPagamento cancelar, CancellationToken ct) =>
        Ok(await cancelar.Executar(id, HttpContext.QuemPedeAoPagamento(), HttpContext.NovoTraceId(), ct));

    /// <summary>Baixar o PDF do boleto</summary>
    /// <remarks>
    /// O boleto gerado na criação, em application/pdf, baixado como boleto-FAT-000402.pdf (RF-188): a fatura, a conta,
    /// a competência, o valor, o vencimento, a linha digitável e o código de barras.
    /// </remarks>
    /// <param name="id">O número do pagamento por boleto, como PAG-000785.</param>
    /// <param name="ler">Caso de uso LerDocumentoDoPagamento.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O PDF do boleto (Content-Disposition: attachment).</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE, ou BOLETO_INEXISTENTE: o pagamento não é boleto.</response>
    [HttpGet("{id}/boleto", Name = "baixarBoleto")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound, "application/json")]
    public async Task<IActionResult> Boleto([FromRoute] string id, [FromServices] ILerDocumentoDoPagamento ler, CancellationToken ct)
    {
        var documento = await ler.Boleto(id, HttpContext.QuemPedeAoPagamento(), ct);
        return this.Pdf(PdfDoPagamento.Boleto(documento), $"boleto-{documento.FaturaId}.pdf");
    }

    /// <summary>Baixar o PDF do comprovante</summary>
    /// <remarks>
    /// O comprovante do pagamento confirmado pelo banco, em application/pdf, baixado como comprovante-FAT-000402.pdf
    /// (RF-189): a fatura, a conta, a forma (e o cartão), o valor pago, quando e a autenticação do banco.
    /// </remarks>
    /// <param name="id">O número do pagamento, como PAG-000773.</param>
    /// <param name="ler">Caso de uso LerDocumentoDoPagamento.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O PDF do comprovante (Content-Disposition: attachment).</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE, inclusive o de outra conta.</response>
    /// <response code="409">PAGAMENTO_NAO_CONFIRMADO: o banco ainda não confirmou.</response>
    [HttpGet("{id}/comprovante", Name = "baixarComprovante")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict, "application/json")]
    public async Task<IActionResult> Comprovante([FromRoute] string id, [FromServices] ILerDocumentoDoPagamento ler, CancellationToken ct)
    {
        var documento = await ler.Comprovante(id, HttpContext.QuemPedeAoPagamento(), ct);
        return this.Pdf(PdfDoPagamento.Comprovante(documento), $"comprovante-{documento.FaturaId}.pdf");
    }

    /// <summary>Acompanhar o pagamento ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RNF-238, RNF-244): cada mudança de status chega em até 5 s como o evento "pagamento"
    /// (id: o status; data: o id, o status e o traceId de quem mudou), ": ping" a cada 30 s, e o fluxo fecha depois do
    /// status final (PAGO, RECUSADO, EXPIRADO ou CANCELADO). Use fetch com o Authorization; o EventSource do navegador
    /// não manda o token.
    /// </remarks>
    /// <param name="id">O número do pagamento, como PAG-000781.</param>
    /// <param name="acompanhar">Caso de uso AcompanharPagamento.</param>
    /// <param name="json">As opções de JSON da API, para o corpo dos eventos.</param>
    /// <param name="ct">Cancelamento do pedido: a conexão fechada.</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U, o administrador do deboaz, o time ou o participante (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE, inclusive o de outra conta.</response>
    [HttpGet("{id}/ao-vivo", Name = "acompanharPagamento")]
    [ComAcesso(PagamentoHttp.Modulo, U, FrenteDoToken = true, SemSessaoValida = true)]
    [ProducesResponseType<EventoDoPagamentoDto>(StatusCodes.Status200OK, "text/event-stream")]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound, "application/json")]
    public async Task AoVivo([FromRoute] string id, [FromServices] IAcompanharPagamento acompanhar, [FromServices] IOptions<JsonOptions> json,
        CancellationToken ct)
    {
        var atual = await acompanhar.Abrir(id, HttpContext.QuemPedeAoPagamento(), ct);
        var fluxo = new EventosDoPagamento(HttpContext, json.Value.JsonSerializerOptions);
        try
        {
            await fluxo.Abrir(ct);
            var desdeOPing = TimeSpan.Zero;
            while (!ct.IsCancellationRequested && !atual.EhFinal())
            {
                if (await acompanhar.Proximo(atual, PagamentoHttp.Conferencia, ct) is { } novo)
                {
                    atual = novo;
                    await fluxo.Evento("pagamento", novo.Status, novo, ct);
                    desdeOPing = TimeSpan.Zero;
                    continue;
                }
                desdeOPing += PagamentoHttp.Conferencia;
                if (desdeOPing < PagamentoHttp.Ping) continue;
                desdeOPing = TimeSpan.Zero;
                await fluxo.Ping(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a conexão fechou do outro lado
        }
    }
}

/// <summary>
/// O aviso do banco (RN-305): sem login, vale a assinatura do intermediador.
/// O corpo e os cabeçalhos chegam como vieram; o adaptador do intermediador os lê.
/// </summary>
[ApiController]
[Route("pagamento/notificacoes")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = PagamentoHttp.Documento)]
[Produces("application/json")]
public sealed class NotificacoesController : ControllerBase
{
    public const string Tag = "Notificações do banco";
    public const string Descricao =
        "Sem token: o intermediador avisa pagamento, recusa e estorno, conferido pela assinatura; idempotente pelo id do aviso";

    /// <summary>Receber o aviso do banco</summary>
    /// <remarks>
    /// O webhook do intermediador (RN-305, RN-303, RF-187, RF-192). Sem token: vale a assinatura, e quem paga nunca
    /// confirma o próprio pagamento. Mercado Pago (produção): x-signature "ts=…,v1=…", com v1 = HMAC-SHA256 em hex de
    /// "id:&lt;data.id&gt;;request-id:&lt;x-request-id&gt;;ts:&lt;ts&gt;;" com o segredo do webhook; o corpo só diz qual
    /// pagamento mudou, e a situação é lida na API do banco. Intermediador falso (máquina e qa): x-assinatura
    /// "sha256=&lt;HMAC-SHA256 do corpo em hex&gt;". Idempotente pelo id do aviso: repetido ou de pagamento desconhecido
    /// responde 200 sem efeito. O traceId da criação, nos metadados do banco, vira o traceIdOrigem; a execução ganha um
    /// novo. Auditado como AVISO_DO_BANCO (RN-309).
    /// </remarks>
    /// <param name="receber">Caso de uso ReceberAvisoDoBanco.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">Aviso aceito (aplicado, repetido ou sem efeito).</response>
    /// <response code="401">ASSINATURA_INVALIDA: a tentativa fica registrada.</response>
    [HttpPost("", Name = "receberAvisoDoBanco")]
    [CabecalhoDoPedido("x-signature", "Mercado Pago: \"ts=<segundos>,v1=<HMAC-SHA256 em hex>\" do manifesto \"id:<data.id>;request-id:<x-request-id>;ts:<ts>;\".")]
    [CabecalhoDoPedido("x-request-id", "Mercado Pago: o id do pedido de aviso, que entra no manifesto da assinatura.")]
    [CabecalhoDoPedido("x-assinatura", "Intermediador falso (máquina e qa): \"sha256=<HMAC-SHA256 do corpo em hex>\".")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receber([FromServices] IReceberAvisoDoBanco receber, CancellationToken ct)
    {
        using var leitor = new StreamReader(Request.Body);
        var corpo = await leitor.ReadToEndAsync(ct);
        var cabecalhos = Request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value.ToString(), StringComparer.Ordinal);
        var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        await receber.Executar(new AvisoRecebido(corpo, cabecalhos, query), HttpContext.NovoTraceId(), ct);
        return Ok();
    }
}
