using Boaz.Core.Hub.Sac.Chamados.Domain;

namespace Boaz.Core.Hub.Tests.Sac.Chamados;

/// <summary>RNF-229: as horas úteis, de segunda a sexta, das 08:00 às 18:00 de Brasília, sem os feriados nacionais.</summary>
public sealed class CalendarioUtilTests
{
    [Theory]
    [InlineData(8, "2026-11-12T10:00:00-03:00", "2026-11-12T18:00:00-03:00")]
    [InlineData(4, "2026-11-12T17:00:00-03:00", "2026-11-13T11:00:00-03:00")]
    [InlineData(4, "2026-11-14T09:00:00-03:00", "2026-11-16T12:00:00-03:00")]
    [InlineData(16, "2026-11-13T14:00:00-03:00", "2026-11-17T10:00:00-03:00")]
    [InlineData(4, "2026-11-19T17:00:00-03:00", "2026-11-23T11:00:00-03:00")]
    [InlineData(4, "2027-02-09T08:00:00-03:00", "2027-02-09T12:00:00-03:00")]
    [InlineData(4, "2026-11-12T06:00:00-03:00", "2026-11-12T12:00:00-03:00")]
    [InlineData(8, "2026-11-12T18:00:00-03:00", "2026-11-13T16:00:00-03:00")]
    public void RNF_229_contagem_do_prazo(int horas, string quando, string vence) =>
        Assert.Equal(DateTimeOffset.Parse(vence), CalendarioUtil.Somar(DateTimeOffset.Parse(quando), horas));

    [Fact]
    public void RNF_229_o_prazo_vai_em_UTC() =>
        Assert.Equal(TimeSpan.Zero, CalendarioUtil.Somar(new DateTimeOffset(2026, 11, 12, 13, 0, 0, TimeSpan.Zero), 8).Offset);

    [Theory]
    [InlineData(2026, "2026-04-05")]
    [InlineData(2027, "2027-03-28")]
    [InlineData(2025, "2025-04-20")]
    public void Pascoa(int ano, string domingo) => Assert.Equal(DateOnly.Parse(domingo), CalendarioUtil.Pascoa(ano));

    [Theory]
    [InlineData("2026-04-03", true)]
    [InlineData("2026-11-20", true)]
    [InlineData("2026-12-25", true)]
    [InlineData("2027-02-09", false)]
    [InlineData("2026-06-04", false)]
    [InlineData("2026-11-12", false)]
    public void RNF_229_feriados_nacionais(string dia, bool feriado) => Assert.Equal(feriado, CalendarioUtil.Feriado(DateOnly.Parse(dia)));
}
