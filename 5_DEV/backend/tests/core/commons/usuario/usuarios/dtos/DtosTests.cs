using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Dtos;

/// <summary>Validação de campo dos pedidos: DADOS_INVALIDOS com o campo do contrato.</summary>
public class DtosTests
{
    static string Campo(Action validar) => Assert.Throws<FalhaDeUsuarios>(validar).Campo!;

    [Fact]
    public void Lista_de_usuarios()
    {
        Assert.Equal((TipoUsuario.Staff, "maria", EstadoUsuario.Ativo, 2), new ListaDeUsuariosDto("q", "STAFF", " maria ", "ATIVO", 2).Validar());
        Assert.Equal((TipoUsuario.Participante, null as string, null as EstadoUsuario?, 1), new ListaDeUsuariosDto("q", "PARTICIPANTE", " ").Validar());
        Assert.Equal("tipo", Campo(() => new ListaDeUsuariosDto("q", "MASTER").Validar()));
        Assert.Equal("tipo", Campo(() => new ListaDeUsuariosDto("q", null).Validar()));
        Assert.Equal("estado", Campo(() => new ListaDeUsuariosDto("q", "STAFF", Estado: "SUMIDO").Validar()));
        Assert.Equal("pagina", Campo(() => new ListaDeUsuariosDto("q", "STAFF", Pagina: 0).Validar()));
    }

    [Fact]
    public void Novo_usuario()
    {
        var (tipo, nome, nomeCurto, matricula, responsaveis) =
            new NovoUsuarioDto("q", "PARTICIPANTE", " Lucas ", "Lucas", null, [new("Carla", "mae")]).Validar();
        Assert.Equal((TipoUsuario.Participante, "Lucas", "lucas", null as string), (tipo, nome, nomeCurto, matricula));
        Assert.Equal([new Responsavel("Carla", "MAE")], responsaveis);
        Assert.Empty(new NovoUsuarioDto("q", "STAFF", "Maria", "maria").Validar().Responsaveis);
        Assert.Equal("tipo", Campo(() => new NovoUsuarioDto("q", "ADMINISTRADOR", "A", "a").Validar()));
        Assert.Equal("nome", Campo(() => new NovoUsuarioDto("q", "STAFF", " ", "a").Validar()));
        Assert.Equal("nomeCurto", Campo(() => new NovoUsuarioDto("q", "STAFF", "A", "a b").Validar()));
        Assert.Equal("matricula", Campo(() => new NovoUsuarioDto("q", "STAFF", "A", "a", "").Validar()));
        Assert.Equal("responsaveis", Campo(() => new NovoUsuarioDto("q", "STAFF", "A", "a", null, []).Validar()));
        Assert.Equal("responsaveis[0].parentesco", Campo(() => new NovoUsuarioDto("q", "PARTICIPANTE", "A", "a", null, [new("Carla", null)]).Validar()));
        Assert.Equal("responsaveis[0].nome", Campo(() => new NovoUsuarioDto("q", "PARTICIPANTE", "A", "a", null, [null]).Validar()));
    }

    [Fact]
    public void Edicao_de_usuario()
    {
        Assert.Equal((null as string, null as string, null as string, null as IReadOnlyList<Responsavel>), new EdicaoDeUsuarioDto("q", "u").Validar());
        Assert.Equal("mariasouza", new EdicaoDeUsuarioDto("q", "u", NomeCurto: "MariaSouza").Validar().NomeCurto);
        Assert.Equal("nome", Campo(() => new EdicaoDeUsuarioDto("q", "u", Nome: "").Validar()));
    }

    [Fact]
    public void Geracao_de_senha()
    {
        Assert.Equal(("maria.ebfga@deboaz.com", "482913"), new GeracaoDeSenhaDto(" Maria.EBFGA@deboaz.com", "482913").Validar());
        Assert.Equal("entrada", Campo(() => new GeracaoDeSenhaDto(null, "1").Validar()));
        Assert.Equal("codigoLiberacao", Campo(() => new GeracaoDeSenhaDto("a", " ").Validar()));
    }

    [Theory]
    [InlineData(null, "tipo")]
    [InlineData("nome", "nome")]
    [InlineData("documento", "documento")]
    [InlineData("responsavel", "responsavel")]
    [InlineData("telefone", "telefone")]
    [InlineData("dominio", "dominio")]
    [InlineData("sigla", "sigla")]
    public void Nova_conta_na_ordem_do_formulario(string? vazio, string campo)
    {
        var dto = new NovaContaDto(vazio is null ? "STAFF" : "INSTITUICAO", vazio == "nome" ? "" : "Escola",
            vazio == "documento" ? "123" : "12.345.678/0001-90", vazio == "responsavel" ? null : "Gustavo",
            vazio == "telefone" ? " " : "(51) 9", vazio == "dominio" ? "Escola Nova" : "escola", vazio == "sigla" ? "AB" : "escol");
        Assert.Equal(campo, Campo(() => dto.Validar()));
    }

    [Fact]
    public void Ajuda_lista_de_contas_e_estado()
    {
        Assert.Equal(("Escola", "(51) 9"), new AjudaDaMasterDto(" Escola ", "(51) 9", "a").Validar());
        Assert.Equal("contato", Campo(() => new AjudaDaMasterDto("Escola", null, "a").Validar()));
        Assert.Equal(new FiltroDeContas("EBFGA", EstadoConta.Suspensa, 1), new ListaDeContasDto("q", "EBFGA", "SUSPENSA").Validar());
        Assert.Equal("estado", Campo(() => new ListaDeContasDto("q", Estado: "x").Validar()));
        Assert.Equal(EstadoConta.Encerrada, new EstadoDaContaDto("q", "c", "ENCERRADA").Validar());
        Assert.Equal("estado", Campo(() => new EstadoDaContaDto("q", "c", null).Validar()));
    }

    [Fact]
    public void Conferencia_da_master()
    {
        Assert.Equal(new ConferenciaDaMaster("12", null, "(51) 9", new DateOnly(2026, 10, 5)),
            new ConferenciaDaMasterDto("q", "c", " 12 ", " ", "(51) 9", "2026-10-05").Validar());
        Assert.Equal("documento", Campo(() => new ConferenciaDaMasterDto("q", "c", null).Validar()));
        Assert.Equal("ultimoPagamento", Campo(() => new ConferenciaDaMasterDto("q", "c", "12", UltimoPagamento: "05/10/2026").Validar()));
    }

    [Fact]
    public void Tempos()
    {
        Assert.Equal(TempoDaConta.Padrao, new TempoDaContaDto("q", "c", "PT8H", "P7D", "P30D").Validar());
        Assert.Equal("staffApp", Campo(() => new TempoDaContaDto("q", "c", "PT8H", "7 dias", "P30D").Validar()));
        Assert.Equal(TempoDoTime.Padrao, new TempoDoTimeDto("q", "PT8H", "PT8H", "PT8H", "PT8H").Validar());
        Assert.Equal("mastersApp", Campo(() => new TempoDoTimeDto("q", "PT8H", "PT8H", "PT8H", null).Validar()));
        Assert.Equal(new TempoDaContaLidoDto("PT8H", "P7D", "P30D"), TempoDaContaLidoDto.De(TempoDaConta.Padrao));
        Assert.Equal(new TempoDoTimeLidoDto("PT8H", "PT8H", "PT8H", "PT8H"), TempoDoTimeLidoDto.De(TempoDoTime.Padrao));
    }
}
