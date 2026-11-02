using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Domain;

/// <summary>A sessão no servidor (RN-224 a RN-226, RN-247, RNF-213).</summary>
public class SessaoTests
{
    static readonly DateTimeOffset Nove = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    static (Sessao Sessao, TokenDeRenovacao Renovacao) Abrir(Frente frente = Frente.Hub, TimeSpan? tempo = null) =>
        Sessao.Abrir("usr-ebfga-maria", "conta-ebfga", "EBFGA", frente, Aparelho.Navegador, null, null, 7,
            tempo ?? TimeSpan.FromHours(8), Nove);

    [Fact]
    public void Abre_com_sid_aleatorio_e_guarda_so_o_resumo_da_renovacao()
    {
        var (sessao, renovacao) = Abrir();
        Assert.Matches("^[0-9a-f]{32}$", sessao.Sid);
        Assert.Equal(sessao.Sid, renovacao.Sid);
        Assert.Equal(renovacao.Resumo(), sessao.ResumoRenovacao);
        Assert.DoesNotContain(renovacao.Segredo, sessao.ResumoRenovacao);
        Assert.Equal((Nove, Nove, 7), (sessao.Inicio, sessao.UltimaAcao, sessao.Pv));
        Assert.NotEqual(Abrir().Sessao.Sid, sessao.Sid);
    }

    [Fact]
    public void Termina_depois_do_tempo_sem_uso_e_cada_pedido_refaz_o_tempo()
    {
        var (sessao, _) = Abrir();
        Assert.Equal(Nove.AddHours(8), sessao.ExpiraEm);
        Assert.True(sessao.EstaAtiva(Nove.AddHours(7)));
        Assert.False(sessao.EstaAtiva(Nove.AddHours(8)));

        sessao.Tocar(Nove.AddHours(7));
        Assert.True(sessao.EstaAtiva(Nove.AddHours(14)));
        Assert.Equal(Nove.AddHours(15), sessao.ExpiraEm);
    }

    [Fact]
    public void Sessao_anonima_nao_tem_tempo_sem_uso()
    {
        var (sessao, _) = Sessao.Abrir("anon-1", "conta-ebfga", "EBFGA", Frente.Sala, Aparelho.Navegador, null, "482107", 0, null, Nove);
        Assert.Null(sessao.ExpiraEm);
        Assert.True(sessao.EstaAtiva(Nove.AddYears(1)));
    }

    [Fact]
    public void Renovar_troca_o_token_e_o_anterior_deixa_de_conferir()
    {
        var (sessao, primeira) = Abrir();
        Assert.True(sessao.RenovacaoConfere(primeira));

        var segunda = sessao.Renovar(8, Nove.AddMinutes(20));
        Assert.False(sessao.RenovacaoConfere(primeira));
        Assert.True(sessao.RenovacaoConfere(segunda));
        Assert.Equal((8, Nove.AddMinutes(20)), (sessao.Pv, sessao.UltimaAcao));
    }

    [Fact]
    public void Renovacao_de_outra_sessao_nao_confere()
    {
        var (sessao, _) = Abrir();
        var (_, deOutra) = Abrir();
        Assert.False(sessao.RenovacaoConfere(deOutra));
    }

    [Fact]
    public void Permissoes_novas_mudam_o_pv()
    {
        var (sessao, _) = Abrir();
        sessao.AtualizarPv(9);
        Assert.Equal(9, sessao.Pv);
    }

    [Fact]
    public void A_sexta_sessao_do_hub_encerra_a_de_ultima_acao_mais_antiga()
    {
        var sessoes = Enumerable.Range(0, 5).Select(i =>
        {
            var s = Abrir().Sessao;
            s.Tocar(Nove.AddMinutes(10 - i));
            return s;
        }).ToList();
        Assert.Equal(sessoes[4], Sessao.MaisAntigaAlemDoLimite(sessoes));
        Assert.Null(Sessao.MaisAntigaAlemDoLimite(sessoes.Take(4)));
    }

    [Fact]
    public void O_quest_nao_tem_limite()
    {
        var doQuest = Enumerable.Range(0, 6).Select(_ => Abrir(Frente.Quest).Sessao);
        Assert.Null(Sessao.MaisAntigaAlemDoLimite(doQuest));
    }
}
