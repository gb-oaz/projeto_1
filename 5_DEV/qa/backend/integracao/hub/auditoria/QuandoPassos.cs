using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Hub.Auditoria.AuditoriaDoTime;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Auditoria;

// Os Quando de 1_Requisitos/20_modulo-auditoria.md (v03, Cenários de API): o pedido
// à Auditoria, os ganchos e os pedidos medidos. Ver DadosPassos.cs.
[Binding]
public sealed partial class QuandoPassos(Mundo mundo, Api api, Partida partida, AuditoriaDoTime a, ScenarioContext cenario, EntaoPassos entao)
{
    bool Titulo(params string[] titulos) => titulos.Contains(cenario.ScenarioInfo.Title);
    bool Tag(string tag) => cenario.ScenarioInfo.Tags.Contains(tag);

    // ── "<quem> envia <MÉTODO> /auditoria/…" ───────────────────────────────
    [When(@"^(o administrador do deboaz|o Caio|o Rui|a conta master da EBFGA|a staff Maria|alguém sem o cabeçalho Authorization)(?:,? (?:com|sem|membro do time sem)[^,]*?)?,? envia (GET|POST|PUT|PATCH|DELETE) (/auditoria/.*)$")]
    public async Task Envia(string quem, string metodo, string resto) => await Pedido(await a.Quem(quem), metodo, resto);

    // sem "o Caio": o do Dado (RN-325, RF-194); "envia GET <caminho>" sozinho é o de commons/autorizacao
    [When(@"^envia (GET) (/auditoria/\S+) (?:às|em) (.+)$")]
    public async Task EnviaComHora(string metodo, string caminho, string resto) =>
        await Pedido(mundo.Sujeito ?? await a.Caio(), metodo, caminho + " às " + resto);

    async Task Pedido(Pessoa? quem, string metodo, string resto)
    {
        int? esperado = null;
        DateTimeOffset? em = null;
        var m = Regex.Match(resto, @" e a resposta é (\d{3})$");
        if (m.Success) { esperado = int.Parse(m.Groups[1].Value); resto = resto[..m.Index]; }
        m = Regex.Match(resto, @" (?:às|em) (\S+)$");
        if (m.Success) { em = Hora(m.Groups[1].Value); resto = resto[..m.Index]; }
        resto = resto.Replace(" com Accept \"text/event-stream\"", "");
        resto = resto.Replace("[12h a 13h]", "de=2026-11-20T12:00:00Z&ate=2026-11-20T13:00:00Z");
        resto = resto.Replace(" com \"de\" depois de \"ate\"", "?de=2026-11-20T10:00:00Z&ate=2026-11-19T10:00:00Z");
        foreach (var sem in new[] { "sem o parâmetro", "sem parâmetros", "sem de nem ate", "sem a ordem", "sem mais parâmetros" })
        {
            if (resto.EndsWith(sem, StringComparison.Ordinal)) resto = resto[..^sem.Length];
        }
        var caminho = resto;
        if (em is { } h) await a.Saltar(h - TimeSpan.FromMinutes(1));
        await Fixtures(quem, caminho);
        if (caminho.Contains("&depois= o proximo da 2ª", StringComparison.Ordinal))
        {
            var @base = caminho[..caminho.IndexOf("&depois=", StringComparison.Ordinal)];
            var p1 = (await a.Pedir(quem, metodo, @base, em)).Corpo;
            var p2 = (await a.Pedir(quem, metodo, $"{@base}&depois={p1?["proximo"]?.GetValue<string>()}", em)).Corpo;
            caminho = $"{@base}&depois={p2?["proximo"]?.GetValue<string>()}";
        }
        var r = await a.Pedir(quem, metodo, caminho, em);
        if (esperado is { } e) Respostas.Confere(r, e);
        Instalar(caminho);
    }

