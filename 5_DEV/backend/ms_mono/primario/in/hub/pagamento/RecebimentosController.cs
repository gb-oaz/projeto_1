using System.Globalization;
using Boaz.Core.Hub.Pagamento.Card.Dtos;
using Boaz.Core.Hub.Pagamento.Card.Ports.In;
using Boaz.Core.Hub.Pagamento.Configuracao.Dtos;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.In;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;
using Boaz.Core.Hub.Pagamento.Recebimentos.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.Out.Hub.Pagamento;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.In.Hub.Pagamento;

/// <summary>
/// O lado de quem recebe (RF-190 a RF-193): só o ADMINISTRADOR do deboaz, que
/// não delega (RN-298). A rota confere a sessão; o papel, o caso de uso
/// (403 ACESSO_NEGADO, auditado nas ações que mudam estado, RN-309).
/// </summary>
[ApiController]
[Route("pagamento/recebimentos")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = PagamentoHttp.Documento)]
[Produces("application/json")]
public sealed class RecebimentosController : ControllerBase
{
    public const string Tag = "Recebimentos";
    public const string Descricao =
        "Só o papel ADMINISTRADOR do deboaz (não se delega): o resumo da competência, as faturas de todas as contas, o " +
        "cancelamento de fatura, o estorno e a configuração (formas, juros, tolerância, vencimento e prazos), com If-Match";

