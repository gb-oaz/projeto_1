using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Driver;
using Reqnroll;
using static Boaz.Qa.Integracao.Hub.Auditoria.AuditoriaDoTime;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Auditoria;

// Os Dado e os Quando de 1_Requisitos/20_modulo-auditoria.md (v03, Cenários de API):
// o estado preparado pelos ganchos /_teste/auditoria/* (AuditoriaDoTime.cs) e o
// "<quem> envia <MÉTODO> /auditoria/…", que é um passo só: o resto da frase diz a
// hora ("às 13:42:00"), o status esperado ("e a resposta é 200") e a consulta; o
// que o cenário não traz em Dado (registros, execuções e endereços de exemplo) vem
// das fixtures de AuditoriaDoTime. "envia GET <caminho>" sem mais nada é o de
// commons/autorizacao (SujeitoPede): estes cenários põem o Caio em Mundo.Sujeito.
[Binding]
public sealed partial class DadosPassos(Mundo mundo, Partida partida, AuditoriaDoTime a,
    ScenarioContext cenario, Hub.Usuarios.IndicesPassos indices)
{
    const string Sujeitos = "o administrador do deboaz|o Caio|o Rui|a conta master da EBFGA|a staff Maria|alguém sem o cabeçalho Authorization";

    // 250 pedidos "em 13:40" esperam a frase seguinte, que diz quantos com erro e o tempo
    (int Total, string Metodo, string Endereco, DateTimeOffset Minuto)? medicaoPendente;

    bool Titulo(params string[] titulos) => titulos.Contains(cenario.ScenarioInfo.Title);

    // ── quem pede ──────────────────────────────────────────────────────────
    [Given(@"^o token do Caio, com ""MOD-17"" com ""U""$")]
    [Given(@"^o token do Caio, com ""MOD-17"" com ""U"", em produção$")]
    public async Task TokenDoCaio() => mundo.Sujeito = await a.Caio();

    // ── ambiente e relógio ─────────────────────────────────────────────────
    // o backend de API_URL já é o de homologação com Teste__Relogio=sim (ci.sh)
    [Given(@"^a aplicação de homologação, com Teste__Relogio ""sim"", e o cache NO_AR$")]
    [Given(@"^a aplicação de homologação, com Teste__Relogio ""sim"", e o cache NO_AR sem ocorrência$")]
    public async Task CacheNoAr()
    {
        mundo.Sujeito = await a.Caio();
        await a.Sonda("cache", "NORMAL", 10);
        a.Definidos.Add("cache");
        await a.Verificar();
    }

    [Given(@"^a aplicação de homologação, com Teste__Relogio ""sim"", às (\S+)$")]
    public async Task HomologacaoAs(string hora)
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora(hora) - TimeSpan.FromSeconds(10));
        a.HoraFixa = Hora(hora);
    }

    [Given(@"^a aplicação de produção, com Teste__Relogio ""sim""$")]
    public static void Producao() => throw new PendingStepException(
        "produção: a suíte roda contra o backend de homologação (API_URL); o 404 do gancho em produção é o cenário da Partida em RN-315");

    [Given(@"^que ""…/"" abrevia ""/_teste/auditoria/"" nos exemplos$")]
    public static void Abreviacao() { }

    [Given(@"^o relógio em (\S+)$")]
    public async Task RelogioEm(string hora)
    {
        await a.Saltar(Hora(hora) - TimeSpan.FromSeconds(10));
        a.HoraFixa = Hora(hora);
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
    }

    // ── serviços e sondas ──────────────────────────────────────────────────
    [Given(@"^o intermediador de pagamento fora do ar desde (\S+)$")]
    [Given(@"^o intermediador fora do ar desde (\S+)$")]
    public async Task IntermediadorForaDesde(string quando)
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora(quando));
        await a.Sonda("intermediador-pagamento", "NENHUMA");
        a.Definidos.Add("intermediador-pagamento");
        await a.Verificar();
        a.QuedaDesde = Hora(quando);
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
    }

    [Given(@"^o intermediador fora de 13:30 a 13:52 e de 06:10 a 06:12 de 2026-11-20, em UTC$")]
    public async Task DuasQuedas()
    {
        mundo.Sujeito = await a.Caio();
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
        // RECUSADA tem a 3ª falha seguida para sair do ar; NENHUMA sai na 1ª
        await a.Saltar(Hora("06:10"));
        await a.Sonda("intermediador-pagamento", "RECUSADA");
        await a.Verificar(3);
        await a.Saltar(Hora("06:12"));
        await a.SondaFim("intermediador-pagamento");
        await a.Verificar();
        await a.Saltar(Hora("13:30"));
        await a.Sonda("intermediador-pagamento", "NENHUMA");
        await a.Verificar();
        await a.Saltar(Hora("13:52"));
        await a.SondaFim("intermediador-pagamento");
        await a.Verificar();
    }

    [Given(@"^todos os serviços NO_AR$")]
    public async Task TodosNoAr() { mundo.Sujeito = await a.Caio(); await a.TodosNoAr(); }

    [Given(@"^o armazenamento DESLIGADO e os outros NO_AR$")]
    public static void Desligado() => throw new PendingStepException(
        "DESLIGADO vem da configuração da aplicação (armazenamento de anexos desligado), e as sondas de /_teste/auditoria não têm essa resposta (NORMAL, LENTA, NENHUMA, RECUSADA, ERRO): lacuna para o deboaz-arc");

    [Given(@"^o cache LENTO e os outros NO_AR$")]
    public async Task CacheLento()
    {
        mundo.Sujeito = await a.Caio();
        await a.TodosNoAr();
        await a.Sonda("cache", "LENTA");
        await a.Verificar();
    }

    [Given(@"^o cache LENTO e o intermediador FORA_DO_AR$")]
    public async Task CacheLentoEIntermediadorFora()
    {
        await CacheLento();
        await a.Sonda("intermediador-pagamento", "NENHUMA");
        await a.Verificar();
    }

    [Given(@"^PUT /_teste/auditoria/sondas/([\w-]+) com (.+?)(?:, em homologação)?$")]
    public async Task SondaCom(string servico, string corpo)
    {
        mundo.Sujeito ??= await a.Caio();
        var json = corpo.StartsWith("\"ultimaExecucao\"", StringComparison.Ordinal)
            ? "{\"resposta\": \"NORMAL\", " + corpo + "}"
            : "{" + corpo + "}";
        await a.SondaComCorpo(servico, json);
        a.Definidos.Add(servico);
        if (servico == "agendamento-sac") ultimaDoAgendamento = Regex.Match(corpo, @"""ultimaExecucao"": ""([^""]+)""").Groups[1].Value;
    }

    string ultimaDoAgendamento = "";

    // RN-315: para os agendamentos a sonda usa só "ultimaExecucao" e ignora "resposta"
    [Given(@"^""resposta"": ""ERRO"" no mesmo corpo$")]
    public async Task RespostaNoMesmoCorpo() =>
        await a.SondaComCorpo("agendamento-sac", "{\"resposta\": \"ERRO\", \"ultimaExecucao\": \"" + ultimaDoAgendamento + "\"}");

    // RN-324: o MOD-01 LENTO pelas medições injetadas (1ª falha seguida) e depois sem pedidos
    [Given(@"^o MOD-01 LENTO desde (\S+), com a ocorrência aberta pelas medições injetadas$")]
    public async Task ModuloLentoComOcorrencia(string hora)
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora(hora));
        await MedirPagamento(2);
        await a.Verificar();
    }

    [Given(@"^nenhum pedido ao MOD-01 de (\S+) a (\S+)$")]
    public static void SemPedidosAoModulo(string de, string ate) { }

    [Given(@"^o cache FORA_DO_AR, com a ocorrência aberta$")]
    public async Task CacheForaComOcorrencia()
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora("13:30:00"));
        await a.Sonda("cache", "NENHUMA");
        await a.Verificar();
    }

    [Given(@"^as verificações do cache com as respostas (.+) na sonda, nessa ordem$")]
    public async Task VerificacoesDoCache(string respostas)
    {
        foreach (var resposta in respostas.Split(", "))
        {
            await a.Sonda("cache", resposta);
            await a.Verificar();
        }
    }

    [Given(@"^o MOD-14 sem pedidos nos últimos 5 min$")]
    [Given(@"^o Catálogo sem pedidos nesses 5 min$")]
    public static void SemPedidos() { }

    [Given(@"^2 instâncias no ar e o cache que passa a LENTO na verificação das (\S+)$")]
    public async Task DuasInstancias(string hora)
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora(hora));
        await a.Sonda("cache", "LENTA");
        await a.Verificar();
    }

    [Given(@"^o banco FORA_DO_AR de (\S+) a (\S+)$")]
    public async Task BancoForaDe(string de, string ate)
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora(de));
        await a.Sonda("banco", "NENHUMA");
        await a.Verificar();
        await a.Saltar(Hora(ate));
        await a.SondaFim("banco");
        await a.Verificar();
    }

    [Given(@"^o banco de dados sem resposta há 1 min e a sessão do Caio ativa$")]
    public async Task BancoSemResposta()
    {
        var caio = await a.Caio();
        mundo.Sujeito = caio;
        await mundo.SessaoParaPedido(caio);
        await a.Sonda("banco", "NENHUMA");
        a.Definidos.Add("banco");
        await a.Verificar();
    }

    [Given(@"^o cache fora do ar quando a Maria cria o Lucas$")]
    public async Task CacheForaQuandoMariaCria()
    {
        await a.MariaDoUsuarios();
        await a.Sonda("cache", "NENHUMA");
        await a.Verificar();
    }

    [Given(@"^o intermediador FORA_DO_AR desde (\S+) e o MOD-01 LENTO desde (\S+)$")]
    public async Task IntermediadorEModulo(string desde, string modulo)
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora(desde));
        await a.Sonda("intermediador-pagamento", "NENHUMA");
        await a.Verificar();
        await a.Saltar(Hora(modulo));
        // só as medições: a verificação das 13:31 (no Dado do cache) é a 1ª falha do MOD-01 e a das 13:41 a 2ª (RN-316: a 3ª seria FORA_DO_AR)
        await MedirPagamento(2);
    }

    // 4 % de erro 5xx em 100 pedidos de 400 ms: 1ª falha seguida, o Pagamento fica LENTO
    async Task MedirPagamento(int minutosAtras) =>
        await a.Medir([
            Medida("GET", "/pagamento/faturas", 200, 400, 96, a.Agora() - TimeSpan.FromMinutes(minutosAtras)),
            Medida("GET", "/pagamento/faturas", 500, 400, 4, a.Agora() - TimeSpan.FromMinutes(minutosAtras)),
        ]);

    [Given(@"^o cache de volta a NO_AR às (\S+), depois de sair dele às (\S+)$")]
    public async Task CacheVolta(string volta, string saida)
    {
        await a.Saltar(Hora(saida));
        await a.Sonda("cache", "NENHUMA");
        await a.Verificar();
        await a.Saltar(Hora(volta));
        await MedirPagamento(2);
        await a.Sonda("cache", "NORMAL", 10);
        await a.Verificar();
    }

    // ── medições ───────────────────────────────────────────────────────────
    [Given(@"^o Pagamento com 250 pedidos, 10 com erro 5xx, nos 5 min antes de ""verificadoEm""$")]
    public async Task PagamentoComErros()
    {
        mundo.Sujeito = await a.Caio();
        var medidas = new List<JsonNode>();
        for (var m = 1; m <= 5; m++)
        {
            medidas.Add(Medida("GET", "/pagamento/faturas", 200, 400, 48, a.Agora() - TimeSpan.FromMinutes(m)));
            medidas.Add(Medida("GET", "/pagamento/faturas", 500, 400, 2, a.Agora() - TimeSpan.FromMinutes(m)));
        }
        await a.Medir(medidas);
    }

    [Given(@"^essa é a 1ª verificação seguida com falha do Pagamento$")]
    public async Task PrimeiraFalha() => await a.Verificar();

    // 1.240 pedidos em 59 minutos (o 9º minuto sem pedido): 1.000 de 90 ms, 200 de 400 ms
    // (31 com 404) e 40 de 900 ms (2 com 500): p50 100, p95 500, p99 1000
    [Given(@"^1\.240 pedidos medidos entre (\S+) e (\S+)$")]
    public async Task MilEDuzentos(string de, string ate)
    {
        mundo.Sujeito = await a.Caio();
        var inicio = Hora(de);
        await a.Saltar(Hora(ate) + TimeSpan.FromSeconds(10));
        var minutos = Enumerable.Range(0, 60).Where(i => i != 8).ToList();
        var medidas = new List<JsonNode>();
        void Parte(int total, int status, int ms, int erros)
        {
            for (var k = 0; k < minutos.Count; k++)
            {
                var n = total / minutos.Count + (k < total % minutos.Count ? 1 : 0);
                if (n == 0) continue;
                var comErro = Math.Min(n, erros / minutos.Count + (k < erros % minutos.Count ? 1 : 0));
                var minuto = inicio + TimeSpan.FromMinutes(minutos[k]);
                if (n - comErro > 0) medidas.Add(Medida("GET", "/sac/chamados", 200, ms, n - comErro, minuto));
                if (comErro > 0) medidas.Add(Medida("GET", "/sac/chamados", status, ms, comErro, minuto));
            }
        }
        Parte(1000, 200, 90, 0);
        Parte(200, 404, 400, 31);
        Parte(40, 500, 900, 2);
        await a.Medir(medidas);
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
    }

    [Given(@"^POST /_teste/auditoria/medicoes com (\d+) pedidos a ""(GET|POST) (\S+)"" em (\S+)$")]
    public async Task MedicoesEm(int total, string metodo, string endereco, string hora)
    {
        mundo.Sujeito ??= await a.Caio();
        medicaoPendente = (total, metodo, endereco, Hora(hora));
        await Task.CompletedTask;
    }

    [Given(@"^desses, (\d+) com o status 500 e o resto com 200 em (\d+) ms$")]
    public async Task DessesComErro(int erros, int ms)
    {
        var (total, metodo, endereco, minuto) = medicaoPendente ?? throw new InvalidOperationException("sem medição à espera");
        var medidas = new List<JsonNode> { Medida(metodo, endereco, 200, ms, total - erros, minuto) };
        if (erros > 0) medidas.Add(Medida(metodo, endereco, 500, ms, erros, minuto));
        await a.Medir(medidas);
    }

    [Given(@"^POST /_teste/auditoria/medicoes com 100 pedidos a ""GET (/sac/chamados)"": (.+)$")]
    public async Task Distribuicao(string endereco, string distribuicao)
    {
        mundo.Sujeito = await a.Caio();
        var medidas = new List<JsonNode>();
        foreach (Match m in Regex.Matches(distribuicao, @"(\d+) de (\d+)"))
        {
            medidas.Add(Medida("GET", endereco, 200, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value), a.Agora() - TimeSpan.FromMinutes(2)));
        }
        await a.Medir(medidas);
    }

    // ── registros injetados ────────────────────────────────────────────────
    [Given(@"^POST /_teste/auditoria/registros gravou para ""(trc-\w+)"": (.+)$")]
    public async Task RegistrosDaExecucao(string traceId, string registros)
    {
        mundo.Sujeito = await a.Caio();
        var itens = Regex.Matches(registros, @"(INICIADO|CONCLUIDO|ALERTA|ERRO) (\d{2}:\d{2}:\d{2}\.\d{3})")
            .Select(m => (JsonNode)Registro(traceId, m.Groups[1].Value, "MUDAR_ESTADO_CONTA", Hora(m.Groups[2].Value),
                m.Groups[1].Value switch { "ALERTA" => "MATRICULA_EM_USO", "ERRO" => "BANCO_INDISPONIVEL", _ => null }));
        await a.Registrar(itens);
        // o GET seguinte ("envia GET …") é o passo genérico da Autorização, que não acerta a hora: o relógio já marca o "às 13:42:00"
        // do cenário (e não 10 s antes), para o limite de 5 min da execução interrompida (RN-318)
        if (a.HoraFixa is { } hora) await a.Acertar(hora);
    }

    [Given(@"^todos com ""acao"": ""MUDAR_ESTADO_CONTA""$")]
    public static void TodosComAcao() { }

    [Given(@"^POST /_teste/auditoria/registros gravou 1 registro de nível ERRO em (\S+)$")]
    public async Task UmErro(string hora)
    {
        // o card antes do registro, para "1 a mais do que antes"
        var caio = await a.Caio();
        a.Base = (await a.Pedir(caio, "GET", "/auditoria/card", Hora("13:42:00"))).Corpo;
        await a.Registrar([Registro(NovoTraceId(), "ERRO", "CRIAR_USUARIO", Hora(hora), "BANCO_INDISPONIVEL")]);
    }

    [Given(@"^um registro da ação ""(\w+)"", que não está em GET /auditoria/acoes$")]
    public async Task AcaoSemNome(string acao)
    {
        mundo.Sujeito = await a.Caio();
        var t = NovoTraceId();
        await a.Registrar([Registro(t, "INICIADO", acao, a.Agora() - TimeSpan.FromMinutes(1)), Registro(t, "CONCLUIDO", acao, a.Agora() - TimeSpan.FromMinutes(1))]);
    }

    // o período fechado de 12:00Z a 13:00Z: o preparo e as consultas gravadas depois das 13:42 ficam de fora
    [Given(@"^que ""\[12h a 13h\]"" abrevia ""de=2026-11-20T12:00:00Z&ate=2026-11-20T13:00:00Z""$")]
    [Given(@"^as consultas gravadas às 13:42 ou depois, fora do período, então não contam$")]
    public static void Periodo() { }

    [Given(@"^130 registros gravados por POST /_teste/auditoria/registros nessa hora, e nenhum outro$")]
    public async Task CentoETrinta()
    {
        await a.Saltar(Hora("13:41:50"));
        a.HoraFixa = Hora("13:42:00");
        mundo.Sujeito = await a.Caio();
        var inicio = Hora("12:00:00");
        await a.Registrar(Enumerable.Range(0, 130).Select(i =>
            (JsonNode)Registro(NovoTraceId(), "CONCLUIDO", "CRIAR_USUARIO", inicio + TimeSpan.FromSeconds(20 * i))));
    }

    // RF-202: os 4 passos de DESATIVAR_USUARIO da Maria sobre o Paulo, com o traceId do exemplo (o gancho aceita quem, conta e alvo)
    [Given(@"^os 4 passos de ""trc-1d6e3f08"" gravados por POST /_teste/auditoria/registros$")]
    [Given(@"^cada um com a ""acao"", a ""etapa"" e o ""quando"" do passo esperado abaixo$")]
    public static void PassosDoExemplo() { }

    // RN-325 (v09): o mesmo Dado do RF-202, "cada um como na execução concluída"
    [Given(@"^cada um como na execução concluída do RF-202-api$")]
    [Given(@"^todos com ""quem"": ""usr-ebfga-maria"", ""conta"": ""conta-ebfga"" e ""alvo"": ""usr-ebfga-paulo""$")]
    public async Task PassosGravados()
    {
        mundo.Sujeito = await a.Caio();
        await a.GravarPassosDoExemplo();
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
    }

    // RF-202: a execução que falhou, pelo gancho
    (string Acao, DateTimeOffset Inicio)? falhaPendente;

    [Given(@"^""trc-4b7e01c9"" gravado pelo gancho com a ação ""(\w+)"": INICIADO às (\S+)$")]
    public async Task FalhaComeca(string acao, string hora)
    {
        mundo.Sujeito = await a.Caio();
        falhaPendente = (acao, Hora(hora));
        await Task.CompletedTask;
    }

    [Given(@"^ERRO às (\S+), com o nível ERRO e o código ""(\w+)""$")]
    public async Task FalhaTermina(string hora, string codigo)
    {
        var (acao, inicio) = falhaPendente ?? throw new InvalidOperationException("sem o INICIADO da execução");
        await a.Registrar([Registro("trc-4b7e01c9", "INICIADO", acao, inicio), Registro("trc-4b7e01c9", "ERRO", acao, Hora(hora), codigo)]);
    }

    [Given(@"^o intermediador fora do ar e 4 erros, 31 alertas e 1 interrompida em 2026-11-20$")]
    public async Task CardDoTime()
    {
        mundo.Sujeito = await a.Caio();
        await a.Saltar(Hora("2026-11-20T15:00:00Z"));
        await a.Sonda("intermediador-pagamento", "NENHUMA");
        await a.Verificar();
        var itens = new List<JsonNode>();
        for (var i = 0; i < 4; i++) itens.Add(Registro(NovoTraceId(), "ERRO", "CRIAR_USUARIO", a.Agora() - TimeSpan.FromMinutes(10 + i), "BANCO_INDISPONIVEL"));
        for (var i = 0; i < 31; i++) itens.Add(Registro(NovoTraceId(), "ALERTA", "CRIAR_USUARIO", a.Agora() - TimeSpan.FromMinutes(10 + i), "MATRICULA_EM_USO"));
        itens.Add(Registro(NovoTraceId(), "INICIADO", "MUDAR_ESTADO_CONTA", a.Agora() - TimeSpan.FromMinutes(40)));
        await a.Registrar(itens);
        await a.Agendamento();
    }

    [Given(@"^o participante Lucas criado pela Maria em (\S+)$")]
    public async Task LucasPelaMaria(string hora)
    {
        mundo.Sujeito = await a.Caio();
        var maria = await a.MariaDoUsuarios();
        await mundo.SessaoParaPedido(maria);
        await a.Saltar(Hora(hora) - TimeSpan.FromSeconds(1));
        Exigir(await mundo.EnviarComo(maria, "POST", "/usuarios", Lucas()), 201, "POST /usuarios do Lucas pela Maria");
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
    }

    [Given(@"^a criação do Lucas recusada com 409 MATRICULA_EM_USO às (\S+)$")]
    public async Task LucasRecusado(string hora)
    {
        mundo.Sujeito = await a.Caio();
        var master = await mundo.Master();
        await mundo.Maria(); // a matrícula 20260087 já é da Maria
        await a.Saltar(Hora(hora));
        var corpo = JsonNode.Parse(Lucas())!.AsObject();
        corpo["matricula"] = "20260087";
        Exigir(await mundo.EnviarComo(master, "POST", "/usuarios", corpo.ToJsonString()), 409, "POST /usuarios do Lucas com matrícula repetida");
    }

    static string Lucas() => new JsonObject
    {
        ["tipo"] = "PARTICIPANTE", ["nome"] = "Lucas Andrade", ["nomeCurto"] = "lucas", ["matricula"] = null, ["responsaveis"] = new JsonArray(),
    }.ToJsonString();

    // ── ao vivo ────────────────────────────────────────────────────────────
    [Given(@"^o Caio com GET (/auditoria/\S+) aberto$")]
    public async Task CaioConectado(string caminho) => await a.Abrir(await a.Caio(), caminho);

    [Given(@"^o Caio com 3 conexões em (/auditoria/(?:registros|saude)/ao-vivo)$")]
    public async Task TresConexoes(string caminho)
    {
        var caio = await a.Caio();
        for (var i = 0; i < 3; i++) await a.Abrir(caio, caminho);
    }

    [Given(@"^o Caio com 3 conexões ao vivo da Auditoria, entre saúde, registros e ocorrências$")]
    public async Task TresMistas()
    {
        var caio = await a.Caio();
        await a.Abrir(caio, "/auditoria/saude/ao-vivo");
        await a.Abrir(caio, "/auditoria/registros/ao-vivo");
        await a.Abrir(caio, "/auditoria/ocorrencias/ao-vivo");
    }

    [Given(@"^o Caio com GET (/auditoria/metricas\?janela=1h) gravado às (\S+)$")]
    public async Task ConsultaGravada(string caminho, string hora) =>
        await a.Pedir(await a.Caio(), "GET", caminho, Hora(hora));

    // ── partida (RNF-251) ──────────────────────────────────────────────────
    static readonly Dictionary<string, string[]> IndicesDaAuditoria = new()
    {
        ["verificacoesSaude"] = ["servico+quando↓", "guardarAte"],
        ["quedasServico"] = ["servico+inicio↓", "guardarAte"],
        ["medicoesMinuto"] = ["minuto", "modulo+minuto", "guardarAte"],
        ["medicoesHora"] = ["hora", "modulo+hora", "guardarAte"],
        ["cardAuditoria"] = ["guardarAte"],
        ["ocorrencias"] = ["quando↓", "alvo+quando↓", "para+quando↓", "guardarAte"],
        ["auditoria"] = ["quando↓", "acao+quando↓", "contaId+quando↓", "quem+quando↓"],
        ["auditoriaErros"] = ["quando↓", "nivel+quando↓", "codigo+quando↓", "acao+quando↓"],
    };

    [Given(@"^a partida de homologação concluída, com as coleções e os índices da Auditoria$")]
    public async Task PartidaConcluida()
    {
        partida.KeyVaultCompleto("hml");
        await partida.IniciarPronta("homologação");
        var nomes = await BancoDaPartida.NomesDasColecoes();
        foreach (var (colecao, chaves) in IndicesDaAuditoria)
        {
            Assert.True(nomes.Contains(colecao), $"a partida não criou a coleção \"{colecao}\"");
            var tem = (await BancoDaPartida.Indices(colecao)).Select(i => i.Chave).ToList();
            foreach (var chave in chaves.Select(Indice.ChaveDoRequisito))
            {
                Assert.True(tem.Contains(chave), $"a partida não criou o índice {{{chave}}} em \"{colecao}\"; tem {string.Join(", ", tem)}");
            }
        }
    }

    // um índice da suíte na mesma chave, com outro nome: o banco recusa o da aplicação
    [Given(@"^o banco recusa o índice acao_quando em ""(auditoria)"" na partida$")]
    public async Task RecusaNaPartida(string colecao)
    {
        partida.KeyVaultCompleto("hml");
        await BancoDaPartida.Colecao(colecao).Indexes.CreateOneAsync(new MongoDB.Driver.CreateIndexModel<MongoDB.Bson.BsonDocument>(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("acao").Descending("quando"),
            new MongoDB.Driver.CreateIndexOptions { Name = BancoDaPartida.IndiceQueImpede }));
        indices.RecusaPreparada(colecao, "acao");
    }

}