    // o que o cenário não traz em Dado: dados de exemplo para os filtros, as execuções e os endereços
    async Task Fixtures(Pessoa? quem, string caminho)
    {
        if (quem is null || caminho.StartsWith("/auditoria/registros/ao-vivo", StringComparison.Ordinal)) return;
        if (caminho.StartsWith("/auditoria/registros", StringComparison.Ordinal) && Titulo("filtros")) await a.SemearRegistros();
        if (caminho.StartsWith("/auditoria/registros?traceId=trc-4b7e01c9", StringComparison.Ordinal))
        {
            var ini = a.Agora() - TimeSpan.FromDays(20);
            await a.Registrar([
                Registro("trc-4b7e01c9", "INICIADO", "CRIAR_USUARIO", ini),
                Registro("trc-4b7e01c9", "ERRO", "CRIAR_USUARIO", ini + TimeSpan.FromMilliseconds(2107), "BANCO_INDISPONIVEL"),
            ]);
        }
        if (caminho.StartsWith("/auditoria/execucoes?", StringComparison.Ordinal) && (Tag("RF-203")) && !caminho.Contains("PERDIDA", StringComparison.Ordinal))
        {
            await a.SemearExecucoes();
        }
        if (caminho.StartsWith("/auditoria/metricas/enderecos", StringComparison.Ordinal)) await SemearEnderecos();
        if (Tag("RF-205") && !caminho.Contains("&ate=", StringComparison.Ordinal)) await SemearOcorrencias();
    }

    // o intermediador fora de 13:30 a 13:52 e o MOD-01 LENTO desde 13:31; o relógio fecha em 14:00:00
    async Task SemearOcorrencias()
    {
        if (ocorrenciasSemeadas) return;
        ocorrenciasSemeadas = true;
        await a.Saltar(Hora("13:30:00"));
        await a.Sonda("intermediador-pagamento", "NENHUMA");
        await a.Verificar();
        await a.Saltar(Hora("13:31:00"));
        await a.Medir([
            Medida("GET", "/pagamento/faturas", 200, 400, 96, a.Agora() - TimeSpan.FromMinutes(2)),
            Medida("GET", "/pagamento/faturas", 500, 400, 4, a.Agora() - TimeSpan.FromMinutes(2)),
        ]);
        await a.Verificar();
        await a.Saltar(Hora("13:52:00"));
        await a.SondaFim("intermediador-pagamento");
        await a.Verificar();
        a.HoraFixa = Hora("14:00:00");
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
    }
    bool ocorrenciasSemeadas;

    // três endereços com perfis diferentes: ordenar por p95, por pedidos e por erros
    async Task SemearEnderecos()
    {
        var minuto = a.Agora() - TimeSpan.FromMinutes(10);
        await a.Medir([
            Medida("POST", "/pagamento/faturas/{id}/pagamentos", 200, 500, 30, minuto),
            Medida("POST", "/pagamento/faturas/{id}/pagamentos", 200, 3000, 7, minuto),
            Medida("POST", "/pagamento/faturas/{id}/pagamentos", 422, 500, 3, minuto),
            Medida("POST", "/pagamento/faturas/{id}/pagamentos", 500, 3000, 2, minuto),
            Medida("GET", "/sac/chamados", 200, 90, 100, minuto),
            Medida("GET", "/catalogo/vitrine", 200, 40, 5, minuto),
            Medida("GET", "/catalogo/vitrine", 500, 40, 5, minuto),
        ]);
    }

    // o corpo do exemplo tem listas que o ambiente não reproduz por inteiro: o que sai da comparação
    // direta é conferido pelos Então seguintes ou pela conferência extra
    void Instalar(string caminho)
    {
        if (caminho.Contains("/historico", StringComparison.Ordinal)) mundo.CamposSemConferir.Add("serie");
        if (caminho.StartsWith("/auditoria/metricas?janela=1h", StringComparison.Ordinal)) mundo.CamposSemConferir.Add("serie");
        if (caminho.StartsWith("/auditoria/card", StringComparison.Ordinal)) { mundo.CamposSemConferir.Add("versao"); }
        if (caminho.StartsWith("/auditoria/ocorrencias/abertas", StringComparison.Ordinal)) mundo.ConferirDoExemplo = e => { entao.AbertasEVoltaram(e!); return Task.CompletedTask; };
        if (caminho.StartsWith("/auditoria/execucoes/trc-", StringComparison.Ordinal))
        {
            mundo.CamposSemConferir.Add("passos");
            mundo.ConferirDoExemplo = _ => { ConfereDesativar(); return Task.CompletedTask; };
        }
    }

