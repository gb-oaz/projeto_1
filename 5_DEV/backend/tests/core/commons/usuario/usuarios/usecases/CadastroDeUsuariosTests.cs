using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using static Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks.Esperado;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>Criar, ler e listar usuários (RF-106, RF-108, RN-163 a RN-165, RN-168, RN-170, RN-180, RN-184 a RN-188, RN-246).</summary>
public class CadastroDeUsuariosTests
{
    readonly UsuariosEmMemoria mock = new Cenario().Mock;

    static NovoUsuarioDto Staff(string quem, string nomeCurto, string? matricula = null) =>
        new(quem, "STAFF", "Fulana de Tal", nomeCurto, matricula);

    [Fact]
    public async Task RF_106_secretaria_cadastrada_pela_escola()
    {
        var criada = await mock.CriarUsuario.Executar(Staff("usr-ebfga-master", "mariana", "20269999") with { Nome = "Mariana Souza" }, TraceId.Novo());

        Assert.Equal(("usr-ebfga-mariana", "mariana.ebfga@deboaz.com", "PENDENTE"), (criada.Id, criada.Login, criada.Estado));
        Assert.Matches(@"^\d{6}$", criada.CodigoLiberacao);
        Assert.Equal(mock.Relogio.Agora.AddHours(72), criada.VenceEm);
        var guardada = mock.Usuario(criada.Id)!;
        Assert.Equal(("20269999", "usr-ebfga-master", null as string), (guardada.Matricula, guardada.CriadoPor, guardada.ResumoSenha));
        Assert.Contains(mock.Auditoria, a => a.Acao == AcaoAuditada.CriarUsuario && a.Alvo == criada.Id);
    }

