namespace Boaz.Core.Hub.Pagamento.Faturas.Domain;

/// <summary>O status da fatura (RN-301).</summary>
public enum StatusDaFatura
{
    /// <summary>A_PAGAR: emitida, no prazo.</summary>
    APagar,
    /// <summary>VENCIDA: passou do vencimento sem pagamento; segue devida, com juros (RN-310).</summary>
    Vencida,
    /// <summary>PAGA: um pagamento confirmado pelo banco a quitou.</summary>
    Paga,
    /// <summary>CANCELADA: o administrador cancelou, com o motivo (RF-191).</summary>
    Cancelada,
}

/// <summary>A forma de pagamento (RF-184), na ordem da tela.</summary>
public enum FormaDePagamento
{
    /// <summary>PIX.</summary>
    Pix,
    /// <summary>CARTAO_CREDITO.</summary>
    CartaoCredito,
    /// <summary>CARTAO_DEBITO.</summary>
    CartaoDebito,
    /// <summary>BOLETO.</summary>
    Boleto,
}

/// <summary>O status do pagamento (RN-302, RN-303).</summary>
public enum StatusDoPagamento
{
    /// <summary>AGUARDANDO: PIX, débito ou boleto à espera do banco.</summary>
    Aguardando,
    /// <summary>PROCESSANDO: cartão em análise no banco.</summary>
    Processando,
    /// <summary>PAGO: confirmado pelo banco.</summary>
    Pago,
    /// <summary>RECUSADO: o banco recusou.</summary>
    Recusado,
    /// <summary>EXPIRADO: passou do prazo sem confirmação.</summary>
    Expirado,
    /// <summary>CANCELADO: quem paga desistiu para trocar de forma.</summary>
    Cancelado,
    /// <summary>ESTORNO_PENDENTE: estorno pedido ao banco.</summary>
    EstornoPendente,
    /// <summary>ESTORNADO_PARCIAL: parte devolvida.</summary>
    EstornadoParcial,
    /// <summary>ESTORNADO: todo o valor devolvido.</summary>
    Estornado,
}

/// <summary>O status do estorno (RF-192).</summary>
public enum StatusDoEstorno
{
    /// <summary>PENDENTE: pedido ao banco.</summary>
    Pendente,
    /// <summary>CONCLUIDO: o banco devolveu.</summary>
    Concluido,
    /// <summary>RECUSADO: o banco recusou.</summary>
    Recusado,
}

/// <summary>Os textos do contrato, em MAIÚSCULAS com sublinhado.</summary>
public static class TextosDoPagamento
{
    /// <summary>As formas na ordem da tela (RF-184).</summary>
    public static readonly IReadOnlyList<FormaDePagamento> Formas =
        [FormaDePagamento.Pix, FormaDePagamento.CartaoCredito, FormaDePagamento.CartaoDebito, FormaDePagamento.Boleto];

    /// <summary>A_PAGAR, VENCIDA, PAGA ou CANCELADA.</summary>
    public static string Texto(this StatusDaFatura status) => status switch
    {
        StatusDaFatura.APagar => "A_PAGAR",
        StatusDaFatura.Vencida => "VENCIDA",
        StatusDaFatura.Paga => "PAGA",
        _ => "CANCELADA",
    };

    /// <summary>PIX, CARTAO_CREDITO, CARTAO_DEBITO ou BOLETO.</summary>
    public static string Texto(this FormaDePagamento forma) => forma switch
    {
        FormaDePagamento.Pix => "PIX",
        FormaDePagamento.CartaoCredito => "CARTAO_CREDITO",
        FormaDePagamento.CartaoDebito => "CARTAO_DEBITO",
        _ => "BOLETO",
    };

    /// <summary>O status do pagamento no contrato.</summary>
    public static string Texto(this StatusDoPagamento status) => status switch
    {
        StatusDoPagamento.Aguardando => "AGUARDANDO",
        StatusDoPagamento.Processando => "PROCESSANDO",
        StatusDoPagamento.Pago => "PAGO",
        StatusDoPagamento.Recusado => "RECUSADO",
        StatusDoPagamento.Expirado => "EXPIRADO",
        StatusDoPagamento.Cancelado => "CANCELADO",
        StatusDoPagamento.EstornoPendente => "ESTORNO_PENDENTE",
        StatusDoPagamento.EstornadoParcial => "ESTORNADO_PARCIAL",
        _ => "ESTORNADO",
    };

    /// <summary>PENDENTE, CONCLUIDO ou RECUSADO.</summary>
    public static string Texto(this StatusDoEstorno status) => status switch
    {
        StatusDoEstorno.Pendente => "PENDENTE",
        StatusDoEstorno.Concluido => "CONCLUIDO",
        _ => "RECUSADO",
    };

    /// <summary>A forma pelo texto; null se não for uma das quatro.</summary>
    public static FormaDePagamento? LerForma(string? texto) => Formas.Cast<FormaDePagamento?>().FirstOrDefault(f => f!.Value.Texto() == texto);

    /// <summary>O status da fatura pelo texto (o que o banco de dados guarda).</summary>
    public static StatusDaFatura LerStatusDaFatura(string? texto) =>
        Enum.GetValues<StatusDaFatura>().FirstOrDefault(s => s.Texto() == texto);

    /// <summary>O status do pagamento pelo texto (o que o banco de dados guarda).</summary>
    public static StatusDoPagamento LerStatusDoPagamento(string? texto) =>
        Enum.GetValues<StatusDoPagamento>().FirstOrDefault(s => s.Texto() == texto);

    /// <summary>O status do estorno pelo texto (o que o banco de dados guarda).</summary>
    public static StatusDoEstorno LerStatusDoEstorno(string? texto) =>
        Enum.GetValues<StatusDoEstorno>().FirstOrDefault(s => s.Texto() == texto);
}