    // os 4 passos de DESATIVAR_USUARIO, em ordem de quando, e a duração do INICIADO ao CONCLUIDO
    void ConfereDesativar()
    {
        var corpo = mundo.Ultima.Corpo;
        var passos = Lista(corpo, "passos");
        var esperado = new[] { ("DESATIVAR_USUARIO", "INICIADO", false), ("ENCERRAR_SESSOES_DO_USUARIO", "INICIADO", true),
            ("ENCERRAR_SESSOES_DO_USUARIO", "CONCLUIDO", true), ("DESATIVAR_USUARIO", "CONCLUIDO", false) };
        Assert.True(passos.Count == esperado.Length, $"esperava {esperado.Length} passos, vieram {passos.Count}: {corpo}");
        for (var i = 0; i < esperado.Length; i++)
        {
            Assert.True(Txt(passos[i], "acao") == esperado[i].Item1 && Txt(passos[i], "etapa") == esperado[i].Item2
                && passos[i]!["interna"]!.GetValue<bool>() == esperado[i].Item3, $"passo {i}: veio {passos[i]}");
        }
        var inicio = DateTimeOffset.Parse(Txt(corpo, "inicio")!);
        var fim = DateTimeOffset.Parse(Txt(corpo, "fim")!);
        Assert.True(corpo!["duracaoMs"]!.GetValue<long>() == (long)(fim - inicio).TotalMilliseconds, $"duracaoMs diferente de fim - inicio: {corpo}");
    }

    // ── ganchos ────────────────────────────────────────────────────────────
    [When(@"^envia POST /_teste/auditoria/verificacao$")]
    public async Task Verificacao()
    {
        var t = Cronometro.Agora;
        await mundo.Enviar("POST", "/_teste/auditoria/verificacao");
        a.UltimaDuracao = Cronometro.Agora - t;
    }

    [When(@"^envia POST /_teste/auditoria/verificacao (\d+) vez(?:es)?$")]
    public async Task VerificacaoVezes(int vezes) => await Verificar(vezes);

    [When(@"^envia POST /_teste/auditoria/verificacao às (\S+)$")]
    public async Task VerificacaoAs(string hora)
    {
        await a.Saltar(Hora(hora));
        await a.Verificar();
    }

    [When(@"^a verificação seguinte o mostra em ""(SEM_VERIFICACAO|DESLIGADO)""$")]
    public static void MostraEm(string estado) => throw new PendingStepException(
        $"{estado} do cache não se provoca pelas sondas de /_teste/auditoria (NORMAL, LENTA, NENHUMA, RECUSADA, ERRO): lacuna para o deboaz-arc");

    [When(@"^envia POST /_teste/auditoria/verificacao e GET /auditoria/saude$")]
    public async Task VerificacaoESaude() => await VerificarESaude(1);

    [When(@"^envia POST /_teste/auditoria/verificacao (\d+) vez(?:es)? e GET /auditoria/saude$")]
    public async Task VerificacaoVezesESaude(int vezes) => await VerificarESaude(vezes);

    async Task Verificar(int vezes)
    {
        if (a.HoraFixa is { } h) await a.Acertar(h);
        await a.Verificar(vezes);
    }

    async Task VerificarESaude(int vezes)
    {
        await Verificar(vezes);
        await a.Pedir(mundo.Sujeito ?? await a.Caio(), "GET", "/auditoria/saude");
    }

    [When(@"^envia POST /_teste/auditoria/agendamento e depois GET /auditoria/card$")]
    public async Task AgendamentoECard()
    {
        await a.Agendamento();
        await a.Pedir(mundo.Sujeito ?? await a.Caio(), "GET", "/auditoria/card", Hora("13:42:00"));
    }

