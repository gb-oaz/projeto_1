using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// O cartão no pedido: só o token, gerado pela tela direto no banco (RNF-239).
/// Número, CVV e validade nunca devem chegar: se vierem, o pedido é recusado
/// sem guardar nem escrever nada deles.
/// </summary>
/// <param name="Token">O token do cartão.</param>
/// <param name="Numero">Não deve vir.</param>
/// <param name="Cvv">Não deve vir.</param>
/// <param name="Validade">Não deve vir.</param>
public sealed record PedidoDoCartao(string? Token, object? Numero, object? Cvv, object? Validade);

/// <summary>O corpo de POST /pagamento/faturas/{id}/pagamentos (RF-184 a RF-188).</summary>
/// <param name="Forma">PIX, CARTAO_CREDITO, CARTAO_DEBITO ou BOLETO.</param>
/// <param name="Cartao">O token do cartão, no crédito e no débito.</param>
public sealed record PedidoDePagamento(string? Forma, PedidoDoCartao? Cartao)
{
    /// <summary>
    /// A forma e o token, conferidos nesta ordem: dado do cartão que não é o
    /// token (RNF-239), a forma, e o cartão com o token no crédito e no débito.
    /// </summary>
    /// <exception cref="FalhaDoPagamento">DADOS_INVALIDOS em "cartao.numero", "cartao.cvv", "cartao.validade", "forma", "cartao" ou "cartao.token".</exception>
    public (FormaDePagamento Forma, string? Token) Validar()
    {
        if (Cartao?.Numero is not null) throw FalhaDoPagamento.DadosInvalidos("cartao.numero");
        if (Cartao?.Cvv is not null) throw FalhaDoPagamento.DadosInvalidos("cartao.cvv");
        if (Cartao?.Validade is not null) throw FalhaDoPagamento.DadosInvalidos("cartao.validade");
        var forma = TextosDoPagamento.LerForma(Forma) ?? throw FalhaDoPagamento.DadosInvalidos("forma");
        if (forma is not (FormaDePagamento.CartaoCredito or FormaDePagamento.CartaoDebito)) return (forma, null);
        if (Cartao is null) throw FalhaDoPagamento.DadosInvalidos("cartao");
        return string.IsNullOrWhiteSpace(Cartao.Token) ? throw FalhaDoPagamento.DadosInvalidos("cartao.token") : (forma, Cartao.Token.Trim());
    }
}

/// <summary>O PIX no corpo do pagamento (RF-185).</summary>
/// <param name="CopiaECola">"000201…".</param>
/// <param name="QrCodeSvg">"&lt;svg…&lt;/svg&gt;".</param>
public sealed record PixDto(string CopiaECola, string QrCodeSvg);

/// <summary>O boleto no corpo do pagamento (RF-188).</summary>
/// <param name="Vencimento">2026-12-10.</param>
/// <param name="LinhaDigitavel">A linha digitável.</param>
/// <param name="CodigoBarras">O código de barras.</param>
/// <param name="Pdf">/pagamento/pagamentos/{id}/boleto.</param>
public sealed record BoletoDto(DateOnly Vencimento, string LinhaDigitavel, string CodigoBarras, string Pdf);

/// <summary>O débito no corpo do pagamento (RF-187).</summary>
/// <param name="ConfirmacaoUrl">Onde quem paga confirma no banco (https).</param>
public sealed record DebitoDto(string ConfirmacaoUrl);

/// <summary>O cartão no corpo do pagamento: só a bandeira e o final (RNF-239).</summary>
/// <param name="Bandeira">VISA.</param>
/// <param name="Final">4242.</param>
public sealed record CartaoDto(string Bandeira, string Final);

