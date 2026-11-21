using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Domain;

namespace Boaz.Core.Hub.Tests.Auditoria.Saude;

/// <summary>RF-194 a RF-196, RN-315, RN-316: o estado de cada serviço e de cada módulo, as quedas e a disponibilidade.</summary>
public sealed class SaudeTests
{
    static readonly DateTimeOffset Agora = new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero);

    static Verificacao Verifica(Verificacao? anterior, ResultadoDaSonda sonda, DateTimeOffset? quando = null, Servico? servico = null) =>
        Verificacao.Seguinte(anterior, servico ?? Servico.Cache, "i1", sonda, quando ?? Agora);

    [Theory]
    [InlineData(10, EstadoDoServico.NoAr)]
    [InlineData(1000, EstadoDoServico.NoAr)]
    [InlineData(1001, EstadoDoServico.Lento)]
    [InlineData(3000, EstadoDoServico.Lento)]
    [InlineData(3001, EstadoDoServico.ForaDoAr)]
    public void RN_315_estado_pelo_tempo_da_resposta(int tempoMs, EstadoDoServico esperado) =>
        Assert.Equal(esperado, Verifica(null, ResultadoDaSonda.Respondeu(tempoMs)).Estado);

    [Fact]
    public void RN_315_sem_resposta_tira_do_ar_na_hora()
    {
        var v = Verifica(null, ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta));
        Assert.Equal((EstadoDoServico.ForaDoAr, FalhaDaVerificacao.SemResposta, null), (v.Estado, v.Falha, v.TempoMs));
        Assert.True(v.ForaDoAr);
    }

    [Theory]
    [InlineData(FalhaDaVerificacao.RespostaComErro)]
    [InlineData(FalhaDaVerificacao.ConexaoRecusada)]
    public void RN_315_a_3a_falha_seguida_tira_do_ar(FalhaDaVerificacao falha)
    {
        var sonda = ResultadoDaSonda.Falhou(falha);
        var primeira = Verifica(null, sonda, Agora);
        var segunda = Verifica(primeira, sonda, Agora.AddSeconds(30));
        var terceira = Verifica(segunda, sonda, Agora.AddSeconds(60));
        Assert.Equal([EstadoDoServico.Lento, EstadoDoServico.Lento, EstadoDoServico.ForaDoAr], [primeira.Estado, segunda.Estado, terceira.Estado]);
        // o "desde" é o da 1ª falha, mesmo depois de a 3ª tirar o serviço do ar
        Assert.Equal(Agora, terceira.Desde);
        Assert.Equal(3, terceira.FalhasSeguidas);
    }

    [Fact]
    public void RN_315_resposta_boa_zera_as_falhas_e_o_desde_muda_com_o_estado()
    {
        var caiu = Verifica(null, ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta), Agora);
        var voltou = Verifica(caiu, ResultadoDaSonda.Respondeu(12), Agora.AddMinutes(22));
        var segue = Verifica(voltou, ResultadoDaSonda.Respondeu(20), Agora.AddMinutes(23));
        Assert.Equal((EstadoDoServico.NoAr, 0, Agora.AddMinutes(22)), (voltou.Estado, voltou.FalhasSeguidas, voltou.Desde));
        Assert.Equal(Agora.AddMinutes(22), segue.Desde);
    }

    [Theory]
    [InlineData(2, EstadoDoServico.NoAr)]
    [InlineData(5, EstadoDoServico.Lento)]
    [InlineData(6, EstadoDoServico.ForaDoAr)]
    public void RN_315_agendamento_pela_idade_da_ultima_rodada(int minutos, EstadoDoServico esperado)
    {
        var v = Verifica(null, new ResultadoDaSonda(UltimaExecucao: Agora.AddMinutes(-minutos)), servico: Servico.AgendamentoSac);
        Assert.Equal(esperado, v.Estado);
    }

    [Fact]
    public void RN_315_agendamento_que_nunca_rodou_e_o_desligado_nao_se_avaliam()
    {
        Assert.Equal(EstadoDoServico.SemVerificacao, Verifica(null, new ResultadoDaSonda(), servico: Servico.AgendamentoPagamento).Estado);
        Assert.Equal(EstadoDoServico.Desligado, Verifica(null, new ResultadoDaSonda(Desligado: true), servico: Servico.ArmazenamentoAnexos).Estado);
    }

    [Fact]
    public void RF_194_os_8_servicos_e_o_servico_inexistente()
    {
        Assert.Equal(8, Servico.Todos.Count);
        Assert.Equal("Banco de dados", Servico.DeId("banco").Nome);
        var falha = Assert.Throws<FalhaDaAuditoria>(() => Servico.DeId("fila"));
        Assert.Equal("SERVICO_INEXISTENTE", falha.Codigo);
        Assert.Equal(GrupoDaFalhaDaAuditoria.NaoEncontrado, falha.Grupo);
    }

    [Fact]
    public void RF_194_textos_e_ordem_dos_estados()
    {
        Assert.Equal(["FORA_DO_AR", "LENTO", "SEM_VERIFICACAO", "NO_AR", "DESLIGADO"],
            Enum.GetValues<EstadoDoServico>().OrderBy(e => e.Ordem()).Select(e => e.Texto()));
        Assert.Equal(FalhaDaVerificacao.RespostaComErro, TextosDaSaude.FalhaDe("RESPOSTA_COM_ERRO"));
        Assert.Equal(EstadoDoServico.Lento, TextosDaSaude.EstadoDe("LENTO"));
        Assert.Null(TextosDaSaude.FalhaDe("X"));
        Assert.Equal(["SEM_RESPOSTA", "CONEXAO_RECUSADA", "RESPOSTA_COM_ERRO"], Enum.GetValues<FalhaDaVerificacao>().Select(f => f.Texto()));
    }

    static ResumoDeMedicoes Pedidos(long pedidos, long erros5xx, int tempoMs)
    {
        var tempos = new long[FaixasDeTempo.Quantas];
        tempos[(int)FaixasDeTempo.De(tempoMs)] = pedidos;
        return new ResumoDeMedicoes(pedidos, 0, erros5xx, new Histograma(tempos));
    }

    [Theory]
    [InlineData(2, 120, 3, EstadoDoModulo.NoAr)]
    [InlineData(3, 120, 1, EstadoDoModulo.Lento)]
    [InlineData(3, 120, 3, EstadoDoModulo.ForaDoAr)]
    [InlineData(0, 1800, 3, EstadoDoModulo.Lento)]
    [InlineData(0, 4000, 2, EstadoDoModulo.Lento)]
    [InlineData(0, 4000, 3, EstadoDoModulo.ForaDoAr)]
    public void RN_316_estado_do_modulo_pelas_falhas_seguidas(int erros, int tempoMs, int vezes, EstadoDoModulo esperado)
    {
        SaudeDoModulo? m = null;
        for (var i = 0; i < vezes; i++) m = SaudeDoModulo.Seguinte(m, "MOD-01", Pedidos(250, erros, tempoMs));
        Assert.Equal(esperado, m!.Estado);
    }

    [Fact]
    public void RF_196_modulo_com_numeros_e_sem_pedidos()
    {
        var m = SaudeDoModulo.Seguinte(null, "MOD-01", Pedidos(250, 10, 400));
        Assert.Equal(("Pagamento", EstadoDoModulo.Lento, 250L, 10L, 4.0m, 500), (m.Nome, m.Estado, m.Pedidos, m.Erros5xx, m.TaxaErro5xx, m.P95Ms));
        var vazio = SaudeDoModulo.Seguinte(m, "MOD-14", ResumoDeMedicoes.Vazio);
        Assert.Equal((EstadoDoModulo.SemPedidos, null, null, 0), (vazio.Estado, vazio.TaxaErro5xx, vazio.P95Ms, vazio.FalhasSeguidas));
        Assert.Equal(["NO_AR", "LENTO", "FORA_DO_AR", "SEM_PEDIDOS"], Enum.GetValues<EstadoDoModulo>().Select(e => e.Texto()));
        Assert.Equal([0, 1, 2, 3], new[] { EstadoDoModulo.ForaDoAr, EstadoDoModulo.Lento, EstadoDoModulo.NoAr, EstadoDoModulo.SemPedidos }
            .Select(e => new SaudeDoModulo("M", "M", e, 0, 0, null, null, 0).Ordem));
    }

    [Fact]
    public void RF_194_geral_pelo_pior_servico()
    {
        IReadOnlyList<Verificacao> Servicos(params EstadoDoServico[] estados) =>
            [.. estados.Select((e, i) => new Verificacao(Servico.Todos[i], Agora, "i1", e, null, null, null, 0, Agora))];
        SaudeDoSistema Saude(params EstadoDoServico[] estados) => new("HOMOLOGACAO", Agora, Servicos(estados), [], []);
        Assert.Equal((EstadoDoServico.NoAr, 0, 0), (Saude(EstadoDoServico.NoAr, EstadoDoServico.Desligado).Geral, 0, Saude(EstadoDoServico.NoAr).Lentos));
        var lento = Saude(EstadoDoServico.NoAr, EstadoDoServico.Lento);
        Assert.Equal((EstadoDoServico.Lento, 0, 1), (lento.Geral, lento.ForaDoAr, lento.Lentos));
        var fora = Saude(EstadoDoServico.Lento, EstadoDoServico.ForaDoAr);
        Assert.Equal((EstadoDoServico.ForaDoAr, 1, 1), (fora.Geral, fora.ForaDoAr, fora.Lentos));
    }

    [Fact]
    public void RF_195_quedas_duracao_e_disponibilidade_em_24_horas()
    {
        var ate = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        var quedas = new[]
        {
            new Queda("a", Servico.IntermediadorPagamento, new(2026, 11, 20, 13, 30, 0, TimeSpan.Zero), new(2026, 11, 20, 13, 52, 0, TimeSpan.Zero), FalhaDaVerificacao.SemResposta),
            new Queda("b", Servico.IntermediadorPagamento, new(2026, 11, 20, 6, 10, 0, TimeSpan.Zero), new(2026, 11, 20, 6, 12, 0, TimeSpan.Zero), FalhaDaVerificacao.ConexaoRecusada),
        };
        Assert.Equal(98.33m, Queda.Disponibilidade(quedas, ate.AddHours(-24), ate));
        Assert.Equal([22, 2], quedas.Select(q => q.DuracaoMin(ate)));
        Assert.Equal(100m, Queda.Disponibilidade([], ate.AddHours(-24), ate));
        Assert.Equal(100m, Queda.Disponibilidade(quedas, ate, ate));
        Assert.Equal(new DateTimeOffset(2026, 12, 21, 13, 30, 0, TimeSpan.Zero), quedas[0].GuardarAte);
    }

    [Fact]
    public void RF_195_queda_aberta_conta_ate_agora()
    {
        var aberta = new Queda("a", Servico.IntermediadorPagamento, new(2026, 11, 20, 13, 30, 0, TimeSpan.Zero), null, FalhaDaVerificacao.SemResposta);
        Assert.Equal(12, aberta.DuracaoMin(new DateTimeOffset(2026, 11, 20, 13, 42, 0, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromMinutes(12), aberta.DentroDe(new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero), new(2026, 11, 20, 13, 42, 0, TimeSpan.Zero)));
    }
}
