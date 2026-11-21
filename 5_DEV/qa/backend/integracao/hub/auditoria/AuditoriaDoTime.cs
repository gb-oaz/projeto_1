using System.Globalization;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Auditoria;

// Apoio dos Cenários de API de 1_Requisitos/20_modulo-auditoria.md (v03), contrato
// auditoria-contrato-api v02. Pessoas do time deboaz, relógio de teste, os ganchos
// /_teste/auditoria/* (só com AMBIENTE=hml e Teste__Relogio=sim, como /_teste/relogio),
// as conexões ao vivo e os dados que os cenários não trazem em Dado (fixtures).
//
//   PUT    /sondas/{servico}  {"resposta": NORMAL|LENTA|NENHUMA|RECUSADA|ERRO, "tempoMs", "ultimaExecucao"} → 204
//   DELETE /sondas/{servico}                                  → 204
//   POST   /verificacao                                       → 200
//   POST   /medicoes   [{metodo, endereco, status, duracaoMs, quantidade, minuto}] → 204
//   POST   /medicoes/gravar                                   → 204
//   POST   /registros  [{traceId, tipo, etapa|nivel, acao, codigo, quando}]        → 201
//   POST   /agendamento                                       → 204
public sealed class AuditoriaDoTime(Mundo mundo, Api api, Relogio relogio, Execucoes execucoes, AoVivo aoVivo)
{
    public const string Gancho = "/_teste/auditoria";
    public static readonly string[] Servicos =
        ["aplicacao", "banco", "cache", "armazenamento-anexos", "verificacao-anexos", "intermediador-pagamento", "agendamento-sac", "agendamento-pagamento"];
    public static readonly DateTimeOffset Dia = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    readonly HashSet<string> sondas = [];
    DateTimeOffset? relogioEm, relogioReal;
    bool caioPronto, registrosSemeados, execucoesSemeadas;

    // hora fixa do próximo pedido ("às 13:42:00"); null = agora
    public DateTimeOffset? HoraFixa { get; set; }
    public Pessoa? UltimoQuem { get; private set; }
    public string UltimoCaminho { get; private set; } = "";
    public TimeSpan UltimaDuracao { get; set; }
    public JsonNode? Base { get; set; }
    public JsonNode? Depois { get; set; }
    public string? PedidoMedido { get; set; }
    public string? TraceIdDoGancho { get; set; }
    public DateTimeOffset? QuedaDesde { get; set; }
    public readonly HashSet<string> Definidos = [];

    public static string Iso(DateTimeOffset d) => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
    public static string IsoMs(DateTimeOffset d) => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // "13:42", "13:42:05", "13:20:00.184" (dia 2026-11-20) ou ISO completo
    public static DateTimeOffset Hora(string texto) => texto.Contains('T')
        ? DateTimeOffset.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
        : Dia + TimeSpan.Parse(texto, CultureInfo.InvariantCulture);

    public DateTimeOffset Agora() =>
        relogioEm is { } em && relogioReal is { } real ? em + (DateTimeOffset.UtcNow - real) : DateTimeOffset.UtcNow;

    // ── relógio ────────────────────────────────────────────────────────────
    public async Task Acertar(DateTimeOffset agora)
    {
        await relogio.Definir(agora);
        (relogioEm, relogioReal) = (agora, DateTimeOffset.UtcNow);
    }

    // as sessões abertas ficam para trás: cada pessoa entra de novo no relógio novo
    public async Task Saltar(DateTimeOffset agora)
    {
        await Acertar(agora);
        mundo.Sessoes.Clear();
        mundo.Atual = null;
    }

    // o pedido sai na hora exata: a pessoa entra 5 s antes e o relógio é acertado logo antes de enviar
    public async Task NaHora(DateTimeOffset hora, Pessoa? quem)
    {
        await Saltar(hora - TimeSpan.FromSeconds(5));
        if (quem is not null) await mundo.SessaoParaPedido(quem);
        await Acertar(hora);
    }

