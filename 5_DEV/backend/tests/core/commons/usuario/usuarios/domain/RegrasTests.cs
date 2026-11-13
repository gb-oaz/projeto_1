using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using GrupoDeFalha = Boaz.Core.Commons.Usuario.Usuarios.Domain.GrupoDeFalha;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Domain;

/// <summary>Hierarquia, limite do SAC, tempos, textos, card, geração, conta e falhas (RN-164 a RN-189, RN-243, RNF-205).</summary>
public class RegrasTests
{
    static readonly DateTimeOffset Agora = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    static UsuarioCadastrado Novo(string id, TipoUsuario tipo, string conta = "conta-ebfga", string? criadoPor = null,
        string? resumo = null, DateTimeOffset? ultimoAcesso = null) =>
        new(id, conta, "EBFGA", tipo, "Nome", id, $"{id}@deboaz.com", null, EstadoUsuario.Ativo, resumo, [], criadoPor, Agora,
            ultimoAcesso: ultimoAcesso);

    [Theory]
    [InlineData(TipoUsuario.Master, false, TipoUsuario.Staff, true)]
    [InlineData(TipoUsuario.Master, true, TipoUsuario.Staff, false)]
    [InlineData(TipoUsuario.Staff, false, TipoUsuario.Staff, true)]
    [InlineData(TipoUsuario.Staff, false, TipoUsuario.Participante, true)]
    [InlineData(TipoUsuario.Master, true, TipoUsuario.Participante, true)]
    [InlineData(TipoUsuario.Administrador, false, TipoUsuario.MembroTime, true)]
    [InlineData(TipoUsuario.MembroTime, false, TipoUsuario.MembroTime, false)]
    [InlineData(TipoUsuario.Master, false, TipoUsuario.Master, false)]
    public void RN_164_quem_cria_cada_tipo(TipoUsuario quem, bool autonomo, TipoUsuario tipo, bool pode) =>
        Assert.Equal(pode, Hierarquia.PodeCriar(Novo("q", quem), autonomo, tipo));

    [Fact]
    public void RN_164_membro_lista_os_membros()
    {
        Assert.True(Hierarquia.PodeListar(Novo("caio", TipoUsuario.MembroTime), false, TipoUsuario.MembroTime));
        Assert.False(Hierarquia.PodeListar(Novo("m", TipoUsuario.Master), false, TipoUsuario.MembroTime));
        Assert.True(Hierarquia.PodeListar(Novo("m", TipoUsuario.Master), false, TipoUsuario.Staff));
    }

    [Fact]
    public void RN_169_quem_age_sobre_quem()
    {
        var joao = Novo("joao", TipoUsuario.Staff, criadoPor: "master");
        var ana = Novo("ana", TipoUsuario.Staff, criadoPor: "joao");
        Assert.True(Hierarquia.PodeAdministrar(joao, ana, ["joao", "master"]));
        Assert.False(Hierarquia.PodeAdministrar(ana, joao, ["master"]));
        Assert.False(Hierarquia.PodeAdministrar(joao, joao, ["master"]));
        Assert.False(Hierarquia.PodeAdministrar(Novo("master", TipoUsuario.Master), Novo("x", TipoUsuario.Staff, "conta-preis"), []));
        Assert.True(Hierarquia.PodeAdministrar(Novo("admin", TipoUsuario.Administrador), Novo("caio", TipoUsuario.MembroTime), []));
        Assert.False(Hierarquia.PodeAdministrar(Novo("admin", TipoUsuario.Administrador), Novo("s", TipoUsuario.Staff), []));
        Assert.False(Hierarquia.PodeAdministrar(Novo("p", TipoUsuario.Participante), Novo("s", TipoUsuario.Participante), []));
        Assert.True(TipoUsuario.MembroTime.DoTime());
        Assert.False(TipoUsuario.Master.DoTime());
    }

    [Fact]
    public void RN_169_administrador_sobre_outro_administrador()
    {
        var admin = Novo("admin", TipoUsuario.Administrador);
        var qaadmin = Novo("qaadmin", TipoUsuario.Administrador);
        Assert.False(Hierarquia.PodeAdministrar(qaadmin, admin, []));
        Assert.False(Hierarquia.PodeAdministrar(admin, qaadmin, []));
    }

