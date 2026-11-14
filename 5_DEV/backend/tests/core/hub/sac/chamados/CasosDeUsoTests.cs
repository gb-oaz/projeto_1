using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Tests.Sac.Mocks;
using static Boaz.Core.Hub.Tests.Sac.Mocks.Cenario;

namespace Boaz.Core.Hub.Tests.Sac.Chamados;

/// <summary>Os casos de uso dos chamados com as portas em memória (18 Cenários de API).</summary>
public sealed class CasosDeUsoTests
{
    readonly Cenario c = new();

    [Fact]
    public async Task RF_171_abrir_auditado_com_o_prazo()
    {
        var aberto = await c.Abrir.Executar(Corpo(), Master, Trace);
        Assert.Equal(("SAC-000042", "ABERTO", (string?)null), (aberto.Id, aberto.Status, aberto.Responsavel));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero), aberto.PrazoResposta);
        Assert.Equal(["INICIADO", "CONCLUIDO"], c.Mock.Auditoria.Select(a => a.Etapa == Boaz.Core.Commons.Auditoria.Domain.EtapaDaExecucao.Iniciado ? "INICIADO" : "CONCLUIDO"));
        Assert.Equal("SAC-000042", c.Mock.Auditoria[1].Alvo);
        Assert.Single(c.Mock.Avisos);
    }

    [Fact]
    public async Task RF_171_recusas()
    {
        await Recusa("ACESSO_NEGADO", () => c.Abrir.Executar(Corpo(), Caio, Trace));
        var doPedro = await c.Novo(Pedro);
        await Recusa("DADOS_INVALIDOS", () => c.Abrir.Executar(Corpo() with { RelacionadoA = doPedro }, Master, Trace));
        await Recusa("DADOS_INVALIDOS", () => c.Abrir.Executar(Corpo(anexos: "anx-000000"), Master, Trace));
        Assert.Equal("DADOS_INVALIDOS", c.Mock.Erros[^1].Codigo);
    }

    [Fact]
    public async Task RN_294_decimo_primeiro_e_reabrir_sem_limite()
    {
        var resolvido = await c.No("RESOLVIDO");
        for (var i = 0; i < 10; i++) await c.Novo();
        await Recusa("LIMITE_CHAMADOS", () => c.Abrir.Executar(Corpo(), Master, Trace));
        var reaberto = await c.Reabrir.Executar(resolvido, new("Ainda aparece o erro na 8B"), Master, Trace);
        Assert.Equal(("EM_ATENDIMENTO", "Caio Lima"), (reaberto.Status, reaberto.Responsavel));
    }

    [Fact]
    public async Task RF_172_lista_da_conta_e_RF_173_ler_marca_lido()
    {
        var aguardando = await c.No("AGUARDANDO_CLIENTE");
        await c.Novo(Pedro);
        var lista = await c.Listar.Executar(new(null, null, null, null, null, null, null, null), Master);
        var item = Assert.IsType<ResumoDaContaDto>(Assert.Single(lista.Itens));
        Assert.True(item.NovaResposta);
        var lido = await c.Ler.Executar(aguardando, Master);
        Assert.Contains(lido.Mensagens, m => m.Texto == "Qual a turma?");
        Assert.NotEmpty(lido.LinhaDoTempo);
        Assert.Null(lido.Atrasado);
        Assert.False(c.Mock.Chamados[aguardando].NaoLidoPelaConta);
    }

    [Fact]
    public async Task RN_288_conta_nao_ve_o_de_outra_nem_a_nota()
    {
        var doPedro = await c.Novo(Pedro);
        await Recusa("CHAMADO_INEXISTENTE", () => c.Ler.Executar(doPedro, Master));
        await Recusa("CHAMADO_INEXISTENTE", () => c.Ler.Executar("SAC-9", Master));
        await Recusa("ACESSO_NEGADO", () => c.Ler.Executar(doPedro, Maria));
        await c.Enviar.Executar(doPedro, new("Conferir o log", null, true), Caio, Trace);
        Assert.DoesNotContain((await c.Ler.Executar(doPedro, Pedro)).Mensagens, m => m.Interna);
        var doTime = await c.Ler.Executar(doPedro, Caio);
        Assert.Contains(doTime.Mensagens, m => m.Interna && m.Tipo == "NOTA_INTERNA");
        Assert.Equal(("HUB", false), (doTime.Origem, doTime.Atrasado));
    }

    [Fact]
    public async Task RF_174_fila_e_filtros_do_time()
    {
        var primeiro = await c.Novo();
        c.Relogio.Agora = c.Relogio.Agora.AddDays(1);
        var doPedro = await c.No("EM_ATENDIMENTO", Pedro);
        var fila = await c.Listar.Executar(new("EM_ANDAMENTO", null, null, null, 1, null, null, null), Caio);
        Assert.Equal(primeiro, Assert.IsType<ResumoDoTimeDto>(fila.Itens[0]).Id);
        Assert.Equal(2, fila.Total);
        Assert.True(Assert.IsType<ResumoDoTimeDto>(fila.Itens[0]).Atrasado);
        Assert.Equal([doPedro], (await c.Listar.Executar(new(null, null, null, null, null, "eu", null, null), Caio)).Itens.Select(i => ((ResumoDoTimeDto)i).Id));
        Assert.Equal([primeiro], (await c.Listar.Executar(new(null, null, null, null, null, "nenhum", "true", null), Caio)).Itens.Select(i => ((ResumoDoTimeDto)i).Id));
        Assert.Equal(1, (await c.Listar.Executar(new(null, "EM_ATENDIMENTO", "MODULO_COM_PROBLEMA", "PREIS", null, null, "false", null), Caio)).Total);
        Assert.Equal(0, (await c.Listar.Executar(new("ENCERRADOS", null, null, null, null, null, null, "5"), Caio)).Total);
    }

    [Theory]
    [InlineData("SITUACAO", null, null, null, null, "situacao")]
    [InlineData(null, "X", null, null, null, "status")]
    [InlineData(null, null, "X", null, null, "categoria")]
    [InlineData(null, null, null, "todos", null, "responsavel")]
    [InlineData(null, null, null, null, "sim", "atrasado")]
    public async Task RF_174_filtros_invalidos(string? situacao, string? status, string? categoria, string? responsavel, string? atrasado, string campo)
    {
        var falha = await Recusa("DADOS_INVALIDOS", () => c.Listar.Executar(new(situacao, status, categoria, null, null, responsavel, atrasado, null), Caio));
        Assert.Equal(campo, falha.Campo);
    }

    [Theory]
    [InlineData(null, null, "eu", null, null, "responsavel")]
    [InlineData(null, null, null, "true", null, "atrasado")]
    [InlineData(null, null, null, null, "2", "notaMaxima")]
    [InlineData(0, null, null, null, null, "pagina")]
    public async Task RF_174_filtro_do_time_pela_conta(int? pagina, string? _, string? responsavel, string? atrasado, string? nota, string campo) =>
        Assert.Equal(campo, (await Recusa("DADOS_INVALIDOS", () => c.Listar.Executar(new(null, null, null, null, pagina, responsavel, atrasado, nota), Master))).Campo);

    [Fact]
    public async Task RF_174_nota_maxima_invalida_e_staff() =>
        await Recusa("DADOS_INVALIDOS", () => c.Listar.Executar(new(null, null, null, null, null, null, null, "9"), Caio));

    [Fact]
    public async Task RF_173_mensagens_da_conta_e_do_time()
    {
        var id = await c.No("AGUARDANDO_CLIENTE");
        var anexo = (await c.EnviarAnexo.Executar(new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1]), "erro.png", Master)).Id;
        var enviada = await c.Enviar.Executar(id, new("Turma 7A", [anexo], false), Master, Trace);
        Assert.Equal(("Escola Bento Freitas", "EM_ATENDIMENTO"), (enviada.Autor, enviada.StatusChamado));
        Assert.Equal(id, c.Mock.Anexos[anexo].ChamadoId);
        await Recusa("ACESSO_NEGADO", () => c.Enviar.Executar(id, new("Nota", null, true), Master, Trace));
        await Recusa("DADOS_INVALIDOS", () => c.Enviar.Executar(id, new("Turma", [anexo], false), Master, Trace));
        var doTime = await c.Enviar.Executar(id, new("Pode tentar?", null, false), Caio, Trace);
        Assert.StartsWith("msg-", doTime.Id);
    }

    [Fact]
    public async Task RF_175_RF_176_RF_177_RF_178_pelo_caminho()
    {
        var id = await c.Novo();
        await Recusa("ACESSO_NEGADO", () => c.Assumir.Executar(id, new(null), Master, Trace));
        await c.Assumir.Executar(id, new(null), Ana, Trace);
        var ja = await Recusa("JA_ASSUMIDO", () => c.Assumir.Executar(id, new(null), Caio, Trace));
        Assert.Equal("Ana Lima", ja.Responsavel);
        Assert.Equal("Caio Lima", (await c.Assumir.Executar(id, new(true), Caio, Trace)).Responsavel);
        await Recusa("ACESSO_NEGADO", () => c.Status.Executar(id, new("RESOLVIDO", "x"), Master, Trace));
        var resolvido = await c.Status.Executar(id, new("RESOLVIDO", "Corrigido."), Caio, Trace);
        Assert.NotNull(resolvido.ResolvidoEm);
        await Recusa("ACESSO_NEGADO", () => c.Avaliar.Executar(id, new(5, null), Caio, Trace));
        await Recusa("ACESSO_NEGADO", () => c.Reabrir.Executar(id, new("x"), Caio, Trace));
        var avaliado = await c.Avaliar.Executar(id, new(5, "Resolvido em 4 h, obrigado"), Master, Trace);
        Assert.Equal(("FECHADO", 5), (avaliado.Status, avaliado.Avaliacao.Nota));
        await Recusa("JA_AVALIADO", () => c.Avaliar.Executar(id, new(4, null), Master, Trace));
        var outro = await c.No("EM_ATENDIMENTO");
        await Recusa("ACESSO_NEGADO", () => c.Encerrar.Executar(outro, Caio, Trace));
        Assert.Equal("FECHADO", (await c.Encerrar.Executar(outro, Master, Trace)).Status);
    }

    [Fact]
    public async Task Gravacao_rele_uma_vez_e_desiste_na_segunda()
    {
        var id = await c.Novo();
        c.Mock.ConflitosAntesDeGravar = 1;
        Assert.Equal("EM_ATENDIMENTO", (await c.Assumir.Executar(id, new(null), Caio, Trace)).Status);
        c.Mock.ConflitosAntesDeGravar = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Status.Executar(id, new("RESOLVIDO", "x"), Caio, Trace));
        Assert.Equal("ERRO_INESPERADO", c.Mock.Erros[^1].Codigo);
    }

    [Fact]
    public async Task RN_290_RN_297_prazos_pelo_agendamento()
    {
        var aguardando = await c.No("AGUARDANDO_CLIENTE");
        c.Relogio.Agora = c.Relogio.Agora.AddDays(7).AddMinutes(1);
        Assert.Equal(1, await c.Prazos.Executar());
        Assert.Equal(StatusDoChamado.Resolvido, c.Mock.Chamados[aguardando].Status);
        var registro = c.Mock.Auditoria.Last();
        Assert.Equal((Boaz.Core.Commons.Auditoria.Domain.AcaoAuditada.FecharPorPrazo, (string?)null, aguardando),
            (registro.Acao, registro.Quem, registro.Alvo));
        var lido = await c.Ler.Executar(aguardando, Master);
        Assert.Contains(lido.LinhaDoTempo, e => e.Autor.Tipo == "SISTEMA");
        Assert.Equal(0, await c.Prazos.Executar());
    }

    [Fact]
    public async Task RNF_230_ao_vivo_a_partir_da_ultima()
    {
        var id = await c.No("AGUARDANDO_CLIENTE");
        await Recusa("CHAMADO_INEXISTENTE", () => c.AoVivo.Abrir(id, null, Pedro));
        var conversa = await c.AoVivo.Abrir(id, null, Master);
        var (nada, _) = await c.AoVivo.Proximas(conversa, TimeSpan.Zero);
        Assert.Empty(nada);
        var primeira = await c.Enviar.Executar(id, new("Primeira", null, false), Caio, Trace);
        await c.Enviar.Executar(id, new("Nota", null, true), Caio, Trace);
        c.Mock.AvisoNaEspera = true;
        var (chegou, seguinte) = await c.AoVivo.Proximas(conversa, TimeSpan.Zero);
        Assert.Equal([primeira.Id], chegou.Select(m => m.Id));
        Assert.Empty((await c.AoVivo.Proximas(seguinte, TimeSpan.Zero)).Mensagens);
        var reconexao = await c.AoVivo.Abrir(id, primeira.Id, Caio);
        Assert.Equal(["NOTA_INTERNA"], (await c.AoVivo.Proximas(reconexao, TimeSpan.Zero)).Mensagens.Select(m => m.Tipo));
    }

    [Fact]
    public async Task RF_179_pedido_de_senha_vira_chamado()
    {
        var numero = await c.PedidoDeSenha.Executar("10.0.0.1", "Escola Bento Freitas", "(51) 99999-0000", c.Relogio.Agora);
        Assert.Equal("ajuda-000042", numero.Pedido());
        var item = Assert.IsType<ResumoDoTimeDto>(Assert.Single((await c.Listar.Executar(new(null, null, "SENHA_CONTA_MASTER", null, null, null, null, null), Caio)).Itens));
        Assert.Equal(("ENTRAR_SEM_LOGIN", "ajuda-000042", (ContaDto?)null, "(51) 99999-0000"), (item.Origem, item.Pedido, item.Conta, item.Contato));
        await Recusa("SEM_CONVERSA", () => c.Enviar.Executar(numero.Valor, new("Olá", null, false), Caio, Trace));
        await c.Status.Executar(numero.Valor, new("RESOLVIDO", "Código enviado ao contato"), Caio, Trace);
        c.Relogio.Agora = c.Relogio.Agora.AddDays(4);
        Assert.False((await c.Ler.Executar(numero.Valor, Caio)).Atrasado);
    }
}
