using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks.Cenario;
using AcaoAuditada = Boaz.Core.Commons.Auditoria.Domain.AcaoAuditada;
using EtapaDaExecucao = Boaz.Core.Commons.Auditoria.Domain.EtapaDaExecucao;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.UseCases;

/// <summary>
/// Auditoria dos 8 endereços que mudam a sessão, sempre pelo Executar (RN-272,
/// RN-273): "INICIADO" e "CONCLUIDO" com o mesmo traceId, a recusa em
/// "auditoriaErros" antes de sair, a falha do Redis como ERRO e nenhum segredo
/// nos registros.
/// </summary>
public class AuditoriaDaAutenticacaoTests
{
    const string Maria = "usr-ebfga-maria";
    const string Lucas = "usr-ebfga-lucas";
    const string Conta = "conta-ebfga";

    readonly Cenario c = new();

    // ── sucesso: INICIADO e CONCLUIDO (RN-272) ─────────────────────────────

    [Fact]
    public async Task RN_272_entrar_no_hub_inicia_sem_ninguem_e_conclui_com_a_maria_e_a_sessao()
    {
        var sessao = await c.MariaEntra();

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.EntrarHub);
        Assert.Equal((null, null, null), (iniciado.Quem, iniciado.ContaId, iniciado.Alvo));
        Assert.Equal((Maria, Conta, Claims(sessao).Sid), (concluido.Quem, concluido.ContaId, concluido.Alvo));
    }

    [Fact]
    public async Task RN_272_entrar_no_quest_conclui_com_o_participante_e_a_sessao()
    {
        var sessao = await c.LucasEntra(tipo: "RESPONSAVEL", parentesco: "Mãe");

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.EntrarQuest);
        Assert.Equal((null, null, null), (iniciado.Quem, iniciado.ContaId, iniciado.Alvo));
        Assert.Equal((Lucas, Conta, Claims(sessao).Sid), (concluido.Quem, concluido.ContaId, concluido.Alvo));
    }

    [Fact]
    public async Task RN_272_entrar_na_sala_conclui_com_o_anonimo_e_a_sala()
    {
        c.Mock.AbrirSala("482107", Conta, "MOD-05");

        var sessao = await c.Mock.EntrarNaSala.Executar(new EntradaNaSalaDto("482107"), Trace);

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.EntrarSala);
        Assert.Equal((null, null, null), (iniciado.Quem, iniciado.ContaId, iniciado.Alvo));
        Assert.Equal((Claims(sessao).Sub, Conta, "482107"), (concluido.Quem, concluido.ContaId, concluido.Alvo));
    }

    [Fact]
    public async Task RN_272_confirmar_a_senha_conclui_com_a_pessoa_e_a_sessao()
    {
        c.Mock.NovoUsuario("usr-ebfga-paulo", Conta, TipoUsuario.Staff, "paulo.ebfga@deboaz.com", "Xu@Xa182482",
            estado: EstadoUsuario.Pendente);
        var comprovante = c.Mock.NovaGeracao("usr-ebfga-paulo");

        var sessao = await c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace);

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.ConfirmarSenha);
        Assert.Equal((null, null, null), (iniciado.Quem, iniciado.ContaId, iniciado.Alvo));
        Assert.Equal(("usr-ebfga-paulo", Conta, Claims(sessao).Sid), (concluido.Quem, concluido.ContaId, concluido.Alvo));
    }

    [Fact]
    public async Task RN_272_renovar_inicia_sem_ninguem_e_conclui_com_a_sessao()
    {
        var sessao = await c.MariaEntra();
        Limpar();

        await c.Renovar(sessao);

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.RenovarSessao);
        Assert.Equal((null, null, null), (iniciado.Quem, iniciado.ContaId, iniciado.Alvo));
        Assert.Equal((Maria, Conta, Claims(sessao).Sid), (concluido.Quem, concluido.ContaId, concluido.Alvo));
    }

    [Fact]
    public async Task RN_272_sair_ja_inicia_com_a_maria_e_a_sessao_do_token()
    {
        var acesso = await c.Conferido(await c.MariaEntra());
        Limpar();

        await c.Mock.SairDoAparelho.Executar(acesso, Trace);

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.Sair);
        Assert.All([iniciado, concluido], r => Assert.Equal((Maria, Conta, acesso.Sid), (r.Quem, r.ContaId, r.Alvo)));
    }

    [Fact]
    public async Task RN_272_encerrar_uma_sessao_ja_inicia_com_o_sid_pedido()
    {
        var navegador = await c.MariaEntra();
        var acesso = await c.Conferido(await c.MariaEntra("APP"));
        Limpar();

        await c.Mock.EncerrarSessao.Executar(acesso, Claims(navegador).Sid, Trace);

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.EncerrarSessao);
        Assert.All([iniciado, concluido], r => Assert.Equal((Maria, Conta, Claims(navegador).Sid), (r.Quem, r.ContaId, r.Alvo)));
    }

    [Fact]
    public async Task RN_272_encerrar_todas_ja_inicia_com_o_proprio_usuario()
    {
        await c.MariaEntra();
        var acesso = await c.Conferido(await c.MariaEntra("APP"));
        Limpar();

        await c.Mock.EncerrarTodasAsSessoes.Executar(acesso, Trace);

        var (iniciado, concluido) = IniciadoEConcluido(AcaoAuditada.EncerrarSessoes);
        Assert.All([iniciado, concluido], r => Assert.Equal((Maria, Conta, Maria), (r.Quem, r.ContaId, r.Alvo)));
    }

    // ── recusas: ALERTA antes de sair (RN-273) ─────────────────────────────

    [Fact]
    public async Task RN_273_senha_errada_vira_alerta_sem_quem()
    {
        await Falha("ENTRADA_INVALIDA", () => c.MariaEntra(senha: "Senha#Errada1"));

        var erro = SoOIniciadoE(AcaoAuditada.EntrarHub, "ENTRADA_INVALIDA");
        Assert.Equal((null, null), (erro.Quem, erro.ContaId));
    }

    [Fact]
    public async Task RN_273_quinta_senha_errada_vira_alerta_de_bloqueio()
    {
        for (var i = 0; i < 4; i++) await Falha("ENTRADA_INVALIDA", () => c.MariaEntra(senha: "Senha#Errada1"));
        Limpar();

        await Falha("ENTRADA_BLOQUEADA", () => c.MariaEntra(senha: "Senha#Errada1"));

        Assert.Null(SoOIniciadoE(AcaoAuditada.EntrarHub, "ENTRADA_BLOQUEADA").Quem);
    }

    [Fact]
    public async Task RN_273_codigo_errado_vira_alerta_sem_quem()
    {
        await Falha("CODIGO_INVALIDO", () => c.LucasEntra("EBFGA-LUCAS-0000"));

        Assert.Null(SoOIniciadoE(AcaoAuditada.EntrarQuest, "CODIGO_INVALIDO").Quem);
    }

    [Fact]
    public async Task RN_273_comprovante_repetido_vira_alerta_sem_quem()
    {
        await Falha("COMPROVANTE_INVALIDO", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto("c7e1"), Trace));

        Assert.Null(SoOIniciadoE(AcaoAuditada.ConfirmarSenha, "COMPROVANTE_INVALIDO").Quem);
    }

    [Fact]
    public async Task RN_273_sessao_sem_uso_renovada_vira_alerta_sem_quem()
    {
        var sessao = await c.MariaEntra();
        c.Mock.Relogio.Avancar(TimeSpan.FromHours(9));
        Limpar();

        await Falha("SESSAO_TERMINADA", () => c.Renovar(sessao));

        var erro = SoOIniciadoE(AcaoAuditada.RenovarSessao, "SESSAO_TERMINADA");
        Assert.Equal((null, null), (erro.Quem, erro.ContaId));
    }

    [Fact]
    public async Task RN_273_sala_fechada_vira_alerta_sem_quem()
    {
        await Falha("SALA_FECHADA", () => c.Mock.EntrarNaSala.Executar(new EntradaNaSalaDto("000000"), Trace));

        Assert.Null(SoOIniciadoE(AcaoAuditada.EntrarSala, "SALA_FECHADA").Quem);
    }

    [Fact]
    public async Task RN_273_conta_suspensa_no_hub_vira_alerta_ja_com_a_maria()
    {
        c.Mock.MudarConta(Conta, EstadoConta.Suspensa);

        await Falha("ACESSO_INDISPONIVEL", () => c.MariaEntra());

        var erro = SoOIniciadoE(AcaoAuditada.EntrarHub, "ACESSO_INDISPONIVEL");
        Assert.Equal((Maria, Conta, null), (erro.Quem, erro.ContaId, erro.Alvo));
        Assert.Null(c.Mock.Auditoria[0].Quem);
    }

    [Fact]
    public async Task RN_273_conta_suspensa_no_quest_vira_alerta_ja_com_o_participante()
    {
        c.Mock.MudarConta(Conta, EstadoConta.Suspensa);

        await Falha("ACESSO_INDISPONIVEL", () => c.LucasEntra());

        var erro = SoOIniciadoE(AcaoAuditada.EntrarQuest, "ACESSO_INDISPONIVEL");
        Assert.Equal((Lucas, Conta), (erro.Quem, erro.ContaId));
    }

    [Fact]
    public async Task RN_273_sessao_de_outra_pessoa_vira_alerta_com_a_maria_e_o_sid()
    {
        var acesso = await c.Conferido(await c.MariaEntra());
        var joao = await c.Mock.EntrarNoHub.Executar(new EntradaNoHubDto("joao.eejsa@deboaz.com", "Xu@Xa182482", "APP"), Trace);
        Limpar();

        await Falha("SESSAO_NAO_ENCONTRADA", () => c.Mock.EncerrarSessao.Executar(acesso, Claims(joao).Sid, Trace));

        var erro = SoOIniciadoE(AcaoAuditada.EncerrarSessao, "SESSAO_NAO_ENCONTRADA");
        Assert.Equal((Maria, Conta, Claims(joao).Sid), (erro.Quem, erro.ContaId, erro.Alvo));
    }

    // ── falha inesperada: ERRO (RN-273) ────────────────────────────────────

    [Fact]
    public async Task RN_273_redis_fora_do_ar_na_entrada_vira_erro_inesperado()
    {
        var foraDoAr = new InvalidOperationException("redis fora do ar");
        c.Mock.FalhaDoRedis = foraDoAr;

        var falha = await Assert.ThrowsAsync<InvalidOperationException>(() => c.MariaEntra());

        Assert.Same(foraDoAr, falha);
        Assert.Equal(["INICIADO", "ERRO ERRO_INESPERADO"], c.Mock.Gravacoes);
        var erro = Assert.Single(c.Mock.Erros);
        Assert.Equal((RegistroDeErro.ErroInesperado, NivelDoErro.Erro, null, Trace), (erro.Codigo, erro.Nivel, erro.Quem, erro.TraceId));
    }

    [Fact]
    public async Task RN_273_redis_fora_do_ar_ao_sair_vira_erro_com_a_maria()
    {
        var acesso = await c.Conferido(await c.MariaEntra());
        Limpar();
        c.Mock.FalhaDoRedis = new InvalidOperationException("redis fora do ar");

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Mock.SairDoAparelho.Executar(acesso, Trace));

        var erro = Assert.Single(c.Mock.Erros);
        Assert.Equal((AcaoAuditada.Sair, NivelDoErro.Erro, Maria, acesso.Sid), (erro.Acao, erro.Nivel, erro.Quem, erro.Alvo));
    }

    // ── nenhum segredo (RN-273) ────────────────────────────────────────────

    [Fact]
    public async Task RN_273_nenhum_segredo_nos_registros()
    {
        var hub = await c.MariaEntra();
        var quest = await c.LucasEntra(tipo: "RESPONSAVEL", parentesco: "Mãe");
        var renovada = await c.Renovar(hub);
        c.Mock.NovoUsuario("usr-ebfga-paulo", Conta, TipoUsuario.Staff, "paulo.ebfga@deboaz.com", "Xu@Xa182482",
            estado: EstadoUsuario.Pendente);
        var comprovante = c.Mock.NovaGeracao("usr-ebfga-paulo");
        await c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace);
        await Falha("COMPROVANTE_INVALIDO", () => c.Mock.ConfirmarSenha.Executar(new ConfirmacaoDeSenhaDto(comprovante), Trace));
        await Falha("ENTRADA_INVALIDA", () => c.MariaEntra(senha: "Senha#Errada1"));
        await c.Mock.SairDoAparelho.Executar(await c.Conferido(renovada), Trace);

        var registros = string.Join('\n', c.Mock.Auditoria.Select(r => r.ToString()).Concat(c.Mock.Erros.Select(e => e.ToString())));
        string[] segredos =
        [
            SenhaDaMaria, "Senha#Errada1", CodigoDoLucas, comprovante,
            hub.TokenAcesso, quest.TokenAcesso, renovada.TokenAcesso,
            hub.TokenRenovacao, quest.TokenRenovacao, renovada.TokenRenovacao,
            Segredo(hub.TokenRenovacao), Segredo(renovada.TokenRenovacao),
        ];
        Assert.NotEmpty(c.Mock.Auditoria);
        Assert.All(segredos, s => Assert.DoesNotContain(s, registros, StringComparison.Ordinal));
    }

    // ── apoio ──────────────────────────────────────────────────────────────

    // Esquece o que a preparação gravou: só a execução conferida fica.
    void Limpar()
    {
        c.Mock.Auditoria.Clear();
        c.Mock.Erros.Clear();
        c.Mock.Gravacoes.Clear();
    }

    // Só o INICIADO e o CONCLUIDO da ação, nessa ordem, com o traceId recebido e sem erro.
    (RegistroDeAuditoria Iniciado, RegistroDeAuditoria Concluido) IniciadoEConcluido(AcaoAuditada acao)
    {
        Assert.Equal(["INICIADO", "CONCLUIDO"], c.Mock.Gravacoes.TakeLast(2));
        Assert.Empty(c.Mock.Erros);
        var (iniciado, concluido) = (c.Mock.Auditoria[^2], c.Mock.Auditoria[^1]);
        Assert.Equal((EtapaDaExecucao.Iniciado, EtapaDaExecucao.Concluido), (iniciado.Etapa, concluido.Etapa));
        Assert.All([iniciado, concluido], r => Assert.Equal((acao, Trace), (r.Acao, r.TraceId)));
        return (iniciado, concluido);
    }

    // Só o INICIADO da ação e o ALERTA com o código, gravado antes da recusa sair, com o mesmo traceId.
    RegistroDeErro SoOIniciadoE(AcaoAuditada acao, string codigo)
    {
        Assert.Equal(["INICIADO", $"ALERTA {codigo}"], c.Mock.Gravacoes);
        var iniciado = Assert.Single(c.Mock.Auditoria);
        var erro = Assert.Single(c.Mock.Erros);
        Assert.Equal((acao, Trace), (iniciado.Acao, iniciado.TraceId));
        Assert.Equal((acao, Trace, NivelDoErro.Alerta), (erro.Acao, erro.TraceId, erro.Nivel));
        return erro;
    }

    // A parte secreta do token de renovação, depois do sid.
    static string Segredo(string tokenRenovacao) => tokenRenovacao[(tokenRenovacao.IndexOf('.') + 1)..];
}
