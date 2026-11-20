using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// O cartão no pedido: o token, o método e o emissor, gerados pela tela direto
/// no intermediador (RF-186, RF-187, RNF-239). Número, CVV e validade nunca
/// devem chegar: se vierem, o pedido é recusado sem guardar nem escrever nada deles.
/// </summary>
/// <param name="Token">O token do cartão, como "tok_9f2c71".</param>
/// <param name="Metodo">O método do intermediador, como "visa" ou "debmaster".</param>
/// <param name="Emissor">O emissor do intermediador, como "25"; pode faltar.</param>
/// <param name="Numero">Não deve vir.</param>
/// <param name="Cvv">Não deve vir.</param>
/// <param name="Validade">Não deve vir.</param>
public sealed record PedidoDoCartao(string? Token, string? Metodo, string? Emissor, object? Numero, object? Cvv, object? Validade);

/// <summary>O endereço de quem paga o boleto (RF-188), no pedido e em GET /pagamento/formas.</summary>
/// <param name="Cep">96015-000.</param>
/// <param name="Logradouro">Rua Sete de Setembro.</param>
/// <param name="Numero">1020.</param>
/// <param name="Bairro">Centro.</param>
/// <param name="Cidade">Pelotas.</param>
/// <param name="Uf">RS.</param>
public sealed record EnderecoDto(string? Cep, string? Logradouro, string? Numero, string? Bairro, string? Cidade, string? Uf)
{
    /// <summary>Do endereço guardado.</summary>
    public static EnderecoDto De(EnderecoDoPagador e) => new(e.Cep, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf);
}