    [When(@"^envia POST /_teste/auditoria/medicoes com ""duracaoMs"": -1$")]
    public async Task MedicaoInvalida()
    {
        mundo.Sujeito ??= await a.Caio();
        await mundo.Enviar("POST", "/_teste/auditoria/medicoes", new JsonArray(Medida("GET", "/sac/chamados", 200, -1, 1, a.Agora())).ToJsonString());
    }

    [When(@"^envia POST /_teste/auditoria/medicoes/gravar$")]
    public async Task GravarMedicoes() => await a.GravarMedicoes();

    [When(@"^envia POST /_teste/auditoria/registros$")]
    public async Task RegistrosFora() => await mundo.Enviar("POST", "/_teste/auditoria/registros", new JsonArray(
        Registro(NovoTraceId(), "INICIADO", "CRIAR_USUARIO", a.Agora())).ToJsonString());

    // a tabela dos ganchos: a instância sobe em (produção|homologação) com o flag (Partida)
    [When(@"^envia (PUT|POST|DELETE) …/([\w/-]+), com ""resposta"": ""NORMAL"" no corpo do PUT$")]
    public async Task Gancho(string metodo, string endereco) =>
        mundo.Guardar(await partida.Api.Enviar(metodo, $"/_teste/auditoria/{endereco}", metodo == "PUT" ? "{\"resposta\": \"NORMAL\"}" : null));

    // o corpo do PUT das sondas (v05): o que falta ou não vale é 422 DADOS_INVALIDOS
    [When(@"^envia PUT /_teste/auditoria/sondas/cache com (.+)$")]
    public async Task SondaComCorpo(string corpo)
    {
        mundo.Sujeito ??= await a.Caio();
        mundo.Guardar(await a.SondaBruta("cache", "{" + corpo.Replace("só ", "", StringComparison.Ordinal) + "}"));
    }

    // ── medições e registros injetados (v05: a medição vale na hora; lista vazia é 422) ──
    [When(@"^envia POST /_teste/auditoria/medicoes com a lista vazia$")]
    public async Task MedicoesVazias() => await mundo.Enviar("POST", "/_teste/auditoria/medicoes", "[]");

    [When(@"^envia POST /_teste/auditoria/medicoes com (\d+) pedidos a ""GET (/sac/chamados)"" em (\S+)$")]
    public async Task DezMedicoes(int total, string endereco, string hora)
    {
        mundo.Sujeito ??= await a.Caio();
        await a.Medir([Medida("GET", endereco, 200, 100, total, Hora(hora))]);
    }

    [When(@"^envia POST /_teste/auditoria/registros com (1 registro sem ""quem"" nem ""conta""|1 registro sem ""acao""|""quem"": ""usr-nao-existe""|""conta"": ""conta-nao-existe""|a lista vazia)$")]
    public async Task RegistrosComCorpo(string corpo)
    {
        mundo.Sujeito ??= await a.Caio();
        var item = Registro(a.TraceIdDoGancho = NovoTraceId(), "INICIADO", "CRIAR_USUARIO", a.Agora());
        if (corpo.StartsWith("1 registro sem \"acao\"", StringComparison.Ordinal)) item.Remove("acao");
        if (corpo.StartsWith("\"quem\"", StringComparison.Ordinal)) item["quem"] = "usr-nao-existe";
        if (corpo.StartsWith("\"conta\"", StringComparison.Ordinal)) item["conta"] = "conta-nao-existe";
        var lista = corpo == "a lista vazia" ? new JsonArray() : new JsonArray(item);
        mundo.Guardar(await api.Enviar("POST", "/_teste/auditoria/registros", lista.ToJsonString()));
    }

    // o registro do gancho com quem, conta e alvo (RN-318): a segunda frase completa a primeira
    [When(@"^envia POST /_teste/auditoria/registros com 1 registro de ""acao"": ""CRIAR_USUARIO""$")]
    public async Task UmRegistro()
    {
        mundo.Sujeito ??= await a.Caio();
        a.TraceIdDoGancho = NovoTraceId();
        await Task.CompletedTask;
    }