    /// <summary>Ler o resumo da competência</summary>
    /// <remarks>
    /// Faturado, recebido, a receber, vencido, inadimplência (vencido ÷ faturado, em %) e duplicidades, por forma e por
    /// tipo de conta (RF-190, RN-308). Sem competência, a anterior à de hoje.
    /// </remarks>
    /// <param name="competencia">A competência, como 2026-10.</param>
    /// <param name="ler">Caso de uso LerRecebimentos.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O resumo.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="422">DADOS_INVALIDOS em "competencia".</response>
    [HttpGet("resumo", Name = "resumoDosRecebimentos")]
    [ComSessao]
    [ProducesResponseType<ResumoDosRecebimentosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resumo([FromQuery] string? competencia, [FromServices] ILerRecebimentos ler, CancellationToken ct) =>
        Ok(await ler.Resumo(competencia, HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Ler as 12 competências</summary>
    /// <remarks>As 12 competências até a pedida, a mais nova primeiro, para o gráfico e o seletor (RF-190, RN-308).</remarks>
    /// <param name="ate">A última competência, como 2026-10; sem ela, a anterior à de hoje.</param>
    /// <param name="ler">Caso de uso LerRecebimentos.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As 12 competências.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="422">DADOS_INVALIDOS em "ate".</response>
    [HttpGet("competencias", Name = "competenciasDosRecebimentos")]
    [ComSessao]
    [ProducesResponseType<IReadOnlyList<CompetenciaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Competencias([FromQuery] string? ate, [FromServices] ILerRecebimentos ler, CancellationToken ct) =>
        Ok(await ler.Competencias(ate, HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Listar as faturas de todas as contas</summary>
    /// <remarks>
    /// As faturas de todas as contas (RF-191, RN-301, RN-313), com a busca na sigla, no nome da conta e no número;
    /// vencidas da mais antiga, 50 por página, com os dias de atraso.
    /// </remarks>
    /// <param name="competencia">Só as desta competência, como 2026-10.</param>
    /// <param name="situacao">A_PAGAR, VENCIDA, PAGAS ou TODAS (padrão).</param>
    /// <param name="busca">Texto buscado na sigla, no nome da conta e no número da fatura.</param>
    /// <param name="pagina">A página, a partir de 1; ausente é a primeira.</param>
    /// <param name="listar">Caso de uso ListarFaturasDoRecebedor.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A página de faturas.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="422">DADOS_INVALIDOS em "competencia", "situacao" ou "pagina".</response>
    [HttpGet("faturas", Name = "faturasDoRecebedor")]
    [ComSessao]
    [ProducesResponseType<FaturasDoRecebedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Faturas([FromQuery] string? competencia, [FromQuery] string? situacao, [FromQuery] string? busca,
        [FromQuery] string? pagina, [FromServices] IListarFaturasDoRecebedor listar, CancellationToken ct) =>
        Ok(await listar.Executar(competencia, situacao, busca, PagamentoHttp.Pagina(pagina), HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Ler uma fatura de qualquer conta</summary>
    /// <remarks>
    /// O detalhe da fatura para o recebedor (RF-191): o mesmo corpo do GET /pagamento/faturas/{id} da conta, com os
    /// itens, os juros de hoje e as tentativas de pagamento.
    /// </remarks>
    /// <param name="id">O número da fatura, como FAT-000371.</param>
    /// <param name="ler">Caso de uso LerFaturaDoRecebedor.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A fatura.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="404">FATURA_INEXISTENTE.</response>
    [HttpGet("faturas/{id}", Name = "faturaDoRecebedor")]
    [ComSessao]
    [ProducesResponseType<FaturaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Fatura([FromRoute] string id, [FromServices] ILerFaturaDoRecebedor ler, CancellationToken ct) =>
        Ok(await ler.Executar(id, HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Ler um pagamento de qualquer conta</summary>
    /// <remarks>
    /// O detalhe do pagamento para o recebedor (RF-192): a conta, quem pagou, a forma, o valor, o saldo que ainda se
    /// estorna e os estornos, do mais novo ao mais antigo.
    /// </remarks>
    /// <param name="id">O número do pagamento, como PAG-000773.</param>
    /// <param name="ler">Caso de uso LerPagamentoDoRecebedor.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O pagamento.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE.</response>
    [HttpGet("pagamentos/{id}", Name = "pagamentoDoRecebedor")]
    [ComSessao]
    [ProducesResponseType<PagamentoDoRecebedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pagamento([FromRoute] string id, [FromServices] ILerPagamentoDoRecebedor ler, CancellationToken ct) =>
        Ok(await ler.Executar(id, HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Cancelar uma fatura em aberto</summary>
    /// <remarks>
    /// Cancela a fatura A_PAGAR ou VENCIDA com o motivo (RF-191, RN-301): o pagamento que esperava é cancelado no banco
    /// e o aviso de atraso se encerra. A paga não se cancela. Auditado como FATURA_CANCELADA (RN-309).
    /// </remarks>
    /// <param name="id">O número da fatura, como FAT-000371.</param>
    /// <param name="corpo">O motivo do cancelamento.</param>
    /// <param name="cancelar">Caso de uso CancelarFatura.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A fatura CANCELADA, no corpo de GET /pagamento/faturas/{id}.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="404">FATURA_INEXISTENTE.</response>
    /// <response code="409">FATURA_PAGA.</response>
    /// <response code="422">DADOS_INVALIDOS em "motivo".</response>
    [HttpPost("faturas/{id}/cancelar", Name = "cancelarFatura")]
    [ComSessao]
    [ProducesResponseType<FaturaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoDeCancelamento? corpo, [FromServices] ICancelarFatura cancelar,
        CancellationToken ct) =>
        Ok(await cancelar.Executar(id, corpo ?? new PedidoDeCancelamento(null), HttpContext.QuemPedeAoPagamento(), HttpContext.NovoTraceId(), ct));

    /// <summary>Pedir um estorno</summary>
    /// <remarks>
    /// Estorno total ou parcial de um pagamento por cartão ou PIX, até o saldo, um por vez (RF-192, RN-306, RN-312): fica
    /// PENDENTE (e o pagamento ESTORNO_PENDENTE) até o aviso do banco. O boleto é devolvido pelo SAC, por quem pagou.
    /// Sem resposta do banco, 503 e nada muda. Auditado como ESTORNO_PEDIDO (RN-309).
    /// </remarks>
    /// <param name="id">O número do pagamento, como PAG-000773.</param>
    /// <param name="corpo">O valor, com duas casas, e o motivo.</param>
    /// <param name="pedir">Caso de uso PedirEstorno.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">O estorno pedido.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="404">PAGAMENTO_INEXISTENTE.</response>
    /// <response code="409">ESTORNO_EM_ANDAMENTO, ESTORNO_PELO_SAC (boleto) ou PAGAMENTO_NAO_CONFIRMADO.</response>
    /// <response code="422">ESTORNO_ACIMA_DO_SALDO, com o "saldo", ou DADOS_INVALIDOS em "valor" (zero ou com 3 casas) ou "motivo".</response>
    /// <response code="503">BANCO_INDISPONIVEL: o banco não respondeu; nada mudou.</response>
    [HttpPost("pagamentos/{id}/estornos", Name = "pedirEstorno")]
    [ComSessao]
    [ProducesResponseType<EstornoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Erro>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Estornar([FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoDeEstornoDto? corpo, [FromServices] IPedirEstorno pedir, CancellationToken ct)
    {
        var estorno = await pedir.Executar(id, corpo ?? new PedidoDeEstornoDto(null, null), HttpContext.QuemPedeAoPagamento(),
            HttpContext.NovoTraceId(), ct);
        return StatusCode(StatusCodes.Status201Created, estorno);
    }

    /// <summary>Ler a configuração</summary>
    /// <remarks>
    /// As formas aceitas, os juros ao dia, a tolerância do aviso de atraso, o dia do vencimento e os prazos de PIX, débito
    /// e boleto, com a versão que o PUT manda no If-Match (RF-193, RN-302, RN-310, RN-311).
    /// </remarks>
    /// <param name="ler">Caso de uso LerConfiguracao.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A configuração vigente.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    [HttpGet("configuracao", Name = "lerConfiguracaoDoPagamento")]
    [ComSessao]
    [ProducesResponseType<ConfiguracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Configuracao([FromServices] ILerConfiguracao ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Salvar a configuração</summary>
    /// <remarks>
    /// Salva a configuração inteira (o corpo do GET, sem "versao") com a versão lida no If-Match (RF-193, RN-309, RN-310):
    /// vale para os próximos pagamentos e faturas. Sem If-Match, 428 VERSAO_AUSENTE; com outra versão, 412
    /// CONFIGURACAO_MUDOU. A resposta traz a versão lida + 1. Limites:
    /// jurosAoDia de 0 a 1 (% ao dia), toleranciaAvisoDias de 0 a 30, diaVencimento 10, pixMinutos de 5 a 1440,
    /// debitoMinutos de 5 a 30, boletoDiasUteis de 1 a 30; ao menos uma forma ligada. Auditado como CONFIGURACAO_SALVA,
    /// com o antes e o depois.
    /// </remarks>
    /// <param name="corpo">As formas, os juros, a tolerância, o dia do vencimento e os prazos.</param>
    /// <param name="salvar">Caso de uso SalvarConfiguracao.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">A configuração salva, com a versão nova.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: quem pede não é o administrador do deboaz (RN-298).</response>
    /// <response code="412">CONFIGURACAO_MUDOU: If-Match de uma versão antiga.</response>
    /// <response code="422">FORMA_OBRIGATORIA (as quatro desligadas) ou DADOS_INVALIDOS no campo fora do limite.</response>
    /// <response code="428">VERSAO_AUSENTE: sem If-Match.</response>
    [HttpPut("configuracao", Name = "salvarConfiguracaoDoPagamento")]
    [ComSessao]
    [CabecalhoDoPedido("If-Match", "A versão lida em GET /pagamento/recebimentos/configuracao, como \"4\" (aceita também 4 e W/\"4\").",
        Obrigatorio = true)]
    [ProducesResponseType<ConfiguracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Erro>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Erro>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Salvar([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoDeConfiguracao? corpo,
        [FromServices] ISalvarConfiguracao salvar, CancellationToken ct) =>
        Ok(await salvar.Executar(corpo ?? new PedidoDeConfiguracao(null, null, null, null, null), VersaoLida(), HttpContext.QuemPedeAoPagamento(),
            HttpContext.NovoTraceId(), ct));

    // o If-Match: "4", 4 ou W/"4"; o que não é número é como ausente (428)
    int? VersaoLida()
    {
        var texto = Request.Headers.IfMatch.ToString().Trim();
        if (texto.StartsWith("W/", StringComparison.Ordinal)) texto = texto[2..];
        return int.TryParse(texto.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var versao) ? versao : null;
    }
}

/// <summary>
/// O card do Pagamento na tela Módulos do hub (RN-307, 15 RN-276 e RN-279):
/// do administrador (DEBOAZ) ou de quem tem MOD-01 · U; os demais, 403.
/// </summary>
[ApiController]
[Route("pagamento/card")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = PagamentoHttp.Documento)]
[Produces("application/json")]
public sealed class CardDoPagamentoController : ControllerBase
{
    public const string Tag = "Card do Pagamento";
    public const string Descricao =
        "O card do Pagamento na tela Módulos do hub, por perfil (DEBOAZ para o administrador; INSTITUICAO ou AUTONOMO para " +
        "quem tem MOD-01 · U), e o card ao vivo";

    /// <summary>Montar o card do Pagamento</summary>
    /// <remarks>
    /// O card no formato comum dos módulos (RN-307): para quem paga, o que há a pagar ou vencido, as vencidas e o pago no
    /// ano; para o administrador (DEBOAZ), o recebido no mês, o a receber, o vencido e a inadimplência. A versão sobe a
    /// cada mudança nas faturas.
    /// </remarks>
    /// <param name="ler">Caso de uso LerCardDoPagamento.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O card.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U e não é o administrador do deboaz.</response>
    [HttpGet("", Name = "lerCardDoPagamento")]
    [ComSessao]
    [ProducesResponseType<CardDoPagamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ler([FromServices] ILerCardDoPagamento ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoPagamento(), ct));

    /// <summary>Acompanhar o card ao vivo</summary>
    /// <remarks>
    /// Fluxo text/event-stream (RN-307, 15_modulos RN-279): o evento "card" (id: a versão; data: o corpo de GET
    /// /pagamento/card) ao abrir e, a cada intervalo, só se a versão mudou; ": ping" a cada 30 s. Use fetch com o
    /// Authorization; o EventSource do navegador não manda o token.
    /// </remarks>
    /// <param name="intervalo">Segundos entre as conferências: 5, 15, 30, 60 ou 300 (padrão).</param>
    /// <param name="acompanhar">Caso de uso AcompanharCardDoPagamento.</param>
    /// <param name="json">As opções de JSON da API, para o corpo dos eventos.</param>
    /// <param name="ct">Cancelamento do pedido: a conexão fechada.</param>
    /// <response code="200">O fluxo de eventos, aberto.</response>
    /// <response code="401">SESSAO_TERMINADA: sem token, token inválido, vencido ou sessão encerrada.</response>
    /// <response code="403">ACESSO_NEGADO: sem MOD-01 · U e não é o administrador do deboaz.</response>
    /// <response code="422">DADOS_INVALIDOS em "intervalo": fora da lista.</response>
    [HttpGet("ao-vivo", Name = "acompanharCardDoPagamento")]
    [ComSessao]
    [ProducesResponseType<CardDoPagamentoDto>(StatusCodes.Status200OK, "text/event-stream")]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity, "application/json")]
    public async Task AoVivo([FromQuery] string? intervalo, [FromServices] IAcompanharCardDoPagamento acompanhar,
        [FromServices] IOptions<JsonOptions> json, CancellationToken ct)
    {
        var quem = HttpContext.QuemPedeAoPagamento();
        var (passo, card) = await acompanhar.Abrir(intervalo, quem, ct);
        var fluxo = new EventosDoPagamento(HttpContext, json.Value.JsonSerializerOptions);
        try
        {
            await fluxo.Abrir(ct);
            await fluxo.Evento("card", card.Versao.ToString(CultureInfo.InvariantCulture), card, ct);
            var versao = card.Versao;
            var espera = passo < PagamentoHttp.Ping ? passo : PagamentoHttp.Ping;
            var desdeACarga = TimeSpan.Zero;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(espera, ct);
                desdeACarga += espera;
                if (desdeACarga < passo)
                {
                    await fluxo.Ping(ct);
                    continue;
                }
                desdeACarga = TimeSpan.Zero;
                if (await acompanhar.Proximo(versao, quem, ct) is { } novo)
                {
                    versao = novo.Versao;
                    await fluxo.Evento("card", novo.Versao.ToString(CultureInfo.InvariantCulture), novo, ct);
                }
                else await fluxo.Ping(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a conexão fechou do outro lado
        }
    }
}
