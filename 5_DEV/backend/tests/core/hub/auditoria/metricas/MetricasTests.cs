using Boaz.Core.Hub.Auditoria.Metricas.Domain;

namespace Boaz.Core.Hub.Tests.Auditoria.Metricas;

/// <summary>RF-197, RF-198, RN-316, RN-317, RNF-247: as faixas do tempo, os percentis, a medição e as janelas.</summary>
public sealed class MetricasTests
{
    static Histograma De(params (int Ms, int Pedidos)[] pedidos)
    {
        var contagens = new long[FaixasDeTempo.Quantas];
        foreach (var (ms, n) in pedidos) contagens[(int)FaixasDeTempo.De(ms)] += n;
        return new Histograma(contagens);
    }

    [Theory]
    [InlineData(0, FaixaDeTempo.Ate50)]
    [InlineData(50, FaixaDeTempo.Ate50)]
    [InlineData(51, FaixaDeTempo.Ate100)]
    [InlineData(500, FaixaDeTempo.Ate500)]
    [InlineData(1000, FaixaDeTempo.Ate1000)]
    [InlineData(3000, FaixaDeTempo.Ate3000)]
    [InlineData(10000, FaixaDeTempo.Ate10000)]
    [InlineData(10001, FaixaDeTempo.Mais)]
    public void RN_317_faixa_pelo_tempo(int ms, FaixaDeTempo esperada) => Assert.Equal(esperada, FaixasDeTempo.De(ms));

    [Fact]
    public void RN_317_fim_e_campo_de_cada_faixa()
    {
        Assert.Equal([50, 100, 250, 500, 1000, 3000, 5000, 10000, null], Enum.GetValues<FaixaDeTempo>().Select(f => f.FimMs()));
        Assert.Equal(["f50", "f100", "f250", "f500", "f1000", "f3000", "f5000", "f10000", "fMais"], FaixasDeTempo.Campos);
    }

    [Theory]
    [InlineData(60, 40, 30, 90, 6, 200, 4, 450, 50, 250, 500)]
    [InlineData(49, 50, 51, 51, 0, 0, 0, 0, 100, 100, 100)]
    [InlineData(97, 300, 3, 12000, 0, 0, 0, 0, 500, 500, null)]
    [InlineData(95, 900, 5, 2600, 0, 0, 0, 0, 1000, 1000, 3000)]
    public void RN_317_percentil_pelo_fim_da_faixa(int n1, int ms1, int n2, int ms2, int n3, int ms3, int n4, int ms4, int p50, int p95, int? p99)
    {
        var h = De((ms1, n1), (ms2, n2), (ms3, n3), (ms4, n4));
        Assert.Equal((p50, p95, p99), (h.Percentil(50), h.Percentil(95), h.Percentil(99)));
    }

    [Fact]
    public void RN_317_sem_pedido_nao_tem_percentil_e_os_histogramas_se_somam()
    {
        Assert.Null(Histograma.Vazio.Percentil(95));
        var soma = De((40, 1)).Somar(De((40, 2), (9000, 1)));
        Assert.Equal(4, soma.Total);
        Assert.Equal(3, soma.Contagens[0]);
    }

    [Fact]
    public void RNF_247_medicao_soma_pedidos_por_resultado_e_tempo()
    {
        var m = new Medicao(new DateTimeOffset(2026, 11, 20, 13, 41, 0, TimeSpan.Zero), "POST", "/pagamento/faturas/{id}/pagamentos", "MOD-01");
        Assert.True(m.Somar(200, 400, 2));
        Assert.True(m.Somar(404, 90));
        Assert.True(m.Somar(500, 2600));
        Assert.Equal((4L, 2L, 1L, 1L), (m.Pedidos, m.Ok, m.Erros4xx, m.Erros5xx));
        Assert.Equal(25.0m, m.TaxaErro5xx);
        Assert.Equal(3000, m.Tempos.Percentil(95));
        Assert.True(m.Lento);
        m.Fechar();
        Assert.False(m.Somar(200, 10));
        Assert.Equal(4L, m.Pedidos);
    }

    [Fact]
    public void RF_198_resumo_lento_e_taxa()
    {
        var r = new ResumoDeMedicoes(1240, 31, 2, De((90, 1000), (400, 200), (900, 40)));
        Assert.Equal((0.16m, 500, false), (r.TaxaErro5xx, r.P95Ms, r.Lento));
        Assert.True(new ResumoDeMedicoes(1, 0, 0, De((12000, 1))).Lento);
        Assert.Null(ResumoDeMedicoes.Vazio.TaxaErro5xx);
        var dobro = r.Somar(r);
        Assert.Equal((2480L, 62L, 4L), (dobro.Pedidos, dobro.Erros4xx, dobro.Erros5xx));
    }