    [When(@"^""quem"": ""usr-ebfga-maria"", ""conta"": ""conta-ebfga"" e ""alvo"": ""usr-ebfga-lucas""$")]
    public async Task ComQuemEConta()
    {
        var (maria, conta, _) = await a.Ids();
        var item = Registro(a.TraceIdDoGancho!, "INICIADO", "CRIAR_USUARIO", a.Agora(), null, maria, conta, await mundo.Traduzir("usr-ebfga-lucas"));
        mundo.Guardar(await api.Enviar("POST", "/_teste/auditoria/registros", new JsonArray(item).ToJsonString()));
    }

    // ── pedidos medidos ────────────────────────────────────────────────────
    [When(@"^a conta master envia 10 vezes GET /sac/chamados$")]
    public async Task DezVezes()
    {
        mundo.Sujeito = await a.Caio();
        a.Base = (await a.Pedir(await a.Caio(), "GET", "/auditoria/metricas/enderecos?janela=1h&modulo=MOD-02")).Corpo;
        var master = await mundo.Master();
        for (var i = 0; i < 10; i++) await mundo.EnviarComo(master, "GET", "/sac/chamados");
    }

    // o endereço pelo modelo da rota: chega, o minuto é gravado e a Auditoria diz o módulo
    [When(@"^chega (GET|POST) (?!/auth/)(\S+)$")]
    public async Task Chega(string metodo, string endereco)
    {
        await Enviar(metodo, endereco);
        a.PedidoMedido = $"{metodo} {endereco}";
    }

    [When(@"^chega (GET|POST) (\S+) e o minuto é gravado$")]
    public async Task ChegaEGravado(string metodo, string endereco)
    {
        var caio = await a.Caio();
        var antes = Chaves((await a.Pedir(caio, "GET", "/auditoria/metricas/enderecos?janela=1h&limite=100")).Corpo);
        await a.GravarMedicoes();
        await Enviar(metodo, endereco);
        await a.GravarMedicoes();
        var depois = Chaves((await a.Pedir(caio, "GET", "/auditoria/metricas/enderecos?janela=1h&limite=100")).Corpo);
        depois.ExceptWith(antes);
        depois.Remove("GET /auditoria/metricas/enderecos");
        a.Base = new JsonArray([.. depois.Select(d => (JsonNode)d)]);
    }

    static HashSet<string> Chaves(JsonNode? corpo) =>
        [.. Lista(corpo, "itens").Select(i => $"{Txt(i, "metodo")} {Txt(i, "endereco")}")];

    async Task Enviar(string metodo, string endereco)
    {
        var caminho = endereco.Replace("{id}", "qa-sem-id");
        if (caminho == "/_teste/auditoria/verificacao") { await a.Verificar(); return; }
        if (caminho == "/auditoria/registros/ao-vivo") { await a.Abrir(await a.Caio(), caminho); return; }
        var token = caminho.StartsWith("/auditoria", StringComparison.Ordinal) ? null : (await mundo.SessaoParaPedido(await mundo.Master())).Acesso;
        if (caminho.StartsWith("/auditoria", StringComparison.Ordinal)) { await mundo.EnviarComo(await a.Caio(), metodo, caminho); return; }
        await api.Enviar(metodo, caminho, metodo == "POST" ? "{}" : null, token);
    }

    // ── ao vivo ────────────────────────────────────────────────────────────
    [When(@"^a execução ""(trc-\w+)"" grava um erro em ""auditoriaErros""$")]
    public async Task GravaErro(string traceId) =>
        await a.Registrar([Registro(traceId, "ERRO", "CRIAR_USUARIO", a.Agora(), "BANCO_INDISPONIVEL")]);

