using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Dtos;

/// <summary>Validação dos campos de cada pedido.</summary>
public class DtosTests
{
    static void Campo(string campo, Action validar)
    {
        var falha = Assert.Throws<FalhaDeAutenticacao>(validar);
        Assert.Equal(("DADOS_INVALIDOS", campo), (falha.Codigo, falha.Campo));
    }

    [Fact]
    public void Entrada_no_hub()
    {
        Assert.Equal(("maria.ebfga@deboaz.com", "Ma!Silva204871", Aparelho.App),
            new EntradaNoHubDto("Maria.EBFGA@deboaz.com", "Ma!Silva204871", "APP").Validar());
        Campo("entrada", () => new EntradaNoHubDto(" ", "x", "APP").Validar());
        Campo("senha", () => new EntradaNoHubDto("a", null, "APP").Validar());
        Campo("aparelho", () => new EntradaNoHubDto("a", "x", "TV").Validar());
        Campo("aparelho", () => new EntradaNoHubDto("a", "x", null).Validar());
    }

    [Fact]
    public void Entrada_no_quest()
    {
        var mae = new EntradaNoQuestDto("ebfga-lucas-0412", "APP", new QuemEntrouDto("RESPONSAVEL", " Mãe ")).Validar();
        Assert.Equal(("EBFGA-LUCAS-0412", Aparelho.App, new QuemEntrou(TipoQuemEntrou.Responsavel, "Mãe")), mae);
        var ele = new EntradaNoQuestDto("X", "NAVEGADOR", new QuemEntrouDto("PARTICIPANTE", "ignorado")).Validar();
        Assert.Equal(new QuemEntrou(TipoQuemEntrou.Participante), ele.Quem);
        Campo("codigo", () => new EntradaNoQuestDto(null, "APP", new QuemEntrouDto("PARTICIPANTE", null)).Validar());
        Campo("quem", () => new EntradaNoQuestDto("X", "APP", null).Validar());
        Campo("quem.tipo", () => new EntradaNoQuestDto("X", "APP", new QuemEntrouDto("TIO", null)).Validar());
        Campo("quem.parentesco", () => new EntradaNoQuestDto("X", "APP", new QuemEntrouDto("RESPONSAVEL", "")).Validar());
    }

    [Fact]
    public void Entrada_na_sala_e_confirmacao_de_senha_tem_navegador_por_padrao()
    {
        Assert.Equal(("482107", Aparelho.Navegador), new EntradaNaSalaDto(" 482107 ").Validar());
        Assert.Equal(("482107", Aparelho.App), new EntradaNaSalaDto("482107", "APP").Validar());
        Campo("sala", () => new EntradaNaSalaDto(null).Validar());
        Campo("aparelho", () => new EntradaNaSalaDto("1", "TV").Validar());

        Assert.Equal(("c7e1", Aparelho.Navegador), new ConfirmacaoDeSenhaDto("c7e1").Validar());
        Assert.Equal(("c7e1", Aparelho.App), new ConfirmacaoDeSenhaDto("c7e1", "APP").Validar());
        Campo("comprovante", () => new ConfirmacaoDeSenhaDto("").Validar());
        Campo("aparelho", () => new ConfirmacaoDeSenhaDto("c7e1", "x").Validar());
    }

    [Fact]
    public void Renovacao_sem_token_e_sessao_terminada()
    {
        Assert.Equal("3f9c2a71", new RenovacaoDto("3f9c2a71.A").Validar().Sid);
        Assert.Equal("SESSAO_TERMINADA", Assert.Throws<FalhaDeAutenticacao>(() => new RenovacaoDto(null).Validar()).Codigo);
    }

    [Fact]
    public void Conferencia_sem_token_e_token_invalido()
    {
        Assert.Equal("abc", new ConferenciaDeAcessoDto(" abc ", Frente.Hub).Validar());
        Assert.Equal("TOKEN_INVALIDO", Assert.Throws<FalhaDeAutenticacao>(() => new ConferenciaDeAcessoDto("", Frente.Hub).Validar()).Codigo);
    }
}
