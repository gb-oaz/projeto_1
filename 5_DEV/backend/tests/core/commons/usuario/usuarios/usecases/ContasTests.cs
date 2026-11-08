using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>Contas assinantes: criação, sigla, ajuda da master, estado e leitura (RF-104, RF-111, RF-113, RN-172 a RN-179, RN-187, RN-242, RNF-205).</summary>
public class ContasTests
{
    readonly Cenario mundo = new();
    UsuariosEmMemoria Mock => mundo.Mock;

    static NovaContaDto Colsa(string tipo = "INSTITUICAO", string documento = "98.765.432/0001-10", string sigla = "COLSA",
        string dominio = "colegio_salesiano") =>
        new(tipo, "Colégio Salesiano", documento, "Maria da Silva", "(51) 97777-0000", dominio, sigla);

    [Fact]
    public async Task RF_104_escola_cria_a_conta()
    {
        var criada = await Mock.CriarConta.Executar(new("INSTITUICAO", "Escola Nova", "11.222.333/0001-44", "Gustavo Boaz de Moura Souza",
            "(51) 99999-1111", "Escola_Nova", "ENOVA"), TraceId.Novo());

        Assert.Equal(("conta-enova", "escola_nova@deboaz.com"), (criada.ContaId, criada.Login));
        Assert.Matches(@"^Gu[!@#$%&*]Boaz\d{6}$", criada.Senha);
        var master = Mock.Usuario("usr-enova-master")!;
        Assert.Equal((TipoUsuario.Master, EstadoUsuario.Pendente), (master.Tipo, master.Estado));
        Assert.True(ResumoDeSenha.Confere(criada.Senha, master.ResumoSenha));
        var geracao = Assert.Single(Mock.Geracoes(master.Id));
        Assert.Equal(Credenciais.Resumo(criada.Comprovante), geracao.ResumoComprovante);
        Assert.Equal(TempoDaConta.Padrao, Mock.Tempo("conta-enova"));
        Assert.Contains(Mock.Auditoria, a => a.Acao == AcaoAuditada.CriarConta && a.Alvo == "conta-enova");
    }