    [Fact]
    public void RNF_205_tres_pedidos_em_24_h()
    {
        Assert.Null(LimiteDeAtendimento.TentarEm([Agora.AddHours(-2), Agora.AddHours(-1)], Agora));
        Assert.Equal(Agora.AddHours(20), LimiteDeAtendimento.TentarEm([Agora.AddHours(-4), Agora.AddHours(-1), Agora.AddHours(-2)], Agora));
        Assert.Null(LimiteDeAtendimento.TentarEm([Agora.AddHours(-30), Agora.AddHours(-1), Agora.AddHours(-2)], Agora));
    }

    [Fact]
    public void RN_183_e_RN_243_tempos_padrao_e_limites()
    {
        Assert.Equal(TempoDaConta.Padrao, TempoDaConta.Padrao.Conferido());
        Assert.Equal(TempoDoTime.Padrao, TempoDoTime.Padrao.Conferido());
        var falha = Assert.Throws<FalhaDeUsuarios>(() => (TempoDaConta.Padrao with { Participante = TimeSpan.FromDays(91) }).Conferido());
        Assert.Equal(("participante", "P1D", "P90D", "O tempo precisa ficar entre 1 dia e 90 dias."), (falha.Campo, falha.Minimo, falha.Maximo, falha.Message));
        falha = Assert.Throws<FalhaDeUsuarios>(() => (TempoDoTime.Padrao with { MastersApp = TimeSpan.FromMinutes(10) }).Conferido());
        Assert.Equal(("mastersApp", "PT15M", "PT24H", "O tempo precisa ficar entre 15 min e 24 h."), (falha.Campo, falha.Minimo, falha.Maximo, falha.Message));
    }

    [Theory]
    [InlineData("PT8H", "PT8H")]
    [InlineData("P1D", "PT24H")]
    [InlineData("PT15M", "PT15M")]
    [InlineData("PT1H30M", "PT1H30M")]
    public void RN_243_tempo_em_horas(string iso, string emHoras) => Assert.Equal(emHoras, Textos.IsoEmHoras(Textos.IsoDe(iso)!.Value));

    [Fact]
    public void Textos_do_contrato()
    {
        foreach (var tipo in Enum.GetValues<TipoUsuario>()) Assert.Equal(tipo, Textos.TipoDe(tipo.Texto()));
        foreach (var estado in Enum.GetValues<EstadoUsuario>()) Assert.Equal(estado, Textos.EstadoDe(estado.Texto()));
        foreach (var estado in Enum.GetValues<EstadoConta>()) Assert.Equal(estado, Textos.EstadoDaContaDe(estado.Texto()));
        foreach (var tipo in Enum.GetValues<TipoConta>()) Assert.Equal(tipo, Textos.TipoDaContaDe(tipo.Texto()));
        Assert.Equal(["CRIAR_CONTA", "CRIAR_USUARIO", "EDITAR_USUARIO", "NOVA_GERACAO_SENHA", "GERAR_SENHA", "NOVO_CODIGO_ACESSO",
            "DESATIVAR_USUARIO", "REATIVAR_USUARIO", "MUDAR_ESTADO_CONTA", "CODIGO_MASTER", "SALVAR_TEMPO_SESSAO", "EDITAR_PERMISSOES",
            "ENTRAR_HUB", "ENTRAR_QUEST", "ENTRAR_SALA", "CONFIRMAR_SENHA", "RENOVAR_SESSAO", "SAIR", "ENCERRAR_SESSAO", "ENCERRAR_SESSOES",
            "ESQUECI_SENHA_MASTER", "ENCERRAR_SESSOES_DO_USUARIO", "AVISAR_PERMISSOES_NOVAS", "AVISAR_CONTRATACAO_MUDOU",
            // 18 RN-297: as ações do SAC
            "ABRIR_CHAMADO", "ENVIAR_MENSAGEM", "ASSUMIR_CHAMADO", "MUDAR_STATUS", "REABRIR_CHAMADO", "ENCERRAR_CHAMADO", "AVALIAR_CHAMADO",
            "SALVAR_PRAZOS_SAC", "FECHAR_POR_PRAZO"],
            Enum.GetValues<AcaoAuditada>().Select(a => a.Texto()));
        Assert.Null(Textos.TipoDe("CHEFE"));
        Assert.Null(Textos.TipoDaContaDe(null));
        Assert.Null(Textos.IsoDe("8 horas"));
        Assert.Null(Textos.IsoDe(" "));
        Assert.Equal("P7D", Textos.Iso(TimeSpan.FromDays(7)));
    }