/// <summary>O corpo de POST /pagamento/faturas/{id}/pagamentos (RF-184 a RF-188).</summary>
/// <param name="Forma">PIX, CARTAO_CREDITO, CARTAO_DEBITO ou BOLETO.</param>
/// <param name="Cartao">O token, o método e o emissor do cartão, no crédito e no débito.</param>
/// <param name="Endereco">O endereço de quem paga, no boleto.</param>
public sealed record PedidoDePagamento(string? Forma, PedidoDoCartao? Cartao, EnderecoDto? Endereco = null)
{
    /// <summary>
    /// A forma, o cartão e o endereço, conferidos nesta ordem: dado do cartão
    /// que não é o token (RNF-239), a forma, o cartão com o token e o método no
    /// crédito e no débito, e o endereço no boleto.
    /// </summary>
    /// <exception cref="FalhaDoPagamento">
    /// DADOS_INVALIDOS em "cartao.numero", "cartao.cvv", "cartao.validade", "forma", "cartao", "cartao.token", "cartao.metodo",
    /// "endereco" ou "endereco.&lt;campo&gt;".
    /// </exception>
    public (FormaDePagamento Forma, CartaoTokenizado? Cartao, EnderecoDoPagador? Endereco) Validar()
    {
        if (Cartao?.Numero is not null) throw FalhaDoPagamento.DadosInvalidos("cartao.numero");
        if (Cartao?.Cvv is not null) throw FalhaDoPagamento.DadosInvalidos("cartao.cvv");
        if (Cartao?.Validade is not null) throw FalhaDoPagamento.DadosInvalidos("cartao.validade");
        var forma = TextosDoPagamento.LerForma(Forma) ?? throw FalhaDoPagamento.DadosInvalidos("forma");
        switch (forma)
        {
            case FormaDePagamento.CartaoCredito or FormaDePagamento.CartaoDebito:
                if (Cartao is null) throw FalhaDoPagamento.DadosInvalidos("cartao");
                if (string.IsNullOrWhiteSpace(Cartao.Token)) throw FalhaDoPagamento.DadosInvalidos("cartao.token");
                if (string.IsNullOrWhiteSpace(Cartao.Metodo)) throw FalhaDoPagamento.DadosInvalidos("cartao.metodo");
                return (forma, new CartaoTokenizado(Cartao.Token.Trim(), Cartao.Metodo.Trim(),
                    string.IsNullOrWhiteSpace(Cartao.Emissor) ? null : Cartao.Emissor.Trim()), null);
            case FormaDePagamento.Boleto:
                var e = Endereco ?? throw FalhaDoPagamento.DadosInvalidos("endereco");
                return (forma, null, EnderecoDoPagador.Validar(e.Cep, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf));
            default:
                return (forma, null, null);
        }
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

/// <summary>Um estorno do pagamento (RF-192-api).</summary>
/// <param name="Id">EST-000012.</param>
/// <param name="Valor">1473.92.</param>
/// <param name="Motivo">Quiz cancelado.</param>
/// <param name="Status">PENDENTE, CONCLUIDO ou RECUSADO.</param>
/// <param name="PedidoEm">Quando foi pedido.</param>
/// <param name="ConcluidoEm">Quando o banco devolveu.</param>
public sealed record EstornoDoPagamentoDto(string Id, decimal Valor, string Motivo, string Status, DateTimeOffset PedidoEm, DateTimeOffset? ConcluidoEm)
{
    /// <summary>Os estornos, o mais antigo primeiro.</summary>
    public static IReadOnlyList<EstornoDoPagamentoDto> De(IEnumerable<Estorno> estornos) =>
        [.. estornos.OrderBy(e => e.PedidoEm).ThenBy(e => e.Id, StringComparer.Ordinal)
            .Select(e => new EstornoDoPagamentoDto(e.Id, e.Valor.Reais, e.Motivo, e.Status.Texto(), e.PedidoEm, e.ConcluidoEm))];
}

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
/// <param name="Estornos">Os estornos, o mais antigo primeiro (RF-192-api).</param>
public sealed record PagamentoDto(
    string Id, string FaturaId, string Forma, decimal Valor, decimal ValorFatura, decimal Juros, string Status, DateTimeOffset CriadoEm,
    DateTimeOffset? ExpiraEm, DateTimeOffset? PagoEm, string? Motivo, bool Duplicidade, PixDto? Pix, BoletoDto? Boleto, DebitoDto? Debito,
    CartaoDto? Cartao, IReadOnlyList<EstornoDoPagamentoDto> Estornos)
{
    /// <summary>Do pagamento; o PIX e a confirmação do débito só enquanto aguardam (RF-185).</summary>
    public static PagamentoDto De(Pagamento p, IEnumerable<Estorno>? estornos = null)
    {
        var aguardando = p.Status == StatusDoPagamento.Aguardando;
        return new(p.Id, p.FaturaId, p.Forma.Texto(), p.Valor.Reais, p.ValorFatura.Reais, p.Juros.Reais, p.Status.Texto(), p.CriadoEm, p.ExpiraEm,
            p.PagoEm, p.Motivo, p.Duplicidade,
            aguardando && p.Pix is { } pix ? new PixDto(pix.CopiaECola, pix.QrCodeSvg) : null,
            p.Boleto is { } b ? new BoletoDto(b.Vencimento, b.LinhaDigitavel, b.CodigoBarras, $"/pagamento/pagamentos/{p.Id}/boleto") : null,
            aguardando && p.ConfirmacaoUrl is { } url ? new DebitoDto(url) : null,
            p.Cartao is { } c ? new CartaoDto(c.Bandeira, c.Final) : null,
            EstornoDoPagamentoDto.De(estornos ?? []));
    }
}

/// <summary>A conta do pagamento, para o recebedor.</summary>
/// <param name="Id">conta-ebfga.</param>
/// <param name="Sigla">EBFGA.</param>
/// <param name="Nome">Escola Bento Freitas.</param>
public sealed record ContaDoPagamentoDto(string Id, string Sigla, string Nome);

/// <summary>200 de GET /pagamento/recebimentos/pagamentos/{id} (RF-192-api): o pagamento com os estornos, para o recebedor.</summary>
/// <param name="Id">PAG-000773.</param>
/// <param name="FaturaId">FAT-000402.</param>
/// <param name="Conta">A conta que pagou.</param>
/// <param name="Forma">CARTAO_CREDITO.</param>
/// <param name="Valor">O valor cobrado.</param>
/// <param name="Status">ESTORNADO_PARCIAL.</param>
/// <param name="PagoEm">Quando o banco confirmou.</param>
/// <param name="PagoPor">O nome de quem pagou, quando confirmado.</param>
/// <param name="Cartao">A bandeira e o final do cartão.</param>
/// <param name="Estornado">O já devolvido.</param>
/// <param name="Saldo">O que ainda pode ser estornado.</param>
/// <param name="Estornos">Os estornos, o mais antigo primeiro.</param>
/// <param name="TraceId">A execução que o criou (RNF-244).</param>
public sealed record PagamentoDoRecebedorDto(
    string Id, string FaturaId, ContaDoPagamentoDto Conta, string Forma, decimal Valor, string Status, DateTimeOffset? PagoEm, string? PagoPor,
    CartaoDto? Cartao, decimal Estornado, decimal Saldo, IReadOnlyList<EstornoDoPagamentoDto> Estornos, string TraceId)
{
    /// <summary>Do pagamento, da conta da fatura, do nome de quem pagou e dos estornos.</summary>
    public static PagamentoDoRecebedorDto De(Pagamento p, ContaDoPagamentoDto conta, string? pagoPor, IEnumerable<Estorno> estornos) =>
        new(p.Id, p.FaturaId, conta, p.Forma.Texto(), p.Valor.Reais, p.Status.Texto(), p.PagoEm, p.PagoEm is null ? null : pagoPor,
            p.Cartao is { } c ? new CartaoDto(c.Bandeira, c.Final) : null, p.Estornado.Reais, p.Saldo.Reais, EstornoDoPagamentoDto.De(estornos),
            p.TraceId);
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
/// <param name="Conta">O nome da conta que paga: o boleto sai em nome dela (RF-188).</param>
/// <param name="Competencia">2026-11.</param>
/// <param name="Forma">BOLETO.</param>
/// <param name="Valor">O valor cobrado.</param>
/// <param name="Vencimento">O vencimento do boleto; null no comprovante.</param>
/// <param name="LinhaDigitavel">A linha do boleto; null no comprovante.</param>
/// <param name="CodigoBarras">O código de barras; null no comprovante.</param>
/// <param name="PagoEm">Quando o banco confirmou; null no boleto.</param>
/// <param name="Autenticacao">O id da cobrança no banco.</param>
/// <param name="Cartao">"VISA final 4242", no cartão.</param>
/// <param name="Documento">O CNPJ ou CPF da conta, no boleto.</param>
/// <param name="Endereco">O endereço de quem paga, no boleto.</param>
public sealed record DocumentoDoPagamentoDto(string FaturaId, string PagamentoId, string Conta, string Competencia, string Forma, decimal Valor,
    DateOnly? Vencimento, string? LinhaDigitavel, string? CodigoBarras, DateTimeOffset? PagoEm, string? Autenticacao, string? Cartao,
    string? Documento = null, EnderecoDto? Endereco = null);
