using Boaz.Core.Hub.Pagamento.Faturas.Domain;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

/// <summary>O código do PIX gerado pelo banco (RF-185).</summary>
/// <param name="CopiaECola">O código copia e cola, "000201…".</param>
/// <param name="QrCodeSvg">O QR Code, em SVG.</param>
public sealed record DadosDoPix(string CopiaECola, string QrCodeSvg);

/// <summary>O boleto gerado pelo banco (RF-188).</summary>
/// <param name="Vencimento">O vencimento do boleto.</param>
/// <param name="LinhaDigitavel">A linha digitável.</param>
/// <param name="CodigoBarras">O código de barras.</param>
public sealed record DadosDoBoleto(DateOnly Vencimento, string LinhaDigitavel, string CodigoBarras);

/// <summary>O cartão como o banco o devolve: só a bandeira e o final (RNF-239).</summary>
/// <param name="Bandeira">VISA, MASTERCARD…</param>
/// <param name="Final">Os 4 últimos dígitos.</param>
public sealed record CartaoMascarado(string Bandeira, string Final);

/// <summary>
/// Um pagamento de uma fatura (RF-184 a RF-188): o valor fica fixo ao nascer
/// (a fatura e os juros do dia, RN-310) e o status segue o que o banco diz
/// (RN-302, RN-305). Nunca guarda número, validade nem CVV do cartão (RNF-239).
/// Cada mudança entra no histórico com o traceId de quem a fez (RNF-244).
/// </summary>
public sealed class Pagamento
{
    readonly List<Mudanca> historico;

    /// <summary>Remonta o pagamento como foi guardado.</summary>
    public Pagamento(string id, string faturaId, string contaId, FormaDePagamento forma, Dinheiro valorFatura, Dinheiro juros,
        StatusDoPagamento status, DateTimeOffset criadoEm, string criadoPor, DateTimeOffset? expiraEm, DateTimeOffset? pagoEm, string? idExterno,
        DadosDoPix? pix, DadosDoBoleto? boleto, string? confirmacaoUrl, CartaoMascarado? cartao, string? motivo, bool duplicidade,
        Dinheiro estornado, string traceId, string? traceIdOrigem, IEnumerable<Mudanca> historico, long versao)
    {
        (Id, FaturaId, ContaId, Forma, ValorFatura, Juros, Status, CriadoEm, CriadoPor) =
            (id, faturaId, contaId, forma, valorFatura, juros, status, criadoEm, criadoPor);
        (ExpiraEm, PagoEm, IdExterno, Pix, Boleto, ConfirmacaoUrl, Cartao, Motivo, Duplicidade) =
            (expiraEm, pagoEm, idExterno, pix, boleto, confirmacaoUrl, cartao, motivo, duplicidade);
        (Estornado, TraceId, TraceIdOrigem, Versao) = (estornado, traceId, traceIdOrigem, versao);
        this.historico = [.. historico];
    }

    /// <summary>O motivo do cartão recusado (RF-186).</summary>
    public const string RecusadoPeloEmissor = "RECUSADO_PELO_EMISSOR";

    /// <summary>PAG-000773.</summary>
    public string Id { get; }

    /// <summary>A fatura que paga.</summary>
    public string FaturaId { get; }

    /// <summary>A conta da fatura (escopo, RN-298).</summary>
    public string ContaId { get; }

    /// <summary>A forma.</summary>
    public FormaDePagamento Forma { get; }

    /// <summary>O valor da fatura ao nascer.</summary>
    public Dinheiro ValorFatura { get; }

    /// <summary>Os juros do dia em que nasceu (RN-310).</summary>
    public Dinheiro Juros { get; }

    /// <summary>O que o banco cobra: a fatura e os juros, fixo depois de gerado (RN-310).</summary>
    public Dinheiro Valor => ValorFatura + Juros;

    /// <summary>O status atual.</summary>
    public StatusDoPagamento Status { get; private set; }

    /// <summary>Quando nasceu.</summary>
    public DateTimeOffset CriadoEm { get; }

    /// <summary>Quem pediu.</summary>
    public string CriadoPor { get; }

