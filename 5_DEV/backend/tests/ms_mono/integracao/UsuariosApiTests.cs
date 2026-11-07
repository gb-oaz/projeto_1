using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Endereços de Usuários (usuarios-contrato-api v03) com banco e cache reais:
// o preparo vem dos casos de uso, o endereço testado vai por HTTP.
public class UsuariosApiTests(Backend backend) : IClassFixture<Backend>
{
    readonly Preparo preparo = new(backend);

    // 5 letras novas a cada teste, para as contas não se encontrarem
    static string Sigla() => Preparo.SiglaSorteada();

    [Fact]
    public async Task RF_104_cria_a_conta_e_recusa_a_sigla_repetida()
    {
        using var api = backend.Cliente();
        var sigla = Sigla();
        var corpo = new
        {
            tipo = "INSTITUICAO",
            nome = "Escola Bento Freitas",
            documento = "12.345.678/0001-90",
            responsavel = "Gustavo Boaz de Moura Souza",
            telefone = "(51) 99999-0000",
            dominio = sigla.ToLowerInvariant(),
            sigla,
        };

        var criada = await api.Enviar("POST", "/contas", corpo);
        var repetida = await api.Enviar("POST", "/contas", corpo with { sigla = sigla.ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.Created, criada.Status);
        Assert.Equal($"conta-{sigla.ToLowerInvariant()}", criada.Texto("contaId"));
        Assert.Matches(@"^[A-Z][a-z][!@#$%&*][A-Z][a-z]+\d{6}$", criada.Texto("senha"));
        Assert.Equal((HttpStatusCode.Conflict, "SIGLA_EM_USO"), (repetida.Status, repetida.Codigo));
    }

    [Fact]
    public async Task Corpo_que_nao_e_json_responde_422()
    {
        using var api = backend.Cliente();
        var resposta = await api.Enviar("POST", "/contas", "{ não é json");
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DADOS_INVALIDOS"), (resposta.Status, resposta.Codigo));
    }

    [Fact]
    public async Task RN_179_sugere_a_sigla()
    {
        using var api = backend.Cliente();
        var resposta = await api.Enviar("GET", "/contas/sigla-sugerida?nome=Escola%20Bento%20Freitas%20Gon%C3%A7alves%20Ara%C3%BAjo");
        Assert.Equal("EBFGA", resposta.Texto("sigla"));
    }

    [Fact]
    public async Task RF_106_RF_109_cria_o_staff_que_gera_a_senha_e_entra()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();

        var criado = await api.Enviar("POST", "/usuarios", new { tipo = "STAFF", nome = "Maria Souza", nomeCurto = "maria" }, master.Token);
        var senha = await api.Enviar("POST", "/usuarios/gerar-senha",
            new { entrada = criado.Texto("login"), codigoLiberacao = criado.Texto("codigoLiberacao") });
        var confirmada = await api.Enviar("POST", "/auth/hub/confirmar-senha", new { comprovante = senha.Texto("comprovante") });

        Assert.Equal((HttpStatusCode.Created, "PENDENTE"), (criado.Status, criado.Texto("estado")));
        Assert.Equal(HttpStatusCode.OK, confirmada.Status);
        var lida = await api.Enviar("GET", $"/usuarios/{criado.Texto("id")}", token: master.Token);
        Assert.Equal(("ATIVO", "STAFF"), (lida.Texto("estado"), lida.Texto("tipo")));
        Assert.Matches(@"^\d{8}$", lida.Texto("matricula"));
    }

    [Fact]
    public async Task RF_109_codigo_errado_conta_e_a_quinta_bloqueia()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();
        var criado = await api.Enviar("POST", "/usuarios", new { tipo = "STAFF", nome = "Gustavo Souza", nomeCurto = "gustavo" }, master.Token);

        var respostas = new List<Resposta>();
        for (var i = 0; i < 5; i++)
        {
            respostas.Add(await api.Enviar("POST", "/usuarios/gerar-senha", new { entrada = criado.Texto("login"), codigoLiberacao = "000000" }));
        }