    [Fact]
    public async Task RN_178_conta_criada_auditada_do_comeco_ao_fim()
    {
        var traceId = new TraceId("trc-7f3a9c21");

        var criada = await Mock.CriarConta.Executar(Colsa(), traceId);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, Mock.Gravacoes);
        Assert.All(Mock.Auditoria, a => Assert.Equal((traceId, AcaoAuditada.CriarConta, (string?)null), (a.TraceId, a.Acao, a.Quem)));
        var (iniciado, concluido) = (Mock.Auditoria[0], Mock.Auditoria[1]);
        Assert.Equal((EtapaDaExecucao.Iniciado, (string?)null, (string?)null), (iniciado.Etapa, iniciado.ContaId, iniciado.Alvo));
        Assert.Equal((EtapaDaExecucao.Concluido, criada.ContaId, criada.ContaId), (concluido.Etapa, concluido.ContaId, concluido.Alvo));
        Assert.Equal(Mock.Relogio.GetUtcNow().AddYears(5), concluido.GuardarAte);
        Assert.Empty(Mock.Erros);
    }

    [Theory]
    [InlineData("EBFGA", "colegio_salesiano", "INSTITUICAO", "SIGLA_EM_USO")]
    [InlineData("COLSA", "objetivo_unidade1", "INSTITUICAO", "DOMINIO_EM_USO")]
    [InlineData("COLSA", "colegio_salesiano", "STAFF", "DADOS_INVALIDOS")]
    [InlineData("EBF", "colegio_salesiano", "INSTITUICAO", "SIGLA_INVALIDA")]
    public async Task RN_270_recusa_grava_o_alerta_antes_de_sair(string sigla, string dominio, string tipo, string codigo)
    {
        var traceId = TraceId.Novo();

        await Falha(codigo, () => Mock.CriarConta.Executar(Colsa(tipo, sigla: sigla, dominio: dominio), traceId));

        Assert.Equal(new[] { "INICIADO", $"ALERTA {codigo}" }, Mock.Gravacoes);
        Assert.Equal(traceId, Assert.Single(Mock.Auditoria).TraceId);
        var erro = Assert.Single(Mock.Erros);
        Assert.Equal((traceId, AcaoAuditada.CriarConta, codigo, NivelDoErro.Alerta), (erro.TraceId, erro.Acao, erro.Codigo, erro.Nivel));
        Assert.Equal(((string?)null, (string?)null, (string?)null), (erro.ContaId, erro.Quem, erro.Alvo));
        Assert.Equal(Mock.Relogio.GetUtcNow().AddYears(5), erro.GuardarAte);
    }

    [Fact]
    public async Task RN_270_falha_inesperada_grava_o_erro_e_segue()
    {
        var traceId = TraceId.Novo();
        var queda = new InvalidOperationException("o banco caiu no meio da gravação");
        Mock.FalhaAoCriarConta = queda;

        Assert.Same(queda, await Assert.ThrowsAsync<InvalidOperationException>(() => Mock.CriarConta.Executar(Colsa(), traceId)));

        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, Mock.Gravacoes);
        var erro = Assert.Single(Mock.Erros);
        Assert.Equal((traceId, NivelDoErro.Erro), (erro.TraceId, erro.Nivel));
        Assert.Null(Mock.Conta("conta-colsa"));
    }

    [Fact]
    public async Task RN_270_pedido_cancelado_nao_e_erro()
    {
        Mock.FalhaAoCriarConta = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Mock.CriarConta.Executar(Colsa(), TraceId.Novo()));

        Assert.Equal(new[] { "INICIADO" }, Mock.Gravacoes);
        Assert.Empty(Mock.Erros);
    }

    [Fact]
    public async Task RF_104_sigla_ja_usada() =>
        await Falha("SIGLA_EM_USO", () => Mock.CriarConta.Executar(Colsa(sigla: "EBFGA"), TraceId.Novo()));

    [Fact]
    public async Task RF_104_dominio_ja_usado() =>
        await Falha("DOMINIO_EM_USO", () => Mock.CriarConta.Executar(Colsa(dominio: "objetivo_unidade1"), TraceId.Novo()));

    [Fact]
    public async Task RN_164_staff_nao_se_cadastra() =>
        Assert.Equal("tipo", (await Falha("DADOS_INVALIDOS", () => Mock.CriarConta.Executar(Colsa(tipo: "STAFF"), TraceId.Novo()))).Campo);

    [Theory]
    [InlineData("ebfga", "SIGLA_EM_USO")]
    [InlineData("DBOAZ", "SIGLA_INVALIDA")]
    [InlineData("EBF", "SIGLA_INVALIDA")]
    [InlineData("COLSA", null)]
    public async Task RN_179_sigla_recusada(string sigla, string? codigo)
    {
        if (codigo is null) Assert.Equal("conta-colsa", (await Mock.CriarConta.Executar(Colsa(sigla: sigla), TraceId.Novo())).ContaId);
        else await Falha(codigo, () => Mock.CriarConta.Executar(Colsa(sigla: sigla), TraceId.Novo()));
    }

    [Theory]
    [InlineData("prd", "QAEDU", "SIGLA_INVALIDA")]
    [InlineData("prd", "qaedu", "SIGLA_INVALIDA")]
    [InlineData(null, "QAEDU", "SIGLA_INVALIDA")]
    [InlineData("hml", "QAEDU", null)]
    [InlineData("prd", "AQUAS", null)]
    public async Task RN_179_sigla_qa_reservada_em_producao(string? ambiente, string sigla, string? codigo)
    {
        var mock = new UsuariosEmMemoria(ambiente!);
        if (codigo is null)
        {
            Assert.Equal($"conta-{sigla.ToLowerInvariant()}", (await mock.CriarConta.Executar(Colsa(sigla: sigla), TraceId.Novo())).ContaId);
            return;
        }
        var falha = await Assert.ThrowsAsync<FalhaDeUsuarios>(() => mock.CriarConta.Executar(Colsa(sigla: sigla), TraceId.Novo()));
        Assert.Equal((codigo, "Siglas QA são reservadas ao time deboaz.", "sigla"), (falha.Codigo, falha.Message, falha.Campo));
    }

    [Fact]
    public async Task RN_179_sigla_sugerida()
    {
        Assert.Equal(new SiglaSugeridaDto("EBFGA", false), await Mock.SugerirSigla.Executar(new("Escola Bento Freitas Gonçalves Araújo")));
        Assert.Equal(new SiglaSugeridaDto("CSALE", true), await Mock.SugerirSigla.Executar(new("Colégio Salesiano")));
        Assert.Equal(new SiglaSugeridaDto(null, false), await Mock.SugerirSigla.Executar(new("Xuxa")));
    }

    [Theory]
    [InlineData("INSTITUICAO", "12.345.678/0001-90", true)]
    [InlineData("AUTONOMO", "123.456.789-09", true)]
    [InlineData("AUTONOMO", "12.345.678/0001-90", false)]
    public async Task RN_242_documento_de_cada_conta(string tipo, string documento, bool aceita)
    {
        if (aceita) Assert.Equal("conta-colsa", (await Mock.CriarConta.Executar(Colsa(tipo, documento), TraceId.Novo())).ContaId);
        else Assert.Equal("documento", (await Falha("DADOS_INVALIDOS", () => Mock.CriarConta.Executar(Colsa(tipo, documento), TraceId.Novo()))).Campo);
    }

    [Fact]
    public async Task RF_111_conta_master_esqueceu_a_senha()
    {
        await Mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "(51) 99999-0000", "aparelho-1"));
        Assert.Equal(("Escola Bento Freitas", "(51) 99999-0000"), (Mock.PedidosDeAjuda[0].Instituicao, Mock.PedidosDeAjuda[0].Contato));
    }

    [Fact]
    public async Task RNF_205_quarto_envio_no_dia()
    {
        var primeiro = Mock.Relogio.Agora;
        for (var i = 0; i < 3; i++)
        {
            await Mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "(51) 99999-0000", "aparelho-1"));
            Mock.Relogio.Avancar(TimeSpan.FromHours(1));
        }
        var falha = await Falha("LIMITE_ATENDIMENTO", () => Mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "x", "aparelho-1")));
        Assert.Equal(primeiro.AddHours(24), falha.TentarEm);
        await Mock.PedirAjudaDaMaster.Executar(new("Outra", "x", "aparelho-2"));
        Mock.Relogio.Agora = primeiro.AddHours(24);
        await Mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "x", "aparelho-1"));
    }

    [Fact]
    public async Task RF_113_falta_de_pagamento_e_regularizacao()
    {
        Assert.Equal(new ContaComEstadoDto("conta-ebfga", "DESATIVADA"),
            await Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "DESATIVADA")));
        Assert.Contains(("conta-ebfga", MotivoFim.ContaIndisponivel), Mock.ContasSemSessao);

        Assert.Equal("ATIVA", (await Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "ATIVA"))).Estado);
        Assert.Single(Mock.ContasSemSessao);
        Assert.Equal(2, Mock.Auditoria.Count(a => a.Acao == AcaoAuditada.MudarEstadoConta));
    }

    [Theory]
    [InlineData("ATIVA")]
    [InlineData("SUSPENSA")]
    [InlineData("DESATIVADA")]
    [InlineData("ENCERRADA")]
    public async Task RN_173_estado_da_conta(string estado) =>
        Assert.Equal(estado, (await Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-admin", "conta-ebfga", estado))).Estado);

    [Fact]
    public async Task RN_173_conta_encerrada_nao_volta()
    {
        await Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "ENCERRADA"));
        await Falha("TRANSICAO_INVALIDA", () => Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "ATIVA")));
    }

    [Fact]
    public async Task RN_174_conta_encerrada_apaga_os_dados()
    {
        await Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "ENCERRADA"));
        Assert.Equal(["conta-ebfga"], Mock.ContasApagadas);
        Assert.Null(Mock.Usuario("usr-ebfga-maria"));
        Assert.Equal(EstadoConta.Encerrada, Mock.Conta("conta-ebfga")!.Estado);
    }

    [Fact]
    public async Task RN_169_so_o_time_muda_o_estado()
    {
        await Falha("ACESSO_NEGADO", () => Mock.MudarEstadoDaConta.Executar(new("usr-ebfga-master", "conta-ebfga", "SUSPENSA")));
        await Falha("CONTA_INEXISTENTE", () => Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-dboaz", "SUSPENSA")));
        await Falha("DADOS_INVALIDOS", () => Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "PAUSADA")));
    }

    [Fact]
    public async Task RN_187_so_o_cadastro()
    {
        var conta = await Mock.LerConta.Executar(new("usr-dboaz-caio", "conta-ebfga"));
        Assert.Equal(new ContaLidaDto("conta-ebfga", "INSTITUICAO", "Escola Bento Freitas", "12.345.678/0001-90", "EBFGA",
            "objetivo_unidade1@deboaz.com", "Gustavo Boaz de Moura Souza", "(51) 99999-0000", "ATIVA",
            new DateTimeOffset(2026, 3, 12, 13, 5, 0, TimeSpan.Zero), "2026-10-05", new DateTimeOffset(2026, 11, 1, 8, 15, 0, TimeSpan.Zero)), conta);
        Assert.Null((await Mock.LerConta.Executar(new("usr-dboaz-caio", "conta-preis"))).UltimoPagamento);
    }

    [Fact]
    public async Task RN_187_conta_que_nao_existe()
    {
        await Falha("CONTA_INEXISTENTE", () => Mock.LerConta.Executar(new("usr-dboaz-caio", "conta-zzzzz")));
        await Falha("CONTA_INEXISTENTE", () => Mock.LerConta.Executar(new("usr-dboaz-caio", "conta-dboaz")));
        await Falha("ACESSO_NEGADO", () => Mock.LerConta.Executar(new("usr-ebfga-master", "conta-ebfga")));
        await Falha("ACESSO_NEGADO", () => Mock.LerConta.Executar(new("usr-zzzzz", "conta-ebfga")));
    }

    [Theory]
    [InlineData("EBFGA")]
    [InlineData("12.345.678/0001-90")]
    [InlineData("bento")]
    public async Task RN_185_busca_nas_contas(string termo)
    {
        var pagina = await Mock.ListarContas.Executar(new("usr-dboaz-caio", termo));
        var conta = Assert.Single(pagina.Itens);
        Assert.Equal(("Escola Bento Freitas", 1), (conta.Nome, pagina.Total));
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 8, 15, 0, TimeSpan.Zero), conta.UltimoAcessoMaster);
    }

    [Fact]
    public async Task RN_185_contas_por_estado_sem_a_do_time()
    {
        var todas = await Mock.ListarContas.Executar(new("usr-dboaz-admin"));
        Assert.Equal(["Escola Bento Freitas", "Pedro Reis"], todas.Itens.Select(c => c.Nome));
        await Mock.MudarEstadoDaConta.Executar(new("usr-dboaz-admin", "conta-preis", "SUSPENSA"));
        Assert.Equal(["PREIS"], (await Mock.ListarContas.Executar(new("usr-dboaz-admin", Estado: "SUSPENSA"))).Itens.Select(c => c.Sigla));
        await Falha("ACESSO_NEGADO", () => Mock.ListarContas.Executar(new("usr-ebfga-master")));
    }

    static ConferenciaDaMasterDto Conferencia(string? telefone = "(51) 99999-0000", string? pagamento = null) =>
        new("usr-dboaz-analima", "conta-ebfga", "12.345.678/0001-90", "Gustavo Boaz de Moura Souza", telefone, pagamento);

    [Fact]
    public async Task RN_172_conferencia_pelo_SAC()
    {
        var codigo = await Mock.GerarCodigoDaMaster.Executar(Conferencia());
        Assert.Matches(@"^\d{6}$", codigo.CodigoLiberacao);
        Assert.Equal(("(51) 99999-0000", Mock.Relogio.Agora.AddHours(72)), (codigo.Contato, codigo.VenceEm));
        Assert.Null(Mock.Usuario("usr-ebfga-master")!.ResumoSenha);
        Assert.Contains(("usr-ebfga-master", MotivoFim.NovaSenha), Mock.SessoesEncerradas);
        Assert.Equal([("conta-ebfga", "usr-dboaz-analima", true)], Mock.Conferencias);

        var senha = await Mock.GerarSenha.Executar(new("objetivo_unidade1@deboaz.com", codigo.CodigoLiberacao));
        Assert.Matches(@"^Gu[!@#$%&*]Boaz\d{6}$", senha.Senha);
    }

    [Fact]
    public async Task RN_172_conferencia_incompleta()
    {
        await Falha("CONFERENCIA_INCOMPLETA", () => Mock.GerarCodigoDaMaster.Executar(
            new("usr-dboaz-analima", "conta-ebfga", "12.345.678/0001-90", null, "(51) 99999-0000", "2026-09-05")));
        Assert.Equal([("conta-ebfga", "usr-dboaz-analima", false)], Mock.Conferencias);
        Assert.Empty(Mock.Geracoes("usr-ebfga-master"));
        Assert.NotNull((await Mock.GerarCodigoDaMaster.Executar(Conferencia(null, "2026-10-05"))).CodigoLiberacao);
    }

    [Fact]
    public async Task RN_169_time_deboaz_e_a_conta_master()
    {
        Assert.NotNull(await Mock.GerarCodigoDaMaster.Executar(Conferencia() with { QuemPede = "usr-dboaz-caio" }));
        await Falha("ACESSO_NEGADO", () => Mock.GerarCodigoDaMaster.Executar(Conferencia() with { QuemPede = "usr-ebfga-maria" }));
    }
}