    [Fact]
    public void RN_182_geracao_de_senha()
    {
        var maria = Novo("usr-ebfga-maria", TipoUsuario.Staff);
        var (geracao, codigo) = GeracaoDeSenhaPedida.Pedir(maria, "usr-ebfga-master", Agora);
        Assert.Matches("^ger-ebfga-[0-9a-f]{12}$", geracao.Id);
        Assert.True(geracao.Aberta && geracao.Confere($" {codigo} ") && !geracao.Confere(codigo == "000000" ? "000001" : "000000"));
        Assert.False(geracao.Vencida(Agora.AddHours(71)));
        Assert.True(geracao.Vencida(Agora.AddHours(72)));
        geracao.MostrarSenha("resumo", Agora);
        Assert.False(geracao.Aberta);

        var criacao = GeracaoDeSenhaPedida.MostradaNaCriacao(maria, "resumo", Agora);
        Assert.False(criacao.Aberta || criacao.Confere(codigo));
    }

    [Fact]
    public void RF_112_reativar_volta_pendente_ou_ativo()
    {
        var pendente = Novo("bia", TipoUsuario.Staff);
        pendente.Desativar();
        pendente.Reativar();
        Assert.Equal(EstadoUsuario.Pendente, pendente.Estado);

        var jaEntrou = Novo("rui", TipoUsuario.Staff, ultimoAcesso: Agora);
        jaEntrou.Desativar();
        jaEntrou.Reativar();
        Assert.Equal(EstadoUsuario.Ativo, jaEntrou.Estado);

        var participante = Novo("lia", TipoUsuario.Participante);
        participante.Desativar();
        participante.Reativar();
        Assert.Equal(EstadoUsuario.Ativo, participante.Estado);
        Assert.Equal("O usuário já está ativo.", Assert.Throws<FalhaDeUsuarios>(participante.Reativar).Message);
    }

    [Fact]
    public void RN_173_estado_da_conta()
    {
        var conta = new ContaAssinante("conta-ebfga", "EBFGA", "d", "Escola", TipoConta.Instituicao, EstadoConta.Ativa, "1", "R", "T", Agora);
        Assert.False(conta.MudarEstado(EstadoConta.Ativa));
        Assert.True(conta.MudarEstado(EstadoConta.Encerrada));
        Assert.Equal("Conta encerrada não volta.", Assert.Throws<FalhaDeUsuarios>(() => conta.MudarEstado(EstadoConta.Ativa)).Message);
        Assert.Equal("conta-ebfga", ContaAssinante.IdDa("EBFGA"));
        Assert.False(conta.DeAutonomo);
    }

