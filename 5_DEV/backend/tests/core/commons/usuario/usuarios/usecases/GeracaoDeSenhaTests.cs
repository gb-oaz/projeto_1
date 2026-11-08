using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;
using GrupoDeFalha = Boaz.Core.Commons.Usuario.Usuarios.Domain.GrupoDeFalha;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>Código de liberação e senha gerada (RF-109, RF-110, RN-170, RN-171, RN-181, RN-182, RN-190, RNF-203).</summary>
public class GeracaoDeSenhaTests
{
    readonly UsuariosEmMemoria mock = new Cenario().Mock;

    const string Entrada = "gustavo.ebfga@deboaz.com";

    async Task<string> GustavoPendente() =>
        (await mock.CriarUsuario.Executar(new("usr-ebfga-master", "STAFF", "Gustavo Boaz de Moura Souza", "gustavo"), TraceId.Novo())).CodigoLiberacao!;

    [Fact]
    public async Task RF_109_primeiro_acesso_do_staff()
    {
        var codigo = await GustavoPendente();
        var gerada = await mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo());

        Assert.Matches(@"^Gu[!@#$%&*]Boaz\d{6}$", gerada.Senha);
        Assert.Matches("^[0-9a-f]{32}$", gerada.Comprovante);
        var gustavo = mock.Usuario("usr-ebfga-gustavo")!;
        Assert.True(ResumoDeSenha.Confere(gerada.Senha, gustavo.ResumoSenha));
        Assert.Equal(EstadoUsuario.Pendente, gustavo.Estado);
        Assert.Equal(Credenciais.Resumo(gerada.Comprovante), mock.Geracoes(gustavo.Id)[^1].ResumoComprovante);
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.GerarSenha && a.Etapa == EtapaDaExecucao.Concluido && a.Alvo == gustavo.Id
            && a.Quem is null);

        await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()));
    }

    [Fact]
    public async Task RN_180_entrada_com_maiusculas()
    {
        var codigo = await GustavoPendente();
        Assert.NotNull(await mock.GerarSenha.Executar(new("  Gustavo.EBFGA@deboaz.com ", codigo), TraceId.Novo()));
    }

    [Fact]
    public async Task RF_109_codigo_errado()
    {
        await GustavoPendente();
        var falha = await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new(Entrada, "111111"), TraceId.Novo()));
        Assert.Equal(GrupoDeFalha.NaoAutenticado, falha.Grupo);
        Assert.Equal(1, mock.Tentativas(Entrada)!.ErrosSeguidos);
        Assert.Null(mock.Usuario("usr-ebfga-gustavo")!.ResumoSenha);
    }

    [Fact]
    public async Task RF_109_quinto_codigo_errado()
    {
        var codigo = await GustavoPendente();
        for (var i = 0; i < 4; i++) await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new(Entrada, "111111"), TraceId.Novo()));
        var falha = await Falha("ENTRADA_BLOQUEADA", () => mock.GerarSenha.Executar(new(Entrada, "111111"), TraceId.Novo()));
        Assert.Equal(mock.Relogio.Agora.AddMinutes(15), falha.TentarEm);
        await Falha("ENTRADA_BLOQUEADA", () => mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()));

        mock.Relogio.Avancar(TimeSpan.FromMinutes(15));
        Assert.NotNull(await mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()));
        Assert.Null(mock.Tentativas(Entrada));
    }

    [Theory]
    [InlineData("usr-ebfga-lucas")]
    [InlineData("ninguem")]
    public async Task RF_109_sem_geracao_aberta_e_codigo_invalido(string quem)
    {
        var entrada = quem == "ninguem" ? "ninguem.ebfga@deboaz.com" : "EBFGA-LUCAS-0412";
        await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new(entrada, "482913"), TraceId.Novo()));
        await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new("maria.ebfga@deboaz.com", "482913"), TraceId.Novo()));
    }

    [Fact]
    public async Task RF_109_desativado_nao_gera()
    {
        var codigo = await GustavoPendente();
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-gustavo"), TraceId.Novo());
        await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()));
    }

    [Theory]
    [InlineData(71, true)]
    [InlineData(73, false)]
    public async Task RN_182_codigo_vencido(int horas, bool gera)
    {
        var codigo = await GustavoPendente();
        mock.Relogio.Avancar(TimeSpan.FromHours(horas));
        if (gera) Assert.NotNull(await mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()));
        else Assert.Equal(GrupoDeFalha.Vencido, (await Falha("CODIGO_VENCIDO", () => mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()))).Grupo);
    }

    [Fact]
    public async Task RN_171_o_que_muda_a_cada_geracao()
    {
        var senhas = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var codigo = (await mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo())).CodigoLiberacao;
            senhas.Add((await mock.GerarSenha.Executar(new("paulo.ebfga@deboaz.com", codigo), TraceId.Novo())).Senha);
        }
        Assert.All(senhas, s => Assert.Matches(@"^Pa[!@#$%&*]Mendes\d{6}$", s));
        Assert.NotEqual(senhas[0], senhas[1]);
    }

    [Fact]
    public async Task RF_110_professor_esqueceu_a_senha()
    {
        var antiga = await mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        var nova = await mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());

        Assert.Matches(@"^\d{6}$", nova.CodigoLiberacao);
        Assert.Equal(mock.Relogio.Agora.AddHours(72), nova.VenceEm);
        Assert.Null(mock.Usuario("usr-ebfga-paulo")!.ResumoSenha);
        Assert.Contains(("usr-ebfga-paulo", MotivoFim.NovaSenha), mock.SessoesEncerradas);
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.NovaGeracaoSenha && a.Alvo == "usr-ebfga-paulo");
        if (antiga.CodigoLiberacao != nova.CodigoLiberacao)
            await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new("paulo.ebfga@deboaz.com", antiga.CodigoLiberacao), TraceId.Novo()));
        Assert.NotNull(await mock.GerarSenha.Executar(new("paulo.ebfga@deboaz.com", nova.CodigoLiberacao), TraceId.Novo()));
    }

    [Fact]
    public async Task RF_110_par_nao_pede_a_geracao() =>
        await Falha("ACESSO_NEGADO", () => mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-joao", "usr-ebfga-rui"), TraceId.Novo()));

    [Fact]
    public async Task RF_110_participante_nao_tem_senha() =>
        await Falha("SEM_SENHA", () => mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-lucas"), TraceId.Novo()));

    [Fact]
    public async Task RN_190_membro_esqueceu_a_senha() =>
        Assert.Matches(@"^\d{6}$", (await mock.PedirNovaGeracaoDeSenha.Executar(new("usr-dboaz-admin", "usr-dboaz-caio"), TraceId.Novo())).CodigoLiberacao);

    [Fact]
    public async Task RN_181_codigo_so_na_criacao()
    {
        var codigo = await GustavoPendente();
        Assert.DoesNotContain(typeof(UsuarioLidoDto).GetProperties(), p => p.Name == "CodigoLiberacao");
        await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new("maria.ebfga@deboaz.com", codigo), TraceId.Novo()));
        Assert.NotNull(await mock.GerarSenha.Executar(new(Entrada, codigo), TraceId.Novo()));
    }
}