    // ── pessoas ────────────────────────────────────────────────────────────
    public async Task<Pessoa> Caio()
    {
        var caio = await mundo.Caio();
        if (!caioPronto)
        {
            await mundo.Conceder(caio, new() { ["MOD-17"] = "U" });
            caioPronto = true;
        }
        return caio;
    }

    // membro do time sem a Auditoria
    public async Task<Pessoa> Rui()
    {
        var rui = await mundo.Membro("Rui Alves", "rui");
        var mods = (await mundo.Permissoes(rui, mundo.Admin()))["mods"]!.AsObject()
            .ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());
        if (mods.Remove("MOD-17")) await mundo.Definir(rui, mods, mundo.Admin());
        return rui;
    }

    public async Task<Pessoa> MariaDoUsuarios()
    {
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "CU" });
        return maria;
    }

    public async Task<Pessoa?> Quem(string quem) => quem switch
    {
        "o administrador do deboaz" => mundo.Admin(),
        "o Caio" => await Caio(),
        "o Rui" => await Rui(),
        "a conta master da EBFGA" => await mundo.Master(),
        "a staff Maria" => await MariaDoUsuarios(),
        "alguém sem o cabeçalho Authorization" => null,
        _ => throw new ArgumentException($"quem não conhecido: {quem}", nameof(quem)),
    };

    // ── pedidos ────────────────────────────────────────────────────────────
    public async Task<Resposta> Pedir(Pessoa? quem, string metodo, string caminho, DateTimeOffset? em = null)
    {
        caminho = await mundo.Traduzir(caminho);
        (UltimoQuem, UltimoCaminho) = (quem, caminho);
        var hora = em ?? HoraFixa;
        if (hora is { } h) await NaHora(h, quem);
        else if (quem is not null) await mundo.SessaoParaPedido(quem);
        return await execucoes.Executar(async () =>
        {
            if (caminho.Contains("/ao-vivo", StringComparison.Ordinal))
            {
                await Abrir(quem, caminho);
                return mundo.Ultima;
            }
            return quem is null ? await mundo.Enviar(metodo, caminho) : await mundo.EnviarComo(quem, metodo, caminho);
        });
    }

    public Task<Resposta> Get(Pessoa quem, string caminho) => Pedir(quem, "GET", caminho);

    // conexão ao vivo; a recusa (JSON) espera o fim do fluxo para ter o corpo
    public async Task<Fluxo> Abrir(Pessoa? quem, string caminho)
    {
        var token = quem is null ? null : (await mundo.SessaoParaPedido(quem)).Acesso;
        var fluxo = await aoVivo.Abrir(caminho, await mundo.Traduzir(caminho), token);
        if (fluxo.Status != 200) await fluxo.EsperarFim(TimeSpan.FromSeconds(5));
        mundo.Guardar(new Resposta(fluxo.Status, fluxo.Texto, [], fluxo.Cabecalhos));
        return fluxo;
    }

    public Fluxo UltimoFluxo => aoVivo.Ultimo;
    public IReadOnlyList<Fluxo> Fluxos => aoVivo.Fluxos;

    // ── ganchos ────────────────────────────────────────────────────────────
    public async Task Sonda(string servico, string corpo)
    {
        sondas.Add(servico);
        Exigir(await api.Enviar("PUT", $"{Gancho}/sondas/{servico}", corpo), 204, $"PUT {Gancho}/sondas/{servico}");
    }

    public Task Sonda(string servico, string resposta, int? tempoMs = null, DateTimeOffset? ultima = null)
    {
        var o = new JsonObject { ["resposta"] = resposta };
        if (tempoMs is { } t) o["tempoMs"] = t;
        if (ultima is { } u) o["ultimaExecucao"] = Iso(u);
        return Sonda(servico, o.ToJsonString());
    }

    public async Task SondaFim(string servico)
    {
        sondas.Add(servico);
        Exigir(await api.Enviar("DELETE", $"{Gancho}/sondas/{servico}"), 204, $"DELETE {Gancho}/sondas/{servico}");
    }

    public async Task Verificar(int vezes = 1)
    {
        for (var i = 0; i < vezes; i++) Exigir(await api.Enviar("POST", $"{Gancho}/verificacao"), 200, $"POST {Gancho}/verificacao");
    }

    // todos os serviços NO_AR pelas sondas
    public async Task TodosNoAr()
    {
        foreach (var s in Servicos)
        {
            if (s.StartsWith("agendamento", StringComparison.Ordinal)) await Sonda(s, "NORMAL", null, Agora() - TimeSpan.FromMinutes(1));
            else await Sonda(s, "NORMAL", 10);
            Definidos.Add(s);
        }
        await Verificar();
    }

    public static JsonObject Medida(string metodo, string endereco, int status, int duracaoMs, int quantidade, DateTimeOffset minuto) => new()
    {
        ["metodo"] = metodo,
        ["endereco"] = endereco,
        ["status"] = status,
        ["duracaoMs"] = duracaoMs,
        ["quantidade"] = quantidade,
        ["minuto"] = Iso(minuto),
    };

    public async Task Medir(IEnumerable<JsonNode> medidas) =>
        Exigir(await api.Enviar("POST", $"{Gancho}/medicoes", new JsonArray([.. medidas]).ToJsonString()), 204, $"POST {Gancho}/medicoes");

    public async Task GravarMedicoes() =>
        Exigir(await api.Enviar("POST", $"{Gancho}/medicoes/gravar"), 204, $"POST {Gancho}/medicoes/gravar");

    public async Task Agendamento() =>
        Exigir(await api.Enviar("POST", $"{Gancho}/agendamento"), 204, $"POST {Gancho}/agendamento");

    public static string NovoTraceId() => $"trc-{Guid.NewGuid():N}"[..12];

    // tipo AUDITORIA com etapa, ou ERRO com nivel (ALERTA|ERRO)
    // quem, conta e alvo são ids que existem (o gancho responde 422 DADOS_INVALIDOS se não existirem)
    public static JsonObject Registro(string traceId, string etapaOuNivel, string acao, DateTimeOffset quando, string? codigo = null,
        string? quem = null, string? conta = null, string? alvo = null)
    {
        var erro = etapaOuNivel is "ALERTA" or "ERRO";
        var o = new JsonObject { ["traceId"] = traceId, ["tipo"] = erro ? "ERRO" : "AUDITORIA", ["acao"] = acao, ["codigo"] = codigo, ["quando"] = IsoMs(quando) };
        o[erro ? "nivel" : "etapa"] = etapaOuNivel;
        if (quem is not null) o["quem"] = quem;
        if (conta is not null) o["conta"] = conta;
        if (alvo is not null) o["alvo"] = alvo;
        return o;
    }

    public async Task<Resposta> SondaBruta(string servico, string corpo)
    {
        sondas.Add(servico);
        return await api.Enviar("PUT", $"{Gancho}/sondas/{servico}", corpo);
    }

    public async Task Registrar(IEnumerable<JsonNode> registros) =>
        Exigir(await api.Enviar("POST", $"{Gancho}/registros", new JsonArray([.. registros]).ToJsonString()), 201, $"POST {Gancho}/registros");

    // n registros AUDITORIA/CONCLUIDO espalhados nas últimas 24 h
    public Task RegistrosRecentes(int n) => Registrar(Enumerable.Range(0, n).Select(i =>
        (JsonNode)Registro(NovoTraceId(), "CONCLUIDO", "CRIAR_USUARIO", Agora() - TimeSpan.FromMinutes(5 + i))));

    // ── fixtures ───────────────────────────────────────────────────────────
    // quem e conta vêm do gancho de registros (v05): ids reais da Maria, da conta EBFGA e da conta master
    public async Task<(string Maria, string Conta, string Master)> Ids() =>
        ((await MariaDoUsuarios()).Id, await mundo.Conta(), await mundo.Traduzir("usr-ebfga-master"));

    // registros para os filtros (RF-200): ações da Maria na EBFGA, um alerta, um erro e uma ação do SAC
    public async Task SemearRegistros()
    {
        if (registrosSemeados) return;
        registrosSemeados = true;
        var (maria, conta, master) = await Ids();
        var agora = Agora();
        var t = NovoTraceId();
        var u = NovoTraceId();
        var c = NovoTraceId();
        await Registrar([
            Registro(t, "INICIADO", "CRIAR_USUARIO", agora - TimeSpan.FromMinutes(6), null, maria, conta, "usr-alvo"),
            Registro(t, "CONCLUIDO", "CRIAR_USUARIO", agora - TimeSpan.FromMinutes(6) + TimeSpan.FromMilliseconds(90), null, maria, conta, "usr-alvo"),
            Registro(u, "INICIADO", "CRIAR_USUARIO", agora - TimeSpan.FromMinutes(5), null, master, conta),
            Registro(u, "ALERTA", "CRIAR_USUARIO", agora - TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(40), "MATRICULA_EM_USO", master, conta),
            Registro(c, "INICIADO", "ABRIR_CHAMADO", agora - TimeSpan.FromMinutes(4), null, master, conta),
            Registro(c, "CONCLUIDO", "ABRIR_CHAMADO", agora - TimeSpan.FromMinutes(4) + TimeSpan.FromMilliseconds(70), null, master, conta),
            Registro(NovoTraceId(), "ERRO", "CRIAR_USUARIO", agora - TimeSpan.FromMinutes(2), "BANCO_INDISPONIVEL"),
            Registro(NovoTraceId(), "CONCLUIDO", "PAGAMENTO_CRIADO", agora - TimeSpan.FromMinutes(3)),
        ]);
    }

    // execuções de todo resultado (RF-203): 398 concluídas, 9 recusadas, 4 falharam, 1 interrompida e 3 do SAC da EBFGA
    public async Task SemearExecucoes()
    {
        if (execucoesSemeadas) return;
        execucoesSemeadas = true;
        var (maria, conta, master) = await Ids();
        var agora = Agora();
        var itens = new List<JsonNode>();
        void Exec(string resultado, int minutosAtras, string acao = "MUDAR_ESTADO_CONTA", string? quem = null, string? contaId = null)
        {
            var t = NovoTraceId();
            var ini = agora - TimeSpan.FromMinutes(minutosAtras);
            itens.Add(Registro(t, "INICIADO", acao, ini, null, quem, contaId));
            if (resultado == "CONCLUIDA") itens.Add(Registro(t, "CONCLUIDO", acao, ini + TimeSpan.FromMilliseconds(150), null, quem, contaId));
            if (resultado == "RECUSADA") itens.Add(Registro(t, "ALERTA", acao, ini + TimeSpan.FromMilliseconds(50), "MATRICULA_EM_USO", quem, contaId));
            if (resultado == "FALHOU") itens.Add(Registro(t, "ERRO", acao, ini + TimeSpan.FromMilliseconds(2100), "BANCO_INDISPONIVEL", quem, contaId));
        }
        for (var i = 0; i < 398; i++) Exec("CONCLUIDA", 10 + i % 600);
        for (var i = 0; i < 9; i++) Exec("RECUSADA", 10 + i);
        for (var i = 0; i < 4; i++) Exec("FALHOU", 10 + i);
        Exec("INTERROMPIDA", 44);
        for (var i = 0; i < 3; i++) Exec("CONCLUIDA", 20 + i, "ABRIR_CHAMADO", master, conta);
        await Registrar(itens);
    }

    // ── conferência ────────────────────────────────────────────────────────
    public static JsonArray Lista(JsonNode? corpo, string campo) =>
        corpo?[campo] as JsonArray ?? throw new Xunit.Sdk.XunitException($"esperava a lista \"{campo}\": {corpo?.ToJsonString()}");

    public static string? Txt(JsonNode? n, string campo) => n?[campo]?.GetValueKind() == System.Text.Json.JsonValueKind.String ? n[campo]!.GetValue<string>() : null;

    public async Task Limpar()
    {
        foreach (var s in sondas)
        {
            try { await api.Enviar("DELETE", $"{Gancho}/sondas/{s}"); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                // o próximo cenário que usar a sonda a define de novo
            }
        }
        sondas.Clear();
    }
}

[Binding]
public sealed class AuditoriaNoFim(AuditoriaDoTime auditoria)
{
    [AfterScenario]
    public Task Fim() => auditoria.Limpar();
}
