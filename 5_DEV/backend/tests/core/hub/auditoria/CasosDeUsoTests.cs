using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.UseCases;
using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Execucoes.Dtos;
using Boaz.Core.Hub.Auditoria.Execucoes.UseCases;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;
using Boaz.Core.Hub.Auditoria.Metricas.UseCases;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Ocorrencias.UseCases;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.UseCases;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.UseCases;
using Boaz.Core.Hub.Tests.Auditoria.Mocks;

namespace Boaz.Core.Hub.Tests.Auditoria;

/// <summary>Os casos de uso da Auditoria com as portas de saída em memória (mocks/).</summary>
public sealed class CasosDeUsoTests
{
    static readonly DateTimeOffset T = new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero);

    readonly RelogioFalso relogio = new(T);
    readonly UltimaVerificacaoFalsa ultima = new();
    readonly InstanciasFalsas instancias = new();
    readonly TravaFalsa trava = new();
    readonly HistoricoFalso historico = new();
    readonly MedicoesFalsas medicoes = new();
    readonly OcorrenciasFalsas ocorrencias = new();
    readonly AuditoriaFalsa auditoria = new();
    readonly ConsultasRecentesFalsas recentes = new();
    readonly LeituraFalsa leitura = new();
    readonly NomesFalsos nomes = new();
    readonly CardFalso card = new();
    readonly ConexoesFalsas conexoes = new();
    readonly AcessoFalso acesso = new();
    readonly Dictionary<Servico, SondaFalsa> sondas = Servico.Todos.ToDictionary(s => s, s => new SondaFalsa(s));
    readonly IdentidadeDaInstancia eu = new("ms-mono--r42-7c9f8", "1.4.2", T.AddDays(-2), "HOMOLOGACAO");

    static readonly QuemPede Caio = new("usr-dboaz-caio", "sid-1", "MEMBRO_TIME");

    VerificarServicos Verificador() =>
        new(sondas.Values, ultima, historico, instancias, trava, medicoes, ocorrencias, ocorrencias, ocorrencias, eu, relogio);

    RegistrarConsulta Registrador() => new(auditoria, recentes, ultima, relogio);

    readonly AcumuladorDeMedicoes acumulador = new();

    GravarMedicoes Gravador() => new(acumulador, medicoes, relogio);

    static ConsultaPedida Consulta(string alvo = "/auditoria/registros", string trace = "trc-7a2c5e90") => new(Caio, alvo, new TraceId(trace));

    SondaFalsa Cache => sondas[Servico.Cache];

    // ── verificação (RNF-246, RN-315, RN-324) ──────────────────────────────

    [Fact]
    public async Task RNF_246_verifica_os_8_servicos_anuncia_a_instancia_e_guarda_o_historico()
    {
        var saude = await Verificador().Executar();
        Assert.Equal(8, saude.Servicos.Count);
        Assert.Equal((EstadoDoServico.NoAr, "HOMOLOGACAO", T), (saude.Geral, saude.Ambiente, saude.VerificadoEm));
        Assert.Equal(T, ultima.VerificadoEm);
        Assert.Equal(8, historico.Verificacoes.Count);
        Assert.Equal("ms-mono--r42-7c9f8", Assert.Single(instancias.Anunciadas).Id);
        Assert.Equal("ms-mono--r42-7c9f8", Assert.Single(saude.Instancias).Id);
        Assert.Equal(5, saude.Modulos.Count);
        Assert.All(saude.Modulos, m => Assert.Equal(EstadoDoModulo.SemPedidos, m.Estado));
    }

    [Fact]
    public async Task RNF_246_servico_que_demora_mais_de_3_s_fica_sem_resposta()
    {
        Cache.Demora = TimeSpan.FromSeconds(30);
        var inicio = DateTime.UtcNow;
        var saude = await Verificador().Executar();
        Assert.True(DateTime.UtcNow - inicio < TimeSpan.FromSeconds(6));
        var v = saude.Servicos.Single(s => s.Servico == Servico.Cache);
        Assert.Equal((EstadoDoServico.ForaDoAr, FalhaDaVerificacao.SemResposta), (v.Estado, v.Falha));
    }

    [Fact]
    public async Task RNF_246_sonda_que_lanca_vira_resposta_com_erro()
    {
        var verificador = new VerificarServicos([new SondaQueLanca()], ultima, historico, instancias, trava, medicoes, ocorrencias, ocorrencias, ocorrencias, eu, relogio);
        var saude = await verificador.Executar();
        Assert.Equal(FalhaDaVerificacao.RespostaComErro, saude.Servicos.Single(s => s.Servico == Servico.Banco).Falha);
        // as sondas que faltam ficam sem resposta
        Assert.Equal(FalhaDaVerificacao.SemResposta, saude.Servicos.First(s => s.Servico == Servico.Cache).Falha);
    }

    sealed class SondaQueLanca : Boaz.Core.Hub.Auditoria.Saude.Ports.Out.ISondaDeServico
    {
        public Servico Servico => Servico.Banco;

        public Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task RN_324_mudanca_de_estado_abre_grava_e_avisa_uma_ocorrencia_so()
    {
        Cache.Resposta = ResultadoDaSonda.Respondeu(2000);
        await Verificador().Executar();
        var o = Assert.Single(ocorrencias.Gravadas);
        Assert.Equal(("cache", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.Lento, T), (o.Alvo, o.De, o.Para, o.Quando));
        Assert.Single(ocorrencias.Publicadas);
        Assert.Contains("cache", ocorrencias.EmAberto.Keys);

        relogio.Agora = T.AddSeconds(30);
        await Verificador().Executar();
        Assert.Single(ocorrencias.Gravadas);
    }

    [Fact]
    public async Task RF_195_queda_abre_na_primeira_falha_e_fecha_na_volta_com_a_ocorrencia_de_volta()
    {
        var verificador = Verificador();
        Cache.Resposta = ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        await verificador.Executar();
        var queda = Assert.Single(historico.Quedas_);
        Assert.Equal((Servico.Cache, T, null, FalhaDaVerificacao.SemResposta), (queda.Servico, queda.Inicio, queda.Fim, queda.Falha));

        relogio.Agora = T.AddMinutes(10);
        Cache.Resposta = ResultadoDaSonda.Respondeu(10);
        await verificador.Executar();
        Assert.Equal(T.AddMinutes(10), historico.Quedas_[0].Fim);
        var volta = ocorrencias.Gravadas[^1];
        Assert.Equal((EstadoDeOcorrencia.ForaDoAr, EstadoDeOcorrencia.NoAr, 10), (volta.De, volta.Para, volta.DuracaoMin));
        Assert.Empty(ocorrencias.EmAberto);
        Assert.Single(ocorrencias.Voltas);
    }

    [Fact]
    public async Task RNF_246_so_a_instancia_com_a_trava_guarda_no_banco()
    {
        trava.Livre = false;
        Cache.Resposta = ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        var saude = await Verificador().Executar();
        Assert.Equal(EstadoDoServico.ForaDoAr, saude.Geral);
        Assert.Equal(T, ultima.VerificadoEm);
        Assert.Empty(historico.Verificacoes);
        Assert.Empty(ocorrencias.Gravadas);
        // o gancho de teste força
        await Verificador().Executar(forcar: true);
        Assert.Equal(8, historico.Verificacoes.Count);
    }

    [Fact]
    public async Task RN_324_cache_limpo_com_o_estado_de_antes_na_memoria_parte_de_no_ar()
    {
        var verificador = Verificador();
        Cache.Resposta = ResultadoDaSonda.Falhou(FalhaDaVerificacao.RespostaComErro);
        await verificador.Executar();
        ocorrencias.EmAberto.Clear();
        ocorrencias.Gravadas.Clear();
        // o Redis foi limpo: o serviço que volta a responder não ganha "volta" de uma ocorrência que não existe mais
        Cache.Resposta = ResultadoDaSonda.Respondeu(10);
        await verificador.Executar();
        Assert.Empty(ocorrencias.Gravadas);
    }

    [Fact]
    public async Task RN_316_modulo_com_5xx_acima_de_1_por_cento_fica_lento_e_vira_ocorrencia_de_modulo()
    {
        var tempos = new long[FaixasDeTempo.Quantas];
        tempos[(int)FaixaDeTempo.Ate500] = 250;
        medicoes.DosModulos["MOD-01"] = new ResumoDeMedicoes(250, 0, 10, new Histograma(tempos));
        var saude = await Verificador().Executar();
        var pagamento = saude.Modulos.Single(m => m.Modulo == "MOD-01");
        Assert.Equal((EstadoDoModulo.Lento, 4.0m, 500), (pagamento.Estado, pagamento.TaxaErro5xx, pagamento.P95Ms));
        var o = Assert.Single(ocorrencias.Gravadas);
        Assert.Equal((TipoDoAlvo.Modulo, "MOD-01", "Pagamento"), (o.Tipo, o.Alvo, o.Nome));
    }

    [Fact]
    public async Task RNF_252_sem_o_banco_os_modulos_ficam_como_estavam_e_a_saude_responde()
    {
        await Verificador().Executar();
        medicoes.Falha = true;
        relogio.Agora = T.AddSeconds(30);
        var saude = await Verificador().Executar();
        Assert.Equal(5, saude.Modulos.Count);
    }

    [Fact]
    public async Task RNF_252_cache_fora_deixa_so_esta_instancia_e_nao_repete_ocorrencia()
    {
        instancias.Falha = true;
        Cache.Resposta = ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        var saude = await Verificador().Executar();
        Assert.Equal("ms-mono--r42-7c9f8", Assert.Single(saude.Instancias).Id);
    }

    // ── saúde (RF-194, RF-195) ─────────────────────────────────────────────

    [Fact]
    public async Task RF_194_ver_saude_verifica_na_primeira_vez_e_grava_a_consulta()
    {
        var ver = new VerSaude(new LeitorDaSaude(ultima, Verificador(), instancias, eu), Registrador());
        var dto = await ver.Executar(Consulta("/auditoria/saude"));
        Assert.Equal(("NO_AR", "HOMOLOGACAO", 0, 0), (dto.Geral, dto.Ambiente, dto.ForaDoAr, dto.Lentos));
        Assert.Equal(8, dto.Servicos.Count);
        Assert.Equal(["CONCLUIDO", "INICIADO"], auditoria.Registros.Select(r => r.Etapa.Texto()).Order());
        Assert.All(auditoria.Registros, r => Assert.Equal((AcaoAuditada.ConsultarAuditoria, "/auditoria/saude", "usr-dboaz-caio", (string?)null),
            (r.Acao, r.Alvo, r.Quem, r.ContaId)));
    }

    [Fact]
    public async Task RF_194_servicos_na_ordem_fora_lento_sem_verificacao_no_ar_desligado()
    {
        sondas[Servico.Banco].Resposta = ResultadoDaSonda.Respondeu(10);
        Cache.Resposta = ResultadoDaSonda.Respondeu(2000);
        sondas[Servico.IntermediadorPagamento].Resposta = ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        sondas[Servico.AgendamentoSac].Resposta = new ResultadoDaSonda();
        sondas[Servico.ArmazenamentoAnexos].Resposta = new ResultadoDaSonda(Desligado: true);
        var dto = await new VerSaude(new LeitorDaSaude(ultima, Verificador(), instancias, eu), Registrador()).Executar(Consulta());
        Assert.Equal(["FORA_DO_AR", "LENTO", "SEM_VERIFICACAO", "SEM_VERIFICACAO"], dto.Servicos.Take(4).Select(s => s.Estado));
        Assert.Equal("DESLIGADO", dto.Servicos[^1].Estado);
        Assert.Equal(("FORA_DO_AR", 1, 1), (dto.Geral, dto.ForaDoAr, dto.Lentos));
    }

    [Fact]
    public async Task RF_195_historico_do_servico_com_quedas_e_serie()
    {
        historico.Quedas_.Add(new Queda("q1", Servico.IntermediadorPagamento, T.AddMinutes(-12), null, FalhaDaVerificacao.SemResposta));
        var ver = new VerHistoricoDoServico(historico, ultima, Registrador(), relogio);
        var dto = await ver.Executar("intermediador-pagamento", null, Consulta("/auditoria/saude/intermediador-pagamento/historico"));
        Assert.Equal(("24h", "Intermediador de pagamento", 24), (dto.Periodo, dto.Nome, dto.Serie.Count));
        var queda = Assert.Single(dto.Quedas);
        Assert.Equal((null, 12, "SEM_RESPOSTA"), (queda.Fim, queda.DuracaoMin, queda.Falha));
        Assert.Equal(99.17m, dto.Disponibilidade);
        Assert.Equal(T, dto.Ate);
        Assert.Equal(7, (await ver.Executar("banco", "7d", Consulta("/auditoria/saude/banco/historico?periodo=7d"))).Serie.Count);
        Assert.Equal(30, (await ver.Executar("banco", "30d", Consulta("/auditoria/saude/banco/historico?periodo=30d"))).Serie.Count);
    }

    [Theory]
    [InlineData("fila", "24h", "SERVICO_INEXISTENTE")]
    [InlineData("banco", "2h", "DADOS_INVALIDOS")]
    public async Task RF_195_servico_ou_periodo_invalidos_nao_gravam_consulta(string servico, string periodo, string codigo)
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() =>
            new VerHistoricoDoServico(historico, ultima, Registrador(), relogio).Executar(servico, periodo, Consulta()));
        Assert.Equal(codigo, falha.Codigo);
        Assert.Empty(auditoria.Registros);
    }

    // ── consulta auditada (RN-325) ─────────────────────────────────────────

    [Fact]
    public async Task RN_325_consulta_grava_iniciado_e_concluido_e_a_repetida_em_5_min_nao()
    {
        var registrador = Registrador();
        Assert.Equal(ResultadoDaConsulta.Gravada, await registrador.Executar(Consulta()));
        Assert.Equal(2, auditoria.Registros.Count);
        Assert.True(auditoria.Registros[1].Quando > auditoria.Registros[0].Quando);
        Assert.Equal([EtapaDaExecucao.Iniciado, EtapaDaExecucao.Concluido], auditoria.Registros.Select(r => r.Etapa));
        Assert.All(auditoria.Registros, r => Assert.Equal("trc-7a2c5e90", r.TraceId.Valor));

        relogio.Agora = T.AddMinutes(4).AddSeconds(59);
        Assert.Equal(ResultadoDaConsulta.Repetida, await registrador.Executar(Consulta()));
        relogio.Agora = T.AddMinutes(5);
        Assert.Equal(ResultadoDaConsulta.Gravada, await registrador.Executar(Consulta()));
        Assert.Equal(4, auditoria.Registros.Count);
    }

    [Fact]
    public async Task RN_325_sem_o_banco_ou_com_a_gravacao_falhando_a_consulta_fica_sem_registro()
    {
        ultima.Guardar(T, [new Verificacao(Servico.Banco, T, "i", EstadoDoServico.ForaDoAr, null, FalhaDaVerificacao.SemResposta, null, 1, T)], []);
        Assert.Equal(ResultadoDaConsulta.SemRegistro, await Registrador().Executar(Consulta()));
        Assert.Empty(auditoria.Registros);

        ultima.Guardar(T, [], []);
        auditoria.Falha = true;
        Assert.Equal(ResultadoDaConsulta.SemRegistro, await Registrador().Executar(Consulta("/auditoria/outra")));
    }

    // ── registros (RF-199, RF-200) ─────────────────────────────────────────

    static RegistroBruto Bruto(string id, int minutosAtras, string acao = "CRIAR_USUARIO", string trace = "trc-5e2a9d14", string? etapa = "CONCLUIDO", string? nivel = null,
        string? codigo = null) =>
        new(id, nivel is null ? TipoDoRegistro.Auditoria : TipoDoRegistro.Erro, trace, etapa, nivel, codigo, acao, "usr-ebfga-maria", "conta-ebfga", "usr-ebfga-lucas",
            T.AddMinutes(-minutosAtras));

    ConsultarRegistros Registros() => new(leitura, nomes, new LeitorDeRegistros(nomes), ultima, Registrador(), relogio);

    [Fact]
    public async Task RF_199_pagina_com_nomes_e_cursor_da_proxima()
    {
        for (var i = 0; i < 3; i++) leitura.Todos.Add(Bruto($"id{i}", i + 1));
        var primeira = await Registros().Executar(new PedidoDeRegistros(null, null, null, null, null, null, null, null, null, null, null, "2"), Consulta());
        Assert.Equal(2, primeira.Itens.Count);
        Assert.NotNull(primeira.Proximo);
        Assert.Equal((T.AddHours(-24), T), (primeira.De, primeira.Ate));
        var item = primeira.Itens[0];
        Assert.Equal(("id0", "AUDITORIA", "Criar usuário", "MOD-03", "Maria Souza", "EBFGA", "usr-ebfga-lucas"), (item.Id, item.Tipo, item.AcaoNome, item.Modulo, item.Quem!.Nome,
            item.Conta!.Sigla, item.Alvo));
        Assert.Equal("2026-11-20T13:41:00.000Z", item.Quando);
        Assert.Equal(3, leitura.UltimaConsulta!.Limite + 1);

        var ultima_ = await Registros().Executar(new PedidoDeRegistros(null, null, null, null, null, null, null, null, null, null, null, "10"), Consulta("/auditoria/registros?x=1"));
        Assert.Null(ultima_.Proximo);
        Assert.Equal(3, ultima_.Itens.Count);
    }

    [Fact]
    public async Task RF_200_traceId_vale_sem_periodo_e_a_conta_que_nao_existe_nao_traz_nada()
    {
        leitura.Todos.Add(Bruto("a", 1));
        var porTrace = await Registros().Executar(new PedidoDeRegistros(null, null, null, null, null, null, null, null, null, "trc-5e2a9d14", null, null), Consulta());
        Assert.Equal((null, null), (porTrace.De, porTrace.Ate));
        Assert.Null(leitura.UltimaConsulta!.Periodo);

        var semConta = await Registros().Executar(new PedidoDeRegistros(null, null, null, null, null, null, "ZZZZZ", null, null, null, null, null), Consulta("/x"));
        Assert.Empty(semConta.Itens);
        Assert.Null(semConta.Proximo);
        var semAcao = await Registros().Executar(new PedidoDeRegistros(null, null, null, null, "MOD-02", "CRIAR_USUARIO", null, null, null, null, null, null), Consulta("/y"));
        Assert.Empty(semAcao.Itens);
    }

    [Fact]
    public async Task RF_199_erro_do_registro_de_recusa_vem_com_nivel_e_codigo()
    {
        leitura.Todos.Add(Bruto("e", 2, etapa: null, nivel: "ALERTA", codigo: "MATRICULA_EM_USO"));
        var dto = await Registros().Executar(new PedidoDeRegistros(null, null, null, "ALERTA", null, null, null, null, null, null, null, null), Consulta());
        var item = Assert.Single(dto.Itens);
        Assert.Equal(("ERRO", null, "ALERTA", "MATRICULA_EM_USO"), (item.Tipo, item.Etapa, item.Nivel, item.Codigo));
    }

    [Theory]
    [InlineData("2026-09-01T00:00:00Z", "2026-11-20T00:00:00Z", "PERIODO_LONGO")]
    [InlineData("2026-11-20T00:00:00Z", "2026-11-19T00:00:00Z", "PERIODO_INVERTIDO")]
    public async Task RN_321_periodo_recusado_nao_grava_consulta(string de, string ate, string codigo)
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() =>
            Registros().Executar(new PedidoDeRegistros(de, ate, null, null, null, null, null, null, null, null, null, null), Consulta()));
        Assert.Equal(codigo, falha.Codigo);
        Assert.Empty(auditoria.Registros);
    }

    [Fact]
    public async Task RNF_252_registros_com_o_banco_fora_respondem_503_sem_ler_nada()
    {
        ultima.Guardar(T, [new Verificacao(Servico.Banco, T, "i", EstadoDoServico.ForaDoAr, null, FalhaDaVerificacao.SemResposta, null, 1, T)], []);
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() =>
            Registros().Executar(new PedidoDeRegistros(null, null, null, null, null, null, null, null, null, null, null, null), Consulta()));
        Assert.Equal((GrupoDaFalhaDaAuditoria.Indisponivel, "BANCO_DE_DADOS_FORA"), (falha.Grupo, falha.Codigo));
        Assert.Null(leitura.UltimaConsulta);
    }

    [Fact]
    public void RN_319_lista_de_acoes_do_catalogo() => Assert.Equal(41, new ListarAcoes().Executar().Count);

    // ── execuções (RF-202, RF-203) ─────────────────────────────────────────

    static PontasDaExecucao Pontas(string trace, int minutosAtras, string? ultimaEtapa, string? nivel = null, string acao = "MUDAR_ESTADO_CONTA", string conta = "conta-ebfga") =>
        new(trace, acao, "usr-dboaz-caio", conta, conta, T.AddMinutes(-minutosAtras), ultimaEtapa, acao, T.AddMinutes(-minutosAtras).AddMilliseconds(150), nivel,
            nivel is null ? null : "CODIGO", nivel is null ? null : T.AddMinutes(-minutosAtras).AddMilliseconds(50));

    ListarExecucoes Execucoes() => new(leitura, nomes, ultima, Registrador(), relogio);

    [Fact]
    public async Task RF_203_lista_com_a_contagem_de_todos_os_resultados_e_so_os_pedidos()
    {
        leitura.Pontas.AddRange([Pontas("trc-00000001", 10, "CONCLUIDO"), Pontas("trc-00000002", 20, "CONCLUIDO"), Pontas("trc-00000003", 30, "INICIADO", "ALERTA"),
            Pontas("trc-00000004", 40, "INICIADO", "ERRO"), Pontas("trc-00000005", 44, "INICIADO"), Pontas("trc-00000006", 1, "INICIADO")]);
        var dto = await Execucoes().Executar(new PedidoDeExecucoes(null, null, "FALHOU,INTERROMPIDA", null, null, null, null, null), Consulta("/auditoria/execucoes"));
        Assert.Equal(new ContagemDto(2, 1, 1, 1, 1), dto.Contagem);
        Assert.Equal(["trc-00000004", "trc-00000005"], dto.Itens.Select(i => i.TraceId));
        Assert.Equal(("INTERROMPIDA", null, "Caio Lima", "EBFGA"), (dto.Itens[1].Resultado, dto.Itens[1].DuracaoMs, dto.Itens[1].Quem!.Nome, dto.Itens[1].Conta!.Sigla));
        Assert.Equal(("FALHOU", 50L, "CODIGO"), (dto.Itens[0].Resultado, dto.Itens[0].DuracaoMs, dto.Itens[0].Codigo));
        Assert.Null(dto.Proximo);
    }

    [Fact]
    public async Task RF_203_filtros_por_modulo_conta_e_pagina()
    {
        leitura.Pontas.AddRange([Pontas("trc-00000001", 10, "CONCLUIDO", acao: "ABRIR_CHAMADO"), Pontas("trc-00000002", 20, "CONCLUIDO", acao: "ABRIR_CHAMADO", conta: "conta-outra"),
            Pontas("trc-00000003", 30, "CONCLUIDO"), Pontas("trc-00000004", 40, "CONCLUIDO", acao: "ABRIR_CHAMADO")]);
        var sac = await Execucoes().Executar(new PedidoDeExecucoes(null, null, null, "MOD-02", null, "ebfga", null, "1"), Consulta("/a"));
        Assert.Equal(["trc-00000001"], sac.Itens.Select(i => i.TraceId));
        Assert.NotNull(sac.Proximo);
        var segunda = await Execucoes().Executar(new PedidoDeExecucoes(null, null, null, "MOD-02", null, "ebfga", sac.Proximo, "1"), Consulta("/b"));
        Assert.Equal(["trc-00000004"], segunda.Itens.Select(i => i.TraceId));
        var semConta = await Execucoes().Executar(new PedidoDeExecucoes(null, null, null, null, null, "ZZZZZ", null, null), Consulta("/c"));
        Assert.Equal((0, 0), (semConta.Itens.Count, semConta.Contagem.Concluidas));
    }

    [Theory]
    [InlineData("PERDIDA", "resultado")]
    public async Task RF_203_resultado_invalido_e_422(string resultado, string campo)
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() => Execucoes().Executar(new PedidoDeExecucoes(null, null, resultado, null, null, null, null, null), Consulta()));
        Assert.Equal(campo, falha.Campo);
    }

    [Fact]
    public async Task RN_321_execucoes_ate_7_dias()
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() =>
            Execucoes().Executar(new PedidoDeExecucoes("2026-11-13T13:41:59Z", "2026-11-20T13:42:00Z", null, null, null, null, null, null), Consulta()));
        Assert.Equal("Escolha um período de até 7 dias.", falha.Message);
    }

    [Fact]
    public async Task RF_202_execucao_pelo_traceId_e_a_que_nao_existe()
    {
        leitura.Todos.AddRange([Bruto("a", 5, "DESATIVAR_USUARIO", "trc-1d6e3f08", "INICIADO"), Bruto("b", 4, "DESATIVAR_USUARIO", "trc-1d6e3f08")]);
        var ver = new VerExecucao(leitura, new LeitorDeRegistros(nomes), ultima, Registrador(), relogio);
        var dto = await ver.Executar("trc-1d6e3f08", Consulta("/auditoria/execucoes/trc-1d6e3f08"));
        Assert.Equal(("CONCLUIDA", "Desativar usuário", 2), (dto.Resultado, dto.AcaoNome, dto.Passos.Count));
        Assert.Equal("EXECUCAO_INEXISTENTE", (await Assert.ThrowsAsync<FalhaDaAuditoria>(() => ver.Executar("trc-00000000", Consulta()))).Codigo);
        Assert.Equal("traceId", (await Assert.ThrowsAsync<FalhaDaAuditoria>(() => ver.Executar("abc", Consulta()))).Campo);
    }

    // ── métricas (RF-197, RF-198) ──────────────────────────────────────────

    static ResumoDeMedicoes Resumo(long pedidos, int ms, long erros4xx = 0, long erros5xx = 0)
    {
        var tempos = new long[FaixasDeTempo.Quantas];
        tempos[(int)FaixasDeTempo.De(ms)] = pedidos;
        return new ResumoDeMedicoes(pedidos, erros4xx, erros5xx, new Histograma(tempos));
    }

    [Fact]
    public async Task RF_197_metricas_da_hora_com_60_pontos_e_o_minuto_sem_pedido()
    {
        medicoes.Total = Resumo(1240, 90, 31, 2);
        medicoes.Pontos.Add(new PontoDaMedicao(T.AddMinutes(-60), Resumo(18, 200)));
        var ver = new VerMetricas(medicoes, Gravador(), ultima, Registrador(), relogio);
        var dto = await ver.Executar(null, null, Consulta("/auditoria/metricas?janela=1h"));
        Assert.Equal(("1h", null, 60), (dto.Janela, dto.Modulo, dto.Serie.Count));
        Assert.Equal((T.AddHours(-1), T), (dto.De, dto.Ate));
        Assert.Equal((1240L, 20.7m, 100, 100, 100, 31L, 2L, 0.16m), (dto.Resumo.Pedidos, dto.Resumo.PorMinuto, dto.Resumo.P50Ms, dto.Resumo.P95Ms, dto.Resumo.P99Ms,
            dto.Resumo.Erros4xx, dto.Resumo.Erros5xx, dto.Resumo.TaxaErro5xx));
        Assert.Equal((18L, 250), (dto.Serie[0].Pedidos, dto.Serie[0].P95Ms));
        Assert.Equal((0L, null), (dto.Serie[1].Pedidos, dto.Serie[1].P95Ms));
        Assert.Equal(["1h", "24h", "7d", "30d"], new[] { "1h", "24h", "7d", "30d" }.Select(j => ver.Executar(j, "MOD-02", Consulta("/" + j)).Result.Janela));
    }

    [Theory]
    [InlineData("2h", null, "janela")]
    [InlineData(null, "MOD-99", "modulo")]
    public async Task RF_197_janela_ou_modulo_invalidos(string? janela, string? modulo, string campo)
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() => new VerMetricas(medicoes, Gravador(), ultima, Registrador(), relogio).Executar(janela, modulo, Consulta()));
        Assert.Equal(campo, falha.Campo);
        Assert.Empty(auditoria.Registros);
    }

    [Fact]
    public async Task RF_198_enderecos_por_p95_pedidos_ou_erros_com_o_selo_lento()
    {
        medicoes.Enderecos.AddRange([new("GET", "/sac/chamados", "MOD-02", Resumo(100, 90)), new("POST", "/pagamento/faturas/{id}/pagamentos", "MOD-01", Resumo(42, 3000, 3, 2)),
            new("GET", "/catalogo/vitrine", "MOD-14", Resumo(10, 40, 5, 5)), new("GET", "/lento", "MOD-02", Resumo(1, 12000))]);
        var ver = new VerTempoDosEnderecos(medicoes, Gravador(), ultima, Registrador(), relogio);
        var p95 = await ver.Executar(null, null, null, null, Consulta("/e1"));
        Assert.Equal(["/lento", "/pagamento/faturas/{id}/pagamentos", "/sac/chamados", "/catalogo/vitrine"], p95.Itens.Select(i => i.Endereco));
        Assert.Equal([true, true, false, false], p95.Itens.Select(i => i.Lento));
        Assert.Equal(("p95", null), (p95.Ordem, p95.Itens[0].P95Ms));
        Assert.Equal("/sac/chamados", (await ver.Executar("1h", null, "pedidos", null, Consulta("/e2"))).Itens[0].Endereco);
        Assert.Equal("/catalogo/vitrine", (await ver.Executar("1h", null, "erros", "1", Consulta("/e3"))).Itens.Single().Endereco);
    }

    [Theory]
    [InlineData("nome", null, "ordem")]
    [InlineData(null, "101", "limite")]
    [InlineData(null, "0", "limite")]
    public async Task RF_198_ordem_ou_limite_invalidos(string? ordem, string? limite, string campo)
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() =>
            new VerTempoDosEnderecos(medicoes, Gravador(), ultima, Registrador(), relogio).Executar(null, null, ordem, limite, Consulta()));
        Assert.Equal(campo, falha.Campo);
    }

    [Fact]
    public async Task RNF_252_metricas_com_o_banco_fora_respondem_503()
    {
        ultima.Guardar(T, [new Verificacao(Servico.Banco, T, "i", EstadoDoServico.ForaDoAr, null, FalhaDaVerificacao.SemResposta, null, 1, T)], []);
        Assert.Equal("BANCO_DE_DADOS_FORA", (await Assert.ThrowsAsync<FalhaDaAuditoria>(() => new VerMetricas(medicoes, Gravador(), ultima, Registrador(), relogio).Executar(null, null, Consulta()))).Codigo);
        Assert.Equal("BANCO_DE_DADOS_FORA", (await Assert.ThrowsAsync<FalhaDaAuditoria>(() =>
            new VerTempoDosEnderecos(medicoes, Gravador(), ultima, Registrador(), relogio).Executar(null, null, null, null, Consulta()))).Codigo);
    }

    [Fact]
    public async Task RNF_247_grava_so_os_minutos_fechados_e_o_gancho_grava_tambem_o_de_agora()
    {
        acumulador.Somar("GET", "/sac/chamados", 200, 50, T.AddMinutes(-1));
        acumulador.Somar("GET", "/sac/chamados", 200, 50, T.AddSeconds(5));
        var gravar = Gravador();
        await gravar.Executar();
        Assert.Equal(T.AddMinutes(-1), Assert.Single(medicoes.Somadas).Inicio);
        await gravar.Executar(tudo: true);
        Assert.Equal(2, medicoes.Somadas.Count);
        await gravar.Executar(tudo: true);
        Assert.Equal(2, medicoes.Somadas.Count);
    }

    // ── ocorrências (RF-204, RF-205) ───────────────────────────────────────

    static Ocorrencia Ocor(string alvo, EstadoDeOcorrencia de, EstadoDeOcorrencia para, int minutosAtras, TipoDoAlvo tipo = TipoDoAlvo.Servico) =>
        new($"id-{alvo}-{minutosAtras}", tipo, alvo, alvo, de, para, T.AddMinutes(-minutosAtras - 5), T.AddMinutes(-minutosAtras), null, para == EstadoDeOcorrencia.NoAr ? 5 : null);

    [Fact]
    public async Task RF_204_abertas_com_fora_do_ar_antes_de_lento_e_as_voltas()
    {
        ocorrencias.EmAberto["MOD-01"] = Ocor("MOD-01", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.Lento, 11, TipoDoAlvo.Modulo);
        ocorrencias.EmAberto["intermediador-pagamento"] = Ocor("intermediador-pagamento", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.ForaDoAr, 12);
        ocorrencias.EmAberto["cache"] = Ocor("cache", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.Lento, 3);
        ocorrencias.Voltas.AddRange([Ocor("banco", EstadoDeOcorrencia.ForaDoAr, EstadoDeOcorrencia.NoAr, 4), Ocor("antiga", EstadoDeOcorrencia.ForaDoAr, EstadoDeOcorrencia.NoAr, 30)]);
        var dto = await new VerOcorrenciasAbertas(ocorrencias, relogio).Executar();
        Assert.Equal(["intermediador-pagamento", "cache", "MOD-01"], dto.Abertas.Select(o => o.Alvo));
        Assert.Equal(["banco"], dto.Voltaram.Select(o => o.Alvo));
    }

    [Fact]
    public async Task RF_205_lista_filtrada_e_parametros_invalidos()
    {
        ocorrencias.Gravadas.AddRange([Ocor("cache", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.Lento, 5), Ocor("MOD-01", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.ForaDoAr, 3, TipoDoAlvo.Modulo)]);
        var listar = new ListarOcorrencias(ocorrencias, ultima, Registrador(), relogio);
        var todas = await listar.Executar(null, null, null, null, null, null, null, Consulta("/auditoria/ocorrencias"));
        Assert.Equal(["MOD-01", "cache"], todas.Itens.Select(o => o.Alvo));
        Assert.Equal((T.AddHours(-24), T), (todas.De, todas.Ate));
        Assert.Equal(["cache"], (await listar.Executar(null, null, "SERVICO", null, null, null, null, Consulta("/b"))).Itens.Select(o => o.Alvo));
        Assert.Equal(["MOD-01"], (await listar.Executar(null, null, null, "MOD-01", "FORA_DO_AR", null, null, Consulta("/c"))).Itens.Select(o => o.Alvo));
        foreach (var (campo, chamada) in new (string, Func<Task>)[]
        {
            ("tipo", () => listar.Executar(null, null, "FILA", null, null, null, null, Consulta())),
            ("alvo", () => listar.Executar(null, null, null, "fila", null, null, null, Consulta())),
            ("para", () => listar.Executar(null, null, null, null, "COM_FALHAS", null, null, Consulta())),
            ("limite", () => listar.Executar(null, null, null, null, null, null, "201", Consulta())),
            ("ate", () => listar.Executar("2026-10-20T13:59:59Z", null, null, null, null, null, null, Consulta())),
        })
        {
            Assert.Equal(campo, (await Assert.ThrowsAsync<FalhaDaAuditoria>(chamada)).Campo);
        }
    }

    // ── card (RN-322) e descarte (RNF-248) ─────────────────────────────────

    [Fact]
    public async Task RN_322_card_com_o_dia_fechado_e_os_servicos_fora_do_ar_da_ultima_verificacao()
    {
        card.Guardados.Add(new DiaDoCard(new DateOnly(2026, 11, 20), 4, 31, 1, 12));
        Cache.Resposta = ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        await Verificador().Executar();
        var dto = await new VerCardDaAuditoria(card, ultima, relogio).Executar();
        Assert.Equal(("MOD-17", "DEBOAZ", 1, 4, 1201L), (dto.Modulo, dto.Perfil, dto.Selo, dto.Normal.Valor, dto.Versao));
        Assert.Equal(12, dto.Series.Dia.Count);
    }

    [Fact]
    public async Task RN_322_card_com_o_banco_fora_mostra_zero_e_nao_cai()
    {
        card.Falha = true;
        var dto = await new VerCardDaAuditoria(card, ultima, relogio).Executar();
        Assert.Equal((0, true), (dto.Normal.Valor, dto.Vazio));
        card.Falha = false;
        ultima.Guardar(T, [new Verificacao(Servico.Banco, T, "i", EstadoDoServico.ForaDoAr, null, FalhaDaVerificacao.SemResposta, null, 1, T)], []);
        Assert.Equal(0, (await new VerCardDaAuditoria(card, ultima, relogio).Executar()).Normal.Valor);
    }

    [Fact]
    public async Task RN_322_card_ao_vivo_so_manda_a_versao_nova_e_recusa_intervalo_fora_da_lista()
    {
        var ver = new VerCardDaAuditoria(card, ultima, relogio);
        var ao = new AcompanharCardDaAuditoriaAoVivo(ver);
        var (intervalo, primeiro) = await ao.Abrir("30");
        Assert.Equal(TimeSpan.FromSeconds(30), intervalo);
        Assert.Equal(TimeSpan.FromSeconds(300), (await ao.Abrir(null)).Intervalo);
        Assert.Null(await ao.Proximo(primeiro.Versao));
        card.Guardados.Add(new DiaDoCard(new DateOnly(2026, 11, 20), 1, 0, 0, 1));
        Assert.Equal(1, (await ao.Proximo(primeiro.Versao))!.Normal.Valor);
        Assert.Equal("intervalo", (await Assert.ThrowsAsync<FalhaDaAuditoria>(() => ao.Abrir("10"))).Campo);
    }

    [Fact]
    public async Task RN_322_agendamento_fecha_o_dia_de_brasilia_e_descarta_so_com_o_pedido()
    {
        leitura.Erros = (4, 31);
        leitura.Pontas.AddRange([Pontas("trc-00000001", 40, "INICIADO"), Pontas("trc-00000002", 2, "INICIADO"), Pontas("trc-00000003", 10, "CONCLUIDO")]);
        var fechar = new FecharCardEDescartar(card, leitura, card, relogio);
        await fechar.Executar(false);
        var dia = Assert.Single(card.Fechados);
        Assert.Equal((new DateOnly(2026, 11, 20), 4, 31, 1), (dia.Dia, dia.Erros, dia.Alertas, dia.Interrompidas));
        Assert.Equal(0, card.Descartes);
        await fechar.Executar(true);
        Assert.Equal(1, card.Descartes);
    }

    // ── ao vivo (RNF-246, RNF-249, 15 RN-279) ──────────────────────────────

    AoVivoDaAuditoria AoVivo() => new(conexoes, acesso, relogio);

    static async Task<List<EventoAoVivo>> Colher(AoVivoAberto aberto, int quantos, TimeSpan limite)
    {
        using var parar = new CancellationTokenSource(limite);
        var colhidos = new List<EventoAoVivo>();
        try
        {
            await foreach (var e in aberto.Eventos(parar.Token))
            {
                colhidos.Add(e);
                if (colhidos.Count == quantos || e.Tipo == "encerrado") break;
            }
        }
        catch (OperationCanceledException)
        {
            // o tempo do teste acabou
        }
        return colhidos;
    }

    [Fact]
    public async Task RF_204_ocorrencias_ao_vivo_abre_com_as_abertas_e_entrega_a_ocorrencia_nova()
    {
        ocorrencias.EmAberto["cache"] = Ocor("cache", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.Lento, 3);
        var aberto = await new AcompanharOcorrenciasAoVivo(new VerOcorrenciasAbertas(ocorrencias, relogio), ocorrencias, AoVivo()).Abrir(Caio);
        var nova = Ocor("banco", EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.ForaDoAr, 0);
        await ocorrencias.Publicar(nova);
        var eventos = await Colher(aberto, 2, TimeSpan.FromSeconds(10));
        Assert.Equal(["abertas", "ocorrencia"], eventos.Select(e => e.Tipo));
        Assert.Equal(nova.Id, eventos[1].Id);
        Assert.Equal(["cache"], ((Boaz.Core.Hub.Auditoria.Ocorrencias.Dtos.AbertasDto)eventos[0].Corpo!).Abertas.Select(o => o.Alvo));
        Assert.Single(conexoes.Fechadas);
    }

    [Fact]
    public async Task RNF_249_a_4a_conexao_encerra_a_mais_antiga_com_limite_de_conexoes()
    {
        var abertos = new List<AoVivoAberto>();
        for (var i = 0; i < 4; i++)
        {
            abertos.Add(await new AcompanharOcorrenciasAoVivo(new VerOcorrenciasAbertas(ocorrencias, relogio), ocorrencias, AoVivo()).Abrir(Caio));
        }
        var primeira = await Colher(abertos[0], 5, TimeSpan.FromSeconds(10));
        Assert.Equal("encerrado", primeira[^1].Tipo);
        Assert.Equal("LIMITE_CONEXOES", primeira[^1].Corpo!.GetType().GetProperty("codigo")!.GetValue(primeira[^1].Corpo));
        Assert.Single(conexoes.Fechadas);
    }

    [Fact]
    public async Task RNF_246_sessao_encerrada_ou_acesso_retirado_encerram_a_conexao()
    {
        acesso.Sessao = false;
        var aberto = await new AcompanharOcorrenciasAoVivo(new VerOcorrenciasAbertas(ocorrencias, relogio), ocorrencias, AoVivo()).Abrir(Caio);
        var eventos = await Colher(aberto, 5, TimeSpan.FromSeconds(15));
        Assert.Equal("SESSAO_TERMINADA", eventos[^1].Corpo!.GetType().GetProperty("codigo")!.GetValue(eventos[^1].Corpo));

        acesso.Sessao = true;
        acesso.Pode = false;
        var outro = await new AcompanharOcorrenciasAoVivo(new VerOcorrenciasAbertas(ocorrencias, relogio), ocorrencias, AoVivo()).Abrir(Caio);
        var segundo = await Colher(outro, 5, TimeSpan.FromSeconds(15));
        Assert.Equal("ACESSO_NEGADO", segundo[^1].Corpo!.GetType().GetProperty("codigo")!.GetValue(segundo[^1].Corpo));
    }

    [Fact]
    public async Task RNF_246_saude_ao_vivo_abre_com_a_saude_conta_o_observador_e_grava_a_consulta_uma_vez()
    {
        var observadores = new ObservadoresDaSaude();
        var ao = new AcompanharSaudeAoVivo(new LeitorDaSaude(ultima, Verificador(), instancias, eu), ultima, ocorrencias, Registrador(), observadores, AoVivo(), relogio);
        var aberto = await ao.Abrir(Consulta("/auditoria/saude/ao-vivo"));
        Assert.Equal(1, observadores.Quantos);
        Assert.Equal(2, auditoria.Registros.Count);
        var primeiro = (await Colher(aberto, 1, TimeSpan.FromSeconds(5)))[0];
        Assert.Equal(("saude", T.ToUnixTimeMilliseconds().ToString()), (primeiro.Tipo, primeiro.Id));
        Assert.Equal("NO_AR", ((Boaz.Core.Hub.Auditoria.Saude.Dtos.SaudeDto)primeiro.Corpo!).Geral);
        Assert.Equal(0, observadores.Quantos);
    }

    [Fact]
    public async Task RF_201_registros_ao_vivo_filtra_e_recusa_o_que_nao_existe_no_ao_vivo()
    {
        var falha = await Assert.ThrowsAsync<FalhaDaAuditoria>(() => ParaAoVivo(new FiltroAvisos()).Abrir(
            new PedidoDeRegistros("2026-11-20T13:00:00Z", null, null, null, null, null, null, null, null, null, null, null), Consulta()));
        Assert.Equal("de", falha.Campo);
        Assert.Equal("traceId", (await Assert.ThrowsAsync<FalhaDaAuditoria>(() => ParaAoVivo(new FiltroAvisos()).Abrir(
            new PedidoDeRegistros(null, null, null, null, null, null, null, null, null, "trc-4b7e01c9", null, null), Consulta()))).Campo);

        var avisos = new FiltroAvisos();
        var aberto = await ParaAoVivo(avisos).Abrir(new PedidoDeRegistros(null, null, null, "ERRO", null, null, null, null, null, null, null, null), Consulta("/auditoria/registros/ao-vivo?nivel=ERRO"));
        await avisos.Publicar(Bruto("a", 0));
        await avisos.Publicar(Bruto("e", 0, etapa: null, nivel: "ERRO", codigo: "BANCO_INDISPONIVEL"));
        var eventos = await Colher(aberto, 1, TimeSpan.FromSeconds(10));
        var evento = Assert.Single(eventos);
        Assert.Equal(("registro", "e"), (evento.Tipo, evento.Id));
        Assert.Equal("BANCO_INDISPONIVEL", ((RegistroDto)evento.Corpo!).Codigo);
        Assert.Equal(2, auditoria.Registros.Count);
    }

    AcompanharRegistrosAoVivo ParaAoVivo(FiltroAvisos avisos) => new(avisos, ocorrencias, new LeitorDeRegistros(nomes), Registrador(), AoVivo());

    sealed class FiltroAvisos : IAvisosDaAuditoria
    {
        readonly System.Threading.Channels.Channel<RegistroBruto> canal = System.Threading.Channels.Channel.CreateUnbounded<RegistroBruto>();

        public Task Publicar(RegistroBruto registro, CancellationToken ct = default)
        {
            canal.Writer.TryWrite(registro);
            return Task.CompletedTask;
        }

        public Task<IAssinatura<RegistroBruto>> Assinar(CancellationToken ct = default) => Task.FromResult<IAssinatura<RegistroBruto>>(new Assinatura(canal));

        sealed class Assinatura(System.Threading.Channels.Channel<RegistroBruto> canal) : IAssinatura<RegistroBruto>
        {
            public async IAsyncEnumerable<RegistroBruto> Avisos([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                await foreach (var r in canal.Reader.ReadAllAsync(ct)) yield return r;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