    /// <summary>Até quando o PIX, o débito ou o boleto esperam o banco (RN-302).</summary>
    public DateTimeOffset? ExpiraEm { get; private set; }

    /// <summary>Quando o banco confirmou.</summary>
    public DateTimeOffset? PagoEm { get; private set; }

    /// <summary>O id da cobrança no banco: liga o aviso ao pagamento (RN-305).</summary>
    public string? IdExterno { get; private set; }

    /// <summary>O código PIX.</summary>
    public DadosDoPix? Pix { get; private set; }

    /// <summary>O boleto.</summary>
    public DadosDoBoleto? Boleto { get; private set; }

    /// <summary>Onde quem paga confirma o débito no banco (RF-187).</summary>
    public string? ConfirmacaoUrl { get; private set; }

    /// <summary>A bandeira e o final do cartão.</summary>
    public CartaoMascarado? Cartao { get; private set; }

    /// <summary>O motivo da recusa.</summary>
    public string? Motivo { get; private set; }

    /// <summary>Pagou uma fatura já paga ou cancelada (RN-303).</summary>
    public bool Duplicidade { get; private set; }

    /// <summary>O já devolvido pelos estornos concluídos (RN-306).</summary>
    public Dinheiro Estornado { get; private set; }

    /// <summary>A execução que o criou (RNF-244).</summary>
    public string TraceId { get; }

    /// <summary>O X-Trace-Id recebido no pedido que o criou, como a tentativa que deu 503 (RNF-243).</summary>
    public string? TraceIdOrigem { get; }

    /// <summary>As mudanças de status (RNF-244).</summary>
    public IReadOnlyList<Mudanca> Historico => historico;

    /// <summary>A versão lida, para a gravação conferir que ninguém mudou antes.</summary>
    public long Versao { get; private set; }

    /// <summary>A gravação conferida subiu a versão guardada: a próxima confere com ela.</summary>
    public void Versionar() => Versao++;

    /// <summary>O traceId da última mudança: o do evento ao vivo (RNF-244).</summary>
    public string TraceIdAtual => historico.Count == 0 ? TraceId : historico[^1].TraceId;

    /// <summary>AGUARDANDO ou PROCESSANDO: ocupa a fatura (RN-303).</summary>
    public bool EmAberto => Status is StatusDoPagamento.Aguardando or StatusDoPagamento.Processando;

    /// <summary>O ao vivo fecha nestes (RNF-238).</summary>
    public bool Final => Status is StatusDoPagamento.Pago or StatusDoPagamento.Recusado or StatusDoPagamento.Expirado
        or StatusDoPagamento.Cancelado;

    /// <summary>O banco já confirmou (PAGO ou um dos de estorno).</summary>
    public bool Confirmado => PagoEm is not null && Status is StatusDoPagamento.Pago or StatusDoPagamento.EstornoPendente
        or StatusDoPagamento.EstornadoParcial or StatusDoPagamento.Estornado;

    /// <summary>O que ainda pode ser estornado (RN-306).</summary>
    public Dinheiro Saldo => Valor - Estornado;

    /// <summary>O pagamento reservado na fatura, antes da resposta do banco: "AGUARDANDO".</summary>
    public static Pagamento Novo(string id, Fatura fatura, FormaDePagamento forma, Dinheiro juros, DateTimeOffset agora, string criadoPor,
        string traceId, string? traceIdOrigem) =>
        new(id, fatura.Id, fatura.Conta.Id, forma, fatura.Valor, juros, StatusDoPagamento.Aguardando, agora, criadoPor, null, null, null, null,
            null, null, null, null, false, Dinheiro.Zero, traceId, traceIdOrigem, [], 0);

    /// <summary>
    /// A resposta do banco à cobrança (RF-185 a RF-188): o nascimento entra no
    /// histórico com o status que o banco deu.
    /// </summary>
    public void Nascer(StatusDoPagamento status, string? idExterno, DateTimeOffset? expiraEm, DateTimeOffset? pagoEm, DadosDoPix? pix,
        DadosDoBoleto? boleto, string? confirmacaoUrl, CartaoMascarado? cartao, string? motivo)
    {
        (IdExterno, ExpiraEm, PagoEm, Pix, Boleto, ConfirmacaoUrl, Cartao, Motivo) =
            (idExterno, expiraEm, pagoEm, pix, boleto, confirmacaoUrl, cartao, motivo);
        Status = status;
        historico.Add(new Mudanca(null, status.Texto(), pagoEm ?? CriadoEm, TraceId, CriadoPor));
    }

