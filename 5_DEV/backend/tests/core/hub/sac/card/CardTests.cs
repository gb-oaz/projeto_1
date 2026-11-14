using Boaz.Core.Hub.Sac.Card.Domain;
using Boaz.Core.Hub.Tests.Sac.Mocks;
using static Boaz.Core.Hub.Tests.Sac.Mocks.Cenario;

namespace Boaz.Core.Hub.Tests.Sac.Card;

/// <summary>RN-295: o card do SAC de cada perfil, as séries e o ao vivo.</summary>
public sealed class CardTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RN_295_card_da_instituicao()
    {
        for (var i = 0; i < 3; i++) await c.No("RESOLVIDO");
        await c.No("EM_ATENDIMENTO");
        var aguardando = await c.No("AGUARDANDO_CLIENTE");
        foreach (var id in c.Mock.Chamados.Keys.Where(k => k != aguardando).ToList()) await c.Ler.Executar(id, Master);
        var card = await c.Card.Executar(Master);
        Assert.Equal(("MOD-02", "INSTITUICAO", false, 1, 2), (card.Modulo, card.Perfil, card.Vazio, card.Selo, card.Reduzida.Valor));
        Assert.Equal(["Aguardando você 1", "Em atendimento 1", "Resolvidos no mês 3"], card.Normal.Apoio.Select(l => $"{l.Nome} {l.Valor}"));
        Assert.Equal(card.Normal.Apoio, card.Expandida.Lista);
        Assert.All(new[] { card.Series.Dia, card.Series.Semana, card.Series.Mes }, s => Assert.Equal(12, s.Count));
    }

    [Fact]
    public async Task RN_295_card_do_time_e_do_autonomo()
    {
        for (var i = 0; i < 5; i++) await c.Novo();
        var time = await c.Card.Executar(Caio);
        Assert.Equal(("DEBOAZ", 5), (time.Perfil, time.Selo));
        Assert.Equal(["Sem resposta", "Atrasados", "Nota média"], time.Normal.Apoio.Select(l => l.Nome));
        Assert.Equal("Aguardando cliente", time.Expandida.Lista[2].Nome);
        var pedro = await c.Card.Executar(Pedro);
        Assert.Equal(("AUTONOMO", 0, true), (pedro.Perfil, pedro.Selo, pedro.Vazio));
        Assert.Equal(c.Relogio.Agora, pedro.AtualizadoEm);
    }

    [Fact]
    public void RN_295_series_no_fim_de_cada_recorte()
    {
        var hoje = new DateOnly(2026, 11, 12);
        var fechados = new Dictionary<DateOnly, int> { [hoje.AddDays(-1)] = 3, [new DateOnly(2026, 11, 8)] = 7, [new DateOnly(2026, 10, 31)] = 9 };
        var (dia, semana, mes) = CardDoSac.Series(fechados, hoje, 2);
        Assert.Equal((3, 2), (dia[10], dia[11]));
        Assert.Equal((7, 2), (semana[10], semana[11]));
        Assert.Equal((9, 2), (mes[10], mes[11]));
        Assert.Equal(0, mes[0]);
    }

    [Fact]
    public async Task RN_295_ao_vivo_e_fechamento_do_dia()
    {
        var (intervalo, card) = await c.CardAoVivo.Abrir("30", Master);
        Assert.Equal(TimeSpan.FromSeconds(30), intervalo);
        Assert.Equal(TimeSpan.FromSeconds(300), (await c.CardAoVivo.Abrir(null, Master)).Intervalo);
        await Recusa("DADOS_INVALIDOS", () => c.CardAoVivo.Abrir("7", Master));
        Assert.Null(await c.CardAoVivo.Proximo(card.Versao, Master));
        await c.Novo();
        Assert.NotNull(await c.CardAoVivo.Proximo(card.Versao, Master));
        await c.Novo(Pedro);
        await c.FecharDia.Executar(new DateOnly(2026, 11, 11));
        Assert.Equal(3, c.Mock.DiasFechados.Count);
        Assert.Equal(1, (await c.Card.Executar(Master)).Series.Dia[10]);
    }
}