/// <summary>201 de POST /pagamento/faturas/{id}/pagamentos e 200 de GET /pagamento/pagamentos/{id} e do cancelar.</summary>
/// <param name="Id">PAG-000773.</param>
/// <param name="FaturaId">FAT-000402.</param>
/// <param name="Forma">CARTAO_CREDITO.</param>
/// <param name="Valor">A fatura e os juros do dia em que nasceu (RN-310).</param>
/// <param name="ValorFatura">O valor da fatura.</param>
/// <param name="Juros">Os juros do dia em que nasceu.</param>
/// <param name="Status">O status atual.</param>
/// <param name="CriadoEm">Quando nasceu.</param>
/// <param name="ExpiraEm">Até quando espera o banco.</param>
/// <param name="PagoEm">Quando o banco confirmou.</param>
/// <param name="Motivo">O motivo da recusa.</param>
/// <param name="Duplicidade">Pagou uma fatura já paga ou cancelada (RN-303).</param>
/// <param name="Pix">O código PIX, enquanto aguarda.</param>
/// <param name="Boleto">O boleto.</param>
/// <param name="Debito">A confirmação do débito, enquanto aguarda.</param>
/// <param name="Cartao">A bandeira e o final do cartão.</param>
public sealed record PagamentoDto(
    string Id, string FaturaId, string Forma, decimal Valor, decimal ValorFatura, decimal Juros, string Status, DateTimeOffset CriadoEm,
    DateTimeOffset? ExpiraEm, DateTimeOffset? PagoEm, string? Motivo, bool Duplicidade, PixDto? Pix, BoletoDto? Boleto, DebitoDto? Debito,
    CartaoDto? Cartao)
{
    /// <summary>Do pagamento; o PIX e a confirmação do débito só enquanto aguardam (RF-185).</summary>
    public static PagamentoDto De(Pagamento p)
    {
        var aguardando = p.Status == StatusDoPagamento.Aguardando;
        return new(p.Id, p.FaturaId, p.Forma.Texto(), p.Valor.Reais, p.ValorFatura.Reais, p.Juros.Reais, p.Status.Texto(), p.CriadoEm, p.ExpiraEm,
            p.PagoEm, p.Motivo, p.Duplicidade,
            aguardando && p.Pix is { } pix ? new PixDto(pix.CopiaECola, pix.QrCodeSvg) : null,
            p.Boleto is { } b ? new BoletoDto(b.Vencimento, b.LinhaDigitavel, b.CodigoBarras, $"/pagamento/pagamentos/{p.Id}/boleto") : null,
            aguardando && p.ConfirmacaoUrl is { } url ? new DebitoDto(url) : null,
            p.Cartao is { } c ? new CartaoDto(c.Bandeira, c.Final) : null);
    }
}

/// <summary>O evento "pagamento" do ao vivo (RNF-238, RNF-244).</summary>
/// <param name="Id">PAG-000781.</param>
/// <param name="Status">PAGO.</param>
/// <param name="TraceId">A execução que mudou o status.</param>
public sealed record EventoDoPagamentoDto(string Id, string Status, string TraceId)
{
    /// <summary>O fluxo fecha depois destes (RNF-238).</summary>
    public static readonly IReadOnlySet<string> Finais = new HashSet<string> { "PAGO", "RECUSADO", "EXPIRADO", "CANCELADO" };

    /// <summary>O status é final.</summary>
    public bool EhFinal() => Finais.Contains(Status);

    /// <summary>Do pagamento.</summary>
    public static EventoDoPagamentoDto De(Pagamento p) => new(p.Id, p.Status.Texto(), p.TraceIdAtual);
}

/// <summary>O que vai no PDF do boleto (RF-188) ou do comprovante (RF-189).</summary>
/// <param name="FaturaId">FAT-000402: o nome do arquivo.</param>
/// <param name="PagamentoId">PAG-000785.</param>
/// <param name="Conta">O nome da conta que paga.</param>
/// <param name="Competencia">2026-11.</param>
/// <param name="Forma">BOLETO.</param>
/// <param name="Valor">O valor cobrado.</param>
/// <param name="Vencimento">O vencimento do boleto; null no comprovante.</param>
/// <param name="LinhaDigitavel">A linha do boleto; null no comprovante.</param>
/// <param name="CodigoBarras">O código de barras; null no comprovante.</param>
/// <param name="PagoEm">Quando o banco confirmou; null no boleto.</param>
/// <param name="Autenticacao">O id da cobrança no banco.</param>
/// <param name="Cartao">"VISA final 4242", no cartão.</param>
public sealed record DocumentoDoPagamentoDto(string FaturaId, string PagamentoId, string Conta, string Competencia, string Forma, decimal Valor,
    DateOnly? Vencimento, string? LinhaDigitavel, string? CodigoBarras, DateTimeOffset? PagoEm, string? Autenticacao, string? Cartao);
