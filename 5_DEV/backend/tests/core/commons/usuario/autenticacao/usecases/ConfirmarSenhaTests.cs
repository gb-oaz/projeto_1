using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>Primeiro acesso pelo comprovante da senha gerada (RF-143).</summary>
public class ConfirmarSenhaTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RF_143_comprovante_abre_a_sessao_uma_vez()
    {
        c.Mock.NovoUsuario("usr-ebfga-paulo", "conta-ebfga", TipoUsuario.Staff, "paulo.ebfga@deboaz.com", "Xu@Xa182482",
            estado: EstadoUsuario.Pendente);
        var comprovante = c.Mock.NovaGeracao("usr-ebfga-paulo");

        var sessao = await c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace);

        Assert.Equal(("usr-ebfga-paulo", "hub"), (Claims(sessao).Sub, Claims(sessao).Aud));
        Assert.Equal(Aparelho.Navegador, sessao.Aparelho);
        Assert.Equal(EstadoUsuario.Ativo, c.Mock.Usuario("usr-ebfga-paulo").Estado);
        // 17 RN-280: o pendente virou ativo, e os cards do Usuários são recontados na mesma execução
        Assert.Equal(new Boaz.Core.Commons.Usuario.Usuarios.Dtos.MudancaNoCadastroDto("conta-ebfga", "usr-ebfga-paulo"), Assert.Single(c.Mock.CardsAtualizados));
        await Falha("COMPROVANTE_INVALIDO", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace));
    }

    [Fact]
    public async Task RN_182_comprovante_vence_em_72_h()
    {
        var comprovante = c.Mock.NovaGeracao("usr-ebfga-maria");
        c.Mock.Relogio.Avancar(TimeSpan.FromHours(72));
        await Falha("COMPROVANTE_INVALIDO", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante, "APP"), Trace));
    }

    [Fact]
    public async Task Comprovante_desconhecido()
    {
        await Falha("COMPROVANTE_INVALIDO", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto("c7e1"), Trace));
    }

    [Fact]
    public async Task Usuario_que_nao_existe_mais()
    {
        var comprovante = c.Mock.NovaGeracao("usr-ebfga-maria");
        c.Mock.NovoUsuario("usr-sem-entrada", "conta-ebfga", TipoUsuario.Staff);
        var outro = c.Mock.NovaGeracao("usr-apagado");
        await Falha("COMPROVANTE_INVALIDO", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(outro), Trace));
        await c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace);
    }

    [Fact]
    public async Task RF_140_desativado_nao_gasta_o_comprovante()
    {
        var comprovante = c.Mock.NovaGeracao("usr-ebfga-maria");
        c.Mock.MudarUsuario("usr-ebfga-maria", EstadoUsuario.Desativado);
        await Falha("ACESSO_INDISPONIVEL", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace));

        c.Mock.MudarUsuario("usr-ebfga-maria", EstadoUsuario.Ativo);
        await c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace);
    }
}
