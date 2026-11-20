namespace Boaz.Core.Hub.Pagamento.Faturas.Domain;

/// <summary>A conta da fatura, copiada na emissão: a busca do administrador não junta com contas.</summary>
/// <param name="Id">conta-ebfga.</param>
/// <param name="Sigla">EBFGA.</param>
/// <param name="Nome">Escola Bento Freitas.</param>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
public sealed record ContaDaFatura(string Id, string Sigla, string Nome, string Tipo);

/// <summary>Um módulo cobrado na fatura: a cobrança fechada do Catálogo (RN-300).</summary>
/// <param name="ModuloId">MOD-03.</param>
/// <param name="Nome">Usuários.</param>
/// <param name="CobrancaId">cob-ebfga-MOD-03-2026-11.</param>
/// <param name="Valor">O total da cobrança.</param>
public sealed record ItemDaFatura(string ModuloId, string Nome, string CobrancaId, Dinheiro Valor);

/// <summary>O cancelamento da fatura pelo administrador (RF-191).</summary>
/// <param name="Motivo">O motivo dito.</param>
/// <param name="Por">Quem cancelou.</param>
/// <param name="Em">Quando.</param>
public sealed record Cancelamento(string Motivo, string Por, DateTimeOffset Em);

/// <summary>Os juros de hoje de uma fatura vencida (RN-310).</summary>
/// <param name="TaxaAoDia">A taxa vigente no dia do vencimento, em % ao dia.</param>
/// <param name="Dias">Dias desde o dia seguinte ao vencimento até hoje.</param>
/// <param name="Valor">valor × taxa × dias, ao centavo.</param>
public sealed record JurosDaFatura(decimal TaxaAoDia, int Dias, Dinheiro Valor)
{
    /// <summary>
    /// Os juros da fatura vencida em "hoje" (dia de Brasília), com a taxa do
    /// dia do vencimento; null sem atraso ou com a taxa 0 (RN-310).
    /// </summary>
    public static JurosDaFatura? Calcular(Dinheiro valor, DateOnly vencimento, DateOnly hoje, decimal taxaAoDia)
    {
        var dias = hoje.DayNumber - vencimento.DayNumber;
        if (dias <= 0 || taxaAoDia <= 0m) return null;
        return new JurosDaFatura(taxaAoDia, dias, Dinheiro.De(valor.Reais * taxaAoDia / 100m * dias));
    }
}

/// <summary>
/// A fatura mensal de uma conta (RN-300): um item por cobrança fechada da
/// competência, emitida à 00:00 de Brasília do dia 1 e vencida no dia 10. O
/// status muda pela tabela da RN-301 e cada mudança entra no histórico com o
/// traceId de quem a fez (RNF-244).
/// </summary>
public sealed class Fatura
{
    readonly List<Mudanca> historico;

    /// <summary>Remonta a fatura como foi guardada.</summary>
    public Fatura(string id, ContaDaFatura conta, Competencia competencia, IReadOnlyList<ItemDaFatura> itens, DateTimeOffset emitidaEm,
        DateOnly vencimento, StatusDaFatura status, DateTimeOffset? pagaEm, string? pagaPor, Dinheiro valorPago, Dinheiro devolvido,
        Cancelamento? cancelamento, string traceId, IEnumerable<Mudanca> historico, long versao)
    {
        (Id, Conta, Competencia, Itens, EmitidaEm, Vencimento, Status) = (id, conta, competencia, itens, emitidaEm, vencimento, status);
        (PagaEm, PagaPor, ValorPago, Devolvido, Cancelamento, TraceId, Versao) = (pagaEm, pagaPor, valorPago, devolvido, cancelamento, traceId, versao);
        this.historico = [.. historico];
    }

    /// <summary>Por quantos anos a fatura fica guardada depois de paga ou cancelada (RNF-240).</summary>
    public const int AnosGuardada = 5;

    /// <summary>FAT-000402.</summary>
    public string Id { get; }

    /// <summary>A conta, copiada na emissão.</summary>
    public ContaDaFatura Conta { get; }

    /// <summary>O mês cobrado.</summary>
    public Competencia Competencia { get; }

    /// <summary>Os módulos cobrados.</summary>
    public IReadOnlyList<ItemDaFatura> Itens { get; }

    /// <summary>A soma dos itens.</summary>
    public Dinheiro Valor => Dinheiro.Somar(Itens.Select(i => i.Valor));

    /// <summary>00:00 de Brasília do dia 1 do mês seguinte à competência.</summary>
    public DateTimeOffset EmitidaEm { get; }

    /// <summary>Dia 10 do mês seguinte à competência.</summary>
    public DateOnly Vencimento { get; }

    /// <summary>O status atual.</summary>
    public StatusDaFatura Status { get; private set; }

    /// <summary>Quando o pagamento que a quitou foi pago.</summary>
    public DateTimeOffset? PagaEm { get; private set; }

    /// <summary>O pagamento que a quitou.</summary>
    public string? PagaPor { get; private set; }

    /// <summary>O valor que o pagamento que a quitou cobrou, com os juros (RN-308).</summary>
    public Dinheiro ValorPago { get; private set; }