        Assert.All(respostas.Take(4), r => Assert.Equal("CODIGO_INVALIDO", r.Codigo));
        Assert.Equal((HttpStatusCode.TooManyRequests, "ENTRADA_BLOQUEADA"), (respostas[4].Status, respostas[4].Codigo));
        Assert.True(DateTimeOffset.Parse(respostas[4].Texto("tentarEm")) > DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task RF_105_lista_busca_edita_desativa_e_reativa()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        await preparo.Usuario(master, "joao");
        using var api = backend.Cliente();

        var lista = await api.Enviar("GET", "/usuarios?tipo=STAFF&busca=MARIA&estado=ATIVO&pagina=1", token: master.Token);
        var editado = await api.Enviar("PATCH", $"/usuarios/{maria.Id}", new { nomeCurto = "mariasouza" }, master.Token);
        var desativado = await api.Enviar("POST", $"/usuarios/{maria.Id}/desativar", token: master.Token);
        var deNovo = await api.Enviar("POST", $"/usuarios/{maria.Id}/desativar", token: master.Token);
        var reativado = await api.Enviar("POST", $"/usuarios/{maria.Id}/reativar", token: master.Token);

        Assert.Equal(1, lista.Corpo!["total"]!.GetValue<int>());
        Assert.Equal(maria.Id, lista.Corpo["itens"]![0]!["id"]!.GetValue<string>());
        Assert.EndsWith("mariasouza", editado.Texto("login").Split('.')[0]);
        Assert.Equal(HttpStatusCode.NoContent, desativado.Status);
        Assert.Equal((HttpStatusCode.Conflict, "ESTADO_IGUAL"), (deNovo.Status, deNovo.Codigo));
        Assert.Equal(HttpStatusCode.NoContent, reativado.Status);
        // as sessões da Maria terminaram com a desativação
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar("GET", "/auth/sessoes", token: maria.Token)).Status);
    }

    [Fact]
    public async Task RN_185_busca_sem_tipo_acha_staff_e_participante()
    {
        var sigla = Sigla();
        var (_, master) = await preparo.Conta(sigla);
        var maria = await preparo.Usuario(master, "maria");
        var (lucas, codigo) = await preparo.Participante(master, "lucas");
        using var api = backend.Cliente();

        var pelaEntrada = await api.Enviar("GET", $"/usuarios?busca={maria.Entrada.Split('@')[0]}", token: master.Token);
        var peloCodigo = await api.Enviar("GET", $"/usuarios?busca={codigo}", token: master.Token);
        var todos = await api.Enviar("GET", "/usuarios", token: master.Token);

        Assert.Equal((maria.Id, "STAFF"), (pelaEntrada.Corpo!["itens"]![0]!["id"]!.GetValue<string>(), pelaEntrada.Corpo["itens"]![0]!["tipo"]!.GetValue<string>()));
        Assert.Equal((lucas, "PARTICIPANTE"), (peloCodigo.Corpo!["itens"]![0]!["id"]!.GetValue<string>(), peloCodigo.Corpo["itens"]![0]!["tipo"]!.GetValue<string>()));
        Assert.Equal(2, todos.Corpo!["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Lista_com_pagina_que_nao_e_numero_responde_422()
    {
        var (_, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();
        var resposta = await api.Enviar("GET", "/usuarios?tipo=STAFF&pagina=um", token: master.Token);
        Assert.Equal(("DADOS_INVALIDOS", "pagina"), (resposta.Codigo, resposta.Texto("campo")));
    }

    [Fact]
    public async Task RF_110_RF_154_nova_geracao_de_senha_e_novo_codigo_de_acesso()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var paulo = await preparo.Usuario(master, "paulo");
        var (lucas, codigo) = await preparo.Participante(master, "lucas");
        using var api = backend.Cliente();

        var geracao = await api.Enviar("POST", $"/usuarios/{paulo.Id}/nova-geracao-senha", token: master.Token);
        var novoCodigo = await api.Enviar("POST", $"/usuarios/{lucas}/novo-codigo-acesso", token: master.Token);
        var semSenha = await api.Enviar("POST", $"/usuarios/{lucas}/nova-geracao-senha", token: master.Token);

        Assert.Equal(HttpStatusCode.Created, geracao.Status);
        Assert.Matches(@"^\d{6}$", geracao.Texto("codigoLiberacao"));
        Assert.NotEqual(codigo, novoCodigo.Texto("login"));
        Assert.Equal("SEM_SENHA", semSenha.Codigo);
        var entrada = await api.Enviar("POST", "/auth/hub/entrar", new { entrada = paulo.Entrada, senha = paulo.Senha, aparelho = "NAVEGADOR" });
        Assert.Equal("ENTRADA_INVALIDA", entrada.Codigo);
    }

    [Fact]
    public async Task RN_186_ultimo_acesso_da_mae_no_quest()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var (lucas, codigo) = await preparo.Participante(master, "lucas");
        using var api = backend.Cliente();

        var quest = await api.Enviar("POST", "/auth/quest/entrar",
            new { codigo, aparelho = "APP", quem = new { tipo = "RESPONSAVEL", parentesco = "Mãe" } });
        var lido = await api.Enviar("GET", $"/usuarios/{lucas}", token: master.Token);

        Assert.Equal(HttpStatusCode.OK, quest.Status);
        Assert.Equal("MAE", lido.Texto("ultimoAcessoQuem"));
        Assert.Equal("MAE", lido.Corpo!["responsaveis"]![0]!["parentesco"]!.GetValue<string>());
    }

    [Fact]
    public async Task RN_189_card_da_escola()
    {
        var (_, master) = await preparo.Conta(Sigla());
        await preparo.Usuario(master, "maria");
        await preparo.Participante(master, "lucas");
        using var api = backend.Cliente();
        await api.Enviar("POST", "/usuarios", new { tipo = "STAFF", nome = "Rui Alves", nomeCurto = "rui" }, master.Token);

        var card = await api.Enviar("GET", "/usuarios/card", token: master.Token);

        Assert.Equal((2, 1, 1, "usuários ativos"), (card.Corpo!["dado"]!.GetValue<int>(), card.Corpo["staff"]!.GetValue<int>(),
            card.Corpo["participantes"]!.GetValue<int>(), card.Texto("rotulo")));
        // o código do Rui vence daqui a 72 h, não hoje
        Assert.Equal(0, card.Corpo["selo"]!.GetValue<int>());
        Assert.Equal(5, card.Corpo["meses"]!.AsArray().Count);
    }

    [Fact]
    public async Task RF_114_tempo_de_sessao_da_conta()
    {
        var (contaId, master) = await preparo.Conta(Sigla());
        using var api = backend.Cliente();

        var padrao = await api.Enviar("GET", $"/contas/{contaId}/tempo-sessao", token: master.Token);
        var salvo = await api.Enviar("PUT", $"/contas/{contaId}/tempo-sessao",
            new { staffNavegador = "PT12H", staffApp = "P10D", participante = "P60D" }, master.Token);
        var fora = await api.Enviar("PUT", $"/contas/{contaId}/tempo-sessao",
            new { staffNavegador = "PT13H", staffApp = "P10D", participante = "P60D" }, master.Token);

        Assert.Equal(("PT8H", "P7D", "P30D"), (padrao.Texto("staffNavegador"), padrao.Texto("staffApp"), padrao.Texto("participante")));
        Assert.Equal("P10D", salvo.Texto("staffApp"));
        Assert.Equal(("FORA_DOS_LIMITES", "staffNavegador", "PT15M", "PT12H"),
            (fora.Codigo, fora.Texto("campo"), fora.Texto("minimo"), fora.Texto("maximo")));
    }

    [Fact]
    public async Task RF_111_ajuda_da_master_tres_por_dia()
    {
        using var api = backend.Cliente();
        var corpo = new { instituicao = "Escola Bento Freitas", contato = "(51) 99999-0000" };
        // o aparelho é o endereço de quem pede: começa sem envios neste banco
        var respostas = new List<Resposta>();
        for (var i = 0; i < 4; i++) respostas.Add(await api.Enviar("POST", "/contas/esqueci-senha-master", corpo));

        Assert.All(respostas.Take(3), r => Assert.Equal(HttpStatusCode.Accepted, r.Status));
        Assert.Equal((HttpStatusCode.TooManyRequests, "LIMITE_ATENDIMENTO"), (respostas[3].Status, respostas[3].Codigo));
    }

    [Fact]
    public async Task RF_113_time_deboaz_lista_le_e_muda_o_estado_da_conta()
    {
        var sigla = Sigla();
        var (contaId, master) = await preparo.Conta(sigla);
        var admin = await preparo.Admin();
        using var api = backend.Cliente();

        var lista = await api.Enviar("GET", $"/contas?busca=12345678000190&estado=ATIVA", token: admin.Token);
        var lida = await api.Enviar("GET", $"/contas/{contaId}", token: admin.Token);
        var doTime = await api.Enviar("GET", "/contas/conta-dboaz", token: admin.Token);
        var codigo = await api.Enviar("POST", $"/contas/{contaId}/codigo-master",
            new { documento = "12.345.678/0001-90", responsavel = "Gustavo Boaz de Moura Souza", telefone = "51999990000" }, admin.Token);
        var desativada = await api.Enviar("POST", $"/contas/{contaId}/estado", new { estado = "DESATIVADA" }, admin.Token);
        var masterDepois = await api.Enviar("GET", "/usuarios/card", token: master.Token);
        var masterProibido = await api.Enviar("GET", "/contas", token: (await preparo.Usuario(admin, "caio", "MEMBRO_TIME")).Token);

        Assert.Contains(lista.Corpo!["itens"]!.AsArray(), i => i!["id"]!.GetValue<string>() == contaId);
        Assert.DoesNotContain(lista.Corpo["itens"]!.AsArray(), i => i!["id"]!.GetValue<string>() == "conta-dboaz");
        Assert.Equal(($"{sigla.ToLowerInvariant()}@deboaz.com", null), (lida.Texto("loginMaster"), lida.Corpo!["ultimoPagamento"]?.ToString()));
        Assert.Equal("CONTA_INEXISTENTE", doTime.Codigo);
        Assert.Equal(HttpStatusCode.Created, codigo.Status);
        Assert.Equal("DESATIVADA", desativada.Texto("estado"));
        Assert.Equal(HttpStatusCode.Unauthorized, masterDepois.Status);
        // o membro do time começa sem módulos (RN-246)
        Assert.Equal((HttpStatusCode.Forbidden, "ACESSO_NEGADO"), (masterProibido.Status, masterProibido.Codigo));
        var conferencias = await backend.Banco.GetCollection<BsonDocument>("contas").Find(Builders<BsonDocument>.Filter.Eq("_id", contaId))
            .FirstAsync();
        Assert.Single(conferencias["conferencias"].AsBsonArray);
    }

    [Fact]
    public async Task RF_152_tempo_de_sessao_do_time()
    {
        var admin = await preparo.Admin();
        using var api = backend.Cliente();

        var salvo = await api.Enviar("PUT", "/time/tempo-sessao",
            new { membrosNavegador = "PT8H", membrosApp = "PT8H", mastersNavegador = "PT4H", mastersApp = "PT24H" }, admin.Token);
        var lido = await api.Enviar("GET", "/time/tempo-sessao", token: admin.Token);

        Assert.Equal(HttpStatusCode.OK, salvo.Status);
        Assert.Equal(("PT4H", "PT24H"), (lido.Texto("mastersNavegador"), lido.Texto("mastersApp")));
    }

    [Fact]
    public async Task Sem_token_ou_de_outra_conta()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var (_, outra) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        using var api = backend.Cliente();

        Assert.Equal("TOKEN_INVALIDO", (await api.Enviar("GET", "/usuarios?tipo=STAFF")).Codigo);
        Assert.Equal("USUARIO_INEXISTENTE", (await api.Enviar("GET", $"/usuarios/{maria.Id}", token: outra.Token)).Codigo);
        // a Maria não tem o módulo Usuários
        Assert.Equal("ACESSO_NEGADO", (await api.Enviar("GET", "/usuarios?tipo=STAFF", token: maria.Token)).Codigo);
    }
}
