using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>
/// Cada caso de uso de Usuários que muda estado, pelo Executar, auditado com o
/// traceId recebido (RN-178, RN-270, RN-271, RN-274): o sucesso grava "INICIADO" e
/// "CONCLUIDO"; a recusa, "INICIADO" e o ALERTA antes de sair; a falha
/// inesperada, "INICIADO" e o ERRO ERRO_INESPERADO.
/// </summary>
public class ExecucoesAuditadasTests
{
    static readonly TraceId Trace = new("trc-7f3a9c21");
    static readonly InvalidOperationException Queda = new("o banco caiu no meio da gravação");

    static UsuariosEmMemoria Novo() => new Cenario().Mock;

    /// <summary>"INICIADO" e "CONCLUIDO" com o mesmo traceId, a ação, a conta e quem; o alvo de cada etapa.</summary>
    static void Concluida(UsuariosEmMemoria mock, AcaoAuditada acao, string? conta, string? quem, string? alvoAoChegar, string? alvoNoFim)
    {
        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, mock.Gravacoes);
        Assert.All(mock.Auditoria, a => Assert.Equal((Trace, acao, quem), (a.TraceId, a.Acao, a.Quem)));
        var (iniciado, concluido) = (mock.Auditoria[0], mock.Auditoria[1]);
        Assert.Equal((EtapaDaExecucao.Iniciado, alvoAoChegar), (iniciado.Etapa, iniciado.Alvo));
        Assert.Equal((EtapaDaExecucao.Concluido, conta, alvoNoFim), (concluido.Etapa, concluido.ContaId, concluido.Alvo));
        Assert.Empty(mock.Erros);
    }

    /// <summary>Só o "INICIADO" e o ALERTA com o código, gravado antes de a recusa sair.</summary>
    static void Recusada(UsuariosEmMemoria mock, AcaoAuditada acao, string codigo, string? quem, string? alvo)
    {
        Assert.Equal(new[] { "INICIADO", $"ALERTA {codigo}" }, mock.Gravacoes);
        var erro = Assert.Single(mock.Erros);
        Assert.Equal((Trace, acao, codigo, NivelDoErro.Alerta, quem, alvo), (erro.TraceId, erro.Acao, erro.Codigo, erro.Nivel, erro.Quem, erro.Alvo));
        Assert.Equal(Trace, Assert.Single(mock.Auditoria).TraceId);
    }

    /// <summary>Só o "INICIADO" e o ERRO ERRO_INESPERADO; a exceção original sai.</summary>
    static async Task Falhou(UsuariosEmMemoria mock, AcaoAuditada acao, Func<Task> executar)
    {
        Assert.Same(Queda, await Assert.ThrowsAsync<InvalidOperationException>(executar));
        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, mock.Gravacoes);
        var erro = Assert.Single(mock.Erros);
        Assert.Equal((Trace, acao, RegistroDeErro.ErroInesperado, NivelDoErro.Erro), (erro.TraceId, erro.Acao, erro.Codigo, erro.Nivel));
    }

    /// <summary>Esquece o que o preparo do cenário gravou.</summary>
    static void Limpar(UsuariosEmMemoria mock)
    {
        mock.Gravacoes.Clear();
        mock.Auditoria.Clear();
        mock.Erros.Clear();
    }

    [Fact]
    public async Task RN_178_editar_usuario()
    {
        var mock = Novo();
        await mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo", Nome: "Paulo Mendes Lima"), Trace);
        Concluida(mock, AcaoAuditada.EditarUsuario, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-paulo", "usr-ebfga-paulo");

        mock = Novo();
        await Falha("NOME_CURTO_EM_USO", () => mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo", NomeCurto: "maria"), Trace));
        Recusada(mock, AcaoAuditada.EditarUsuario, "NOME_CURTO_EM_USO", "usr-ebfga-master", "usr-ebfga-paulo");

        mock = Novo();
        mock.FalhaAoGravarUsuario = Queda;
        await Falhou(mock, AcaoAuditada.EditarUsuario, () => mock.EditarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo", Nome: "Paulo"), Trace));
    }

    [Fact]
    public async Task RN_178_reativar_usuario()
    {
        var mock = Novo();
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        Limpar(mock);
        await mock.ReativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), Trace);
        Concluida(mock, AcaoAuditada.ReativarUsuario, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-paulo", "usr-ebfga-paulo");

        mock = Novo();
        await Falha("ESTADO_IGUAL", () => mock.ReativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), Trace));
        Recusada(mock, AcaoAuditada.ReativarUsuario, "ESTADO_IGUAL", "usr-ebfga-master", "usr-ebfga-paulo");

        mock = Novo();
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        Limpar(mock);
        mock.FalhaAoGravarUsuario = Queda;
        await Falhou(mock, AcaoAuditada.ReativarUsuario, () => mock.ReativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), Trace));
    }

    [Fact]
    public async Task RN_178_novo_codigo_de_acesso()
    {
        var mock = Novo();
        await mock.GerarNovoCodigoDeAcesso.Executar(new("usr-ebfga-master", "usr-ebfga-lucas"), Trace);
        Concluida(mock, AcaoAuditada.NovoCodigoAcesso, "conta-ebfga", "usr-ebfga-master", "usr-ebfga-lucas", "usr-ebfga-lucas");

        mock = Novo();
        await Falha("SO_PARTICIPANTE", () => mock.GerarNovoCodigoDeAcesso.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), Trace));
        Recusada(mock, AcaoAuditada.NovoCodigoAcesso, "SO_PARTICIPANTE", "usr-ebfga-master", "usr-ebfga-paulo");

        mock = Novo();
        mock.FalhaAoGravarUsuario = Queda;
        await Falhou(mock, AcaoAuditada.NovoCodigoAcesso, () => mock.GerarNovoCodigoDeAcesso.Executar(new("usr-ebfga-master", "usr-ebfga-lucas"), Trace));
    }

    /// <summary>O Gustavo pendente, com o código de liberação, sem o que o preparo gravou.</summary>
    static async Task<string> GustavoPendente(UsuariosEmMemoria mock)
    {
        var codigo = (await mock.CriarUsuario.Executar(new("usr-ebfga-master", "STAFF", "Gustavo Boaz de Moura Souza", "gustavo"), TraceId.Novo()))
            .CodigoLiberacao!;
        Limpar(mock);
        return codigo;
    }

    [Fact]
    public async Task RN_178_gerar_senha_sem_login()
    {
        const string entrada = "gustavo.ebfga@deboaz.com";
        var mock = Novo();
        await mock.GerarSenha.Executar(new(entrada, await GustavoPendente(mock)), Trace);
        Concluida(mock, AcaoAuditada.GerarSenha, "conta-ebfga", null, null, "usr-ebfga-gustavo");
        Assert.Null(mock.Auditoria[0].ContaId);

        mock = Novo();
        await GustavoPendente(mock);
        await Falha("CODIGO_INVALIDO", () => mock.GerarSenha.Executar(new(entrada, "000000"), Trace));
        Recusada(mock, AcaoAuditada.GerarSenha, "CODIGO_INVALIDO", null, null);

        mock = Novo();
        var codigo = await GustavoPendente(mock);
        mock.FalhaAoGravarUsuario = Queda;
        await Falhou(mock, AcaoAuditada.GerarSenha, () => mock.GerarSenha.Executar(new(entrada, codigo), Trace));
    }

    [Fact]
    public async Task RN_178_estado_da_conta()
    {
        var mock = Novo();
        await mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "SUSPENSA"), Trace);
        Concluida(mock, AcaoAuditada.MudarEstadoConta, "conta-ebfga", "usr-dboaz-caio", "conta-ebfga", "conta-ebfga");

        mock = Novo();
        await Falha("CONTA_INEXISTENTE", () => mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-zzzzz", "SUSPENSA"), Trace));
        Recusada(mock, AcaoAuditada.MudarEstadoConta, "CONTA_INEXISTENTE", "usr-dboaz-caio", "conta-zzzzz");

        mock = Novo();
        mock.FalhaAoGravarConta = Queda;
        await Falhou(mock, AcaoAuditada.MudarEstadoConta, () => mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "SUSPENSA"), Trace));
    }

    [Fact]
    public async Task RN_178_estado_igual_da_conta_tambem_conclui()
    {
        var mock = Novo();
        await mock.MudarEstadoDaConta.Executar(new("usr-dboaz-caio", "conta-ebfga", "ATIVA"), Trace);
        Concluida(mock, AcaoAuditada.MudarEstadoConta, "conta-ebfga", "usr-dboaz-caio", "conta-ebfga", "conta-ebfga");
    }

    static ConferenciaDaMasterDto Conferencia(string? responsavel = "Gustavo Boaz de Moura Souza") =>
        new("usr-dboaz-analima", "conta-ebfga", "12.345.678/0001-90", responsavel, "(51) 99999-0000", null);

    [Fact]
    public async Task RN_178_codigo_da_master_alvo_so_na_conclusao()
    {
        var mock = Novo();
        await mock.GerarCodigoDaMaster.Executar(Conferencia(), Trace);
        Concluida(mock, AcaoAuditada.CodigoMaster, "conta-ebfga", "usr-dboaz-analima", null, "usr-ebfga-master");

        mock = Novo();
        await Falha("CONFERENCIA_INCOMPLETA", () => mock.GerarCodigoDaMaster.Executar(Conferencia(responsavel: null), Trace));
        Recusada(mock, AcaoAuditada.CodigoMaster, "CONFERENCIA_INCOMPLETA", "usr-dboaz-analima", null);

        mock = Novo();
        mock.FalhaAoGravarUsuario = Queda;
        await Falhou(mock, AcaoAuditada.CodigoMaster, () => mock.GerarCodigoDaMaster.Executar(Conferencia(), Trace));
    }

    [Fact]
    public async Task RN_178_tempo_da_conta()
    {
        var mock = Novo();
        await mock.SalvarTempoDaConta.Executar(new("usr-ebfga-master", "conta-ebfga", "PT4H", "P14D", "P60D"), Trace);
        Concluida(mock, AcaoAuditada.SalvarTempoSessao, "conta-ebfga", "usr-ebfga-master", "conta-ebfga", "conta-ebfga");

        mock = Novo();
        await Falha("CONTA_INEXISTENTE", () => mock.SalvarTempoDaConta.Executar(new("usr-preis-pedro", "conta-ebfga", "PT8H", "P7D", "P30D"), Trace));
        Recusada(mock, AcaoAuditada.SalvarTempoSessao, "CONTA_INEXISTENTE", "usr-preis-pedro", "conta-ebfga");

        mock = Novo();
        mock.FalhaAoGravarConta = Queda;
        await Falhou(mock, AcaoAuditada.SalvarTempoSessao,
            () => mock.SalvarTempoDaConta.Executar(new("usr-ebfga-master", "conta-ebfga", "PT4H", "P14D", "P60D"), Trace));
    }

    [Fact]
    public async Task RN_178_tempo_do_time()
    {
        var mock = Novo();
        await mock.SalvarTempoDoTime.Executar(new("usr-dboaz-admin", "PT8H", "PT8H", "PT4H", "PT8H"), Trace);
        Concluida(mock, AcaoAuditada.SalvarTempoSessao, "conta-dboaz", "usr-dboaz-admin", "time", "time");

        mock = Novo();
        await Falha("ACESSO_NEGADO", () => mock.SalvarTempoDoTime.Executar(new("usr-ebfga-master", "PT8H", "PT8H", "PT8H", "PT8H"), Trace));
        Recusada(mock, AcaoAuditada.SalvarTempoSessao, "ACESSO_NEGADO", "usr-ebfga-master", "time");

        mock = Novo();
        mock.FalhaAoGravarConta = Queda;
        await Falhou(mock, AcaoAuditada.SalvarTempoSessao, () => mock.SalvarTempoDoTime.Executar(new("usr-dboaz-admin", "PT8H", "PT8H", "PT4H", "PT8H"), Trace));
    }

    [Fact]
    public async Task RN_274_pedido_de_ajuda_da_conta_master()
    {
        var mock = Novo();
        await mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "(51) 99999-0000", "aparelho-1"), Trace);
        Concluida(mock, AcaoAuditada.EsqueciSenhaMaster, null, null, null, mock.PedidosDeAjuda[0].Id);
        Assert.Equal(("ajuda-000001", (string?)null), (mock.PedidosDeAjuda[0].Id, mock.Auditoria[0].ContaId));
    }

    [Fact]
    public async Task RN_274_quarto_pedido_do_dia_e_alerta()
    {
        var mock = Novo();
        for (var i = 0; i < 3; i++) await mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "x", "aparelho-1"), TraceId.Novo());
        Limpar(mock);

        await Falha("LIMITE_ATENDIMENTO", () => mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "x", "aparelho-1"), Trace));
        Recusada(mock, AcaoAuditada.EsqueciSenhaMaster, "LIMITE_ATENDIMENTO", null, null);
        Assert.Null(mock.Erros[0].ContaId);
    }

    [Fact]
    public async Task RN_270_pedido_de_ajuda_com_o_banco_fora()
    {
        var mock = Novo();
        mock.FalhaAoPedirAjuda = Queda;
        await Falhou(mock, AcaoAuditada.EsqueciSenhaMaster, () => mock.PedirAjudaDaMaster.Executar(new("Escola Bento Freitas", "x", "aparelho-1"), Trace));
    }
}