    [Fact]
    public async Task RN_178_criar_usuario_auditado_do_comeco_ao_fim()
    {
        var traceId = new TraceId("trc-7f3a9c21");

        var criado = await mock.CriarUsuario.Executar(new("usr-ebfga-maria", "PARTICIPANTE", "Lia Souza", "lia"), traceId);

        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, mock.Gravacoes);
        Assert.All(mock.Auditoria, a => Assert.Equal(
            (traceId, AcaoAuditada.CriarUsuario, "conta-ebfga", "usr-ebfga-maria"),
            (a.TraceId, a.Acao, a.ContaId, a.Quem)));
        Assert.Equal(new[] { (EtapaDaExecucao.Iniciado, (string?)null), (EtapaDaExecucao.Concluido, criado.Id) },
            mock.Auditoria.Select(a => (a.Etapa, a.Alvo)));
        Assert.Equal("usr-ebfga-lia", criado.Id);
        Assert.Empty(mock.Erros);
    }

    [Fact]
    public async Task RN_270_criar_usuario_recusado_grava_o_alerta_antes_de_sair()
    {
        var traceId = TraceId.Novo();

        await Falha("NOME_CURTO_EM_USO", () => mock.CriarUsuario.Executar(Staff("usr-ebfga-master", "maria"), traceId));

        Assert.Equal(new[] { "INICIADO", "ALERTA NOME_CURTO_EM_USO" }, mock.Gravacoes);
        var erro = Assert.Single(mock.Erros);
        Assert.Equal((traceId, AcaoAuditada.CriarUsuario, "conta-ebfga", "usr-ebfga-master", NivelDoErro.Alerta),
            (erro.TraceId, erro.Acao, erro.ContaId, erro.Quem, erro.Nivel));
    }

    [Theory]
    [InlineData("usr-zzzzz", "bia", "ACESSO_NEGADO")]
    [InlineData("usr-ebfga-master", "Bia Lima", "DADOS_INVALIDOS")]
    public async Task RN_270_recusa_sem_alvo_previsto_vai_sem_alvo(string quem, string nomeCurto, string codigo)
    {
        await Falha(codigo, () => mock.CriarUsuario.Executar(Staff(quem, nomeCurto), TraceId.Novo()));

        Assert.Null(Assert.Single(mock.Auditoria).Alvo);
        Assert.Equal((codigo, NivelDoErro.Alerta, (string?)null), (Assert.Single(mock.Erros).Codigo, mock.Erros[0].Nivel, mock.Erros[0].Alvo));
    }

    [Fact]
    public async Task RN_270_criar_usuario_com_o_banco_fora_grava_o_erro()
    {
        var queda = new InvalidOperationException("o banco caiu no meio da gravação");
        mock.FalhaAoGravarUsuario = queda;

        Assert.Same(queda, await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.CriarUsuario.Executar(Staff("usr-ebfga-master", "bia"), TraceId.Novo())));

        Assert.Equal(new[] { "INICIADO", "ERRO ERRO_INESPERADO" }, mock.Gravacoes);
        Assert.Equal((RegistroDeErro.ErroInesperado, (string?)null), (mock.Erros[0].Codigo, mock.Erros[0].Alvo));
    }

    [Fact]
    public async Task RF_106_educador_autonomo_nao_cria_staff() =>
        await Falha("ACESSO_NEGADO", () => mock.CriarUsuario.Executar(Staff("usr-preis-pedro", "bia"), TraceId.Novo()));

    [Fact]
    public async Task RF_106_nome_curto_repetido() =>
        await Falha("NOME_CURTO_EM_USO", () => mock.CriarUsuario.Executar(Staff("usr-ebfga-master", "maria"), TraceId.Novo()));

    [Fact]
    public async Task RF_108_aluno_novo_com_dois_responsaveis()
    {
        var criado = await mock.CriarUsuario.Executar(new("usr-ebfga-master", "PARTICIPANTE", "Bruno Andrade", "bruno", null,
            [new ResponsavelDto("Carla Andrade", "mae"), new ResponsavelDto("Paulo Andrade", "PAI")]), TraceId.Novo());

        Assert.Matches(@"^EBFGA-BRUNO-\d{4}$", criado.Login);
        Assert.Equal(("ATIVO", null as string, null as DateTimeOffset?), (criado.Estado, criado.CodigoLiberacao, criado.VenceEm));
        var lido = await mock.LerUsuario.Executar(new("usr-ebfga-master", criado.Id));
        Assert.Equal([new ResponsavelDto("Carla Andrade", "MAE"), new ResponsavelDto("Paulo Andrade", "PAI")], lido.Responsaveis);
        Assert.Empty(mock.Geracoes(criado.Id));
    }

    [Fact]
    public async Task RN_164_so_o_administrador_cria_membros()
    {
        await Falha("ACESSO_NEGADO", () => mock.CriarUsuario.Executar(new("usr-ebfga-master", "MEMBRO_TIME", "Bia", "bia"), TraceId.Novo()));
        await Falha("ACESSO_NEGADO", () => mock.CriarUsuario.Executar(new("usr-dboaz-caio", "MEMBRO_TIME", "Bia", "bia"), TraceId.Novo()));
        await Falha("ACESSO_NEGADO", () => mock.CriarUsuario.Executar(new("usr-ebfga-lucas", "PARTICIPANTE", "Bia", "bia"), TraceId.Novo()));
    }

    [Fact]
    public async Task RN_246_administrador_cria_membro_do_time()
    {
        var criado = await mock.CriarUsuario.Executar(new("usr-dboaz-admin", "MEMBRO_TIME", "Gustavo Boaz", "gustavo"), TraceId.Novo());
        Assert.Equal(("usr-dboaz-gustavo", "gustavo.dboaz@deboaz.com", "PENDENTE"), (criado.Id, criado.Login, criado.Estado));
        Assert.Null(mock.Usuario(criado.Id)!.Matricula);
    }

    [Fact]
    public async Task RN_168_staff_que_administra_usuarios_cria_staff() =>
        Assert.Equal("usr-ebfga-bia", (await mock.CriarUsuario.Executar(Staff("usr-ebfga-joao", "bia"), TraceId.Novo())).Id);

    [Fact]
    public async Task RN_188_matricula_gerada_ou_da_escola()
    {
        var lucas = await mock.CriarUsuario.Executar(new("usr-ebfga-master", "PARTICIPANTE", "Lucas Lima", "lucasl"), TraceId.Novo());
        Assert.Matches(@"^\d{8}$", mock.Usuario(lucas.Id)!.Matricula);
        await Falha("MATRICULA_EM_USO", () => mock.CriarUsuario.Executar(Staff("usr-ebfga-master", "bia", "20260087"), TraceId.Novo()));
        Assert.Equal("matricula", (await Falha("DADOS_INVALIDOS",
            () => mock.CriarUsuario.Executar(new("usr-preis-pedro", "PARTICIPANTE", "Bia", "bia", "12345678"), TraceId.Novo()))).Campo);
        Assert.Null(mock.Usuario((await mock.CriarUsuario.Executar(new("usr-preis-pedro", "PARTICIPANTE", "Bia", "bia"), TraceId.Novo())).Id)!.Matricula);
    }

    [Fact]
    public async Task RN_184_codigo_unico_e_id_com_numero_se_ja_existe()
    {
        mock.Usuario("usr-ebfga-lucas")!.Editar(null, "lucas_antigo", null, null, mock.Relogio.Agora, () => "EBFGA-LUCAS_ANTIGO-0001");
        var novo = await mock.CriarUsuario.Executar(new("usr-ebfga-master", "PARTICIPANTE", "Lucas Novo", "lucas"), TraceId.Novo());
        Assert.Equal("usr-ebfga-lucas-2", novo.Id);
        Assert.Matches(@"^EBFGA-LUCAS-\d{4}$", novo.Login);
    }

    [Theory]
    [InlineData("usr-ebfga-master", "MASTER")]
    [InlineData("usr-preis-pedro", "MASTER")]
    [InlineData("usr-ebfga-maria", "STAFF")]
    [InlineData("usr-ebfga-lucas", "PARTICIPANTE")]
    [InlineData("usr-dboaz-caio", "MEMBRO_TIME")]
    public async Task RN_163_quem_e_usuario(string id, string tipo) =>
        Assert.Equal(tipo, (await mock.LerUsuario.Executar(new(id, id))).Tipo);

    [Fact]
    public async Task RN_170_leitura_sem_senha_nem_codigo()
    {
        var maria = await mock.LerUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-maria"));
        Assert.Equal(("maria.ebfga@deboaz.com", "20260087", "ATIVO", null as object), (maria.Login, maria.Matricula, maria.Estado, (object?)maria.Responsaveis));
        Assert.DoesNotContain(typeof(UsuarioLidoDto).GetProperties(), p => p.Name.Contains("Senha") || p.Name.Contains("Codigo"));
    }

    [Fact]
    public async Task RN_186_participante_com_quem_entrou()
    {
        var lucas = await mock.LerUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-lucas"));
        Assert.Equal((new DateTimeOffset(2026, 11, 1, 7, 55, 0, TimeSpan.Zero), "MAE"), (lucas.UltimoAcesso!.Value, lucas.UltimoAcessoQuem));
    }

    [Fact]
    public async Task RN_169_usuario_de_outra_conta_nao_existe()
    {
        await Falha("USUARIO_INEXISTENTE", () => mock.LerUsuario.Executar(new("usr-preis-pedro", "usr-ebfga-maria")));
        await Falha("USUARIO_INEXISTENTE", () => mock.LerUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-zzz")));
        await Falha("ACESSO_NEGADO", () => mock.LerUsuario.Executar(new("usr-ebfga-maria", "usr-ebfga-master")));
    }

    [Fact]
    public async Task RN_165_autonomo_so_com_participantes()
    {
        await Falha("ACESSO_NEGADO", () => mock.ListarUsuarios.Executar(new("usr-preis-pedro", "STAFF")));
        Assert.Equal(0, (await mock.ListarUsuarios.Executar(new("usr-preis-pedro", "PARTICIPANTE"))).Total);
    }

    [Theory]
    [InlineData("STAFF", "maria.ebfga", "Maria Souza")]
    [InlineData("STAFF", "20260087", "Maria Souza")]
    [InlineData("PARTICIPANTE", "EBFGA-LUCAS-0412", "Lucas Andrade")]
    public async Task RN_185_busca_nas_listas(string tipo, string termo, string nome) =>
        Assert.Equal(nome, Assert.Single((await mock.ListarUsuarios.Executar(new("usr-ebfga-master", tipo, termo))).Itens).Nome);

    [Theory]
    [InlineData("maria.ebfga", "Maria Souza")]
    [InlineData("20260087", "Maria Souza")]
    [InlineData("EBFGA-LUCAS-0412", "Lucas Andrade")]
    public async Task RN_185_busca_sem_tipo_em_todos_os_tipos_que_pode_listar(string termo, string nome) =>
        Assert.Equal(nome, Assert.Single((await mock.ListarUsuarios.Executar(new("usr-ebfga-master", null, termo))).Itens).Nome);

    [Fact]
    public async Task RN_185_sem_tipo_respeita_quem_pede()
    {
        var daMaster = await mock.ListarUsuarios.Executar(new("usr-ebfga-master", null));
        var staff = await mock.ListarUsuarios.Executar(new("usr-ebfga-master", "STAFF"));
        var participantes = await mock.ListarUsuarios.Executar(new("usr-ebfga-master", "PARTICIPANTE"));
        Assert.Equal(staff.Total + participantes.Total, daMaster.Total);
        Assert.Equal(daMaster.Itens.Select(u => u.Nome).Order(StringComparer.Ordinal), daMaster.Itens.Select(u => u.Nome));
        Assert.Equal(2, (await mock.ListarUsuarios.Executar(new("usr-dboaz-caio", null))).Total);
        Assert.Equal(["PARTICIPANTE", "STAFF"], daMaster.Itens.Select(u => u.Tipo).Distinct().Order());
        Assert.All((await mock.ListarUsuarios.Executar(new("usr-preis-pedro", null))).Itens, u => Assert.Equal("PARTICIPANTE", u.Tipo));
        await Falha("ACESSO_NEGADO", () => mock.ListarUsuarios.Executar(new("usr-ebfga-lucas", null)));
    }

    [Fact]
    public async Task RN_185_ordem_filtro_e_pagina()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-joao"), TraceId.Novo());
        var pagina = await mock.ListarUsuarios.Executar(new("usr-ebfga-master", "STAFF", null, "DESATIVADO", 1));
        Assert.Equal(["João Pereira", "Paulo Mendes"], pagina.Itens.Select(u => u.Nome));
        Assert.Equal((1, 2), (pagina.Pagina, pagina.Total));
        var todos = await mock.ListarUsuarios.Executar(new("usr-ebfga-master", "STAFF"));
        Assert.Equal(5, todos.Total);
        Assert.Equal(todos.Itens.Select(u => u.Nome).Order(StringComparer.Ordinal), todos.Itens.Select(u => u.Nome));
        Assert.Empty((await mock.ListarUsuarios.Executar(new("usr-ebfga-master", "STAFF", Pagina: 2))).Itens);
    }

    [Fact]
    public async Task RN_164_time_lista_membros()
    {
        Assert.Equal(2, (await mock.ListarUsuarios.Executar(new("usr-dboaz-caio", "MEMBRO_TIME"))).Total);
        await Falha("ACESSO_NEGADO", () => mock.ListarUsuarios.Executar(new("usr-ebfga-master", "MEMBRO_TIME")));
    }

    [Fact]
    public async Task RN_174_usuario_desativado_continua_na_lista()
    {
        await mock.DesativarUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"), TraceId.Novo());
        Assert.Equal("DESATIVADO", (await mock.LerUsuario.Executar(new("usr-ebfga-master", "usr-ebfga-paulo"))).Estado);
        Assert.Equal(EstadoUsuario.Desativado, mock.Usuario("usr-ebfga-paulo")!.Estado);
    }
}
