using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;
using GrupoDeFalha = Boaz.Core.Commons.Usuario.Usuarios.Domain.GrupoDeFalha;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>Desativar, reativar, editar, novo código, auditoria e card (RF-112, RF-153, RF-154, RN-169, RN-178, RN-189, RN-244, RN-245).</summary>
public class AcoesSobreUsuariosTests
{
    readonly UsuariosEmMemoria mock = new Cenario().Mock;

    [Fact]
    public async Task RF_112_professor_que_saiu_da_escola()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        Assert.Equal(EstadoUsuario.Desativado, mock.Usuario("usr-ebfga-paulo")!.Estado);
        Assert.Contains(("usr-ebfga-paulo", MotivoFim.UsuarioDesativado), mock.SessoesEncerradas);
    }

    [Fact]
    public async Task RF_112_reativar()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        await mock.ReativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"));
        Assert.Equal(EstadoUsuario.Ativo, mock.Usuario("usr-ebfga-paulo")!.Estado);
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.ReativarUsuario);
    }

    [Fact]
    public async Task RF_112_ja_desativado_ou_ja_ativo()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        var falha = await Falha("ESTADO_IGUAL", () => mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo()));
        Assert.Equal(GrupoDeFalha.Conflito, falha.Grupo);
        await Falha("ESTADO_IGUAL", () => mock.ReativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-maria")));
    }

    [Fact]
    public async Task RF_112_reativar_quem_nunca_gerou_a_senha_volta_pendente()
    {
        var criada = await mock.CriarUsuario.Executar(new("usr-ebfga-master", "STAFF", "Bia Lima", "bia"), TraceId.Novo());
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", criada.Id), TraceId.Novo());
        await mock.ReativarUsuario.Executar(new("usr-ebfga-master", criada.Id));
        Assert.Equal(EstadoUsuario.Pendente, mock.Usuario(criada.Id)!.Estado);
    }

    [Theory]
    [InlineData("usr-dboaz-admin", "usr-dboaz-caio", null)]
    [InlineData("usr-ebfga-master", "usr-ebfga-joao", null)]
    [InlineData("usr-ebfga-joao", "usr-ebfga-ana", null)]
    [InlineData("usr-ebfga-joao", "usr-ebfga-lucas", null)]
    [InlineData("usr-ebfga-joao", "usr-ebfga-rui", "ACESSO_NEGADO")]
    [InlineData("usr-ebfga-ana", "usr-ebfga-joao", "ACESSO_NEGADO")]
    [InlineData("usr-ebfga-maria", "usr-ebfga-maria", "ACESSO_NEGADO")]
    [InlineData("usr-ebfga-maria", "usr-ebfga-master", "ACESSO_NEGADO")]
    [InlineData("usr-preis-pedro", "usr-ebfga-lucas", "USUARIO_INEXISTENTE")]
    public async Task RN_169_quem_age_sobre_quem(string quem, string alvo, string? codigo)
    {
        if (codigo is null) await mock.DesativarUsuario.Executar(new(quem, alvo), TraceId.Novo());
        else await Falha(codigo, () => mock.DesativarUsuario.Executar(new(quem, alvo), TraceId.Novo()));
    }

    [Fact]
    public async Task RF_153_nome_curto_do_staff()
    {
        var editada = await mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-maria", "Maria Souza Lima", "mariasouza"));
        Assert.Equal(new UsuarioEditadoDto("usr-ebfga-maria", "mariasouza.ebfga@deboaz.com"), editada);
        Assert.Contains(("usr-ebfga-maria", MotivoFim.NovoNomeCurto), mock.SessoesEncerradas);
        var maria = mock.Usuario("usr-ebfga-maria")!;
        Assert.Equal("Maria Souza Lima", maria.Nome);
        Assert.True(ResumoDeSenha.Confere(Cenario.SenhaDaMaria, maria.ResumoSenha));
    }

    [Fact]
    public async Task RF_153_so_o_nome_nao_encerra_sessoes()
    {
        await mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-maria", "Maria S.", "maria"));
        Assert.Empty(mock.SessoesEncerradas);
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.EditarUsuario);
    }

    [Fact]
    public async Task RF_153_responsavel_novo()
    {
        await mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-lucas",
            Responsaveis: [new("Carla Andrade", "MAE"), new("Paulo Andrade", "PAI")]));
        Assert.Equal(2, mock.Usuario("usr-ebfga-lucas")!.Responsaveis.Count);
        Assert.Equal("EBFGA-LUCAS-0412", mock.Usuario("usr-ebfga-lucas")!.Login);
        Assert.Equal("responsaveis", (await Falha("DADOS_INVALIDOS", () => mock.EditarUsuario.Executar(
            new("usr-ebfga-master", "usr-ebfga-maria", Responsaveis: [new("Carla", "MAE")])))).Campo);
    }

    [Fact]
    public async Task RF_153_nome_curto_repetido()
    {
        await Falha("NOME_CURTO_EM_USO", () => mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-ana", NomeCurto: "maria")));
        Assert.Equal("ana.ebfga@deboaz.com", mock.Usuario("usr-ebfga-ana")!.Login);
    }

    [Fact]
    public async Task RN_244_nome_curto_do_participante()
    {
        var editado = await mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-lucas", NomeCurto: "lucasa"));
        Assert.Matches(@"^EBFGA-LUCASA-\d{4}$", editado.Login);
        Assert.Contains(("usr-ebfga-lucas", MotivoFim.NovoCodigo), mock.SessoesEncerradas);
        Assert.Equal(mock.Relogio.Agora, mock.Usuario("usr-ebfga-lucas")!.CodigoGeradoEm);
    }

    [Fact]
    public async Task RN_244_matricula_repetida()
    {
        await Falha("MATRICULA_EM_USO", () => mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-ana", Matricula: "20260087")));
        Assert.Equal("20260013", mock.Usuario("usr-ebfga-ana")!.Matricula);
        await mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-ana", Matricula: "20260013"));
    }

    [Fact]
    public async Task RN_188_autonomo_nao_edita_matricula()
    {
        var bia = await mock.CriarUsuario.Executar(new("usr-preis-pedro", "PARTICIPANTE", "Bia", "bia"), TraceId.Novo());
        await Falha("DADOS_INVALIDOS", () => mock.EditarUsuario.Executar(new("usr-preis-pedro", bia.Id, Matricula: "12345678")));
    }

    [Fact]
    public async Task RF_154_familia_perdeu_o_codigo()
    {
        var novo = await mock.GerarNovoCodigoDeAcesso.Executar(new("usr-ebfga-master", "usr-ebfga-lucas"));
        Assert.Matches(@"^EBFGA-LUCAS-\d{4}$", novo.Login);
        Assert.Equal(mock.Relogio.Agora, novo.CodigoGeradoEm);
        Assert.Contains(("usr-ebfga-lucas", MotivoFim.NovoCodigo), mock.SessoesEncerradas);
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.NovoCodigoAcesso);
    }

    [Fact]
    public async Task RF_154_so_participante() =>
        await Falha("SO_PARTICIPANTE", () => mock.GerarNovoCodigoDeAcesso.Executar(new("usr-ebfga-master", "usr-ebfga-maria")));

    [Fact]
    public async Task RN_178_acao_auditada()
    {
        await mock.CriarUsuario.Executar(new("usr-ebfga-maria", "PARTICIPANTE", "Lia", "lia"), TraceId.Novo());
        await mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-ana"), TraceId.Novo());
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());

        var registro = mock.Auditoria[0];
        Assert.Equal(("conta-ebfga", "usr-ebfga-maria", AcaoAuditada.CriarUsuario, "usr-ebfga-lia"),
            (registro.ContaId, registro.Quem, registro.Acao, registro.Alvo));
        Assert.Equal(registro.Quando.AddYears(5), registro.GuardarAte);
        Assert.Equal([AcaoAuditada.CriarUsuario, AcaoAuditada.NovaGeracaoSenha, AcaoAuditada.DesativarUsuario],
            mock.Auditoria.Where(a => a.Etapa == EtapaDaExecucao.Concluido).Select(a => a.Acao));
    }

    [Fact]
    public async Task RN_178_desativar_auditado_do_comeco_ao_fim()
    {
        var traceId = new TraceId("trc-7f3a9c21");

        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), traceId);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, mock.Gravacoes);
        Assert.All(mock.Auditoria, a => Assert.Equal(
            (traceId, AcaoAuditada.DesativarUsuario, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-paulo"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem, a.Alvo)));
        Assert.Empty(mock.Erros);
    }

    [Fact]
    public async Task RN_270_desativar_quem_ja_esta_desativado_grava_o_alerta_antes_de_sair()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        mock.Gravacoes.Clear();
        var traceId = TraceId.Novo();

        await Falha("ESTADO_IGUAL", () => mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), traceId));

        Assert.Equal(new[] { "INICIADO", "ALERTA ESTADO_IGUAL" }, mock.Gravacoes);
        var erro = Assert.Single(mock.Erros);
        Assert.Equal((traceId, AcaoAuditada.DesativarUsuario, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-paulo", NivelDoErro.Alerta),
            (erro.TraceId, erro.Acao, erro.ContaId, erro.Quem, erro.Alvo, erro.Nivel));
    }

    [Fact]
    public async Task RN_270_desativar_com_o_banco_fora_grava_o_erro()
    {
        var queda = new InvalidOperationException("o banco caiu no meio da gravação");
        mock.FalhaAoGravarUsuario = queda;

        Assert.Same(queda, await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo())));

        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, mock.Gravacoes);
        Assert.Empty(mock.SessoesEncerradas);
    }

    [Fact]
    public async Task RN_178_nova_geracao_de_senha_auditada_do_comeco_ao_fim()
    {
        var traceId = new TraceId("trc-7f3a9c21");

        await mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), traceId);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, mock.Gravacoes);
        Assert.All(mock.Auditoria, a => Assert.Equal(
            (traceId, AcaoAuditada.NovaGeracaoSenha, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-paulo"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem, a.Alvo)));
        Assert.Empty(mock.Erros);
    }

    [Fact]
    public async Task RN_270_nova_geracao_de_senha_recusada_grava_o_alerta_antes_de_sair()
    {
        var traceId = TraceId.Novo();

        await Falha("SEM_SENHA", () => mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-lucas"), traceId));

        Assert.Equal(new[] { "INICIADO", "ALERTA SEM_SENHA" }, mock.Gravacoes);
        var erro = Assert.Single(mock.Erros);
        Assert.Equal((traceId, AcaoAuditada.NovaGeracaoSenha, "usr-ebfga-lucas", NivelDoErro.Alerta), (erro.TraceId, erro.Acao, erro.Alvo, erro.Nivel));
    }

    [Fact]
    public async Task RN_270_nova_geracao_de_senha_com_o_banco_fora_grava_o_erro()
    {
        mock.FalhaAoGravarUsuario = new InvalidOperationException("o banco caiu no meio da gravação");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.PedirNovaGeracaoDeSenha.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo()));

        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, mock.Gravacoes);
        Assert.Equal(NivelDoErro.Erro, Assert.Single(mock.Erros).Nivel);
    }

    [Fact]
    public async Task RN_270_pedido_cancelado_nao_e_erro()
    {
        mock.FalhaAoGravarUsuario = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo()));

        Assert.Equal(new[] { "INICIADO" }, mock.Gravacoes);
        Assert.Empty(mock.Erros);
    }

    [Fact]
    public async Task RN_189_dados_do_card()
    {
        mock.Meses["conta-ebfga"] = [301, 310, 318, 323, 335];
        mock.Ativos[("conta-ebfga", TipoUsuario.Staff)] = 37;
        mock.Ativos[("conta-ebfga", TipoUsuario.Participante)] = 298;
        mock.Selos["conta-ebfga"] = 2;

        Assert.Equal(new CardDeUsuariosDto(2, 335, "usuários ativos", 12, [301, 310, 318, 323, 335], 37, 298) with { Meses = mock.Meses["conta-ebfga"] },
            await mock.LerCardDeUsuarios.Executar(new("usr-ebfga-master")));
    }

    [Theory]
    [InlineData("usr-preis-pedro", "conta-preis", 32, "participantes ativos")]
    [InlineData("usr-dboaz-admin", "time", 112, "contas ativas")]
    public async Task RN_189_dado_de_cada_perfil(string quem, string chave, int dado, string rotulo)
    {
        mock.Meses[chave] = [dado - 4, dado];
        var card = await mock.LerCardDeUsuarios.Executar(new(quem));
        Assert.Equal((dado, rotulo, 4, null as int?), (card.Dado, card.Rotulo, card.NoMes, card.Staff));
    }

    [Fact]
    public async Task RN_189_participante_nao_ve_o_card() =>
        await Falha("ACESSO_NEGADO", () => mock.LerCardDeUsuarios.Executar(new("usr-ebfga-lucas")));
}
