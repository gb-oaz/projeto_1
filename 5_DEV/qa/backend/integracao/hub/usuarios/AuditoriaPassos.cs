using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos da auditoria de 1_Requisitos/17_modulo-usuario.md: RN-178 (o que muda
// estado grava "INICIADO" e "CONCLUIDO"), RN-270 ("auditoriaErros", nivel
// ALERTA ou ERRO), RN-271 (um traceId por execução, no cabeçalho X-Trace-Id)
// e RN-274 (o pedido de ajuda da conta master). As frases que valem também
// para a Autenticação ficam em suporte/PassosDeAuditoria.cs.
//
// Os registros são lidos nas coleções "auditoria" e "auditoriaErros" do banco
// do backend (suporte/Auditoria.cs, a exceção da README, seção 6). Cada pedido
// do Quando prepara antes tudo o que cita (a Maria com o MOD-03, o alvo, a
// sessão já renovada) e só então marca o que as coleções já tinham: o que o
// preparo gravou não conta como registro "ganho".
[Binding]
public sealed class AuditoriaPassos(Mundo mundo, Execucoes execucoes)
{
    // o alvo que o registro deve trazer e o pedido, para repeti-lo ("já desativado")
    string alvo = "";
    (string Metodo, string Caminho, string? Corpo) pedido;
    // a versão das permissões do João antes do PUT da conta master (RN-178)
    int versaoAntes;

    BsonDocument? registro { get => execucoes.Registro; set => execucoes.Registro = value; }
    BsonDocument? erro { get => execucoes.Erro; set => execucoes.Erro = value; }

    // ── pedidos auditados ──────────────────────────────────────────────────
    [When(@"^a Maria envia POST /usuarios do participante (Lucas|Pedro)$")]
    [When(@"^depois envia POST /usuarios do participante (Lucas|Pedro)$")]
    public async Task CriaParticipante(string nome) =>
        await Executar(await MariaPronta(), "POST", "/usuarios", Participante(nome));

    [When(@"^a Maria envia POST (/usuarios/usr-ebfga-(ana|paulo)/(?:nova-geracao-senha|desativar))$")]
    public async Task AgeSobre(string caminho, string chave)
    {
        var maria = await MariaPronta();
        alvo = (await mundo.StaffCriadoPor(chave, maria)).Id;
        await Executar(maria, "POST", await mundo.Traduzir(caminho), null);
    }

    // o reativar do Paulo, desativado pela Maria antes da marca; a edição e o
    // novo código de acesso do Lucas, criado pela Maria (abaixo dela); o
    // tempo de sessão da escola, com a aba Configuração (MOD-03 · C) que a
    // MariaPronta dá. O alvo é o usuário, ou a conta pedida no tempo (RN-178)
    [When(@"^a Maria envia (POST|PATCH|PUT) (/usuarios/usr-ebfga-paulo/reativar|/usuarios/usr-ebfga-lucas|/usuarios/usr-ebfga-lucas/novo-codigo-acesso|/contas/conta-ebfga/tempo-sessao)$")]
    public async Task AgeSobreOutros(string metodo, string caminho)
    {
        var maria = await MariaPronta();
        string? corpo = null;
        if (caminho.Contains("paulo", StringComparison.Ordinal))
        {
            var paulo = await mundo.StaffCriadoPor("paulo", maria);
            alvo = paulo.Id;
            Exigir(await mundo.EnviarComo(maria, "POST", $"/usuarios/{paulo.Id}/desativar"), 204, "POST /usuarios/{id}/desativar do Paulo");
        }
        else if (caminho.Contains("lucas", StringComparison.Ordinal))
        {
            alvo = await LucasDaMaria(maria);
            if (metodo == "PATCH") corpo = """
                {
                  "matricula": "2026-0413"
                }
                """;
        }
        else
        {
            alvo = await mundo.Conta();
            corpo = """
                {
                  "staffNavegador": "PT4H",
                  "staffApp": "P7D",
                  "participante": "P30D"
                }
                """;
        }
        await Executar(maria, metodo, await mundo.Traduzir(caminho), corpo);
    }

    // a Maria salva as permissões do João na versão em que estão
    [When(@"^a Maria envia PUT (/usuarios/usr-ebfga-joao/permissoes)$")]
    public async Task EditaPermissoes(string caminho)
    {
        var maria = await MariaPronta();
        var joao = await mundo.StaffCriadoPor("joao", maria);
        alvo = joao.Id;
        var versao = (await mundo.Permissoes(joao))["versao"]!.GetValue<int>().ToString();
        await Executar(maria, "PUT", await mundo.Traduzir(caminho), """
            {
              "mods": {}
            }
            """, versao);
    }

    // a conta master concede ao João a Utilização do Quiz (MOD-05, de staff,
    // RN-166, RN-241, RN-252), ou a tira se ele já a tem: só com mudança há
    // versão nova em "registrosPermissao" (RN-254, RN-256), além da
    // "auditoria" (RN-178). Nesta feature a EBFGA não tem o MOD-05: o passo
    // o contrata antes, como o Mundo.Definir faz, e fora do Executar, para
    // não cair entre o antes e o depois da auditoria
    [When(@"^a conta master envia PUT (/usuarios/usr-ebfga-joao/permissoes)$")]
    public async Task MasterEditaPermissoes(string caminho)
    {
        var master = await mundo.Master();
        var joao = await mundo.StaffCriadoPor("joao", master);
        alvo = joao.Id;
        await mundo.Contratar("MOD-05");
        var atuais = await mundo.Permissoes(joao);
        versaoAntes = atuais["versao"]!.GetValue<int>();
        var mods = (JsonObject)atuais["mods"]!.DeepClone();
        if (!mods.Remove("MOD-05")) mods["MOD-05"] = "U";
        await Executar(master, "PUT", await mundo.Traduzir(caminho),
            new JsonObject { ["mods"] = mods }.ToJsonString(), versaoAntes.ToString());
    }

    [When(@"^envia POST /contas, sem login, com a sigla ""(\w+)""$")]
    public async Task ContaSemLogin(string sigla)
    {
        await mundo.Conta();
        await Executar(null, "POST", "/contas", ContaDaSigla(sigla));
    }

    [When(@"^a Maria envia POST /contas com a sigla ""(\w+)""$")]
    public async Task MariaCriaConta(string sigla) =>
        await Executar(await MariaPronta(), "POST", "/contas", ContaDaSigla(sigla));

    // leitura (RN-178, RN-270): os GET da Maria; o do histórico de permissões
    // fica em AutorizacaoPassos.cs
    [When(@"^a Maria envia GET (/contas/.+|/usuarios\?\S+|/usuarios/[^/\s]+)$")]
    public async Task Le(string caminho)
    {
        var maria = await MariaPronta();
        var real = await mundo.Traduzir(caminho);
        if (real.IndexOf("?nome=", StringComparison.Ordinal) is var i and >= 0)
        {
            real = real[..(i + 6)] + Uri.EscapeDataString(real[(i + 6)..]);
        }
        await Executar(maria, "GET", real, null);
    }

    // ── situações do desativar (RN-270) ────────────────────────────────────
    // a frase vem depois do pedido: o primeiro desativar deixa o Paulo
    // desativado, e o mesmo pedido é repetido, agora com o Paulo já desativado
    [When(@"^o Paulo já está desativado$")]
    public async Task PauloJaDesativado()
    {
        var paulo = await mundo.StaffCriadoPor("paulo", await mundo.Maria());
        if (execucoes.Resultados[^1].Status is < 200 or > 299)
        {
            Exigir(await mundo.EnviarComo(await mundo.Master(), "POST", $"/usuarios/{paulo.Id}/desativar"), 200, "POST /usuarios/{id}/desativar do Paulo");
        }
        await Executar(await mundo.Maria(), pedido.Metodo, pedido.Caminho, pedido.Corpo);
    }

    // vale para o desativar e para a "falha inesperada" do POST /usuarios do Lucas
    [When(@"^o banco cai no meio da gravação$")]
    public static void BancoCai() => throw new PendingStepException(
        "derrubar o banco no meio de um pedido não dá para provocar de fora, numa suíte caixa-preta: " +
        "o 500 ERRO_INESPERADO com nivel ERRO fica nos testes do backend, com a porta de saída falhando");

    // ── respostas ──────────────────────────────────────────────────────────
    [When(@"^a resposta é 201 com o contaId ""(.+)""$")]
    public void ContaCriada(string exemplo)
    {
        Respostas.Confere(mundo.Ultima, 201);
        var contaId = Texto(mundo.Ultima, "contaId");
        mundo.Apelidos[exemplo] = () => Task.FromResult(contaId);
    }

    [When(@"^a resposta é (\d{3}) ([A-Z_]+)$")]
    public void Recusa(int status, string codigo) => Respostas.Confere(mundo.Ultima, status, codigo);

    // ── auditoria (RN-178) ─────────────────────────────────────────────────
    [Then(@"^a coleção ""auditoria"" ganha 2 registros com o mesmo traceId$")]
    public async Task DoisRegistros()
    {
        var novos = await execucoes.Proprios();
        Assert.True(novos.Count == 2, $"esperava 2 registros novos em \"auditoria\", vieram {novos.Count}: {Auditoria.Resumo(novos)}");
        Auditoria.MesmoTraceId(novos);
    }

    [Then(@"^o primeiro com a etapa ""(\w+)"", o segundo com a etapa ""(\w+)""$")]
    public async Task Etapas(string primeira, string segunda)
    {
        var novos = await execucoes.Proprios();
        Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
    }

    [Then(@"^os dois com a Maria, a ação ""(\w+)"", o alvo e a hora, guardados até 5 anos depois$")]
    public async Task ComQuemAcaoAlvoEHora(string acao)
    {
        var maria = await mundo.Maria();
        // o alvo já existe antes do pedido (RN-178): o CRIAR_USUARIO, sem
        // alvo no INICIADO, tem cenário próprio
        Assert.False(string.IsNullOrEmpty(alvo), "o pedido não guardou o alvo");
        var novos = await execucoes.Proprios();
        Assert.NotEmpty(novos);
        Assert.All(novos, d =>
        {
            Assert.Equal(maria.Id, Auditoria.Texto(d, "quem"));
            Assert.Equal(acao, Auditoria.Texto(d, "acao"));
            Assert.Equal(alvo, Auditoria.Texto(d, "alvo"));
            execucoes.NaHoraDoPedido(d);
            Auditoria.GuardadoCincoAnos(d);
        });
    }

    // as duas etapas da mesma execução, com a conta master, a ação e o alvo
    [Then(@"^a coleção ""auditoria"" ganha ""(\w+)"" e ""(\w+)"" da ação ""(\w+)""$")]
    public async Task GanhaAsDuasEtapas(string primeira, string segunda, string acao)
    {
        // a Pessoa da conta master não guarda o id: ele vem do sub do token dela
        var master = await mundo.Traduzir("usr-ebfga-master");
        var novos = await execucoes.Proprios();
        Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
        Auditoria.MesmoTraceId(novos);
        Assert.All(novos, d =>
        {
            Assert.Equal(master, Auditoria.Texto(d, "quem"));
            Assert.Equal(acao, Auditoria.Texto(d, "acao"));
            Assert.Equal(alvo, Auditoria.Texto(d, "alvo"));
            execucoes.NaHoraDoPedido(d);
            Auditoria.GuardadoCincoAnos(d);
        });
    }

    // "registrosPermissao" é lido pelo histórico do contrato (RN-256): a versão
    // nova, a seguinte à de antes do PUT, é a primeira do histórico do João
    [Then(@"^""registrosPermissao"" ganha a versão nova das permissões do João, como na RN-256$")]
    public async Task RegistroDaVersaoNova()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var versao = mundo.Ultima.Corpo!["versao"]!.GetValue<int>();
        Assert.Equal(versaoAntes + 1, versao);
        var historico = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", $"/usuarios/{alvo}/permissoes/historico"), 200, "GET …/historico do João");
        Assert.Equal(versao, historico.Corpo![0]?["versao"]?.GetValue<int>());
    }

    [Then(@"^a coleção ""auditoria"" ganha o registro ""(\w+)"" da ação ""(\w+)""$")]
    public async Task GanhaORegistro(string etapa, string acao)
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        registro = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == etapa && Auditoria.Texto(d, "acao") == acao);
        Assert.True(registro is not null, $"esperava o registro {etapa} de {acao} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        execucoes.NaHoraDoPedido(registro);
        Auditoria.GuardadoCincoAnos(registro);
    }

    [Then(@"^esse registro vai sem quem, sem contaId e sem alvo$")]
    public void SemQuemContaEAlvo()
    {
        Assert.NotNull(registro);
        Assert.Null(Auditoria.Texto(registro, "quem"));
        Assert.Null(Auditoria.Texto(registro, "contaId"));
        Assert.Null(Auditoria.Texto(registro, "alvo"));
    }

    [Then(@"^o registro ""CONCLUIDO"", com o mesmo traceId, vai com contaId e alvo ""(.+)"", sem quem$")]
    public async Task ConcluidoComAConta(string exemplo)
    {
        Assert.NotNull(registro);
        var conta = await mundo.Traduzir(exemplo);
        var novos = await execucoes.Novos(Auditoria.Registros);
        var concluido = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == "CONCLUIDO");
        Assert.True(concluido is not null, $"esperava o registro CONCLUIDO em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        Assert.Equal(Auditoria.TraceId(registro), Auditoria.TraceId(concluido));
        Assert.Equal(conta, Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(conta, Auditoria.Texto(concluido, "alvo"));
        Assert.Null(Auditoria.Texto(concluido, "quem"));
    }

    // ── erros e alertas (RN-270) ───────────────────────────────────────────
    [Then(@"^a coleção ""auditoria"" fica só com o registro ""(\w+)"" da ação ""(\w+)""$")]
    public async Task SoORegistro(string etapa, string acao)
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.True(novos.Count == 1, $"esperava só o registro {etapa} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        registro = novos[0];
        Assert.Equal(etapa, Auditoria.Texto(registro, "etapa"));
        Assert.Equal(acao, Auditoria.Texto(registro, "acao"));
    }

    [Then(@"^a coleção ""auditoriaErros"" ganha 1 registro com o código ""(\w+)"" e o nivel ""(\w+)""$")]
    public async Task UmErro(string codigo, string nivel)
    {
        var erro = await execucoes.UmNovoErro();
        Assert.Equal(codigo, Auditoria.Texto(erro, "codigo"));
        Assert.Equal(nivel, Auditoria.Texto(erro, "nivel"));
    }

    [Then(@"^os dois registros têm o mesmo traceId$")]
    public async Task MesmoTraceIdNasDuas()
    {
        Assert.NotNull(registro);
        Assert.Equal(Auditoria.TraceId(registro), Auditoria.TraceId(await execucoes.UmNovoErro()));
    }

    [Then(@"^nenhum registro ""CONCLUIDO"" é gravado com esse traceId$")]
    public async Task SemConcluido()
    {
        Assert.NotNull(registro);
        var traceId = Auditoria.TraceId(registro);
        var concluidos = (await Auditoria.Todos(Auditoria.Registros))
            .Where(d => Auditoria.Texto(d, "traceId") == traceId && Auditoria.Texto(d, "etapa") == "CONCLUIDO").ToList();
        Assert.True(concluidos.Count == 0, $"a execução recusada gravou {Auditoria.Resumo(concluidos)}");
    }

    [Then(@"^""auditoriaErros"" ganha o registro com o código ""(\w+)"" e o nivel ""(\w+)""$")]
    public async Task ErroComCodigoENivel(string codigo, string nivel)
    {
        erro = await execucoes.UmNovoErro();
        Assert.Equal(codigo, Auditoria.Texto(erro, "codigo"));
        Assert.Equal(nivel, Auditoria.Texto(erro, "nivel"));
    }

    // "antes de a recusa sair do caso de uso": o registro já está gravado
    // quando a resposta chega, com a hora do pedido
    [Then(@"^esse registro é gravado antes de a recusa sair do caso de uso$")]
    public void GravadoAntesDaRecusa()
    {
        Assert.NotNull(erro);
        execucoes.NaHoraDoPedido(erro);
    }

    [Given(@"^o registro ""auderr-\d+"" da sigla ""(\w+)"" repetida, gravado em \d{4}-\d{2}-\d{2}$")]
    public async Task SiglaRepetida(string sigla)
    {
        await mundo.Conta();
        await Executar(null, "POST", "/contas", ContaDaSigla(sigla));
        Exigir(mundo.Ultima, 409, $"POST /contas com a sigla {sigla} repetida");
        registro = await execucoes.UmNovoErro();
    }

    [Then(@"^ele fica guardado até \d{4}-\d{2}-\d{2}, 5 anos depois, como na RN-178$")]
    public void GuardadoComoAAuditoria()
    {
        Assert.NotNull(registro);
        Auditoria.GuardadoCincoAnos(registro);
    }

    [Then(@"^não pode ser alterado nem apagado, como na RNF-206$")]
    public static void Imutavel() => throw new PendingStepException(
        "nenhum endereço da API altera ou apaga a auditoria, e a recusa do banco é conferida nos testes do backend, como na RNF-206");

    // ── traceId (RN-271) ───────────────────────────────────────────────────
    [Then(@"^cada execução tem um traceId próprio, diferente do da outra$")]
    public async Task TraceIdsDiferentes()
    {
        Assert.True(execucoes.Resultados.Count == 2, $"esperava 2 execuções, vieram {execucoes.Resultados.Count}");
        var traceIds = new List<string>();
        for (var i = 0; i < execucoes.Resultados.Count; i++)
        {
            var novos = await execucoes.Novos(Auditoria.Registros, i);
            Assert.True(novos.Count > 0, $"a execução {i + 1} não gravou nada em \"auditoria\"");
            traceIds.Add(Auditoria.MesmoTraceId(novos));
        }
        Assert.NotEqual(traceIds[0], traceIds[1]);
    }

    [Then(@"^os registros ""(\w+)"" e ""(\w+)"" de cada uma repetem o traceId dela$")]
    public async Task CadaUmaRepete(string primeira, string segunda)
    {
        for (var i = 0; i < execucoes.Resultados.Count; i++)
        {
            var novos = await execucoes.Novos(Auditoria.Registros, i);
            Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
            Auditoria.MesmoTraceId(novos);
        }
    }

    [Then(@"^o valor de X-Trace-Id é o traceId gravado em ""INICIADO"" e ""CONCLUIDO""$")]
    public async Task TraceIdDosRegistros()
    {
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
        Assert.All(novos, d => Assert.Equal(traceId, Auditoria.TraceId(d)));
    }

    [Then(@"^o valor de X-Trace-Id é o traceId gravado em ""INICIADO"" e ""auditoriaErros""$")]
    public async Task TraceIdDoErro()
    {
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        var novos = await execucoes.Novos(Auditoria.Registros);
        var iniciado = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == "INICIADO");
        Assert.True(iniciado is not null, $"esperava o registro INICIADO em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        Assert.Equal(traceId, Auditoria.TraceId(iniciado));
        Assert.Equal(traceId, Auditoria.TraceId(await execucoes.UmNovoErro()));
    }

    // "sem log" é conferido por inspeção do caso de uso, não por teste
    [Given(@"^o traceId ""trc-7f3a9c21"" de uma execução de POST /contas recusada com 409 SIGLA_EM_USO$")]
    [When(@"^o time deboaz procura o que aconteceu nessa execução$")]
    [Then(@"^encontra tudo nas coleções ""auditoria"" e ""auditoriaErros"" pelo traceId ""trc-7f3a9c21""$")]
    [Then(@"^o caso de uso não escreve nada em log; .+$")]
    public static void PorInspecao() => throw new PendingStepException(
        "acompanhamento e ausência de log: conferidos por inspeção do caso de uso, não pela suíte (o traceId já é coberto pelos outros cenários da RN-271)");

    // ── criação de usuário: alvo só na conclusão (RN-178) ─────────────────
    [When(@"^a resposta é 201 com o id ""(usr-ebfga-lucas)""$")]
    public void UsuarioCriado(string exemplo)
    {
        Respostas.Confere(mundo.Ultima, 201);
        var id = Texto(mundo.Ultima, "id");
        mundo.Apelidos[exemplo] = () => Task.FromResult(id);
    }

    [Then(@"^""auditoria"" ganha o ""INICIADO"" da ação ""(\w+)"" com a Maria e a ""(conta-ebfga)""$")]
    public async Task IniciadoDaMaria(string acao, string conta)
    {
        registro = await execucoes.Etapa("INICIADO", acao);
        Assert.Equal((await mundo.Maria()).Id, Auditoria.Texto(registro, "quem"));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(registro, "contaId"));
        execucoes.NaHoraDoPedido(registro);
        Auditoria.GuardadoCincoAnos(registro);
    }

    [Then(@"^esse registro vai sem alvo, porque o Lucas ainda não existe$")]
    public void IniciadoSemAlvo()
    {
        Assert.NotNull(registro);
        Assert.True(Auditoria.Texto(registro, "alvo") is null, $"o INICIADO não devia trazer alvo: {registro.ToJson()}");
    }

    [Then(@"^o ""CONCLUIDO"", com o mesmo traceId, vai com a Maria, a ""(conta-ebfga)"" e o alvo ""(usr-ebfga-lucas)""$")]
    public async Task ConcluidoComOAlvo(string conta, string exemploAlvo)
    {
        Assert.NotNull(registro);
        var concluido = await execucoes.Etapa("CONCLUIDO", Auditoria.Texto(registro, "acao")!);
        Assert.Equal(Auditoria.TraceId(registro), Auditoria.TraceId(concluido));
        Assert.Equal((await mundo.Maria()).Id, Auditoria.Texto(concluido, "quem"));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(await mundo.Traduzir(exemploAlvo), Auditoria.Texto(concluido, "alvo"));
    }

    // o Rafael é criado pela conta master, antes da marca
    [Given(@"^o participante ""(Rafael)"" com a matrícula ""(.+)"" na EBFGA$")]
    public async Task ParticipanteComMatricula(string nome, string matricula) =>
        Exigir(await mundo.EnviarComo(await mundo.Master(), "POST", "/usuarios", Participante(nome, matricula)), 201, $"POST /usuarios do {nome}");

    [When(@"^a Maria envia POST /usuarios do participante (Lucas) com a matrícula ""(.+)""$")]
    public async Task CriaComMatricula(string nome, string matricula) =>
        await Executar(await MariaPronta(), "POST", "/usuarios", Participante(nome, matricula));

    [Then(@"^""auditoria"" fica só com o ""INICIADO"" da ação ""(\w+)"", sem alvo$")]
    public async Task SoOIniciadoSemAlvo(string acao)
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.True(novos.Count == 1 && Auditoria.Texto(novos[0], "etapa") == "INICIADO" && Auditoria.Texto(novos[0], "acao") == acao,
            $"esperava só o INICIADO de {acao} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        registro = novos[0];
        Assert.True(Auditoria.Texto(registro, "alvo") is null, $"o INICIADO não devia trazer alvo: {registro.ToJson()}");
    }

    [Then(@"^o registro de ""auditoriaErros"" vai com a Maria e a ""(conta-ebfga)"", sem alvo$")]
    public async Task ErroDaMariaSemAlvo(string conta)
    {
        erro = await execucoes.UmNovoErro();
        Assert.Equal((await mundo.Maria()).Id, Auditoria.Texto(erro, "quem"));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(erro, "contaId"));
        Assert.True(Auditoria.Texto(erro, "alvo") is null, $"o registro de \"auditoriaErros\" não devia trazer alvo: {erro.ToJson()}");
        if (registro is not null) Assert.Equal(Auditoria.TraceId(registro), Auditoria.TraceId(erro));
    }

    // ── corpo que não é JSON (RN-271) ──────────────────────────────────────
    [When(@"^a Maria envia POST /usuarios com o corpo ""(.+)"", que não é JSON$")]
    public async Task CorpoQueNaoEJson(string corpo) =>
        await Executar(await MariaPronta(), "POST", "/usuarios", corpo);

    // o mesmo endereço é auditado com um corpo válido (201) e sem o módulo
    // (403 da RN-270): as duas respostas trazem o cabeçalho
    [Then(@"^o X-Trace-Id vale só para as respostas auditadas, com o 403 sem o módulo da RN-270$")]
    public async Task SoAsAuditadas()
    {
        var r = await mundo.EnviarComo(await mundo.Maria(), "POST", "/usuarios", Participante("Pedro"));
        Respostas.Confere(r, 201);
        Auditoria.TraceIdDoCabecalho(r);
        await AnaSemOModulo("MOD-03", "U");
        var semModulo = await mundo.EnviarComo(mundo.Sujeito!, "POST", "/usuarios", Participante("Rafael"));
        Respostas.Confere(semModulo, 403, "ACESSO_NEGADO");
        Auditoria.TraceIdDoCabecalho(semModulo);
    }

    // ── pedido de ajuda da conta master (RN-274) ───────────────────────────
    [When(@"^envia POST /contas/esqueci-senha-master, sem login, da ""(.+)""$")]
    public async Task PedidoDeAjuda(string instituicao)
    {
        await mundo.Conta();
        await Executar(null, "POST", "/contas/esqueci-senha-master", new JsonObject
        {
            ["instituicao"] = instituicao,
            ["contato"] = "(51) 99999-0000",
        }.ToJsonString());
    }

    [Then(@"^os dois com o traceId de X-Trace-Id, sem quem e sem contaId$")]
    public async Task SemQuemNemConta()
    {
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.NotEmpty(novos);
        Assert.All(novos, d =>
        {
            Assert.Equal(traceId, Auditoria.TraceId(d));
            Assert.True(Auditoria.Texto(d, "quem") is null && Auditoria.Texto(d, "contaId") is null,
                $"esperava quem e contaId null, veio {d.ToJson()}");
        });
    }

    // o alvo é o id do pedido gravado em "pedidosAjuda": a suíte só lê as
    // coleções da auditoria (README, seção 6), então confere só que o
    // CONCLUIDO traz um alvo
    [Then(@"^o ""CONCLUIDO"" vai com o alvo ""ajuda-000087"", o pedido gravado em ""pedidosAjuda""$")]
    public async Task ConcluidoComOPedido()
    {
        var concluido = await execucoes.Etapa("CONCLUIDO", "ESQUECI_SENHA_MASTER");
        Assert.False(string.IsNullOrWhiteSpace(Auditoria.Texto(concluido, "alvo")), $"o CONCLUIDO veio sem alvo: {concluido.ToJson()}");
    }

    // ── GERAR_SENHA, CODIGO_MASTER e SALVAR_TEMPO_SESSAO do time (RN-178) ──
    // a Maria pendente (RF-109): criada pela conta master, com o código ainda sem uso
    [When(@"^envia POST /usuarios/gerar-senha, sem login, com o código de liberação da Maria$")]
    public async Task GeraASenhaDaMaria()
    {
        var (maria, codigo) = await mundo.Pendente("Maria Souza", "maria", "20260087");
        await mundo.Conta();
        await Executar(null, "POST", "/usuarios/gerar-senha", new JsonObject
        {
            ["entrada"] = maria.Entrada,
            ["codigoLiberacao"] = codigo,
        }.ToJsonString());
    }

    [Then(@"^""auditoria"" ganha o ""INICIADO"" da ação ""(\w+)"" sem quem, sem contaId e sem alvo$")]
    public async Task IniciadoVazio(string acao)
    {
        registro = await execucoes.Etapa("INICIADO", acao);
        Assert.True(Auditoria.Texto(registro, "quem") is null && Auditoria.Texto(registro, "contaId") is null && Auditoria.Texto(registro, "alvo") is null,
            $"o INICIADO devia ir sem quem, sem contaId e sem alvo: {registro.ToJson()}");
        execucoes.NaHoraDoPedido(registro);
        Auditoria.GuardadoCincoAnos(registro);
    }

    [Then(@"^o ""CONCLUIDO"", com o mesmo traceId, vai com a ""(conta-ebfga)"" e o alvo ""(usr-ebfga-maria)"", sem quem$")]
    public async Task ConcluidoSemQuem(string conta, string exemploAlvo)
    {
        Assert.NotNull(registro);
        var concluido = await execucoes.Etapa("CONCLUIDO", Auditoria.Texto(registro, "acao")!);
        Assert.Equal(Auditoria.TraceId(registro), Auditoria.TraceId(concluido));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(await mundo.Traduzir(exemploAlvo), Auditoria.Texto(concluido, "alvo"));
        Assert.True(Auditoria.Texto(concluido, "quem") is null, $"o CONCLUIDO devia ir sem quem: {concluido.ToJson()}");
    }

    // o Caio com o MOD-03 em "U" e a conferência certa (CNPJ, responsável e
    // telefone). O login do Caio e o da conta master (o alvo é o sub do token
    // dela) saem antes da marca, para não cair entre o antes e o depois
    [When(@"^o Caio, do time deboaz, envia POST (/contas/conta-ebfga/codigo-master)$")]
    public async Task CaioPedeOCodigoMaster(string caminho)
    {
        var caio = await CaioCom("U");
        await mundo.Traduzir("usr-ebfga-master");
        await Executar(caio, "POST", await mundo.Traduzir(caminho), Conferencia(null, "Gustavo Boaz de Moura Souza"));
    }

    [When(@"^a resposta é 201 com o código de liberação$")]
    public void ComOCodigoDeLiberacao()
    {
        Respostas.Confere(mundo.Ultima, 201);
        Assert.Matches(@"^\d{6}$", Texto(mundo.Ultima, "codigoLiberacao"));
    }

    [Then(@"^o ""INICIADO"" da ação ""(\w+)"" vai com ""(usr-dboaz-caio)"" e a ""(conta-ebfga)"", sem alvo$")]
    public async Task IniciadoDoCaio(string acao, string quem, string conta)
    {
        registro = await execucoes.Etapa("INICIADO", acao);
        Assert.Equal(await mundo.Traduzir(quem), Auditoria.Texto(registro, "quem"));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(registro, "contaId"));
        Assert.True(Auditoria.Texto(registro, "alvo") is null, $"o INICIADO não devia trazer alvo: {registro.ToJson()}");
        execucoes.NaHoraDoPedido(registro);
        Auditoria.GuardadoCincoAnos(registro);
    }

    [Then(@"^o ""CONCLUIDO"", com o mesmo traceId, repete os dois e vai com o alvo ""(usr-ebfga-master)""$")]
    public async Task ConcluidoComAMaster(string exemploAlvo)
    {
        Assert.NotNull(registro);
        var concluido = await execucoes.Etapa("CONCLUIDO", Auditoria.Texto(registro, "acao")!);
        Assert.Equal(Auditoria.TraceId(registro), Auditoria.TraceId(concluido));
        Assert.Equal(Auditoria.Texto(registro, "quem"), Auditoria.Texto(concluido, "quem"));
        Assert.Equal(Auditoria.Texto(registro, "contaId"), Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(await mundo.Traduzir(exemploAlvo), Auditoria.Texto(concluido, "alvo"));
    }

    // o tempo do time pede a aba Configuração (MOD-03 · C), como na RF-152
    [When(@"^o Caio envia PUT /time/tempo-sessao e a resposta é 200$")]
    public async Task CaioSalvaOTempoDoTime()
    {
        var caio = await CaioCom("CU");
        Respostas.Confere(await Executar(caio, "PUT", "/time/tempo-sessao", TempoDoTime("PT8H")), 200);
    }

    // "conta-dboaz" é o contaId do time (suporte/ContasIniciais.cs), sem apelido
    [Then(@"^os dois vão com ""(usr-dboaz-caio)"", a ""(conta-dboaz)"", a conta de quem pede, e o alvo ""(\w+)""$")]
    public async Task OsDoisDoCaio(string quem, string conta, string alvoEsperado)
    {
        var id = await mundo.Traduzir(quem);
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.Equal(2, novos.Count);
        Assert.All(novos, d =>
        {
            Assert.Equal(id, Auditoria.Texto(d, "quem"));
            Assert.Equal(conta, Auditoria.Texto(d, "contaId"));
            Assert.Equal(alvoEsperado, Auditoria.Texto(d, "alvo"));
        });
    }

    // ── recusa prevista de cada caso de uso (RN-270) ───────────────────────
    // quem pede, com o módulo e a sessão já prontos, e o que o pedido cita
    // existem antes da marca: só o pedido recusado cai entre o antes e o depois
    [When(@"^chega (o CNPJ de um AUTONOMO em POST /contas|o nome curto ""maria"" no POST /usuarios|a matrícula da Maria no PATCH da Ana|o código ""111111"" em gerar-senha|o código de 73 h em gerar-senha|a nova geração do Rui, pedida pelo João|a nova geração do participante Lucas|o novo código de acesso da staff Maria|o reativar do Paulo já ativo|o João dando à Ana mais do que ele tem|as permissões da Maria com If-Match ""6""|a EBFGA encerrada de volta a ""ATIVA""|o pagamento errado no codigo-master|""PT30H"" no tempo do time|a master da EBFGA no tempo do time)$")]
    public async Task ChegaARecusaPrevista(string pedido)
    {
        switch (pedido)
        {
            case "o CNPJ de um AUTONOMO em POST /contas":
                await Executar(null, "POST", "/contas", ContaNova("AUTONOMO", "Pedro Reis", Cnpj(20000003), "pedro_reis", "PREIS"));
                break;
            case "o nome curto \"maria\" no POST /usuarios":
                {
                    await mundo.Maria();
                    var master = await MasterPronta();
                    await Executar(master, "POST", "/usuarios", new JsonObject
                    {
                        ["tipo"] = "STAFF",
                        ["nome"] = "Maria Oliveira",
                        ["nomeCurto"] = "maria",
                        ["matricula"] = "20260199",
                    }.ToJsonString());
                    break;
                }
            case "a matrícula da Maria no PATCH da Ana":
                {
                    await mundo.Maria();
                    var ana = await mundo.Ana();
                    var master = await MasterPronta();
                    await Executar(master, "PATCH", $"/usuarios/{ana.Id}", """
                        {
                          "matricula": "20260087"
                        }
                        """);
                    break;
                }
            case "o código \"111111\" em gerar-senha":
                {
                    var (gustavo, _) = await mundo.Gustavo();
                    await Executar(null, "POST", "/usuarios/gerar-senha", GeracaoDeSenha(gustavo.Entrada, "111111"));
                    break;
                }
            case "o código de 73 h em gerar-senha":
                {
                    var (maria, codigo) = await mundo.Pendente("Maria Souza", "maria", "20260087");
                    await Estado.PedidoHa(maria.Id, TimeSpan.FromHours(73));
                    await Executar(null, "POST", "/usuarios/gerar-senha", GeracaoDeSenha(maria.Entrada, codigo));
                    break;
                }
            case "a nova geração do Rui, pedida pelo João":
                {
                    var rui = await mundo.Rui();
                    var joao = await JoaoPronto();
                    await Executar(joao, "POST", $"/usuarios/{rui.Id}/nova-geracao-senha", null);
                    break;
                }
            case "a nova geração do participante Lucas":
                {
                    var lucas = await mundo.Lucas();
                    await Executar(await MasterPronta(), "POST", $"/usuarios/{lucas.Id}/nova-geracao-senha", null);
                    break;
                }
            case "o novo código de acesso da staff Maria":
                {
                    var maria = await mundo.Maria();
                    await Executar(await MasterPronta(), "POST", $"/usuarios/{maria.Id}/novo-codigo-acesso", null);
                    break;
                }
            case "o reativar do Paulo já ativo":
                {
                    var paulo = await mundo.Paulo();
                    await Executar(await MasterPronta(), "POST", $"/usuarios/{paulo.Id}/reativar", null);
                    break;
                }
            case "o João dando à Ana mais do que ele tem":
                {
                    // o João só tem Administrar usuários; a Sugestão (MOD-04) passa do que ele tem
                    var ana = await mundo.Ana();
                    var joao = await JoaoPronto();
                    var atuais = await mundo.Permissoes(ana);
                    var mods = (JsonObject)atuais["mods"]!.DeepClone();
                    mods["MOD-04"] = "U";
                    await Executar(joao, "PUT", $"/usuarios/{ana.Id}/permissoes",
                        new JsonObject { ["mods"] = mods }.ToJsonString(), atuais["versao"]!.GetValue<int>().ToString());
                    break;
                }
            case "as permissões da Maria com If-Match \"6\"":
                {
                    // a Maria está na versão 7 (17 exemplo-entidades): a 6 já passou
                    var maria = await mundo.Maria();
                    var mods = (JsonObject)(await mundo.Permissoes(maria))["mods"]!.DeepClone();
                    await Executar(await MasterPronta(), "PUT", $"/usuarios/{maria.Id}/permissoes",
                        new JsonObject { ["mods"] = mods }.ToJsonString(), "6");
                    break;
                }
            case "a EBFGA encerrada de volta a \"ATIVA\"":
                {
                    var conta = await mundo.Conta();
                    var admin = mundo.Admin();
                    Exigir(await mundo.EnviarComo(admin, "POST", $"/contas/{conta}/estado", new JsonObject { ["estado"] = "ENCERRADA" }.ToJsonString()),
                        200, "POST /contas/{id}/estado ENCERRADA");
                    await Executar(admin, "POST", $"/contas/{conta}/estado", new JsonObject { ["estado"] = "ATIVA" }.ToJsonString());
                    break;
                }
            case "o pagamento errado no codigo-master":
                {
                    // CNPJ e telefone certos, sem o responsável, e a data do último pagamento errada
                    var conta = await mundo.Conta();
                    var caio = await CaioCom("U");
                    await Executar(caio, "POST", $"/contas/{conta}/codigo-master", Conferencia("2020-01-01", null));
                    break;
                }
            case "\"PT30H\" no tempo do time":
                {
                    var admin = mundo.Admin();
                    await mundo.TokenDe(admin);
                    await Executar(admin, "PUT", "/time/tempo-sessao", TempoDoTime("PT30H"));
                    break;
                }
            default:
                await Executar(await MasterPronta(), "PUT", "/time/tempo-sessao", TempoDoTime("PT8H"));
                break;
        }
    }

    // ── sem o módulo (RN-270) ──────────────────────────────────────────────
    // a Ana sem a aba pedida do módulo, salva pela conta master: sem "U", fica
    // sem o módulo; sem "C", fica só com "U", para faltar só a Configuração.
    // O Paulo e o João existem e o login dela sai antes da marca
    [Given(@"^o token da staff Ana sem ""(MOD-\d+)"" com ""(\w+)""$")]
    public async Task AnaSemOModulo(string mod, string aba)
    {
        var ana = await mundo.Ana();
        var atuais = (await mundo.Permissoes(ana))["mods"]!.AsObject()
            .ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());
        var antes = atuais.GetValueOrDefault(mod);
        var depois = aba == "U" ? "" : "U";
        if (antes != (depois.Length == 0 ? null : depois))
        {
            if (depois.Length == 0) atuais.Remove(mod);
            else atuais[mod] = depois;
            await mundo.Definir(ana, atuais);
        }
        await mundo.Paulo();
        await mundo.Conta();
        await mundo.TokenDe(ana);
        mundo.Sujeito = ana;
    }

    // o corpo de cada pedido é válido: a única falta é a do módulo. A versão
    // das permissões do João é lida antes da marca, para o If-Match
    [When(@"^ela envia (POST|PUT|GET) (\S+)$")]
    public async Task ElaEnvia(string metodo, string caminho)
    {
        var real = await mundo.Traduzir(caminho);
        string? corpo = null, ifMatch = null;
        if (real == "/usuarios" && metodo == "POST")
        {
            corpo = Participante("Pedro");
        }
        else if (real.EndsWith("/tempo-sessao", StringComparison.Ordinal))
        {
            corpo = """
                {
                  "staffNavegador": "PT4H",
                  "staffApp": "P7D",
                  "participante": "P30D"
                }
                """;
        }
        else if (real.EndsWith("/permissoes", StringComparison.Ordinal))
        {
            ifMatch = (await mundo.Permissoes(await mundo.Joao()))["versao"]!.GetValue<int>().ToString();
            corpo = """
                {
                  "mods": {}
                }
                """;
        }
        await Executar(mundo.Sujeito!, metodo, real, corpo, ifMatch);
    }

    [Then(@"^""auditoriaErros"" ganha ""([A-Z_]+)"" com o nivel ""(\w+)"", com esse traceId$")]
    public async Task ErroDoCodigoComOTraceId(string codigo, string nivel)
    {
        erro = await execucoes.UmNovoErro();
        Assert.Equal(codigo, Auditoria.Texto(erro, "codigo"));
        Assert.Equal(nivel, Auditoria.Texto(erro, "nivel"));
        Assert.Equal(Auditoria.TraceIdDoCabecalho(mundo.Ultima), Auditoria.TraceId(erro));
        execucoes.NaHoraDoPedido(erro);
    }

    // "os dois": o INICIADO da frase anterior e o registro de "auditoriaErros"
    [Then(@"^os dois vão com ""(usr-ebfga-ana)"", a ""(conta-ebfga)"" e o alvo (nenhum|""[^""]+"")$")]
    public async Task OsDoisDaAna(string quem, string conta, string alvoDoExemplo)
    {
        Assert.NotNull(registro);
        Assert.NotNull(erro);
        var id = await mundo.Traduzir(quem);
        var contaId = await mundo.Traduzir(conta);
        var esperado = alvoDoExemplo == "nenhum" ? null : await mundo.Traduzir(alvoDoExemplo.Trim('"'));
        foreach (var d in new[] { registro, erro })
        {
            Assert.Equal(id, Auditoria.Texto(d, "quem"));
            Assert.Equal(contaId, Auditoria.Texto(d, "contaId"));
            Assert.True(Auditoria.Texto(d, "alvo") == esperado, $"esperava o alvo {esperado ?? "null"}: {d.ToJson()}");
        }
    }

    // o mesmo pedido com o token da Ana vencido (assinado de novo, com exp no
    // passado): 401 antes do caso de uso, sem registro nas duas coleções
    [Then(@"^o 401 do token vencido continua sem registro, como na RN-273 do 17-a$")]
    public async Task TokenVencidoSemRegistro()
    {
        var token = await mundo.TokenDe(mundo.Sujeito!);
        var carga = Jwt.Carga(token);
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        carga["iat"] = agora - 960;
        carga["exp"] = agora - 60;
        var vencido = Jwt.Assinar(Jwt.Cabecalho(token), carga);
        var (metodo, caminho, corpo) = pedido;
        var r = await execucoes.Executar(() => mundo.Enviar(metodo, caminho, corpo, vencido));
        Respostas.Confere(r, 401);
        var registros = await execucoes.Novos(Auditoria.Registros);
        var erros = await execucoes.Novos(Auditoria.Erros);
        Assert.True(registros.Count == 0 && erros.Count == 0,
            $"o 401 gravou {Auditoria.Resumo(registros)} em \"auditoria\" e {Auditoria.Resumo(erros)} em \"auditoriaErros\"");
    }

    // ── casos de uso internos (RN-275) ─────────────────────────────────────
    // o registro em conferência é o INICIADO da ação interna; o pedido é o
    // INICIADO e o CONCLUIDO das outras ações da mesma execução
    [Then(@"^os dois com o traceId da execução do pedido, entre o ""INICIADO"" e o ""CONCLUIDO"" dela$")]
    public async Task InternaDentroDaExecucao()
    {
        var (iniciado, concluido, internas) = await PedidoEInternas();
        var traceId = Auditoria.TraceId(iniciado);
        Assert.Equal(traceId, Auditoria.TraceId(concluido));
        Assert.All(internas, d =>
        {
            Assert.Equal(traceId, Auditoria.TraceId(d));
            var quando = Auditoria.Data(d, "quando");
            Assert.True(quando >= Auditoria.Data(iniciado, "quando") && quando <= Auditoria.Data(concluido, "quando"),
                $"a ação interna devia ficar entre o INICIADO e o CONCLUIDO do pedido: {d.ToJson()}, pedido {Auditoria.Resumo([iniciado, concluido])}");
        });
    }

    [Then(@"^os dois com quem e contaId da execução do pedido e o alvo, o usuário do pedido$")]
    public async Task InternaComQuemContaEAlvo()
    {
        Assert.False(string.IsNullOrEmpty(alvo), "o pedido não guardou o alvo");
        var (iniciado, _, internas) = await PedidoEInternas();
        Assert.All(internas, d =>
        {
            Assert.Equal(Auditoria.Texto(iniciado, "quem"), Auditoria.Texto(d, "quem"));
            Assert.Equal(Auditoria.Texto(iniciado, "contaId"), Auditoria.Texto(d, "contaId"));
            Assert.Equal(alvo, Auditoria.Texto(d, "alvo"));
        });
    }

    async Task<(BsonDocument Iniciado, BsonDocument Concluido, List<BsonDocument> Internas)> PedidoEInternas()
    {
        Assert.NotNull(registro);
        var acao = Auditoria.Texto(registro, "acao");
        var novos = await execucoes.Novos(Auditoria.Registros);
        var internas = novos.Where(d => Auditoria.Texto(d, "acao") == acao).ToList();
        var proprios = await execucoes.Proprios();
        Assert.True(proprios.Count == 2 && Auditoria.Texto(proprios[0], "etapa") == "INICIADO" && Auditoria.Texto(proprios[1], "etapa") == "CONCLUIDO",
            $"esperava o INICIADO e o CONCLUIDO do pedido, vieram {Auditoria.Resumo(proprios)}");
        return (proprios[0], proprios[1], internas);
    }

    // o Catálogo é outro contexto: o contrato de API não tem endereço para
    // descontratar nem para o aviso, e a suíte não injeta o traceId de outra execução
    [Given(@"^a execução do Catálogo com o traceId ""trc-5b81e0d4"", em que o Caio descontrata a Presença$")]
    [When(@"^o Catálogo avisa que a contratação da EBFGA mudou$")]
    [Then(@"^os dois com o traceId ""trc-5b81e0d4"", ""usr-dboaz-caio"", a ""conta-ebfga"" e o alvo ""conta-ebfga""$")]
    [Then(@"^cada usuário da EBFGA ganha ""INICIADO"" e ""CONCLUIDO"" de ""AVISAR_PERMISSOES_NOVAS""$")]
    [Then(@"^esses vão com o mesmo traceId, o Caio, a ""conta-ebfga"" e o usuário como alvo$")]
    public static void AvisoDoCatalogo() => throw new PendingStepException(
        "o aviso do Catálogo (AVISAR_CONTRATACAO_MUDOU) não tem endereço no contrato nem é provocável de fora, numa suíte caixa-preta: " +
        "fica nos testes do backend, chamando o caso de uso com o traceId, o Caio e a conta da execução do Catálogo");

    // a falha da gravação no meio da execução interna não é provocável de fora,
    // como o "banco cai" da RN-270; as frases seguintes conferem o que ela grava
    [When(@"^a gravação falha ao encerrar as sessões do Paulo$")]
    public static void FalhaNaInterna() => throw new PendingStepException(
        "derrubar a gravação no meio de ENCERRAR_SESSOES_DO_USUARIO não dá para provocar de fora, numa suíte caixa-preta: " +
        "os 2 registros ERRO_INESPERADO com nivel ERRO ficam nos testes do backend, com a porta de saída falhando");

    [Then(@"^""auditoriaErros"" ganha (\d+) registros ""([A-Z_]+)"" com o nivel ""(\w+)"" e esse traceId$")]
    public async Task ErrosDaExecucao(int quantos, string codigo, string nivel)
    {
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        var erros = await execucoes.Novos(Auditoria.Erros);
        Assert.True(erros.Count == quantos, $"esperava {quantos} registros em \"auditoriaErros\", vieram {Auditoria.Resumo(erros)}");
        Assert.All(erros, d =>
        {
            Assert.Equal(codigo, Auditoria.Texto(d, "codigo"));
            Assert.Equal(nivel, Auditoria.Texto(d, "nivel"));
            Assert.Equal(traceId, Auditoria.TraceId(d));
        });
    }

    [Then(@"^um da ação ""(\w+)"", o outro de ""(\w+)""$")]
    public async Task UmDeCadaAcao(string interna, string doPedido)
    {
        var acoes = (await execucoes.Novos(Auditoria.Erros)).Select(d => Auditoria.Texto(d, "acao")).Order().ToArray();
        Assert.Equal(new[] { interna, doPedido }.Order().ToArray(), acoes);
    }

    [Then(@"^nenhuma das duas ações ganha ""CONCLUIDO""$")]
    public async Task NenhumConcluido()
    {
        var concluidos = (await execucoes.Novos(Auditoria.Registros)).Where(d => Auditoria.Texto(d, "etapa") == "CONCLUIDO").ToList();
        Assert.True(concluidos.Count == 0, $"a execução que falhou gravou {Auditoria.Resumo(concluidos)}");
    }

    // ── apoio da RN-178 e da RN-270 ────────────────────────────────────────
    // o Lucas criado pela Maria antes da marca, para ficar abaixo dela (17-b RN-252)
    async Task<string> LucasDaMaria(Pessoa maria)
    {
        if (!mundo.Apelidos.ContainsKey("lucas-da-maria"))
        {
            var r = Exigir(await mundo.EnviarComo(maria, "POST", "/usuarios", Participante("Lucas")), 201, "POST /usuarios do Lucas, pela Maria");
            var id = Texto(r, "id");
            mundo.Apelidos["lucas-da-maria"] = () => Task.FromResult(id);
            mundo.Apelidos["usr-ebfga-lucas"] = () => Task.FromResult(id);
        }
        return await mundo.Apelidos["lucas-da-maria"]();
    }

    // a conta master com a sessão já aberta e renovada antes da marca
    async Task<Pessoa> MasterPronta()
    {
        var master = await mundo.Master();
        await mundo.EnviarComo(master, "GET", "/usuarios?tipo=STAFF");
        return master;
    }

    // o João com Administrar usuários (MOD-03 · CU) e a sessão já renovada
    async Task<Pessoa> JoaoPronto()
    {
        var joao = await mundo.Joao();
        await mundo.Conceder(joao, new() { ["MOD-03"] = "CU" });
        await mundo.EnviarComo(joao, "GET", "/usuarios?tipo=STAFF");
        return joao;
    }

    // o Caio com as abas do MOD-03 pedidas, salvas pelo administrador antes do login dele
    async Task<Pessoa> CaioCom(string abas)
    {
        var caio = await mundo.Caio();
        await mundo.Conceder(caio, new() { ["MOD-03"] = abas });
        await mundo.EnviarComo(caio, "GET", "/contas?estado=ATIVA");
        return caio;
    }

    static string GeracaoDeSenha(string entrada, string codigo) =>
        new JsonObject { ["entrada"] = entrada, ["codigoLiberacao"] = codigo }.ToJsonString();

    // a conferência da conta EBFGA (RN-172): o CNPJ e o telefone certos, com o
    // responsável e a data do último pagamento pedidos
    static string Conferencia(string? ultimoPagamento, string? responsavel) => new JsonObject
    {
        ["documento"] = "12.345.678/0001-90",
        ["responsavel"] = responsavel,
        ["telefone"] = "(51) 99999-0000",
        ["ultimoPagamento"] = ultimoPagamento,
    }.ToJsonString();

    static string TempoDoTime(string mastersNavegador) => new JsonObject
    {
        ["membrosNavegador"] = "PT8H",
        ["membrosApp"] = "PT8H",
        ["mastersNavegador"] = mastersNavegador,
        ["mastersApp"] = "PT8H",
    }.ToJsonString();

    // ── apoio ──────────────────────────────────────────────────────────────
    // a Maria com Administrar usuários (MOD-03, abas C e U) e a sessão já
    // renovada: o login e a renovação acontecem antes da marca
    async Task<Pessoa> MariaPronta()
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "CU" });
        await mundo.EnviarComo(maria, "GET", "/usuarios?tipo=STAFF");
        return maria;
    }

    async Task<Resposta> Executar(Pessoa? quem, string metodo, string caminho, string? corpo, string? ifMatch = null)
    {
        pedido = (metodo, caminho, corpo);
        return await execucoes.Executar(async () => quem is null
            ? await mundo.Enviar(metodo, caminho, corpo, ifMatch: ifMatch)
            : await mundo.EnviarComo(quem, metodo, caminho, corpo, ifMatch));
    }

    static string Participante(string nome, string? matricula = null) => new JsonObject
    {
        ["tipo"] = "PARTICIPANTE",
        ["nome"] = nome switch { "Lucas" => "Lucas Andrade", "Rafael" => "Rafael Costa", _ => "Pedro Alves" },
        ["nomeCurto"] = nome.ToLowerInvariant(),
        ["matricula"] = matricula,
        ["responsaveis"] = new JsonArray(),
    }.ToJsonString();

    // a COLSA dos exemplos; outra sigla vai com documento e domínio próprios,
    // para a recusa ser só a da sigla (409 SIGLA_EM_USO)
    static string ContaDaSigla(string sigla) => sigla == "COLSA"
        ? ContaNova("INSTITUICAO", "Colégio Santo Antônio", "98.765.432/0001-10", "colegio_sa", sigla)
        : ContaNova("INSTITUICAO", "Escola Nova", Cnpj(20000001), $"dominio_{sigla.ToLowerInvariant()}", sigla);
}