    [When(@"^a Maria envia POST /usuarios do participante Lucas e a resposta é 201$")]
    public async Task MariaCriaLucas()
    {
        var maria = await a.MariaDoUsuarios();
        await mundo.SessaoParaPedido(maria);
        Exigir(await mundo.EnviarComo(maria, "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "PARTICIPANTE", ["nome"] = "Lucas Andrade", ["nomeCurto"] = "lucas", ["matricula"] = null, ["responsaveis"] = new JsonArray(),
        }.ToJsonString()), 201, "POST /usuarios do Lucas pela Maria");
    }

    [When(@"^abre a 4ª$")]
    public async Task QuartaConexao() => await a.Abrir(await a.Caio(), "/auditoria/registros/ao-vivo");

    [When(@"^abre GET (/auditoria/ocorrencias/ao-vivo) mais uma vez$")]
    public async Task MaisUma(string caminho) => await a.Abrir(await a.Caio(), caminho);

    [When(@"^a verificação das (\S+) mostra o cache LENTO$")]
    public async Task VerificacaoMostraLento(string hora)
    {
        await a.Acertar(Hora(hora) - TimeSpan.FromSeconds(1));
        await a.Sonda("cache", "LENTA");
        await a.Verificar();
    }

    [When(@"^o cache passa a LENTO$")]
    public async Task CachePassaALento()
    {
        await a.Sonda("cache", "LENTA");
        await a.Verificar();
    }

    // ── repetir ────────────────────────────────────────────────────────────
    [When(@"^o relógio marca (\S+)$")]
    public async Task RelogioMarca(string hora) =>
        await a.Pedir(a.UltimoQuem, "GET", a.UltimoCaminho, Hora(hora));

    [When(@"^envia o mesmo GET às (\S+)$")]
    public async Task MesmoGet(string hora) =>
        await a.Pedir(a.UltimoQuem ?? await a.Caio(), "GET", a.UltimoCaminho, Hora(hora));

    // ── formato comum ──────────────────────────────────────────────────────
    [When(@"^um pedido à Auditoria é recusado$")]
    public async Task PedidoRecusado() =>
        await a.Pedir(await a.Caio(), "GET", "/auditoria/registros?de=2026-09-01T00:00:00Z&ate=2026-11-20T00:00:00Z");

    [When(@"^a resposta da Auditoria é (.+)$")]
    public async Task RespostaDaAuditoria(string resposta)
    {
        var caio = await a.Caio();
        switch (resposta)
        {
            case "403 ACESSO_NEGADO": await a.Pedir(await a.Rui(), "GET", "/auditoria/saude"); break;
            case "404 SERVICO_INEXISTENTE": await a.Pedir(caio, "GET", "/auditoria/saude/fila/historico"); break;
            case "404 EXECUCAO_INEXISTENTE": await a.Pedir(caio, "GET", "/auditoria/execucoes/trc-00000000"); break;
            case "422 DADOS_INVALIDOS, campo \"janela\"": await a.Pedir(caio, "GET", "/auditoria/metricas?janela=2h"); break;
            case "422 PERIODO_LONGO, em /registros": await a.Pedir(caio, "GET", "/auditoria/registros?de=2026-09-01T00:00:00Z&ate=2026-11-20T00:00:00Z"); break;
            case "422 PERIODO_LONGO, em /execucoes": await a.Pedir(caio, "GET", "/auditoria/execucoes?de=2026-11-01T00:00:00Z&ate=2026-11-20T00:00:00Z"); break;
            case "422 PERIODO_LONGO, em /ocorrencias": await a.Pedir(caio, "GET", "/auditoria/ocorrencias?de=2026-09-01T00:00:00Z&ate=2026-11-20T00:00:00Z"); break;
            case "422 PERIODO_INVERTIDO": await a.Pedir(caio, "GET", "/auditoria/registros?de=2026-11-20T00:00:00Z&ate=2026-11-19T00:00:00Z"); break;
            case "503 BANCO_DE_DADOS_FORA":
                await a.Sonda("banco", "NENHUMA");
                await a.Verificar();
                await a.Pedir(caio, "GET", "/auditoria/registros");
                break;
            default: throw new ArgumentException($"resposta da Auditoria sem pedido: {resposta}");
        }
    }
}
