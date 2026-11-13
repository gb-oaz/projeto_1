using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;
using Boaz.Core.Hub.Tests.Sac.Mocks;
using static Boaz.Core.Hub.Tests.Sac.Mocks.Cenario;

namespace Boaz.Core.Hub.Tests.Sac.Configuracao;

/// <summary>RF-181 e RN-289: os prazos por categoria, de 1 a 80 horas úteis.</summary>
public sealed class PrazosTests
{
    readonly Cenario c = new();

    [Fact]
    public void RN_289_padrao()
    {
        Assert.Equal([4, 8, 8, 16, 40, 4], Enum.GetValues<Categoria>().Select(PrazosDoSac.Padrao.HorasDe));
        Assert.Equal(4, new PrazosDoSac(new Dictionary<Categoria, int>(), null, null).HorasDe(Categoria.AcessoESenha));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(81)]
    public void RF_181_fora_dos_limites(int horas) =>
        Assert.Equal("FORA_DOS_LIMITES", Assert.Throws<FalhaDoSac>(() => PrazosDoSac.Padrao.Definir([(Categoria.DuvidaDeUso, horas)], "Adm", DateTimeOffset.UnixEpoch)).Codigo);

    [Fact]
    public async Task RF_181_salvar_e_ler()
    {
        var salvos = await c.SalvarPrazos.Executar(new([new("ACESSO_E_SENHA", 4), new("DUVIDA_DE_USO", 12)]), Caio, Trace);
        Assert.Contains(new PrazoDto("DUVIDA_DE_USO", 12), salvos.Prazos);
        Assert.Equal("Caio Lima", salvos.SalvaPor);
        var lidos = await c.LerPrazos.Executar(Ana);
        Assert.Equal(6, lidos.Prazos.Count);
        Assert.Equal(12, c.Mock.Prazos.HorasDe(Categoria.DuvidaDeUso));
        Assert.Equal("SAC-000042", (await c.Abrir.Executar(Corpo(categoria: "DUVIDA_DE_USO"), Master, Trace)).Id);
    }

    [Fact]
    public async Task RF_181_recusas()
    {
        await Recusa("ACESSO_NEGADO", () => c.LerPrazos.Executar(Master));
        await Recusa("ACESSO_NEGADO", () => c.SalvarPrazos.Executar(new([new("ACESSO_E_SENHA", 4)]), Master, Trace));
        await Recusa("DADOS_INVALIDOS", () => c.SalvarPrazos.Executar(new(null), Caio, Trace));
        await Recusa("DADOS_INVALIDOS", () => c.SalvarPrazos.Executar(new([new("OUTRA", 4)]), Caio, Trace));
        await Recusa("DADOS_INVALIDOS", () => c.SalvarPrazos.Executar(new([new("COBRANCA", null)]), Caio, Trace));
        await Recusa("FORA_DOS_LIMITES", () => c.SalvarPrazos.Executar(new([new("COBRANCA", 81)]), Caio, Trace));
    }
}
