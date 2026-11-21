using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;

namespace Boaz.Core.Hub.Tests.Auditoria.Card;

/// <summary>RN-322: o card da Auditoria, as séries e a versão.</summary>
public sealed class CardTests
{
    // 2026-11-20 é uma sexta-feira
    static readonly DateOnly Hoje = new(2026, 11, 20);
    static readonly DateTimeOffset Agora = new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero);

    static DiaDoCard Dia(DateOnly dia, int erros, int alertas = 0, int interrompidas = 0, long versao = 1) => new(dia, erros, alertas, interrompidas, versao);

    [Fact]
    public void RN_322_card_do_time_com_o_intermediador_fora_do_ar()
    {
        var card = new CardDaAuditoria(1, [Dia(Hoje, 4, 31, 1, 12), Dia(Hoje.AddDays(-1), 7)], Hoje, Agora);
        var dto = CardDaAuditoriaDto.De(card);
        Assert.Equal(("MOD-17", "DEBOAZ", false, 1, 1201L), (dto.Modulo, dto.Perfil, dto.Vazio, dto.Selo, dto.Versao));
        Assert.Equal((1, "serviço fora do ar"), (dto.Reduzida.Valor, dto.Reduzida.Rotulo));
        Assert.Equal((4, "erros hoje"), (dto.Normal.Valor, dto.Normal.Rotulo));
        Assert.Equal(["Fora do ar 1", "Alertas 31", "Interrompidas 1"], dto.Normal.Apoio.Select(l => $"{l.Nome} {l.Valor}"));
        Assert.Equal(["Fora do ar 1", "Erros hoje 4", "Interrompidas 1"], dto.Expandida.Lista.Select(l => $"{l.Nome} {l.Valor}"));
        Assert.Equal(Agora, dto.AtualizadoEm);
    }

    [Fact]
    public void RN_322_tudo_no_ar_e_card_vazio_e_o_rotulo_vai_para_o_plural()
    {
        var vazio = new CardDaAuditoria(0, [], Hoje, Agora);
        Assert.True(vazio.Vazio);
        Assert.Equal((0, 0L), (CardDaAuditoriaDto.De(vazio).Normal.Valor, CardDaAuditoriaDto.De(vazio).Versao));
        Assert.Equal("serviços fora do ar", new CardDaAuditoria(2, [], Hoje, Agora).Rotulo);
        Assert.False(new CardDaAuditoria(0, [Dia(Hoje, 1)], Hoje, Agora).Vazio);
    }

    [Fact]
    public void RN_322_serie_do_dia_tem_12_numeros_do_mais_antigo_ao_atual()
    {
        var dias = new[] { Dia(Hoje, 4), Dia(Hoje.AddDays(-1), 7), Dia(Hoje.AddDays(-11), 2), Dia(Hoje.AddDays(-12), 99) };
        var serie = new CardDaAuditoria(0, dias, Hoje, Agora).SerieDoDia;
        Assert.Equal(12, serie.Count);
        Assert.Equal((2, 7, 4), (serie[0], serie[10], serie[11]));
    }

    [Fact]
    public void RN_322_semanas_de_segunda_a_domingo_e_meses_somados_na_leitura()
    {
        // a semana de 16 a 22 de novembro tem hoje (sexta 20) e a quarta 18; a anterior, de 9 a 15
        var dias = new[] { Dia(Hoje, 4), Dia(new(2026, 11, 18), 3), Dia(new(2026, 11, 15), 5), Dia(new(2026, 11, 9), 1), Dia(new(2026, 10, 31), 8) };
        var card = new CardDaAuditoria(0, dias, Hoje, Agora);
        Assert.Equal((7, 6, 12), (card.SerieDaSemana[11], card.SerieDaSemana[10], card.SerieDaSemana.Count));
        Assert.Equal((13, 8, 12), (card.SerieDoMes[11], card.SerieDoMes[10], card.SerieDoMes.Count));
        Assert.Equal(new DateOnly(2025, 12, 1), CardDaAuditoria.Desde(Hoje));
    }

    [Fact]
    public void RN_322_dia_guardado_400_dias_e_muda_quando_os_numeros_mudam()
    {
        var dia = Dia(Hoje, 1, 2, 3, 5);
        Assert.Equal("deboaz:2026-11-20", dia.Id);
        Assert.Equal(new DateTimeOffset(2027, 12, 25, 0, 0, 0, TimeSpan.Zero), dia.GuardarAte);
        Assert.False(dia.MudouDe(Dia(Hoje, 1, 2, 3, 9)));
        Assert.True(dia.MudouDe(Dia(Hoje, 2, 2, 3)));
        Assert.True(dia.MudouDe(null));
        Assert.False(Dia(Hoje, 0).MudouDe(null));
    }

    [Fact]
    public void RN_322_versao_junta_a_do_dia_e_os_servicos_fora_do_ar()
    {
        var a = new CardDaAuditoria(0, [Dia(Hoje, 1, versao: 3)], Hoje, Agora).Versao;
        var b = new CardDaAuditoria(1, [Dia(Hoje, 1, versao: 3)], Hoje, Agora).Versao;
        var c = new CardDaAuditoria(0, [Dia(Hoje, 2, versao: 4)], Hoje, Agora).Versao;
        Assert.Equal([300L, 301L, 400L], [a, b, c]);
    }
}
