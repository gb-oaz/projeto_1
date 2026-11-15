using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

namespace Boaz.Core.Hub.Tests.Pagamento.Faturas;

using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>Os valores, a fatura e o pagamento do 19_modulo-pagamento.</summary>
public sealed class DominioTests
{
    static readonly ContaDaFatura Ebfga = new("conta-ebfga", "EBFGA", "Escola Bento Freitas", "INSTITUICAO");

    static Fatura Nova(string competencia = "2026-11", decimal valor = 3698.33m) =>
        Fatura.Emitir("FAT-000402", Ebfga, Competencia.Ler(competencia)!.Value, [new ItemDaFatura("MOD-03", "Usuários", "cob-1", Dinheiro.De(valor))],
            10, "trc-00000001");

    [Fact]
    public void Dinheiro_tem_duas_casas_e_texto_em_reais()
    {
        Assert.Equal(369833, Dinheiro.De(3698.33m).Centavos);
        Assert.Equal("3.698,33", Dinheiro.De(3698.33m).EmTexto());
        Assert.Equal(2, Dinheiro.De(1m).Reais.Scale);
        Assert.True(Dinheiro.TemDuasCasas(10.01m));
        Assert.False(Dinheiro.TemDuasCasas(10.005m));
        Assert.Equal(Dinheiro.De(3m), Dinheiro.Somar([Dinheiro.De(1m), Dinheiro.De(2m)]));
        Assert.True(Dinheiro.De(1m) < Dinheiro.De(2m) && Dinheiro.De(2m) >= Dinheiro.De(2m));
    }

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("2026-13", false)]
    [InlineData("26-10", false)]
    [InlineData(null, false)]
    public void RF_190_competencia_no_formato_ano_mes(string? texto, bool valida) => Assert.Equal(valida, Competencia.Ler(texto) is not null);

    [Fact]
    public void RN_300_emitida_no_dia_1_as_00h_de_brasilia_e_vencida_no_dia_10()
    {
        var outubro = Competencia.Ler("2026-10")!.Value;
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero), outubro.EmitidaEm.ToUniversalTime());
        Assert.Equal(new DateOnly(2026, 11, 10), outubro.Vencimento(10));
        Assert.Equal("2026-09", outubro.Anterior.ToString());
        Assert.Equal("2026-01", Competencia.Ler("2025-12")!.Value.Proxima.ToString());
        Assert.True(outubro > outubro.Anterior);
    }

    [Fact]
    public void Calendario_conta_dias_uteis()
    {
        Assert.Equal(new DateOnly(2026, 12, 8), Calendario.SomarDiasUteis(new DateOnly(2026, 12, 3), 3));
        Assert.Equal(5, Calendario.DiasUteisAte(new DateOnly(2026, 12, 3), new DateOnly(2026, 12, 10)));
        Assert.False(Calendario.Util(new DateOnly(2026, 12, 5)));
        Assert.Equal(new DateOnly(2026, 12, 2), Calendario.Dia(new DateTimeOffset(2026, 12, 3, 2, 59, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void RN_301_a_pagar_vira_vencida_no_dia_seguinte_e_paga_com_o_pagamento()
    {
        var fatura = Nova();
        Assert.False(fatura.Vencer(new DateOnly(2026, 12, 10), DateTimeOffset.UtcNow, "trc-00000002"));
        Assert.True(fatura.Vencer(new DateOnly(2026, 12, 11), DateTimeOffset.UtcNow, "trc-00000002"));
        Assert.Equal(StatusDaFatura.Vencida, fatura.Status);
        Assert.Equal(1, fatura.DiasDeAtraso(new DateOnly(2026, 12, 11)));
        Assert.True(fatura.Quitar("PAG-000773", fatura.Valor, DateTimeOffset.UtcNow, "trc-00000003", "usr"));
        Assert.False(fatura.Quitar("PAG-000774", fatura.Valor, DateTimeOffset.UtcNow, "trc-00000003", "usr"));
        Assert.Equal(StatusDaFatura.Paga, fatura.Status);
        Assert.Equal(Dinheiro.Zero, fatura.TotalAPagar(null));
        Assert.NotNull(fatura.GuardarAte);
        Assert.Equal(3, fatura.Historico.Count);
        Assert.Equal("trc-00000003", fatura.Historico[^1].TraceId);
    }

    [Fact]
    public void RN_310_juros_ao_dia_com_a_taxa_do_vencimento()
    {
        var fatura = Nova("2026-10", 294m);
        fatura.Vencer(new DateOnly(2026, 11, 11), DateTimeOffset.UtcNow, "trc-00000002");
        var juros = fatura.Juros(new DateOnly(2026, 11, 14), 0.1m);
        Assert.Equal((0.1m, 4, Dinheiro.De(1.18m)), (juros!.TaxaAoDia, juros.Dias, juros.Valor));
        Assert.Equal(Dinheiro.De(295.18m), fatura.TotalAPagar(juros));
        Assert.Null(fatura.Juros(new DateOnly(2026, 11, 14), 0m));
    }

    [Fact]
    public void RF_191_cancelar_a_paga_e_recusado()
    {
        var fatura = Nova();
        fatura.Quitar("PAG-1", fatura.Valor, DateTimeOffset.UtcNow, "trc-00000003", "usr");
        var falha = Assert.Throws<FalhaDoPagamento>(() => fatura.Cancelar("motivo", "admin", DateTimeOffset.UtcNow, "trc-00000004"));
        Assert.Equal("FATURA_PAGA", falha.Codigo);

        var aberta = Nova();
        aberta.Cancelar("Cobrança em dobro", "admin", DateTimeOffset.UtcNow, "trc-00000004");
        aberta.Cancelar("de novo", "admin", DateTimeOffset.UtcNow, "trc-00000004");
        Assert.Equal((StatusDaFatura.Cancelada, "Cobrança em dobro"), (aberta.Status, aberta.Cancelamento!.Motivo));
    }

    [Fact]
    public void RN_306_estorno_ate_o_saldo_e_um_por_vez()
    {
        var fatura = Nova();
        var pagamento = Pagamento.Novo("PAG-000773", fatura, FormaDePagamento.CartaoCredito, Dinheiro.Zero, DateTimeOffset.UtcNow, "usr", "trc-00000005",
            null);
        Assert.Equal("PAGAMENTO_NAO_CONFIRMADO",
            Assert.Throws<FalhaDoPagamento>(() => pagamento.PedirEstorno(Dinheiro.De(1m), DateTimeOffset.UtcNow, "t", "admin")).Codigo);
        pagamento.Nascer(StatusDoPagamento.Pago, "card_1", null, DateTimeOffset.UtcNow, null, null, null, new CartaoMascarado("VISA", "4242"), null);
        Assert.Equal("ESTORNO_ACIMA_DO_SALDO",
            Assert.Throws<FalhaDoPagamento>(() => pagamento.PedirEstorno(Dinheiro.De(4000m), DateTimeOffset.UtcNow, "t", "admin")).Codigo);
        pagamento.PedirEstorno(Dinheiro.De(1000m), DateTimeOffset.UtcNow, "t", "admin");
        Assert.Equal("ESTORNO_EM_ANDAMENTO",
            Assert.Throws<FalhaDoPagamento>(() => pagamento.PedirEstorno(Dinheiro.De(1m), DateTimeOffset.UtcNow, "t", "admin")).Codigo);
        pagamento.ConcluirEstorno(Dinheiro.De(1000m), DateTimeOffset.UtcNow, "t");
        Assert.Equal((StatusDoPagamento.EstornadoParcial, Dinheiro.De(2698.33m)), (pagamento.Status, pagamento.Saldo));
        pagamento.PedirEstorno(Dinheiro.De(2698.33m), DateTimeOffset.UtcNow, "t", "admin");
        pagamento.DesfazerEstorno(DateTimeOffset.UtcNow, "t", "banco");
        Assert.Equal(StatusDoPagamento.EstornadoParcial, pagamento.Status);
        pagamento.PedirEstorno(Dinheiro.De(2698.33m), DateTimeOffset.UtcNow, "t", "admin");
        pagamento.ConcluirEstorno(Dinheiro.De(2698.33m), DateTimeOffset.UtcNow, "t");
        Assert.Equal(StatusDoPagamento.Estornado, pagamento.Status);
    }

    [Fact]
    public void RF_192_boleto_se_estorna_pelo_sac()
    {
        var pagamento = Pagamento.Novo("PAG-000790", Nova(), FormaDePagamento.Boleto, Dinheiro.Zero, DateTimeOffset.UtcNow, "usr", "t", null);
        Assert.Equal("ESTORNO_PELO_SAC",
            Assert.Throws<FalhaDoPagamento>(() => pagamento.PedirEstorno(Dinheiro.De(1m), DateTimeOffset.UtcNow, "t", "admin")).Codigo);
    }

    [Fact]
    public void RN_302_e_RN_303_fim_do_pagamento_em_aberto()
    {
        var pix = Pagamento.Novo("PAG-1", Nova(), FormaDePagamento.Pix, Dinheiro.Zero, DateTimeOffset.UtcNow, "usr", "t", null);
        pix.Nascer(StatusDoPagamento.Aguardando, "pix_1", DateTimeOffset.UtcNow, null, new DadosDoPix("000201", "<svg/>"), null, null, null, null);
        Assert.True(pix.EmAberto);
        Assert.True(pix.Expirar(DateTimeOffset.UtcNow, "t"));
        Assert.False(pix.Expirar(DateTimeOffset.UtcNow, "t"));
        Assert.True(pix.Pagar(DateTimeOffset.UtcNow, true, "t", "banco"));
        Assert.True(pix.Duplicidade);
        Assert.Equal("PAGAMENTO_CONFIRMADO", Assert.Throws<FalhaDoPagamento>(() => pix.Cancelar(DateTimeOffset.UtcNow, "t", "usr")).Codigo);

        var debito = Pagamento.Novo("PAG-2", Nova(), FormaDePagamento.CartaoDebito, Dinheiro.Zero, DateTimeOffset.UtcNow, "usr", "t", null);
        Assert.True(debito.Recusar(Pagamento.RecusadoPeloEmissor, DateTimeOffset.UtcNow, "t", "banco"));
        Assert.False(debito.Cancelar(DateTimeOffset.UtcNow, "t", "usr"));
    }

    [Fact]
    public void Textos_do_contrato_ida_e_volta()
    {
        foreach (var forma in TextosDoPagamento.Formas) Assert.Equal(forma, TextosDoPagamento.LerForma(forma.Texto()));
        foreach (var status in Enum.GetValues<StatusDaFatura>()) Assert.Equal(status, TextosDoPagamento.LerStatusDaFatura(status.Texto()));
        foreach (var status in Enum.GetValues<StatusDoPagamento>()) Assert.Equal(status, TextosDoPagamento.LerStatusDoPagamento(status.Texto()));
        foreach (var status in Enum.GetValues<StatusDoEstorno>()) Assert.Equal(status, TextosDoPagamento.LerStatusDoEstorno(status.Texto()));
        Assert.Null(TextosDoPagamento.LerForma("CHEQUE"));
        Assert.Equal("FAT-000402", Numeros.De(Numeros.Faturas, 402));
        Assert.Equal("EST-000012", Numeros.De(Numeros.Estornos, 12));
    }
}