    /// <summary>O já estornado do pagamento que a quitou (RF-192).</summary>
    public Dinheiro Devolvido { get; private set; }

    /// <summary>O cancelamento, se houve.</summary>
    public Cancelamento? Cancelamento { get; private set; }

    /// <summary>A execução que a emitiu.</summary>
    public string TraceId { get; }

    /// <summary>As mudanças de status, com o traceId de cada uma (RNF-244).</summary>
    public IReadOnlyList<Mudanca> Historico => historico;

    /// <summary>A versão lida, para a gravação conferir que ninguém mudou antes.</summary>
    public long Versao { get; private set; }

    /// <summary>A gravação conferida subiu a versão guardada: a próxima confere com ela.</summary>
    public void Versionar() => Versao++;

    /// <summary>A_PAGAR ou VENCIDA: ainda se paga.</summary>
    public bool EmAberto => Status is StatusDaFatura.APagar or StatusDaFatura.Vencida;

    /// <summary>Até quando fica guardada (RNF-240): 5 anos depois de paga ou cancelada.</summary>
    public DateTimeOffset? GuardarAte => (PagaEm ?? Cancelamento?.Em)?.AddYears(AnosGuardada);

    /// <summary>A última mudança: a emissão ou a última do histórico (card, RN-307).</summary>
    public DateTimeOffset AtualizadaEm => historico.Count == 0 ? EmitidaEm : historico.Max(m => m.Em);

    /// <summary>A fatura nova do fechamento da competência (RN-300), "A_PAGAR".</summary>
    public static Fatura Emitir(string id, ContaDaFatura conta, Competencia competencia, IReadOnlyList<ItemDaFatura> itens, int diaVencimento,
        string traceId) =>
        new(id, conta, competencia, itens, competencia.EmitidaEm, competencia.Vencimento(diaVencimento), StatusDaFatura.APagar, null, null,
            Dinheiro.Zero, Dinheiro.Zero, null, traceId, [new Mudanca(null, StatusDaFatura.APagar.Texto(), competencia.EmitidaEm, traceId, "sistema")],
            0);

    /// <summary>Os juros de hoje, com a taxa vigente no dia do vencimento (RN-310); null se não está vencida.</summary>
    public JurosDaFatura? Juros(DateOnly hoje, decimal taxaDoVencimento) =>
        Status == StatusDaFatura.Vencida ? JurosDaFatura.Calcular(Valor, Vencimento, hoje, taxaDoVencimento) : null;

    /// <summary>O que falta pagar hoje: o valor e os juros; 0,00 na paga e na cancelada (RF-183).</summary>
    public Dinheiro TotalAPagar(JurosDaFatura? juros) => EmAberto ? Valor + (juros?.Valor ?? Dinheiro.Zero) : Dinheiro.Zero;

    /// <summary>Dias de atraso em "hoje"; 0 se não está vencida (RF-191).</summary>
    public int DiasDeAtraso(DateOnly hoje) => Status == StatusDaFatura.Vencida ? Math.Max(0, hoje.DayNumber - Vencimento.DayNumber) : 0;

    /// <summary>"A_PAGAR" vira "VENCIDA" no dia seguinte ao vencimento (RN-301); false se nada mudou.</summary>
    public bool Vencer(DateOnly hoje, DateTimeOffset agora, string traceId)
    {
        if (Status != StatusDaFatura.APagar || hoje <= Vencimento) return false;
        Mudar(StatusDaFatura.Vencida, agora, traceId, "sistema");
        return true;
    }

    /// <summary>
    /// O pagamento confirmado quita a fatura "A_PAGAR" ou "VENCIDA" (RN-301,
    /// RN-305); a fatura já paga ou cancelada não muda (o pagamento fica em duplicidade, RN-303).
    /// </summary>
    public bool Quitar(string pagamentoId, Dinheiro valorPago, DateTimeOffset pagoEm, string traceId, string por)
    {
        if (!EmAberto) return false;
        (PagaEm, PagaPor, ValorPago) = (pagoEm, pagamentoId, valorPago);
        Mudar(StatusDaFatura.Paga, pagoEm, traceId, por);
        return true;
    }

    /// <summary>O administrador cancela a fatura em aberto (RF-191); a paga se estorna.</summary>
    /// <exception cref="FalhaDoPagamento">FATURA_PAGA.</exception>
    public void Cancelar(string motivo, string por, DateTimeOffset agora, string traceId)
    {
        if (Status == StatusDaFatura.Paga) throw FalhaDoPagamento.FaturaPaga("Fatura paga não pode ser cancelada. Faça o estorno.");
        if (Status == StatusDaFatura.Cancelada) return;
        Cancelamento = new Cancelamento(motivo, por, agora);
        Mudar(StatusDaFatura.Cancelada, agora, traceId, por);
    }

    /// <summary>O estorno concluído do pagamento que a quitou entra no devolvido (RF-192).</summary>
    public void Devolver(Dinheiro valor) => Devolvido += valor;

    void Mudar(StatusDaFatura para, DateTimeOffset em, string traceId, string? por)
    {
        historico.Add(new Mudanca(Status.Texto(), para.Texto(), em, traceId, por));
        Status = para;
    }
}
