using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Domain;

/// <summary>Os textos do contrato: valores das enumerações e códigos de erro.</summary>
public class NomesEFalhasTests
{
    [Theory]
    [InlineData(TipoQuemEntrou.Responsavel, "Mãe", "MAE")]
    [InlineData(TipoQuemEntrou.Responsavel, " avó ", "AVO")]
    [InlineData(TipoQuemEntrou.Participante, null, null)]
    public void RN_186_rotulo_de_quem_entrou(TipoQuemEntrou tipo, string? parentesco, string? rotulo) =>
        Assert.Equal(rotulo, new QuemEntrou(tipo, parentesco).Rotulo);

    [Fact]
    public void Valores_como_no_contrato()
    {
        Assert.Equal(["HUB", "QUEST", "SALA"], Enum.GetValues<Frente>().Select(f => f.Texto()));
        Assert.Equal(["NAVEGADOR", "APP"], Enum.GetValues<Aparelho>().Select(a => a.Texto()));
        Assert.Equal(["MASTER", "STAFF", "PARTICIPANTE", "MEMBRO_TIME", "ADMINISTRADOR"],
            Enum.GetValues<TipoUsuario>().Select(t => t.Texto()));
        Assert.Equal(["PARTICIPANTE", "RESPONSAVEL"], Enum.GetValues<TipoQuemEntrou>().Select(t => t.Texto()));
        Assert.Equal(["ENTRADA", "CODIGO_ACESSO", "CODIGO_LIBERACAO"], Enum.GetValues<AlvoTentativa>().Select(a => a.Texto()));
    }

    [Fact]
    public void Leitura_dos_valores()
    {
        Assert.Equal(Aparelho.App, Nomes.AparelhoDe("APP"));
        Assert.Equal(Aparelho.Navegador, Nomes.AparelhoDe("NAVEGADOR"));
        Assert.Null(Nomes.AparelhoDe("app"));
        Assert.Null(Nomes.AparelhoDe(null));
        Assert.Equal(TipoQuemEntrou.Responsavel, Nomes.QuemEntrouDe("RESPONSAVEL"));
        Assert.Null(Nomes.QuemEntrouDe("TIO"));
    }

    public static TheoryData<FalhaDeAutenticacao, string, GrupoDeFalha> Falhas() => new()
    {
        { FalhaDeAutenticacao.EntradaInvalida(), "ENTRADA_INVALIDA", GrupoDeFalha.NaoAutenticado },
        { FalhaDeAutenticacao.CodigoInvalido(), "CODIGO_INVALIDO", GrupoDeFalha.NaoAutenticado },
        { FalhaDeAutenticacao.AcessoIndisponivel(), "ACESSO_INDISPONIVEL", GrupoDeFalha.Proibido },
        { FalhaDeAutenticacao.ComprovanteInvalido(), "COMPROVANTE_INVALIDO", GrupoDeFalha.NaoAutenticado },
        { FalhaDeAutenticacao.SalaFechada(), "SALA_FECHADA", GrupoDeFalha.NaoEncontrado },
        { FalhaDeAutenticacao.SessaoTerminada(), "SESSAO_TERMINADA", GrupoDeFalha.NaoAutenticado },
        { FalhaDeAutenticacao.SessaoNaoEncontrada(), "SESSAO_NAO_ENCONTRADA", GrupoDeFalha.NaoEncontrado },
        { FalhaDeAutenticacao.TokenInvalido(), "TOKEN_INVALIDO", GrupoDeFalha.NaoAutenticado },
        { FalhaDeAutenticacao.PermissoesDesatualizadas(), "PERMISSOES_DESATUALIZADAS", GrupoDeFalha.NaoAutenticado },
    };

    [Theory]
    [MemberData(nameof(Falhas))]
    public void Codigo_e_grupo_de_cada_falha(FalhaDeAutenticacao falha, string codigo, GrupoDeFalha grupo)
    {
        Assert.Equal((codigo, grupo), (falha.Codigo, falha.Grupo));
        Assert.Null(falha.TentarEm);
        Assert.Null(falha.Campo);
    }

    [Fact]
    public void Mensagens_do_contrato()
    {
        Assert.Equal("Entrada ou senha incorretas.", FalhaDeAutenticacao.EntradaInvalida().Message);
        Assert.Equal("Código de acesso inválido.", FalhaDeAutenticacao.CodigoInvalido().Message);
        Assert.Equal("Sua sessão terminou. Entre de novo.", FalhaDeAutenticacao.SessaoTerminada().Message);
    }

    [Fact]
    public void Bloqueio_traz_tentarEm_e_dado_invalido_traz_o_campo()
    {
        var quando = new DateTimeOffset(2026, 11, 1, 9, 15, 0, TimeSpan.Zero);
        var bloqueio = FalhaDeAutenticacao.EntradaBloqueada(quando);
        Assert.Equal(("ENTRADA_BLOQUEADA", GrupoDeFalha.Bloqueado, quando), (bloqueio.Codigo, bloqueio.Grupo, bloqueio.TentarEm));
        var dados = FalhaDeAutenticacao.DadosInvalidos("aparelho");
        Assert.Equal(("DADOS_INVALIDOS", GrupoDeFalha.DadosInvalidos, "aparelho"), (dados.Codigo, dados.Grupo, dados.Campo));
    }
}
