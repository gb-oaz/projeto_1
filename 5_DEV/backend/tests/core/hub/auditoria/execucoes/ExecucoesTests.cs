using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Execucoes.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Domain;

namespace Boaz.Core.Hub.Tests.Auditoria.Execucoes;

/// <summary>RF-202, RF-203, RN-318: o resultado e a duração de uma execução, pelos registros do mesmo traceId.</summary>
public sealed class ExecucoesTests
{
    static readonly DateTimeOffset Dia = new(2026, 11, 20, 13, 20, 0, TimeSpan.Zero);
    static readonly DateTimeOffset Agora = new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero);

    static RegistroLido Auditoria(string etapa, string acao, int ms, string id) =>
        new(id, TipoDoRegistro.Auditoria, "trc-90ab12cd", etapa, null, null, acao, CatalogoDeAcoes.Nome(acao), CatalogoDeAcoes.Modulo(acao),
            new PessoaLida("usr-ebfga-maria", "Maria Souza"), new ContaLida("conta-ebfga", "EBFGA"), "usr-ebfga-paulo", Dia.AddMilliseconds(ms));

    static RegistroLido Erro(string nivel, string codigo, int ms) =>
        new("e1", TipoDoRegistro.Erro, "trc-90ab12cd", null, nivel, codigo, "DESATIVAR_USUARIO", "Desativar usuário", "MOD-03", null, null, null, Dia.AddMilliseconds(ms));

    [Fact]
    public void RN_318_concluida_com_a_duracao_do_iniciado_ao_concluido()
    {
        var e = Execucao.Montar([Auditoria("INICIADO", "DESATIVAR_USUARIO", 0, "a"), Auditoria("CONCLUIDO", "DESATIVAR_USUARIO", 184, "b")], Agora);
        Assert.Equal((ResultadoDaExecucao.Concluida, 184L, "MOD-03"), (e.Resultado, e.DuracaoMs, e.Principal.Modulo));
        Assert.Equal(Dia.AddMilliseconds(184), e.Fim);
    }

    [Fact]
    public void RF_202_acoes_internas_ficam_entre_o_iniciado_e_o_concluido_da_principal()
    {
        var e = Execucao.Montar(
        [
            Auditoria("INICIADO", "DESATIVAR_USUARIO", 0, "a"), Auditoria("INICIADO", "ENCERRAR_SESSOES_DO_USUARIO", 88, "b"),
            Auditoria("CONCLUIDO", "ENCERRAR_SESSOES_DO_USUARIO", 149, "c"), Auditoria("CONCLUIDO", "DESATIVAR_USUARIO", 184, "d"),
        ], Agora);
        Assert.Equal([false, true, true, false], e.Passos.Select(p => p.Interna));
        Assert.Equal(ResultadoDaExecucao.Concluida, e.Resultado);
        var dto = ExecucaoDto.De(e);
        Assert.Equal(("trc-90ab12cd", "Desativar usuário", "CONCLUIDA", "2026-11-20T13:20:00.184Z", 184L), (dto.TraceId, dto.AcaoNome, dto.Resultado, dto.Fim, dto.DuracaoMs));
        Assert.Equal(("Maria Souza", "EBFGA", "usr-ebfga-paulo"), (dto.Quem!.Nome, dto.Conta!.Sigla, dto.Alvo));
        Assert.Equal([false, true, true, false], dto.Passos.Select(p => p.Interna));
        Assert.Equal(["INICIADO", "INICIADO", "CONCLUIDO", "CONCLUIDO"], dto.Passos.Select(p => p.Etapa));
    }

    [Fact]
    public void RN_318_recusada_pelo_alerta_e_falhou_pelo_erro()
    {
        var recusada = Execucao.Montar([Auditoria("INICIADO", "DESATIVAR_USUARIO", 0, "a"), Erro("ALERTA", "MATRICULA_EM_USO", 50)], Agora);
        Assert.Equal((ResultadoDaExecucao.Recusada, 50L, "MATRICULA_EM_USO"), (recusada.Resultado, recusada.DuracaoMs, recusada.Codigo));
        var falhou = Execucao.Montar([Auditoria("INICIADO", "DESATIVAR_USUARIO", 0, "a"), Erro("ERRO", "BANCO_INDISPONIVEL", 2107)], Agora);
        Assert.Equal((ResultadoDaExecucao.Falhou, 2107L), (falhou.Resultado, falhou.DuracaoMs));
        Assert.Equal("ERRO", falhou.Passos[^1].Registro.Nivel);
    }

    [Theory]
    [InlineData(1000, ResultadoDaExecucao.EmAndamento)]
    [InlineData(0, ResultadoDaExecucao.Interrompida)]
    public void RN_318_so_o_iniciado_e_em_andamento_ate_5_min_e_depois_interrompida(int msDeDiferenca, ResultadoDaExecucao esperado)
    {
        // o INICIADO às 13:37:01 e às 13:37:00, com o relógio em 13:42:00
        var inicio = new DateTimeOffset(2026, 11, 20, 13, 37, 0, TimeSpan.Zero).AddMilliseconds(msDeDiferenca);
        var registro = Auditoria("INICIADO", "CRIAR_USUARIO", 0, "a") with { Quando = inicio };
        var e = Execucao.Montar([registro], Agora);
        Assert.Equal((esperado, null, null), (e.Resultado, e.DuracaoMs, e.Fim));
    }

    [Fact]
    public void RF_202_execucao_sem_registro_e_inexistente()
    {
        var falha = Assert.Throws<FalhaDaAuditoria>(() => Execucao.Montar([], Agora));
        Assert.Equal(("EXECUCAO_INEXISTENTE", GrupoDaFalhaDaAuditoria.NaoEncontrado), (falha.Codigo, falha.Grupo));
    }

    [Fact]
    public void RF_203_resumo_da_lista_pelas_pontas_de_cada_execucao()
    {
        PontasDaExecucao Pontas(string? etapa, string? nivel = null) =>
            new("trc-1", "MUDAR_ESTADO_CONTA", "usr-dboaz-caio", "conta-ebfga", "conta-ebfga", Dia, etapa, "MUDAR_ESTADO_CONTA", Dia.AddMilliseconds(150), nivel,
                nivel is null ? null : "CODIGO", nivel is null ? null : Dia.AddMilliseconds(50));
        Assert.Equal((ResultadoDaExecucao.Concluida, 150L, "MOD-03"), (ResumoDaExecucao.De(Pontas("CONCLUIDO"), Agora).Resultado,
            ResumoDaExecucao.De(Pontas("CONCLUIDO"), Agora).DuracaoMs, ResumoDaExecucao.De(Pontas("CONCLUIDO"), Agora).Modulo));
        Assert.Equal(ResultadoDaExecucao.Recusada, ResumoDaExecucao.De(Pontas("INICIADO", "ALERTA"), Agora).Resultado);
        Assert.Equal(ResultadoDaExecucao.Falhou, ResumoDaExecucao.De(Pontas("INICIADO", "ERRO"), Agora).Resultado);
        Assert.Equal(ResultadoDaExecucao.Interrompida, ResumoDaExecucao.De(Pontas("INICIADO"), Agora).Resultado);
        Assert.Equal(ResultadoDaExecucao.EmAndamento, ResumoDaExecucao.De(Pontas("INICIADO"), Dia.AddMinutes(4)).Resultado);
        Assert.Null(ResumoDaExecucao.De(Pontas("INICIADO"), Agora).DuracaoMs);
    }

    [Fact]
    public void RN_318_textos_dos_resultados()
    {
        Assert.Equal(["CONCLUIDA", "RECUSADA", "FALHOU", "EM_ANDAMENTO", "INTERROMPIDA"], Enum.GetValues<ResultadoDaExecucao>().Select(r => r.Texto()));
        Assert.Equal(ResultadoDaExecucao.Falhou, TextosDaExecucao.ResultadoDe("FALHOU"));
        Assert.Null(TextosDaExecucao.ResultadoDe("PERDIDA"));
    }
}