    /// <summary>
    /// O banco confirmou (RN-305). Vale também para o expirado ou cancelado que
    /// o banco pagou mesmo assim; se a fatura já estava paga ou cancelada, fica
    /// em duplicidade (RN-303). false se já estava confirmado.
    /// </summary>
    public bool Pagar(DateTimeOffset pagoEm, bool duplicidade, string traceId, string por)
    {
        if (Confirmado || Status == StatusDoPagamento.Recusado) return false;
        (PagoEm, Duplicidade) = (pagoEm, duplicidade);
        Mudar(StatusDoPagamento.Pago, pagoEm, traceId, por);
        return true;
    }

    /// <summary>O banco recusou o que estava em aberto (RF-186, RF-187); false se não estava.</summary>
    public bool Recusar(string motivo, DateTimeOffset agora, string traceId, string por)
    {
        if (!EmAberto) return false;
        Motivo = motivo;
        Mudar(StatusDoPagamento.Recusado, agora, traceId, por);
        return true;
    }

    /// <summary>Passou do prazo sem o banco confirmar (RN-302); false se não estava em aberto.</summary>
    public bool Expirar(DateTimeOffset agora, string traceId)
    {
        if (!EmAberto) return false;
        Mudar(StatusDoPagamento.Expirado, agora, traceId, "sistema");
        return true;
    }

    /// <summary>Quem paga desiste do que está em aberto para trocar de forma (RF-184, RN-303).</summary>
    /// <exception cref="FalhaDoPagamento">PAGAMENTO_CONFIRMADO.</exception>
    public bool Cancelar(DateTimeOffset agora, string traceId, string por)
    {
        if (Confirmado) throw FalhaDoPagamento.PagamentoConfirmado();
        if (!EmAberto) return false;
        Mudar(StatusDoPagamento.Cancelado, agora, traceId, por);
        return true;
    }

    /// <summary>O administrador pede o estorno (RF-192, RN-306): o pagamento fica "ESTORNO_PENDENTE".</summary>
    /// <exception cref="FalhaDoPagamento">ESTORNO_PELO_SAC, PAGAMENTO_NAO_CONFIRMADO, ESTORNO_EM_ANDAMENTO ou ESTORNO_ACIMA_DO_SALDO.</exception>
    public void PedirEstorno(Dinheiro valor, DateTimeOffset agora, string traceId, string por)
    {
        if (Forma == FormaDePagamento.Boleto) throw FalhaDoPagamento.EstornoPeloSac();
        if (Status == StatusDoPagamento.EstornoPendente) throw FalhaDoPagamento.EstornoEmAndamento();
        if (!Confirmado) throw FalhaDoPagamento.PagamentoNaoConfirmado();
        if (valor > Saldo) throw FalhaDoPagamento.EstornoAcimaDoSaldo(Saldo);
        Mudar(StatusDoPagamento.EstornoPendente, agora, traceId, por);
    }

    /// <summary>O banco devolveu (RF-192): "ESTORNADO_PARCIAL" ou, sem saldo, "ESTORNADO".</summary>
    public void ConcluirEstorno(Dinheiro valor, DateTimeOffset agora, string traceId)
    {
        Estornado += valor;
        Mudar(Saldo <= Dinheiro.Zero ? StatusDoPagamento.Estornado : StatusDoPagamento.EstornadoParcial, agora, traceId, "banco");
    }

    /// <summary>O banco recusou o estorno, ou não respondeu: volta ao status de antes (RF-192).</summary>
    public void DesfazerEstorno(DateTimeOffset agora, string traceId, string por)
    {
        if (Status != StatusDoPagamento.EstornoPendente) return;
        Mudar(Estornado > Dinheiro.Zero ? StatusDoPagamento.EstornadoParcial : StatusDoPagamento.Pago, agora, traceId, por);
    }

