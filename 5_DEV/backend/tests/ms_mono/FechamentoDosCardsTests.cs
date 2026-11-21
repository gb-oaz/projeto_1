using Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Sistema;

namespace Boaz.MsMono.Tests;

// A vigia do dia do fechamento da 00:00 de Brasília (15 RN-278): o salto do relógio de teste não é a virada do dia.
public class FechamentoDosCardsTests
{
    sealed class RelogioManual(DateTimeOffset agora) : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = agora;

        public override DateTimeOffset GetUtcNow() => Agora;
    }

    static readonly DateTimeOffset Dia20 = new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero);

    [Fact]
    public void RN_278_a_virada_natural_do_dia_fecha_uma_vez()
    {
        var relogio = new RelogioManual(Dia20);
        var vigia = new VigiaDoDia(relogio, () => 0);

        Assert.Null(vigia.Virou());
        relogio.Agora = Dia20.AddDays(1);
        var novo = vigia.Virou();
        Assert.NotNull(novo);
        vigia.Fechado(novo.Value);

        Assert.Null(vigia.Virou());
    }

    // o PUT e o DELETE /_teste/relogio trocam o dia à força: o tick seguinte não fecha nada, nem o card sobe de versão sem mudança
    [Theory]
    [InlineData(1)]
    [InlineData(-5)]
    [InlineData(200)]
    public void RN_278_salto_do_relogio_de_teste_nao_fecha_os_cards(int dias)
    {
        var relogio = new RelogioManual(Dia20);
        long ajustes = 0;
        var vigia = new VigiaDoDia(relogio, () => ajustes);

        relogio.Agora = Dia20.AddDays(dias);
        ajustes++;

        Assert.Null(vigia.Virou());
        Assert.Null(vigia.Virou());
        relogio.Agora = relogio.Agora.AddDays(1);
        Assert.NotNull(vigia.Virou());
    }

    [Fact]
    public void RN_278_o_relogio_da_aplicacao_conta_cada_ajuste_do_teste()
    {
        var relogio = new RelogioDaAplicacao();

        relogio.Definir(Dia20);
        relogio.Real();

        Assert.Equal(2, relogio.Ajustes);
    }
}
