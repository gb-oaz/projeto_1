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

    // a conta master salva as permissões do João na versão em que estão: grava
    // em "auditoria" e em "registrosPermissao" (RN-178, RN-256)
    [When(@"^a conta master envia PUT (/usuarios/usr-ebfga-joao/permissoes)$")]
    public async Task MasterEditaPermissoes(string caminho)
    {
        var master = await mundo.Master();
        var joao = await mundo.StaffCriadoPor("joao", master);
        alvo = joao.Id;
        versaoAntes = (await mundo.Permissoes(joao))["versao"]!.GetValue<int>();
        await Executar(master, "PUT", await mundo.Traduzir(caminho), """
            {
              "mods": {}
            }
            """, versaoAntes.ToString());
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
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.True(novos.Count == 2, $"esperava 2 registros novos em \"auditoria\", vieram {novos.Count}: {Auditoria.Resumo(novos)}");
        Auditoria.MesmoTraceId(novos);
    }

    [Then(@"^o primeiro com a etapa ""(\w+)"", o segundo com a etapa ""(\w+)""$")]
    public async Task Etapas(string primeira, string segunda)
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
    }

    [Then(@"^os dois com a Maria, a ação ""(\w+)"", o alvo e a hora, guardados até 5 anos depois$")]
    public async Task ComQuemAcaoAlvoEHora(string acao)
    {
        var maria = await mundo.Maria();
        // o alvo já existe antes do pedido (RN-178): o CRIAR_USUARIO, sem
        // alvo no INICIADO, tem cenário próprio
        Assert.False(string.IsNullOrEmpty(alvo), "o pedido não guardou o alvo");
        var novos = await execucoes.Novos(Auditoria.Registros);
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
        var master = await mundo.Master();
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
        Auditoria.MesmoTraceId(novos);
        Assert.All(novos, d =>
        {
            Assert.Equal(master.Id, Auditoria.Texto(d, "quem"));
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

    [Then(@"^as coleções ""auditoria"" e ""auditoriaErros"" não ganham registro$")]
    public async Task NadaGravado()
    {
        var registros = await execucoes.Novos(Auditoria.Registros);
        var erros = await execucoes.Novos(Auditoria.Erros);
        Assert.True(registros.Count == 0 && erros.Count == 0,
            $"a leitura gravou {Auditoria.Resumo(registros)} em \"auditoria\" e {Auditoria.Resumo(erros)} em \"auditoriaErros\"");
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

    // o mesmo endereço, com um corpo válido, passa pelo caso de uso e traz o cabeçalho
    [Then(@"^o X-Trace-Id vale só para as respostas que passam pelo caso de uso$")]
    public async Task SoQuemPassaPeloCasoDeUso()
    {
        var r = await mundo.EnviarComo(await mundo.Maria(), "POST", "/usuarios", Participante("Pedro"));
        Respostas.Confere(r, 201);
        Auditoria.TraceIdDoCabecalho(r);
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
