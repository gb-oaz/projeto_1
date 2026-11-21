using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.MsMono.Secundario.Out.Hub.Auditoria;

namespace Boaz.MsMono.Tests;

// As sondas que o gancho de teste da Auditoria troca (20 RN-315, RN-315-api).
public class SondasDeTesteTests
{
    static readonly DateTimeOffset Ultima = DateTimeOffset.Parse("2026-11-20T13:41:00Z");
    static readonly DateTimeOffset Agora = DateTimeOffset.Parse("2026-11-20T13:42:00Z");

    // RN-315-api: para os agendamentos a sonda usa só "ultimaExecucao" e ignora "resposta", mesmo ERRO, NENHUMA ou RECUSADA
    [Theory]
    [InlineData("NORMAL")]
    [InlineData("LENTA")]
    [InlineData("ERRO")]
    [InlineData("NENHUMA")]
    [InlineData("RECUSADA")]
    public void RN_315_agendamento_ignora_a_resposta_e_conta_so_a_ultima_execucao(string resposta)
    {
        var resultado = new RespostaDaSondaDeTeste(resposta, null, Ultima).Resultado();

        var verificacao = Verificacao.Seguinte(null, Servico.AgendamentoSac, "i1", resultado, Agora);

        Assert.Equal((EstadoDoServico.NoAr, null, Ultima), (verificacao.Estado, verificacao.Falha, verificacao.UltimaExecucao));
    }

    [Fact]
    public void RN_315_servico_comum_com_resposta_de_falha_continua_falhando()
    {
        var resultado = new RespostaDaSondaDeTeste("NENHUMA", null, null).Resultado();

        Assert.Equal(FalhaDaVerificacao.SemResposta, resultado.Falha);
    }

    // o reinício do estado em memória entre cenários do qa (gancho POST /_teste/auditoria/reiniciar)
    [Fact]
    public void RN_315_sondas_limpas_voltam_todas_a_verificacao_real()
    {
        var sondas = new SondasDeTeste();
        sondas.Definir(Servico.Banco, new RespostaDaSondaDeTeste("NENHUMA", null, null));

        sondas.Limpar();

        Assert.Null(sondas.De(Servico.Banco));
    }

    [Fact]
    public void RN_315_ultima_verificacao_limpa_esquece_o_banco_fora_do_ar()
    {
        var ultima = new UltimaVerificacaoEmMemoria();
        var fora = Verificacao.Seguinte(null, Servico.Banco, "i1", ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta), Agora);
        ultima.Guardar(Agora, [fora], []);
        Assert.True(ultima.BancoForaDoAr);

        ultima.Limpar();

        Assert.False(ultima.BancoForaDoAr);
        Assert.Null(ultima.VerificadoEm);
    }
}