    void Mudar(StatusDoPagamento para, DateTimeOffset em, string traceId, string? por)
    {
        historico.Add(new Mudanca(Status.Texto(), para.Texto(), em, traceId, por));
        Status = para;
    }
}

/// <summary>
/// Um estorno de um pagamento por cartão ou PIX (RF-192, RN-306): pedido pelo
/// administrador e concluído ou recusado pelo aviso do banco.
/// </summary>
public sealed class Estorno
{
    readonly List<Mudanca> historico;

    /// <summary>Remonta o estorno como foi guardado.</summary>
    public Estorno(string id, string pagamentoId, string faturaId, Dinheiro valor, string motivo, StatusDoEstorno status, DateTimeOffset pedidoEm,
        string pedidoPor, DateTimeOffset? concluidoEm, string? idExterno, string traceId, IEnumerable<Mudanca> historico, long versao)
    {
        (Id, PagamentoId, FaturaId, Valor, Motivo, Status, PedidoEm, PedidoPor) = (id, pagamentoId, faturaId, valor, motivo, status, pedidoEm, pedidoPor);
        (ConcluidoEm, IdExterno, TraceId, Versao) = (concluidoEm, idExterno, traceId, versao);
        this.historico = [.. historico];
    }

    /// <summary>EST-000012.</summary>
    public string Id { get; }

    /// <summary>O pagamento estornado.</summary>
    public string PagamentoId { get; }

    /// <summary>A fatura do pagamento.</summary>
    public string FaturaId { get; }

    /// <summary>Quanto devolver.</summary>
    public Dinheiro Valor { get; }

    /// <summary>O motivo dito.</summary>
    public string Motivo { get; }

    /// <summary>O status atual.</summary>
    public StatusDoEstorno Status { get; private set; }

    /// <summary>Quando foi pedido.</summary>
    public DateTimeOffset PedidoEm { get; }

    /// <summary>Quem pediu.</summary>
    public string PedidoPor { get; }

    /// <summary>Quando o banco devolveu.</summary>
    public DateTimeOffset? ConcluidoEm { get; private set; }

    /// <summary>O id do estorno no banco (RN-305).</summary>
    public string? IdExterno { get; private set; }

    /// <summary>A execução que o pediu.</summary>
    public string TraceId { get; }

    /// <summary>As mudanças de status (RNF-244).</summary>
    public IReadOnlyList<Mudanca> Historico => historico;

    /// <summary>A versão lida, para a gravação conferir que ninguém mudou antes.</summary>
    public long Versao { get; private set; }

    /// <summary>A gravação conferida subiu a versão guardada: a próxima confere com ela.</summary>
    public void Versionar() => Versao++;

    /// <summary>O estorno novo, "PENDENTE".</summary>
    public static Estorno Pedir(string id, Pagamento pagamento, Dinheiro valor, string motivo, DateTimeOffset agora, string por, string traceId) =>
        new(id, pagamento.Id, pagamento.FaturaId, valor, motivo, StatusDoEstorno.Pendente, agora, por, null, null, traceId,
            [new Mudanca(null, StatusDoEstorno.Pendente.Texto(), agora, traceId, por)], 0);

    /// <summary>O id que o banco deu ao estorno.</summary>
    public void Enviado(string? idExterno) => IdExterno = idExterno;

    /// <summary>O banco devolveu; false se o estorno não estava pendente.</summary>
    public bool Concluir(DateTimeOffset em, string traceId)
    {
        if (Status != StatusDoEstorno.Pendente) return false;
        ConcluidoEm = em;
        Mudar(StatusDoEstorno.Concluido, em, traceId);
        return true;
    }

    /// <summary>O banco recusou; false se o estorno não estava pendente.</summary>
    public bool Recusar(DateTimeOffset em, string traceId)
    {
        if (Status != StatusDoEstorno.Pendente) return false;
        Mudar(StatusDoEstorno.Recusado, em, traceId);
        return true;
    }

    void Mudar(StatusDoEstorno para, DateTimeOffset em, string traceId)
    {
        historico.Add(new Mudanca(Status.Texto(), para.Texto(), em, traceId, "banco"));
        Status = para;
    }
}