    [Theory]
    [InlineData("12.345.678/0001-90", "gustavo boaz de moura souza", "51999990000", null, true)]
    [InlineData("12345678000190", null, "(51) 99999-0000", "2026-10-05", true)]
    [InlineData("12.345.678/0001-90", null, "(51) 99999-0000", "2026-09-05", false)]
    [InlineData("12.345.678/0001-91", "Gustavo Boaz de Moura Souza", "(51) 99999-0000", "2026-10-05", false)]
    [InlineData("", "Gustavo Boaz de Moura Souza", "(51) 99999-0000", "2026-10-05", false)]
    [InlineData("12.345.678/0001-90", "Gustavo Boaz de Moura Souza", "", null, false)]
    public void RN_172_documento_e_2_de_3(string documento, string? responsavel, string? telefone, string? pagamento, bool confere)
    {
        var conta = new ContaAssinante("conta-ebfga", "EBFGA", "d", "Escola", TipoConta.Instituicao, EstadoConta.Ativa,
            "12.345.678/0001-90", "Gustavo Boaz de Moura Souza", "(51) 99999-0000", Agora);
        var conferencia = new ConferenciaDaMaster(documento, responsavel, telefone, pagamento is null ? null : DateOnly.Parse(pagamento));
        Assert.Equal(confere, conferencia.Confere(conta, new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public void RN_178_auditoria_por_5_anos() =>
        Assert.Equal(new DateTimeOffset(2031, 3, 20, 15, 0, 0, TimeSpan.Zero),
            new RegistroDeAuditoria(new TraceId("trc-1b8e4d02"), EtapaDaExecucao.Concluido, "conta-ebfga", "usr-ebfga-maria",
                AcaoAuditada.CriarUsuario, "usr-ebfga-lucas", new DateTimeOffset(2026, 3, 20, 15, 0, 0, TimeSpan.Zero)).GuardarAte);

    [Fact]
    public void RN_270_erro_guardado_por_5_anos_como_a_auditoria() =>
        Assert.Equal(new DateTimeOffset(2031, 11, 8, 1, 10, 0, TimeSpan.Zero),
            new RegistroDeErro(new TraceId("trc-7f3a9c21"), null, null, AcaoAuditada.CriarConta, null, "SIGLA_EM_USO", NivelDoErro.Alerta,
                new DateTimeOffset(2026, 11, 8, 1, 10, 0, TimeSpan.Zero)).GuardarAte);

    [Fact]
    public void RN_271_traceId_novo_a_cada_execucao()
    {
        var (um, outro) = (TraceId.Novo(), TraceId.Novo());
        Assert.Matches("^trc-[0-9a-f]{8}$", um.Valor);
        Assert.Equal(um.Valor, um.ToString());
        Assert.NotEqual(um, outro);
    }

    [Fact]
    public void RN_178_RN_270_textos_da_etapa_e_do_nivel() =>
        Assert.Equal(new[] { "INICIADO", "CONCLUIDO", "ALERTA", "ERRO" },
            new[] { EtapaDaExecucao.Iniciado.Texto(), EtapaDaExecucao.Concluido.Texto(), NivelDoErro.Alerta.Texto(), NivelDoErro.Erro.Texto() });

    [Fact]
    public void Falhas_com_codigo_grupo_e_mensagem()
    {
        var todas = new[]
        {
            (FalhaDeUsuarios.AcessoNegado(), "ACESSO_NEGADO"), (FalhaDeUsuarios.UsuarioInexistente(), "USUARIO_INEXISTENTE"),
            (FalhaDeUsuarios.ContaInexistente(), "CONTA_INEXISTENTE"), (FalhaDeUsuarios.SiglaEmUso(), "SIGLA_EM_USO"),
            (FalhaDeUsuarios.DominioEmUso(), "DOMINIO_EM_USO"), (FalhaDeUsuarios.NomeCurtoEmUso(), "NOME_CURTO_EM_USO"),
            (FalhaDeUsuarios.MatriculaEmUso(), "MATRICULA_EM_USO"), (FalhaDeUsuarios.CodigoInvalido(), "CODIGO_INVALIDO"),
            (FalhaDeUsuarios.CodigoVencido(), "CODIGO_VENCIDO"), (FalhaDeUsuarios.EntradaBloqueada(Agora), "ENTRADA_BLOQUEADA"),
            (FalhaDeUsuarios.LimiteAtendimento(Agora), "LIMITE_ATENDIMENTO"), (FalhaDeUsuarios.SemSenha(), "SEM_SENHA"),
            (FalhaDeUsuarios.SoParticipante(), "SO_PARTICIPANTE"), (FalhaDeUsuarios.EstadoIgual(true), "ESTADO_IGUAL"),
            (FalhaDeUsuarios.TransicaoInvalida(), "TRANSICAO_INVALIDA"), (FalhaDeUsuarios.ConferenciaIncompleta(), "CONFERENCIA_INCOMPLETA"),
            (FalhaDeUsuarios.DadosInvalidos("nome"), "DADOS_INVALIDOS"),
        };
        Assert.All(todas, f => Assert.Equal(f.Item2, f.Item1.Codigo));
        Assert.All(todas, f => Assert.False(string.IsNullOrWhiteSpace(f.Item1.Message)));
        Assert.Equal(GrupoDeFalha.Bloqueado, FalhaDeUsuarios.EntradaBloqueada(Agora).Grupo);
        Assert.Equal(Agora, FalhaDeUsuarios.LimiteAtendimento(Agora).TentarEm);
        Assert.Equal("O usuário já está desativado.", FalhaDeUsuarios.EstadoIgual(true).Message);
        Assert.Equal("O campo \"nome\" está faltando ou é inválido.", FalhaDeUsuarios.DadosInvalidos("nome").Message);
    }
}
