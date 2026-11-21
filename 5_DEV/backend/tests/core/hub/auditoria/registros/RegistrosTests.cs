using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;

namespace Boaz.Core.Hub.Tests.Auditoria.Registros;

/// <summary>RF-199, RF-200, RN-319, RN-321, RN-325: o catálogo das ações, o período, o filtro, o cursor e o alvo da consulta.</summary>
public sealed class RegistrosTests
{
    static readonly DateTimeOffset Agora = new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero);

    [Fact]
    public void RN_319_catalogo_das_41_acoes_com_nome_modulo_e_sem_quem()
    {
        Assert.Equal(Enum.GetValues<AcaoAuditada>().Select(a => a.Texto()), CatalogoDeAcoes.Todas.Select(a => a.Acao));
        Assert.Equal(41, CatalogoDeAcoes.Todas.Count);
        Assert.Equal(("Criar usuário", "MOD-03", "SEM_LOGIN"), (CatalogoDeAcoes.Nome("CRIAR_USUARIO"), CatalogoDeAcoes.Modulo("CRIAR_USUARIO"),
            CatalogoDeAcoes.Todas.Single(a => a.Acao == "CRIAR_USUARIO").SemQuemTexto));
        Assert.Equal(("MOD-02", "SISTEMA"), (CatalogoDeAcoes.Modulo("FECHAR_POR_PRAZO"), CatalogoDeAcoes.Todas.Single(a => a.Acao == "FECHAR_POR_PRAZO").SemQuemTexto));
        Assert.Equal("BANCO", CatalogoDeAcoes.Todas.Single(a => a.Acao == "AVISO_DO_BANCO").SemQuemTexto);
        Assert.Equal(("Consultar a Auditoria", "MOD-17"), (CatalogoDeAcoes.Nome("CONSULTAR_AUDITORIA"), CatalogoDeAcoes.Modulo("CONSULTAR_AUDITORIA")));
        Assert.Equal(["MOD-01", "MOD-02", "MOD-03", "MOD-17"], CatalogoDeAcoes.Todas.Select(a => a.Modulo!).Distinct().Order());
        Assert.Equal(9, CatalogoDeAcoes.DoModulo("MOD-02").Count);
    }

    [Fact]
    public void RN_319_acao_fora_do_catalogo_vai_pelo_codigo_sem_modulo()
    {
        Assert.Equal("PUBLICAR_AGENDA", CatalogoDeAcoes.Nome("PUBLICAR_AGENDA"));
        Assert.Null(CatalogoDeAcoes.Modulo("PUBLICAR_AGENDA"));
        Assert.False(CatalogoDeAcoes.Existe("PUBLICAR_AGENDA"));
        Assert.False(CatalogoDeAcoes.Existe(null));
    }

    [Theory]
    [InlineData(null, null, "2026-11-19T13:42:00Z", "2026-11-20T13:42:00Z")]
    [InlineData("2026-11-20T10:00:00Z", null, "2026-11-20T10:00:00Z", "2026-11-20T13:42:00Z")]
    [InlineData(null, "2026-11-19T10:00:00Z", "2026-11-18T10:00:00Z", "2026-11-19T10:00:00Z")]
    [InlineData("2026-10-20T13:42:00Z", "2026-11-20T13:42:00Z", "2026-10-20T13:42:00Z", "2026-11-20T13:42:00Z")]
    public void RN_321_periodo_que_falta_e_o_de_31_dias(string? de, string? ate, string esperadoDe, string esperadoAte)
    {
        var p = Periodo.Criar(Periodo.Ler(de, "de"), Periodo.Ler(ate, "ate"), Periodo.MaximoDeDias, Agora);
        Assert.Equal((DateTimeOffset.Parse(esperadoDe), DateTimeOffset.Parse(esperadoAte)), (p.De, p.Ate));
    }

    [Theory]
    [InlineData("2026-10-20T13:41:59Z", 31, "PERIODO_LONGO", "Escolha um período de até 31 dias.")]
    [InlineData("2026-11-13T13:41:59Z", 7, "PERIODO_LONGO", "Escolha um período de até 7 dias.")]
    public void RN_321_periodo_longo(string de, int maximo, string codigo, string mensagem)
    {
        var falha = Assert.Throws<FalhaDaAuditoria>(() => Periodo.Criar(Periodo.Ler(de, "de"), Agora, maximo, Agora));
        Assert.Equal((codigo, mensagem, "ate", GrupoDaFalhaDaAuditoria.DadosInvalidos), (falha.Codigo, falha.Message, falha.Campo, falha.Grupo));
    }

    [Fact]
    public void RN_321_periodo_invertido_e_data_invalida()
    {
        var invertido = Assert.Throws<FalhaDaAuditoria>(() => Periodo.Criar(Agora, Agora.AddDays(-1), 31, Agora));
        Assert.Equal(("PERIODO_INVERTIDO", "O fim deve ser depois do começo.", "ate"), (invertido.Codigo, invertido.Message, invertido.Campo));
        Assert.Equal("de", Assert.Throws<FalhaDaAuditoria>(() => Periodo.Ler("ontem", "de")).Campo);
        Assert.Null(Periodo.Ler(null, "de"));
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 13, 0, 0, TimeSpan.Zero), Periodo.Ler("2026-11-20T10:00:00-03:00", "de"));
    }

    [Fact]
    public void RF_200_filtro_valida_cada_campo_e_nomeia_o_invalido()
    {
        foreach (var (campo, ler) in new (string, Func<FiltroDosRegistros>)[]
        {
            ("tipo", () => FiltroDosRegistros.Ler("LOG", null, null, null, null, null, null, null)),
            ("nivel", () => FiltroDosRegistros.Ler(null, "GRAVE", null, null, null, null, null, null)),
            ("modulo", () => FiltroDosRegistros.Ler(null, null, "MOD-99", null, null, null, null, null)),
            ("acao", () => FiltroDosRegistros.Ler(null, null, null, "VOAR", null, null, null, null)),
            ("traceId", () => FiltroDosRegistros.Ler(null, null, null, null, null, null, null, "abc")),
        })
        {
            var falha = Assert.Throws<FalhaDaAuditoria>(() => ler());
            Assert.Equal(("DADOS_INVALIDOS", campo, "O campo \"" + campo + "\" está faltando ou é inválido."), (falha.Codigo, falha.Campo, falha.Message));
        }
        Assert.Equal(TipoDoRegistro.Erro, FiltroDosRegistros.Ler("ERRO", "ALERTA", null, null, null, null, null, null).Tipo);
    }

    [Fact]
    public void RF_200_acoes_que_o_filtro_de_modulo_e_de_acao_deixam_passar()
    {
        Assert.Null(FiltroDosRegistros.Nenhum.Acoes());
        Assert.Equal(["CRIAR_USUARIO"], FiltroDosRegistros.Ler(null, null, null, "CRIAR_USUARIO", null, null, null, null).Acoes());
        Assert.Equal(9, FiltroDosRegistros.Ler(null, null, "MOD-02", null, null, null, null, null).Acoes()!.Count);
        Assert.Equal(["ABRIR_CHAMADO"], FiltroDosRegistros.Ler(null, null, "MOD-02", "ABRIR_CHAMADO", null, null, null, null).Acoes());
        Assert.Empty(FiltroDosRegistros.Ler(null, null, "MOD-02", "CRIAR_USUARIO", null, null, null, null).Acoes()!);
    }

    static RegistroLido Registro(TipoDoRegistro tipo, string acao, string? nivel = null, string? codigo = null, string? conta = "EBFGA", string quem = "usr-ebfga-maria") =>
        new("id", tipo, "trc-5e2a9d14", tipo == TipoDoRegistro.Auditoria ? "CONCLUIDO" : null, nivel, codigo, acao, CatalogoDeAcoes.Nome(acao), CatalogoDeAcoes.Modulo(acao),
            new PessoaLida(quem, "Maria Souza"), conta is null ? null : new ContaLida("conta-ebfga", conta), "alvo", Agora);

    [Fact]
    public void RF_201_aceita_um_registro_de_cada_vez_para_o_ao_vivo()
    {
        var erro = Registro(TipoDoRegistro.Erro, "CRIAR_USUARIO", "ALERTA", "MATRICULA_EM_USO");
        var auditoria = Registro(TipoDoRegistro.Auditoria, "PAGAMENTO_CRIADO");
        Assert.True(FiltroDosRegistros.Nenhum.Aceita(erro));
        Assert.True(FiltroDosRegistros.Ler("ERRO", "ALERTA", null, null, "ebfga", null, "MATRICULA_EM_USO", null).Aceita(erro));
        Assert.False(FiltroDosRegistros.Ler("AUDITORIA", null, null, null, null, null, null, null).Aceita(erro));
        Assert.False(FiltroDosRegistros.Ler(null, "ERRO", null, null, null, null, null, null).Aceita(erro));
        Assert.False(FiltroDosRegistros.Ler(null, null, null, null, null, null, "OUTRO", null).Aceita(erro));
        Assert.True(FiltroDosRegistros.Ler(null, null, "MOD-01", null, null, "usr-ebfga-maria", null, null).Aceita(auditoria));
        Assert.False(FiltroDosRegistros.Ler(null, null, "MOD-02", null, null, null, null, null).Aceita(auditoria));
        Assert.False(FiltroDosRegistros.Ler(null, null, null, null, "ZZZZZ", null, null, null).Aceita(auditoria));
        Assert.False(FiltroDosRegistros.Ler(null, null, null, null, null, "usr-outro", null, null).Aceita(auditoria));
        Assert.False(FiltroDosRegistros.Ler(null, null, null, "CRIAR_USUARIO", null, null, null, null).Aceita(auditoria));
        Assert.False(FiltroDosRegistros.Ler(null, null, null, null, null, null, null, "trc-00000000").Aceita(auditoria));
        Assert.Equal("AUDITORIA", auditoria.TipoTexto);
        Assert.Equal("2026-11-20T13:42:00.000Z", auditoria.QuandoTexto);
    }

    [Fact]
    public void RF_199_cursor_ida_e_volta_e_recusa_do_que_nao_e_base64()
    {
        var cursor = new Cursor(new DateTimeOffset(2026, 11, 20, 13, 41, 7, 311, TimeSpan.Zero), "9c1f");
        Assert.Equal(cursor, Cursor.Ler(cursor.Texto));
        Assert.Equal("MjAyNi0xMS0yMFQxMzo0MTowNy4zMTFa", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("2026-11-20T13:41:07.311Z")));
        Assert.Equal(cursor.Quando, Cursor.Ler("MjAyNi0xMS0yMFQxMzo0MTowNy4zMTFa")!.Value.Quando);
        Assert.Null(Cursor.Ler(null));
        Assert.Null(Cursor.Ler("MjAy"));
        Assert.Equal("depois", Assert.Throws<FalhaDaAuditoria>(() => Cursor.Ler("abc")).Campo);
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData("1", 1)]
    [InlineData("200", 200)]
    public void RF_199_limite_da_pagina(string? texto, int esperado) => Assert.Equal(esperado, Cursor.LerLimite(texto));

    [Theory]
    [InlineData("0")]
    [InlineData("201")]
    [InlineData("muitos")]
    public void RF_199_limite_fora_de_1_a_200_e_invalido(string texto) =>
        Assert.Equal("limite", Assert.Throws<FalhaDaAuditoria>(() => Cursor.LerLimite(texto)).Campo);

    [Fact]
    public void RN_325_alvo_da_consulta_com_os_parametros_em_ordem_sem_depois_nem_limite()
    {
        var consulta = ConsultaDaAuditoria.De("usr-dboaz-caio", "/auditoria/registros",
            [new("nivel", "ERRO"), new("conta", "ebfga"), new("depois", "MjAy"), new("limite", "100")]);
        Assert.Equal(("usr-dboaz-caio", "/auditoria/registros?conta=ebfga&nivel=ERRO"), (consulta.Quem, consulta.Alvo));
        Assert.Equal("/auditoria/saude", ConsultaDaAuditoria.De("u", "/auditoria/saude", []).Alvo);
        Assert.Equal("/auditoria/registros", ConsultaDaAuditoria.De("u", "/auditoria/registros", [new("limite", "10")]).Alvo);
        Assert.Equal(TimeSpan.FromMinutes(5), ConsultaDaAuditoria.Janela);
    }

    [Fact]
    public void Formato_comum_das_recusas_da_auditoria()
    {
        Assert.Equal(("ACESSO_NEGADO", "Você não tem permissão para esta ação.", GrupoDaFalhaDaAuditoria.Proibido),
            (FalhaDaAuditoria.AcessoNegado().Codigo, FalhaDaAuditoria.AcessoNegado().Message, FalhaDaAuditoria.AcessoNegado().Grupo));
        Assert.Equal(("EXECUCAO_INEXISTENTE", "Execução não encontrada."), (FalhaDaAuditoria.ExecucaoInexistente().Codigo, FalhaDaAuditoria.ExecucaoInexistente().Message));
        Assert.Equal(("BANCO_DE_DADOS_FORA", "Os registros dependem do banco de dados, fora do ar.", GrupoDaFalhaDaAuditoria.Indisponivel),
            (FalhaDaAuditoria.BancoDeDadosFora().Codigo, FalhaDaAuditoria.BancoDeDadosFora().Message, FalhaDaAuditoria.BancoDeDadosFora().Grupo));
    }
}