    [Theory]
    [InlineData("/auth/hub/entrar", "MOD-03")]
    [InlineData("/usuarios/{id}", "MOD-03")]
    [InlineData("/contas", "MOD-03")]
    [InlineData("/sac/chamados/{id}", "MOD-02")]
    [InlineData("/pagamento/faturas/{id}/pagamentos", "MOD-01")]
    [InlineData("/catalogo/vitrine", "MOD-14")]
    [InlineData("/auditoria/registros", "MOD-17")]
    [InlineData("/health", null)]
    [InlineData("/_teste/auditoria/verificacao", null)]
    [InlineData("/nao-existe", null)]
    public void RN_316_modulo_de_cada_endereco(string endereco, string? modulo) => Assert.Equal(modulo, ModuloDoEndereco.De(endereco));

    [Fact]
    public void RN_316_modulos_medidos()
    {
        Assert.True(ModuloDoEndereco.Valido("MOD-02"));
        Assert.False(ModuloDoEndereco.Valido("MOD-99"));
        Assert.False(ModuloDoEndereco.Valido(null));
        Assert.Equal(5, ModuloDoEndereco.Modulos.Count);
    }

    [Fact]
    public void RNF_247_acumulador_soma_por_minuto_e_retira_so_os_fechados()
    {
        var acumulador = new AcumuladorDeMedicoes();
        var agora = new DateTimeOffset(2026, 11, 20, 13, 41, 30, TimeSpan.Zero);
        acumulador.Somar("GET", "/sac/chamados", 200, 80, agora);
        acumulador.Somar("GET", "/sac/chamados", 500, 80, agora.AddSeconds(10));
        acumulador.Somar("GET", "/sac/chamados", 200, 80, agora.AddMinutes(1));
        Assert.Equal(2, acumulador.Esperando);

        var fechados = acumulador.Retirar(Janelas.Minuto(agora.AddMinutes(1)));
        var minuto = Assert.Single(fechados);
        Assert.Equal((2L, 1L, "MOD-02"), (minuto.Pedidos, minuto.Erros5xx, minuto.Modulo));
        Assert.Equal(1, acumulador.Esperando);
        Assert.Single(acumulador.Retirar(agora.AddHours(1)));
        Assert.Equal(0, acumulador.Esperando);
    }

    [Fact]
    public void RNF_247_acumulador_descarta_o_que_passa_do_maximo_de_enderecos()
    {
        var acumulador = new AcumuladorDeMedicoes();
        var agora = new DateTimeOffset(2026, 11, 20, 13, 41, 30, TimeSpan.Zero);
        for (var i = 0; i < AcumuladorDeMedicoes.Maximo + 5; i++) acumulador.Somar("GET", $"/catalogo/{i}", 404, 10, agora);
        Assert.Equal(AcumuladorDeMedicoes.Maximo, acumulador.Esperando);
        acumulador.Somar("GET", "/catalogo/0", 404, 10, agora);
        Assert.Equal(AcumuladorDeMedicoes.Maximo, acumulador.Esperando);
    }

    [Fact]
    public void RNF_247_acumulador_limpo_nao_guarda_mais_nada_para_gravar()
    {
        var acumulador = new AcumuladorDeMedicoes();
        acumulador.Somar("GET", "/sac/chamados", 200, 10, new DateTimeOffset(2026, 11, 20, 13, 31, 30, TimeSpan.Zero));

        acumulador.Limpar();

        Assert.Equal(0, acumulador.Esperando);
        Assert.Empty(acumulador.Retirar(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void RF_197_janelas_pontos_passos_e_limites()
    {
        var agora = new DateTimeOffset(2026, 11, 20, 13, 42, 20, TimeSpan.Zero);
        Assert.Equal([60, 24, 7, 30], Enum.GetValues<Janela>().Select(j => j.Pontos()));
        Assert.Equal(["1h", "24h", "7d", "30d"], Enum.GetValues<Janela>().Select(j => j.Texto()));
        Assert.Equal([Janela.H1, Janela.D30], [Janelas.De("1h")!.Value, Janelas.De("30d")!.Value]);
        Assert.Null(Janelas.De("2h"));
        Assert.False(Janela.H1.PorHora());
        Assert.True(Janela.D7.PorHora());
        Assert.Equal((new DateTimeOffset(2026, 11, 20, 12, 42, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 20, 13, 42, 0, TimeSpan.Zero)), Janela.H1.Limites(agora));
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 14, 0, 0, TimeSpan.Zero), Janela.H24.Limites(agora).De);
        // 7 dias: 00:00 de Brasília (03:00 UTC) do dia 14 de novembro
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 3, 0, 0, TimeSpan.Zero), Janela.D7.Limites(agora).De);
        Assert.Equal(TimeSpan.FromDays(1), Janela.D30.Passo());
    }
}
