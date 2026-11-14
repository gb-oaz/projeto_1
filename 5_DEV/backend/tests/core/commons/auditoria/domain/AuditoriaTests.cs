using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Commons.Tests.Auditoria.Domain;

/// <summary>Registro, erro, traceId e textos da auditoria compartilhada (RN-178, RN-270, RN-271; 18 RN-297).</summary>
public class AuditoriaTests
{
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
    public void Textos_das_acoes_auditadas() =>
        Assert.Equal(["CRIAR_CONTA", "CRIAR_USUARIO", "EDITAR_USUARIO", "NOVA_GERACAO_SENHA", "GERAR_SENHA", "NOVO_CODIGO_ACESSO",
            "DESATIVAR_USUARIO", "REATIVAR_USUARIO", "MUDAR_ESTADO_CONTA", "CODIGO_MASTER", "SALVAR_TEMPO_SESSAO", "EDITAR_PERMISSOES",
            "ENTRAR_HUB", "ENTRAR_QUEST", "ENTRAR_SALA", "CONFIRMAR_SENHA", "RENOVAR_SESSAO", "SAIR", "ENCERRAR_SESSAO", "ENCERRAR_SESSOES",
            "ESQUECI_SENHA_MASTER", "ENCERRAR_SESSOES_DO_USUARIO", "AVISAR_PERMISSOES_NOVAS", "AVISAR_CONTRATACAO_MUDOU",
            // 18 RN-297: as ações do SAC
            "ABRIR_CHAMADO", "ENVIAR_MENSAGEM", "ASSUMIR_CHAMADO", "MUDAR_STATUS", "REABRIR_CHAMADO", "ENCERRAR_CHAMADO", "AVALIAR_CHAMADO",
            "SALVAR_PRAZOS_SAC", "FECHAR_POR_PRAZO"],
            Enum.GetValues<AcaoAuditada>().Select(a => a.Texto()));
}
