using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Dtos;
using Boaz.Core.Hub.Auditoria.Saude.Domain;

namespace Boaz.Core.Hub.Tests.Auditoria.Ocorrencias;

/// <summary>RF-204, RF-205, RN-324: o que vira ocorrência quando o estado de um serviço ou de um módulo muda.</summary>
public sealed class OcorrenciasTests
{
    static readonly DateTimeOffset T = new(2026, 11, 20, 10, 31, 0, TimeSpan.Zero);

    static Ocorrencia? Detectar(Ocorrencia? aberta, EstadoDeOcorrencia atual, DateTimeOffset quando, FalhaDaVerificacao? falha = null) =>
        Ocorrencia.Detectar(aberta, TipoDoAlvo.Servico, "cache", "Cache", atual, falha, quando, "id1");

    [Fact]
    public void RN_324_no_ar_para_lento_abre_a_ocorrencia()
    {
        var o = Detectar(null, EstadoDeOcorrencia.Lento, T)!;
        Assert.Equal((EstadoDeOcorrencia.NoAr, EstadoDeOcorrencia.Lento, T, T), (o.De, o.Para, o.Inicio, o.Quando));
        Assert.Null(o.DuracaoMin);
        Assert.Equal(T.AddDays(31), o.GuardarAte);
    }

    [Fact]
    public void RN_324_lento_para_fora_do_ar_muda_a_ocorrencia_e_mantem_o_inicio()
    {
        var lento = Detectar(null, EstadoDeOcorrencia.Lento, T)!;
        var fora = Detectar(lento, EstadoDeOcorrencia.ForaDoAr, T.AddMinutes(2), FalhaDaVerificacao.SemResposta)!;
        Assert.Equal((EstadoDeOcorrencia.Lento, EstadoDeOcorrencia.ForaDoAr, T, T.AddMinutes(2), FalhaDaVerificacao.SemResposta),
            (fora.De, fora.Para, fora.Inicio, fora.Quando, fora.Falha));
        Assert.Equal(0, fora.Gravidade);
        Assert.Equal(1, lento.Gravidade);
    }

    [Fact]
    public void RN_324_volta_a_no_ar_fecha_com_a_duracao_desde_que_saiu_de_no_ar()
    {
        var lento = Detectar(null, EstadoDeOcorrencia.Lento, T)!;
        var fora = Detectar(lento, EstadoDeOcorrencia.ForaDoAr, T.AddMinutes(2))!;
        var volta = Detectar(fora, EstadoDeOcorrencia.NoAr, T.AddMinutes(10), FalhaDaVerificacao.SemResposta)!;
        Assert.Equal((EstadoDeOcorrencia.ForaDoAr, EstadoDeOcorrencia.NoAr, 10, T), (volta.De, volta.Para, volta.DuracaoMin, volta.Inicio));
        Assert.Null(volta.Falha);
    }

    [Fact]
    public void RN_324_a_duracao_conta_minutos_inteiros()
    {
        var fora = Detectar(null, EstadoDeOcorrencia.ForaDoAr, T)!;
        Assert.Equal(6, Detectar(fora, EstadoDeOcorrencia.NoAr, T.AddMinutes(6).AddSeconds(30))!.DuracaoMin);
    }

    [Fact]
    public void RN_324_estado_que_nao_muda_nao_abre_ocorrencia()
    {
        var lento = Detectar(null, EstadoDeOcorrencia.Lento, T)!;
        Assert.Null(Detectar(lento, EstadoDeOcorrencia.Lento, T.AddSeconds(30)));
        Assert.Null(Detectar(null, EstadoDeOcorrencia.NoAr, T));
    }

    [Fact]
    public void RN_324_sem_pedidos_sem_verificacao_e_desligado_contam_como_no_ar()
    {
        Assert.Equal(EstadoDeOcorrencia.NoAr, EstadoDoModulo.SemPedidos.Ocorre());
        Assert.Equal(EstadoDeOcorrencia.NoAr, EstadoDoServico.Desligado.Ocorre());
        Assert.Equal(EstadoDeOcorrencia.NoAr, EstadoDoServico.SemVerificacao.Ocorre());
        Assert.Equal(EstadoDeOcorrencia.Lento, EstadoDoModulo.Lento.Ocorre());
        Assert.Equal(EstadoDeOcorrencia.ForaDoAr, EstadoDoServico.ForaDoAr.Ocorre());
        Assert.Equal(EstadoDeOcorrencia.ForaDoAr, EstadoDoModulo.ForaDoAr.Ocorre());
    }

    [Fact]
    public void RF_205_textos_e_dto_da_ocorrencia()
    {
        var volta = Ocorrencia.Detectar(Detectar(null, EstadoDeOcorrencia.ForaDoAr, T), TipoDoAlvo.Servico, "cache", "Cache", EstadoDeOcorrencia.NoAr, null, T.AddMinutes(22), "x")!;
        var dto = OcorrenciaDto.Da(volta);
        Assert.Equal(("x", "SERVICO", "cache", "FORA_DO_AR", "NO_AR", 22), (dto.Id, dto.Tipo, dto.Alvo, dto.De, dto.Para, dto.DuracaoMin));
        Assert.Equal(["SERVICO", "MODULO"], Enum.GetValues<TipoDoAlvo>().Select(t => t.Texto()));
        Assert.Equal(TipoDoAlvo.Modulo, TextosDaOcorrencia.TipoDe("MODULO"));
        Assert.Equal(EstadoDeOcorrencia.Lento, TextosDaOcorrencia.EstadoDe("LENTO"));
        Assert.Null(TextosDaOcorrencia.TipoDe("FILA"));
        Assert.Null(TextosDaOcorrencia.EstadoDe("COM_FALHAS"));
    }
}
