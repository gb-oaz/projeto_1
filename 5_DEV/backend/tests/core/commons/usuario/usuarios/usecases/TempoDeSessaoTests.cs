using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>Tempo sem uso da conta e do time deboaz (RF-114, RF-152, RN-183, RN-243).</summary>
public class TempoDeSessaoTests
{
    readonly UsuariosEmMemoria mock = new Cenario().Mock;

    [Fact]
    public async Task RF_114_ler_e_salvar_o_tempo_da_escola()
    {
        Assert.Equal(new TempoDaContaLidoDto("PT8H", "P7D", "P30D"), await mock.LerTempoDaConta.Executar(new("usr-ebfga-master", "conta-ebfga")));
        var salvo = await mock.SalvarTempoDaConta.Executar(new("usr-ebfga-master", "conta-ebfga", "PT4H", "P14D", "P60D"));
        Assert.Equal(new TempoDaContaLidoDto("PT4H", "P14D", "P60D"), salvo);
        Assert.Equal(salvo, await mock.LerTempoDaConta.Executar(new("usr-ebfga-maria", "conta-ebfga")));
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.SalvarTempoSessao && a.Quem == "usr-ebfga-master");
    }

    [Fact]
    public async Task RF_114_tempo_de_outra_conta_nao_existe()
    {
        await Falha("CONTA_INEXISTENTE", () => mock.LerTempoDaConta.Executar(new("usr-preis-pedro", "conta-ebfga")));
        await Falha("CONTA_INEXISTENTE", () => mock.LerTempoDaConta.Executar(new("usr-ebfga-lucas", "conta-ebfga")));
        await Falha("CONTA_INEXISTENTE", () => mock.SalvarTempoDaConta.Executar(new("usr-dboaz-admin", "conta-dboaz", "PT8H", "P7D", "P30D")));
    }

    [Theory]
    [InlineData("staffNavegador", "PT15M", "PT12H")]
    [InlineData("staffApp", "P1D", "P30D")]
    [InlineData("participante", "P1D", "P90D")]
    public async Task RN_183_limites_e_padrao(string campo, string minimo, string maximo)
    {
        TempoDaContaDto Com(string valor) => new("usr-ebfga-master", "conta-ebfga",
            campo == "staffNavegador" ? valor : "PT8H", campo == "staffApp" ? valor : "P7D", campo == "participante" ? valor : "P30D");

        await mock.SalvarTempoDaConta.Executar(Com(minimo));
        await mock.SalvarTempoDaConta.Executar(Com(maximo));
        var menor = Textos.Iso(Textos.IsoDe(minimo)!.Value - TimeSpan.FromMinutes(1));
        var falha = await Falha("FORA_DOS_LIMITES", () => mock.SalvarTempoDaConta.Executar(Com(menor)));
        Assert.Equal((campo, minimo, maximo), (falha.Campo, falha.Minimo, falha.Maximo));
    }

    [Fact]
    public async Task RF_152_time_deboaz_muda_o_tempo_da_conta_master()
    {
        Assert.Equal(new TempoDoTimeLidoDto("PT8H", "PT8H", "PT8H", "PT8H"), await mock.LerTempoDoTime.Executar(new("usr-dboaz-caio")));
        var salvo = await mock.SalvarTempoDoTime.Executar(new("usr-dboaz-admin", "PT8H", "PT8H", "PT4H", "PT8H"));
        Assert.Equal(salvo, await mock.LerTempoDoTime.Executar(new("usr-dboaz-admin")));
        Assert.Equal("PT4H", salvo.MastersNavegador);
    }

    [Fact]
    public async Task RF_152_valor_fora_dos_limites()
    {
        var falha = await Falha("FORA_DOS_LIMITES", () => mock.SalvarTempoDoTime.Executar(new("usr-dboaz-admin", "PT8H", "PT8H", "PT30H", "PT8H")));
        Assert.Equal(("mastersNavegador", "PT15M", "PT24H"), (falha.Campo, falha.Minimo, falha.Maximo));
    }

    [Theory]
    [InlineData("PT15M", "PT12H", "PT13H", "membrosNavegador")]
    [InlineData("PT15M", "PT24H", "PT25H", "mastersApp")]
    public async Task RN_243_limites_e_padrao(string minimo, string maximo, string fora, string campo)
    {
        TempoDoTimeDto Com(string valor) => campo == "membrosNavegador"
            ? new("usr-dboaz-admin", valor, "PT8H", "PT8H", "PT8H")
            : new("usr-dboaz-admin", "PT8H", "PT8H", "PT8H", valor);

        await mock.SalvarTempoDoTime.Executar(Com(minimo));
        Assert.Equal(maximo, (await mock.SalvarTempoDoTime.Executar(Com(maximo))) is var lido && campo == "mastersApp" ? lido.MastersApp : lido.MembrosNavegador);
        Assert.Equal(campo, (await Falha("FORA_DOS_LIMITES", () => mock.SalvarTempoDoTime.Executar(Com(fora)))).Campo);
    }

    [Fact]
    public async Task RN_243_escola_nao_muda_a_propria_conta_master()
    {
        await Falha("ACESSO_NEGADO", () => mock.SalvarTempoDoTime.Executar(new("usr-ebfga-master", "PT8H", "PT8H", "PT8H", "PT8H")));
        await Falha("ACESSO_NEGADO", () => mock.LerTempoDoTime.Executar(new("usr-ebfga-master")));
    }
}
