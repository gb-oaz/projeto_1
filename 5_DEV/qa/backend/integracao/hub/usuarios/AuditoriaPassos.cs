using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos da auditoria de 1_Requisitos/17_modulo-usuario.md: RN-178 (o que muda
// estado grava "INICIADO" e "CONCLUIDO"), RN-270 ("auditoriaErros", nivel
// ALERTA ou ERRO) e RN-271 (um traceId por execução, no cabeçalho X-Trace-Id).
//
// Os registros são lidos nas coleções "auditoria" e "auditoriaErros" do banco
// do backend (suporte/Auditoria.cs, a exceção da README, seção 6). Cada pedido
// do Quando prepara antes tudo o que cita (a Maria com o MOD-03, o alvo, a
// sessão já renovada) e só então marca o que as coleções já tinham: o que o
// preparo gravou não conta como registro "ganho".
[Binding]
public sealed class AuditoriaPassos(Mundo mundo)
{
    static readonly TimeSpan Folga = TimeSpan.FromMinutes(1);

    // as marcas de antes e de depois de cada execução do cenário (RN-271 tem
    // duas): o preparo da segunda não entra nos registros da primeira
    readonly List<(Auditoria.Marca Antes, Auditoria.Marca Depois)> marcas = [];
    readonly List<Resposta> execucoes = [];
    // o alvo que o registro deve trazer e o pedido, para repeti-lo ("já desativado")
    string alvo = "";
    (string Metodo, string Caminho, string? Corpo) pedido;
    BsonDocument? registro;

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
        if (execucoes[^1].Status is < 200 or > 299)
        {
            Exigir(await mundo.EnviarComo(await mundo.Master(), "POST", $"/usuarios/{paulo.Id}/desativar"), 200, "POST /usuarios/{id}/desativar do Paulo");
        }
        await Executar(await mundo.Maria(), pedido.Metodo, pedido.Caminho, pedido.Corpo);
    }

    [When(@"^o banco cai no meio da gravação$")]
    public static void BancoCai() => throw new PendingStepException(
        "derrubar o banco no meio de um pedido não dá para provocar de fora, numa suíte caixa-preta: " +
        "o 500 com nivel ERRO fica nos testes do backend, com a porta de saída falhando");

    // ── respostas ──────────────────────────────────────────────────────────
    [When(@"^a resposta é de sucesso$")]
    public void Sucesso() =>
        Assert.True(mundo.Ultima.Status is >= 200 and < 300, $"esperava sucesso, veio {mundo.Ultima}");

    [When(@"^a resposta é 201 com o contaId ""(.+)""$")]
    public void ContaCriada(string exemplo)
    {
        Respostas.Confere(mundo.Ultima, 201);
        var contaId = Texto(mundo.Ultima, "contaId");
        mundo.Apelidos[exemplo] = () => Task.FromResult(contaId);
    }

    [When(@"^a resposta é (\d{3}) ([A-Z_]+)$")]
    public void Recusa(int status, string codigo) => Respostas.Confere(mundo.Ultima, status, codigo);

    // o código do 409 do "já desativado" ainda não está no requisito: só o status
    [Then(@"^a resposta é (\d{3}), uma (?:recusa prevista|falha inesperada)$")]
    public void StatusDaSituacao(int status) => Respostas.Confere(mundo.Ultima, status);

    [Then(@"^a resposta é (\d{3}) com o cabeçalho X-Trace-Id$")]
    public void ComTraceId(int status)
    {
        Respostas.Confere(mundo.Ultima, status);
        TraceIdDoCabecalho();
    }

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com o cabeçalho X-Trace-Id$")]
    public void RecusaComTraceId(int status, string codigo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        TraceIdDoCabecalho();
    }

    // ── auditoria (RN-178) ─────────────────────────────────────────────────
    [Then(@"^a coleção ""auditoria"" ganha 2 registros com o mesmo traceId$")]
    public async Task DoisRegistros()
    {
        var novos = await Novos(Auditoria.Registros);
        Assert.True(novos.Count == 2, $"esperava 2 registros novos em \"auditoria\", vieram {novos.Count}: {Auditoria.Resumo(novos)}");
        MesmoTraceId(novos);
    }

    [Then(@"^o primeiro com a etapa ""(\w+)"", o segundo com a etapa ""(\w+)""$")]
    public async Task Etapas(string primeira, string segunda)
    {
        var novos = await Novos(Auditoria.Registros);
        Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
    }

    [Then(@"^os dois com a Maria, a ação ""(\w+)"", o alvo e a hora, guardados até 5 anos depois$")]
    public async Task ComQuemAcaoAlvoEHora(string acao)
    {
        var maria = await mundo.Maria();
        var esperado = alvo.Length > 0 ? alvo : Texto(execucoes[^1], "id");
        var novos = await Novos(Auditoria.Registros);
        Assert.NotEmpty(novos);
        Assert.All(novos, d =>
        {
            Assert.Equal(maria.Id, Auditoria.Texto(d, "quem"));
            Assert.Equal(acao, Auditoria.Texto(d, "acao"));
            Assert.Equal(esperado, Auditoria.Texto(d, "alvo"));
            NaHoraDoPedido(d);
            GuardadoCincoAnos(d);
        });
    }

    [Then(@"^a coleção ""auditoria"" ganha o registro ""(\w+)"" da ação ""(\w+)""$")]
    public async Task GanhaORegistro(string etapa, string acao)
    {
        var novos = await Novos(Auditoria.Registros);
        registro = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == etapa && Auditoria.Texto(d, "acao") == acao);
        Assert.True(registro is not null, $"esperava o registro {etapa} de {acao} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        NaHoraDoPedido(registro);
        GuardadoCincoAnos(registro);
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
        var novos = await Novos(Auditoria.Registros);
        var concluido = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == "CONCLUIDO");
        Assert.True(concluido is not null, $"esperava o registro CONCLUIDO em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        Assert.Equal(TraceId(registro), TraceId(concluido));
        Assert.Equal(conta, Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(conta, Auditoria.Texto(concluido, "alvo"));
        Assert.Null(Auditoria.Texto(concluido, "quem"));
    }

    [Then(@"^as coleções ""auditoria"" e ""auditoriaErros"" não ganham registro$")]
    public async Task NadaGravado()
    {
        var registros = await Novos(Auditoria.Registros);
        var erros = await Novos(Auditoria.Erros);
        Assert.True(registros.Count == 0 && erros.Count == 0,
            $"a leitura gravou {Auditoria.Resumo(registros)} em \"auditoria\" e {Auditoria.Resumo(erros)} em \"auditoriaErros\"");
    }

    // ── erros e alertas (RN-270) ───────────────────────────────────────────
    [Then(@"^a coleção ""auditoria"" fica só com o registro ""(\w+)"" da ação ""(\w+)""$")]
    public async Task SoORegistro(string etapa, string acao)
    {
        var novos = await Novos(Auditoria.Registros);
        Assert.True(novos.Count == 1, $"esperava só o registro {etapa} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        registro = novos[0];
        Assert.Equal(etapa, Auditoria.Texto(registro, "etapa"));
        Assert.Equal(acao, Auditoria.Texto(registro, "acao"));
    }

    [Then(@"^a coleção ""auditoriaErros"" ganha 1 registro com o código ""(\w+)"" e o nivel ""(\w+)""$")]
    public async Task UmErro(string codigo, string nivel)
    {
        var erro = await UmNovoErro();
        Assert.Equal(codigo, Auditoria.Texto(erro, "codigo"));
        Assert.Equal(nivel, Auditoria.Texto(erro, "nivel"));
    }

    [Then(@"^os dois registros têm o mesmo traceId$")]
    public async Task MesmoTraceIdNasDuas()
    {
        Assert.NotNull(registro);
        Assert.Equal(TraceId(registro), TraceId(await UmNovoErro()));
    }

    [Then(@"^nenhum registro ""CONCLUIDO"" é gravado com esse traceId$")]
    public async Task SemConcluido()
    {
        Assert.NotNull(registro);
        var traceId = TraceId(registro);
        var concluidos = (await Auditoria.Todos(Auditoria.Registros))
            .Where(d => Auditoria.Texto(d, "traceId") == traceId && Auditoria.Texto(d, "etapa") == "CONCLUIDO").ToList();
        Assert.True(concluidos.Count == 0, $"a execução recusada gravou {Auditoria.Resumo(concluidos)}");
    }

    // "antes de a recusa sair do caso de uso": o registro já está gravado
    // quando a resposta chega, com a hora do pedido
    [Then(@"^""auditoriaErros"" ganha o registro com o nivel ""(\w+)"" antes de a recusa sair do caso de uso$")]
    public async Task ErroComNivel(string nivel)
    {
        var erro = await UmNovoErro();
        Assert.Equal(nivel, Auditoria.Texto(erro, "nivel"));
        NaHoraDoPedido(erro);
    }

    [Given(@"^o registro ""auderr-\d+"" da sigla ""(\w+)"" repetida, gravado em \d{4}-\d{2}-\d{2}$")]
    public async Task SiglaRepetida(string sigla)
    {
        await mundo.Conta();
        await Executar(null, "POST", "/contas", ContaDaSigla(sigla));
        Exigir(mundo.Ultima, 409, $"POST /contas com a sigla {sigla} repetida");
        registro = await UmNovoErro();
    }

    [Then(@"^ele fica guardado até \d{4}-\d{2}-\d{2}, 5 anos depois, como na RN-178$")]
    public void GuardadoComoAAuditoria()
    {
        Assert.NotNull(registro);
        GuardadoCincoAnos(registro);
    }

    [Then(@"^não pode ser alterado nem apagado, como na RNF-206$")]
    public static void Imutavel() => throw new PendingStepException(
        "nenhum endereço da API altera ou apaga a auditoria, e a recusa do banco é conferida nos testes do backend, como na RNF-206");

    // ── traceId (RN-271) ───────────────────────────────────────────────────
    [Then(@"^cada execução tem um traceId próprio, diferente do da outra$")]
    public async Task TraceIdsDiferentes()
    {
        Assert.True(execucoes.Count == 2, $"esperava 2 execuções, vieram {execucoes.Count}");
        var traceIds = new List<string>();
        for (var i = 0; i < execucoes.Count; i++)
        {
            var novos = await Novos(Auditoria.Registros, i);
            Assert.True(novos.Count > 0, $"a execução {i + 1} não gravou nada em \"auditoria\"");
            traceIds.Add(MesmoTraceId(novos));
        }
        Assert.NotEqual(traceIds[0], traceIds[1]);
    }

    [Then(@"^os registros ""(\w+)"" e ""(\w+)"" de cada uma repetem o traceId dela$")]
    public async Task CadaUmaRepete(string primeira, string segunda)
    {
        for (var i = 0; i < execucoes.Count; i++)
        {
            var novos = await Novos(Auditoria.Registros, i);
            Assert.Equal(new[] { primeira, segunda }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
            MesmoTraceId(novos);
        }
    }

    [Then(@"^o valor de X-Trace-Id é o traceId gravado em ""INICIADO"" e ""CONCLUIDO""$")]
    public async Task TraceIdDosRegistros()
    {
        var traceId = TraceIdDoCabecalho();
        var novos = await Novos(Auditoria.Registros);
        Assert.Equal(new[] { "INICIADO", "CONCLUIDO" }, novos.Select(d => Auditoria.Texto(d, "etapa")).ToArray());
        Assert.All(novos, d => Assert.Equal(traceId, TraceId(d)));
    }

    [Then(@"^o valor de X-Trace-Id é o traceId gravado em ""INICIADO"" e ""auditoriaErros""$")]
    public async Task TraceIdDoErro()
    {
        var traceId = TraceIdDoCabecalho();
        var novos = await Novos(Auditoria.Registros);
        var iniciado = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == "INICIADO");
        Assert.True(iniciado is not null, $"esperava o registro INICIADO em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        Assert.Equal(traceId, TraceId(iniciado));
        Assert.Equal(traceId, TraceId(await UmNovoErro()));
    }

    // "sem log" é conferido por inspeção do caso de uso, não por teste
    [Given(@"^o traceId ""trc-7f3a9c21"" de uma execução de POST /contas recusada com 409 SIGLA_EM_USO$")]
    [When(@"^o time deboaz procura o que aconteceu nessa execução$")]
    [Then(@"^encontra tudo nas coleções ""auditoria"" e ""auditoriaErros"" pelo traceId ""trc-7f3a9c21""$")]
    [Then(@"^o caso de uso não escreve nada em log; .+$")]
    public static void PorInspecao() => throw new PendingStepException(
        "acompanhamento e ausência de log: conferidos por inspeção do caso de uso, não pela suíte (o traceId já é coberto pelos outros cenários da RN-271)");

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

    async Task Executar(Pessoa? quem, string metodo, string caminho, string? corpo, string? ifMatch = null)
    {
        pedido = (metodo, caminho, corpo);
        var antes = await Auditoria.Marcar();
        execucoes.Add(quem is null
            ? await mundo.Enviar(metodo, caminho, corpo, ifMatch: ifMatch)
            : await mundo.EnviarComo(quem, metodo, caminho, corpo, ifMatch));
        marcas.Add((antes, await Auditoria.Marcar()));
    }

    // os registros da última execução (até agora), ou da execução i (entre as
    // marcas de antes e de depois dela)
    Task<List<BsonDocument>> Novos(string colecao) => Novos(colecao, marcas.Count - 1);

    Task<List<BsonDocument>> Novos(string colecao, int i)
    {
        Assert.True(marcas.Count > i && i >= 0, "nenhum pedido marcado neste cenário");
        return Auditoria.Novos(colecao, marcas[i].Antes, i + 1 < marcas.Count ? marcas[i].Depois : null);
    }

    async Task<BsonDocument> UmNovoErro()
    {
        var erros = await Novos(Auditoria.Erros);
        Assert.True(erros.Count == 1, $"esperava 1 registro novo em \"auditoriaErros\", vieram {erros.Count}: {Auditoria.Resumo(erros)}");
        return erros[0];
    }

    string TraceIdDoCabecalho()
    {
        var traceId = mundo.Ultima.Cabecalho("X-Trace-Id");
        Assert.False(string.IsNullOrWhiteSpace(traceId), $"a resposta veio sem o cabeçalho X-Trace-Id: {mundo.Ultima}");
        return traceId!;
    }

    static string TraceId(BsonDocument d)
    {
        var traceId = Auditoria.Texto(d, "traceId");
        Assert.False(string.IsNullOrWhiteSpace(traceId), $"registro sem traceId: {d.ToJson()}");
        return traceId!;
    }

    static string MesmoTraceId(List<BsonDocument> registros)
    {
        var traceIds = registros.Select(TraceId).Distinct().ToList();
        Assert.True(traceIds.Count == 1, $"esperava um só traceId, vieram {string.Join(", ", traceIds)}");
        return traceIds[0];
    }

    void NaHoraDoPedido(BsonDocument d)
    {
        var quando = Auditoria.Data(d, "quando");
        Assert.True(quando is { } q && q >= mundo.HoraDoPedido - Folga && q <= DateTimeOffset.UtcNow + Folga,
            $"esperava a hora do pedido ({mundo.HoraDoPedido:O}), veio {d.ToJson()}");
    }

    static void GuardadoCincoAnos(BsonDocument d)
    {
        var quando = Auditoria.Data(d, "quando");
        var guardarAte = Auditoria.Data(d, "guardarAte");
        Assert.True(quando is { } q && guardarAte is { } g && (g - q.AddYears(5)).Duration() < TimeSpan.FromSeconds(1),
            $"esperava guardarAte 5 anos depois de quando, veio {d.ToJson()}");
    }

    static string Participante(string nome) => new JsonObject
    {
        ["tipo"] = "PARTICIPANTE",
        ["nome"] = nome == "Lucas" ? "Lucas Andrade" : "Pedro Alves",
        ["nomeCurto"] = nome.ToLowerInvariant(),
        ["matricula"] = null,
        ["responsaveis"] = new JsonArray(),
    }.ToJsonString();

    // a COLSA dos exemplos; outra sigla vai com documento e domínio próprios,
    // para a recusa ser só a da sigla (409 SIGLA_EM_USO)
    static string ContaDaSigla(string sigla) => sigla == "COLSA"
        ? ContaNova("INSTITUICAO", "Colégio Santo Antônio", "98.765.432/0001-10", "colegio_sa", sigla)
        : ContaNova("INSTITUICAO", "Escola Nova", Cnpj(20000001), $"dominio_{sigla.ToLowerInvariant()}", sigla);
}
