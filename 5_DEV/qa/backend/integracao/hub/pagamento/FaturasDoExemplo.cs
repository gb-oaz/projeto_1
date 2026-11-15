using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// As faturas, os pagamentos e os estornos dos exemplos de
// 1_Requisitos/19_modulo-pagamento.md (v04), contrato pagamento-contrato-api
// v02 e modelo pagamento-documentos v02, criados só quando um passo os cita.
// Os números são gerados pelo backend (contadores): "FAT-000402",
// "PAG-000773" e "EST-000012" do exemplo viram o número real pelo
// Mundo.Apelidos, como "SAC-000042" no 18.
//
// De onde vem cada coisa:
//   fatura      a API não cria fatura. A suíte grava as cobranças fechadas do
//               Catálogo (coleção cobrancas, Estado.Cobranca), leva o relógio
//               de teste ao dia 1 e roda o agendamento: o fechamento emite a
//               fatura de verdade (RN-300). "VENCIDA" é o agendamento do dia
//               seguinte ao vencimento; "PAGA" e "CANCELADA" são pedidos da API
//   pagamento   POST /pagamento/faturas/{id}/pagamentos; o banco é o
//               intermediador falso (BancoFalso.cs) e a confirmação é o aviso
//               em POST /pagamento/notificacoes
//   estorno     POST /pagamento/recebimentos/pagamentos/{id}/estornos
//
// Donos: a FAT-000402 (2026-11), a FAT-000318 (2026-10) e a FAT-000247
// (2026-09) são da EBFGA, pagas pela staff Rita Gomes, com "MOD-01" com "U";
// a FAT-000371 (2026-10) é do Pedro Reis (autônomo, conta PREIS). O recebedor
// é o administrador dos cenários (Mundo.Admin).
//
// Gancho de teste do backend, só com AMBIENTE=hml e Teste__Relogio=sim (o
// backend do qa no ci.sh), como o PUT /_teste/relogio (suporte/Relogio.cs):
//
//   POST /_teste/pagamento/agendamento                               → 204
//       roda agora, com o relógio da aplicação, o que o AgendamentoDoPagamento
//       faz a cada 1 min (no dia 1, IFecharCompetencia da competência
//       anterior; IVirarVencidas; IExpirarPagamentos; IConsultarPendentes;
//       IMontarCardDoDia; os avisos de atraso) e só responde ao terminar
//
// O tempo (vencimento, juros, validade do PIX, do débito e do boleto,
// tolerância do aviso) anda pelo relógio de teste; a cada salto as pessoas
// entram de novo (o token vence pelo relógio da aplicação).
public sealed partial class FaturasDoExemplo
{
    public const string TokenDeCredito = "tok_9f2c71", TokenDeDebito = "tok_d41b07";
    public const string MotivoDoCancelamento = "Cobrança em dobro do Quiz", MotivoDoEstorno = "Quiz cancelado";

    readonly Mundo mundo;
    readonly Api api;
    readonly Relogio relogio;
    readonly BancoFalso banco;

    // número do exemplo → número real
    readonly Dictionary<string, string> exemplos = [];
    // quem paga cada fatura (a Rita pela EBFGA, o Pedro pela dele) e de qual fatura é cada pagamento
    readonly Dictionary<string, Pessoa> pagadores = [];
    readonly Dictionary<string, string> faturas = [];
    // o corpo do 201 e o X-Trace-Id da execução que criou cada pagamento
    readonly Dictionary<string, JsonObject> criados = [];
    readonly Dictionary<string, string?> execucoes = [];
    Pessoa? rita, bia;
    bool ritaComPagamento, outubro, outubroInteiro;
    DateTimeOffset? relogioEm, relogioReal;

    public FaturasDoExemplo(Mundo mundo, Api api, Relogio relogio, BancoFalso banco)
    {
        (this.mundo, this.api, this.relogio, this.banco) = (mundo, api, relogio, banco);
        mundo.Apelidos["conta-pedro-reis"] = mundo.ContaDoPedro;
        mundo.Apelidos["usr-ebfga-rita"] = async () => (await RitaSemPagamento()).Id;
        mundo.Apelidos["usr-ebfga-bia"] = async () => (await Bia()).Id;
        // o número que o pedido do cenário acabou de gerar: "PAG-000772" e
        // "EST-000012" do JSON do exemplo viram o id do 201, se um Dado ainda
        // não os criou
        foreach (var exemplo in new[] { "PAG-000771", "PAG-000772", "PAG-000773", "PAG-000774", "PAG-000785", "EST-000012" })
        {
            mundo.Apelidos[exemplo] = () => Task.FromResult(DoUltimo201(exemplo));
        }
    }

    // o último pagamento criado pelo preparo: "o banco confirma às 13:04" é dele
    public string? UltimoPagamento { get; private set; }

    // ── pessoas ────────────────────────────────────────────────────────────
    // a Rita Gomes, do setor financeiro: staff da EBFGA, ainda sem o Pagamento (RN-299)
    public async Task<Pessoa> RitaSemPagamento() => rita ??= await mundo.NovoStaff("rita", "Rita Gomes", await mundo.Master());

    // a conta master delega o Pagamento à Rita: "MOD-01" com "U" (RN-299)
    public async Task<Pessoa> Rita()
    {
        var pessoa = await RitaSemPagamento();
        if (!ritaComPagamento) await mundo.Conceder(pessoa, new() { ["MOD-01"] = "U" });
        ritaComPagamento = true;
        return pessoa;
    }

    // a Bia Santos, staff da EBFGA criada por quem a "criador" diz (a conta
    // master, se ninguém disse): abaixo de quem a criou (17-b RN-252)
    public async Task<Pessoa> Bia(Pessoa? criador = null) =>
        bia ??= await mundo.NovoStaff("bia", "Bia Santos", criador ?? await mundo.Master());

    // quem paga a fatura ou o pagamento (número real)
    public Pessoa Pagador(string id) => pagadores.TryGetValue(id, out var pessoa) ? pessoa
        : throw new InvalidOperationException($"preparo: {id} não foi criado neste cenário");

    // ── números ────────────────────────────────────────────────────────────
    public bool Conhecido(string exemplo) => exemplos.ContainsKey(exemplo);

    public string Real(string exemplo) => exemplos.TryGetValue(exemplo, out var id) ? id
        : throw new InvalidOperationException($"o {exemplo} do exemplo ainda não foi criado neste cenário");

    public void Anotar(string exemplo, string real)
    {
        exemplos[exemplo] = real;
        mundo.Apelidos[exemplo] = () => Task.FromResult(real);
    }

    string DoUltimo201(string exemplo)
    {
        var forma = exemplo.StartsWith("EST-", StringComparison.Ordinal) ? NumeroDeEstorno() : NumeroDePagamento();
        if (mundo.Ultima.Status == 201 && mundo.Ultima.Corpo?["id"] is JsonValue v
            && v.GetValueKind() == System.Text.Json.JsonValueKind.String && forma.IsMatch(v.GetValue<string>()))
        {
            Anotar(exemplo, v.GetValue<string>());
            return v.GetValue<string>();
        }
        return exemplo;
    }

    // ── relógio e agendamento ──────────────────────────────────────────────
    public bool RelogioMudou => relogioEm is not null;

    public DateTimeOffset Agora() =>
        relogioEm is { } em && relogioReal is { } real ? em + (DateTimeOffset.UtcNow - real) : DateTimeOffset.UtcNow;

    // "2026-11-14" é o meio-dia UTC desse dia (09:00 de Brasília); com hora, a hora dita
    public static DateTimeOffset Hora(string quando) =>
        DateTimeOffset.Parse(quando.Length == 10 ? $"{quando}T12:00:00Z" : quando, System.Globalization.CultureInfo.InvariantCulture);

    // o relógio da aplicação vai para a hora pedida; as sessões abertas antes
    // ficam para trás e cada pessoa entra de novo no próximo pedido
    public async Task Saltar(DateTimeOffset agora)
    {
        await relogio.Definir(agora);
        (relogioEm, relogioReal) = (agora, DateTimeOffset.UtcNow);
        mundo.Sessoes.Clear();
        mundo.Atual = null;
    }

    public Task Saltar(string quando) => Saltar(Hora(quando));

    // o começo da história do cenário, se o relógio ainda não saiu do real: as
    // pessoas e a configuração nascem antes da fatura
    async Task Comecar(string quando)
    {
        if (!RelogioMudou) await Saltar(quando);
    }

    public async Task<Resposta> Agendamento() =>
        Exigir(await api.Enviar("POST", "/_teste/pagamento/agendamento"), 204,
            "POST /_teste/pagamento/agendamento (agendamento do Pagamento sob demanda, Teste__Relogio=sim)");

    // a hora dita e o agendamento dela ("chega 2026-12-11T03:00:00Z sem pagamento")
    public async Task<Resposta> Chegar(string quando)
    {
        await Saltar(Hora(quando).AddSeconds(5));
        return await Agendamento();
    }

    // ── configuração (RF-193) ──────────────────────────────────────────────
    public async Task<JsonObject> Configuracao() =>
        Exigir(await Ler(mundo.Admin(), "/pagamento/recebimentos/configuracao"), 200, "GET /pagamento/recebimentos/configuracao").Corpo!.AsObject();

    // o corpo do PUT: o do GET, sem "versao", com a mudança
    public static JsonObject CorpoDaConfiguracao(JsonObject lida, Action<JsonObject>? mudar = null)
    {
        var corpo = (JsonObject)lida.DeepClone();
        corpo.Remove("versao");
        mudar?.Invoke(corpo);
        return corpo;
    }

    public async Task Configurar(Action<JsonObject> mudar)
    {
        var lida = await Configuracao();
        var ultima = mundo.Ultima;
        Exigir(await mundo.EnviarComo(mundo.Admin(), "PUT", "/pagamento/recebimentos/configuracao",
            CorpoDaConfiguracao(lida, mudar).ToJsonString(), lida["versao"]!.ToJsonString()), 200, "PUT /pagamento/recebimentos/configuracao");
        mundo.Guardar(ultima);
    }

    public Task Juros(decimal aoDia) => Configurar(c => c["jurosAoDia"] = aoDia);
    public Task Tolerancia(int dias) => Configurar(c => c["toleranciaAvisoDias"] = dias);
    public Task Forma(string forma, bool aceita) => Configurar(c => c["formas"]![forma] = aceita);

    // ── faturas ────────────────────────────────────────────────────────────
    // as cobranças fechadas da competência e o fechamento do dia 1: a fatura nasce
    public async Task<string> Emitir(Pessoa dono, string contaId, string sigla, string competencia, params (string Modulo, decimal Valor)[] itens)
    {
        foreach (var (modulo, valor) in itens) await Estado.Cobranca(contaId, sigla, modulo, competencia, valor);
        await Fechar(competencia);
        return await FaturaDa(dono, competencia);
    }

    // 00:00 de Brasília do dia 1 do mês seguinte (03:00 UTC): o fechamento da competência
    public async Task Fechar(string competencia)
    {
        await Saltar(Hora($"{competencia}-01T03:00:05Z").AddMonths(1));
        await Agendamento();
    }

    // o dia seguinte ao vencimento (dia 10 do mês seguinte), às 00:00 de Brasília: a virada para "VENCIDA"
    public async Task Vencer(string competencia)
    {
        await Saltar(Hora($"{competencia}-11T03:00:05Z").AddMonths(1));
        await Agendamento();
    }

    public async Task<string> FaturaDa(Pessoa dono, string competencia)
    {
        var r = Exigir(await Ler(dono, "/pagamento/faturas?situacao=TODAS"), 200, $"GET /pagamento/faturas?situacao=TODAS de {dono.Nome}");
        var item = (r.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>().FirstOrDefault(i => Campo(i, "competencia") == competencia)
            ?? throw new InvalidOperationException($"preparo: o fechamento de {competencia} não emitiu a fatura de {dono.Nome}: {r}");
        return Campo(item, "id") ?? throw new InvalidOperationException($"preparo: fatura sem id: {item.ToJsonString()}");
    }

    // a FAT-000402: novembro de 2026 da EBFGA, Usuários 2.224,41 e Quiz 1.473,92,
    // "A_PAGAR", com o relógio em 2026-12-03, pouco antes das 13:00 UTC
    public async Task<string> Fat402()
    {
        if (exemplos.TryGetValue("FAT-000402", out var id)) return id;
        await Comecar("2026-11-30T12:00:00Z");
        var rita = await Rita();
        id = await Emitir(await mundo.Master(), await mundo.Conta(), "EBFGA", "2026-11", ("MOD-03", 2224.41m), ("MOD-05", 1473.92m));
        Anotar("FAT-000402", id);
        pagadores[id] = rita;
        await Saltar("2026-12-03T12:59:40Z");
        return id;
    }

    // uma fatura paga da EBFGA numa competência anterior: emitida, paga no dia 5
    // com o cartão de crédito (aprovado na hora pelo banco falso). Se o relógio
    // já passou dali, volta para onde estava
    public async Task<string> PagaDaEbfga(string exemplo, string competencia, decimal valor, string? pagamento = null)
    {
        if (exemplos.TryGetValue(exemplo, out var id)) return id;
        var volta = RelogioMudou && Agora() > Hora($"{competencia}-05T13:00:00Z").AddMonths(1) ? Agora() : (DateTimeOffset?)null;
        await Comecar($"{competencia}-28T12:00:00Z");
        var rita = await Rita();
        id = await Emitir(await mundo.Master(), await mundo.Conta(), "EBFGA", competencia, ("MOD-03", valor));
        Anotar(exemplo, id);
        pagadores[id] = rita;
        await Saltar(Hora($"{competencia}-05T13:00:00Z").AddMonths(1));
        await Pagar(rita, id, "CARTAO_CREDITO", pagamento, "PAGO");
        if (volta is { } hora) await Saltar(hora);
        return id;
    }

    // a FAT-000318 (outubro, paga pelo PAG-000655) e a FAT-000247 (setembro):
    // juntas, o "Pago no ano" de 39.810,44 do card da EBFGA (RN-307)
    public Task<string> Fat318() => PagaDaEbfga("FAT-000318", "2026-10", 19905.22m, "PAG-000655");
    public Task<string> Fat247() => PagaDaEbfga("FAT-000247", "2026-09", 19905.22m);

    // a FAT-000371: outubro de 2026 do Pedro Reis, Usuários 147,00 e Quiz 147,00,
    // vencimento em 2026-11-10. A taxa e a tolerância são salvas antes do
    // vencimento (RN-310: vale a taxa do dia do vencimento). Vencida, o relógio
    // fica em 2026-11-11, logo depois das 00:00 de Brasília
    public async Task<string> Fat371(decimal juros = 0m, int tolerancia = 0, bool vencida = true)
    {
        if (exemplos.TryGetValue("FAT-000371", out var id)) return id;
        var volta = RelogioMudou && Agora() > Hora("2026-11-11T03:00:05Z") ? Agora() : (DateTimeOffset?)null;
        await Comecar("2026-10-31T12:00:00Z");
        if (juros != 0m) await Juros(juros);
        if (tolerancia != 0) await Tolerancia(tolerancia);
        var pedro = await mundo.Pedro();
        id = await Emitir(pedro, await mundo.ContaDoPedro(), "PREIS", "2026-10", ("MOD-03", 147.00m), ("MOD-05", 147.00m));
        Anotar("FAT-000371", id);
        pagadores[id] = pedro;
        if (vencida) await Vencer("2026-10");
        if (volta is { } hora) await Saltar(hora);
        return id;
    }

    // RF-182: a EBFGA com uma fatura de cada status, e o relógio em 2026-12-03:
    // agosto "CANCELADA", setembro "PAGA", outubro "VENCIDA" e novembro "A_PAGAR"
    public async Task TodosOsStatus()
    {
        await Comecar("2026-08-31T12:00:00Z");
        var (master, conta, rita) = (await mundo.Master(), await mundo.Conta(), await Rita());
        var agosto = await Emitir(master, conta, "EBFGA", "2026-08", ("MOD-03", 2100.00m));
        pagadores[agosto] = rita;
        await Cancelar(agosto);
        await Fat247();
        var outubro = await Emitir(master, conta, "EBFGA", "2026-10", ("MOD-03", 2150.00m));
        pagadores[outubro] = rita;
        await Vencer("2026-10");
        await Fat402();
    }

    // o administrador cancela a fatura com o motivo do exemplo (RF-191)
    public async Task Cancelar(string fatura, string motivo = MotivoDoCancelamento) =>
        await Preparo(mundo.Admin(), "POST", $"/pagamento/recebimentos/faturas/{fatura}/cancelar", new JsonObject { ["motivo"] = motivo }, 200);

    // ── pagamentos ─────────────────────────────────────────────────────────
    public static JsonObject Corpo(string forma) => forma switch
    {
        "CARTAO_CREDITO" => new() { ["forma"] = forma, ["cartao"] = new JsonObject { ["token"] = TokenDeCredito } },
        "CARTAO_DEBITO" => new() { ["forma"] = forma, ["cartao"] = new JsonObject { ["token"] = TokenDeDebito } },
        _ => new() { ["forma"] = forma },
    };

    // o pagamento da fatura (número real) na forma pedida; o status que o preparo espera
    public async Task<string> Pagar(Pessoa quem, string fatura, string forma, string? exemplo = null, string? status = null)
    {
        var r = await Preparo(quem, "POST", $"/pagamento/faturas/{fatura}/pagamentos", Corpo(forma), 201);
        var id = Criado(r, fatura, quem, exemplo);
        if (status is not null && Campo(r.Corpo, "status") != status)
        {
            throw new InvalidOperationException($"preparo: o pagamento {forma} da {fatura} devia nascer {status}: {r}");
        }
        return id;
    }

    // o 201 de um pagamento, do preparo ou do pedido do cenário: fica anotado
    public string Criado(Resposta r, string fatura, Pessoa quem, string? exemplo = null)
    {
        var id = Texto(r, "id");
        pagadores[id] = quem;
        faturas[id] = fatura;
        criados[id] = r.Corpo!.AsObject();
        execucoes[id] = r.Cabecalho("X-Trace-Id");
        UltimoPagamento = id;
        if (exemplo is not null) Anotar(exemplo, id);
        return id;
    }

    public JsonObject CorpoDoCriado(string id) => criados[id];
    // o X-Trace-Id da execução que criou o pagamento (RN-304, RNF-244)
    public string? ExecucaoDe(string id) => execucoes.GetValueOrDefault(id);
    public string FaturaDo(string pagamento) => faturas[pagamento];

    // os pagamentos dos exemplos, todos da FAT-000402, pela Rita
    public async Task<string> NaFat402(string exemplo, string forma, string? status = "AGUARDANDO") =>
        exemplos.TryGetValue(exemplo, out var id) ? id : await Pagar(await Rita(), await Fat402(), forma, exemplo, status);

    public Task<string> Pix(string exemplo) => NaFat402(exemplo, "PIX");
    public Task<string> Boleto(string exemplo) => NaFat402(exemplo, "BOLETO");
    public Task<string> Debito(string exemplo) => NaFat402(exemplo, "CARTAO_DEBITO");
    public Task<string> CreditoPago(string exemplo) => NaFat402(exemplo, "CARTAO_CREDITO", "PAGO");

    // o banco avisa que o pagamento foi pago (ou recusado), na hora dita
    public async Task<Resposta> Confirmar(string pagamento, DateTimeOffset? em = null, string tipo = BancoFalso.Confirmado) =>
        Exigir(await banco.Avisar(pagamento, tipo, em ?? Agora()), 200, $"POST /pagamento/notificacoes ({tipo} do {pagamento})");

    public async Task CancelarPagamento(string pagamento) =>
        await Preparo(Pagador(pagamento), "POST", $"/pagamento/pagamentos/{pagamento}/cancelar", null, 200);

    // ── estornos ───────────────────────────────────────────────────────────
    public static JsonObject CorpoDoEstorno(decimal valor = 1473.92m) => new() { ["valor"] = valor, ["motivo"] = MotivoDoEstorno };

    public async Task<string> Estornar(string pagamento, decimal valor = 1473.92m, string? exemplo = null)
    {
        var r = await Preparo(mundo.Admin(), "POST", $"/pagamento/recebimentos/pagamentos/{pagamento}/estornos", CorpoDoEstorno(valor), 201);
        var id = Texto(r, "id");
        if (exemplo is not null) Anotar(exemplo, id);
        return id;
    }

    // ── o que um endereço cita ─────────────────────────────────────────────
    // cada número de exemplo do caminho, criado no estado de que os cenários
    // precisam quando nenhum Dado o criou antes
    public async Task Garantir(string caminho)
    {
        foreach (Match numero in Numero().Matches(caminho))
        {
            if (Conhecido(numero.Value)) continue;
            switch (numero.Value)
            {
                case "FAT-000402": await Fat402(); break;
                case "FAT-000371": await Fat371(); break;
                case "FAT-000318" or "PAG-000655": await Fat318(); break;
                case "FAT-000247": await Fat247(); break;
                case "PAG-000772" or "PAG-000781": await Pix(numero.Value); break;
                case "PAG-000785": await Boleto(numero.Value); break;
                case "PAG-000774": await Debito(numero.Value); break;
                case "PAG-000773": await CreditoPago(numero.Value); break;
                // o boleto já compensado (RF-192: não se estorna por aqui)
                case "PAG-000790": await Confirmar(await Boleto(numero.Value)); break;
                default: break; // FAT-999999 e os que não existem vão como vieram
            }
        }
    }

    // ── leitura ────────────────────────────────────────────────────────────
    // um GET que não é o pedido do cenário: a última resposta fica como estava
    public async Task<Resposta> Ler(Pessoa quem, string caminho)
    {
        var ultima = mundo.Ultima;
        var r = await mundo.EnviarComo(quem, "GET", caminho);
        mundo.Guardar(ultima);
        return r;
    }

    public async Task<JsonObject> Fatura(string id, Pessoa? quem = null) =>
        Exigir(await Ler(quem ?? Pagador(id), $"/pagamento/faturas/{id}"), 200, $"GET /pagamento/faturas/{id}").Corpo!.AsObject();

    public async Task<JsonObject> Pagamento(string id, Pessoa? quem = null) =>
        Exigir(await Ler(quem ?? Pagador(id), $"/pagamento/pagamentos/{id}"), 200, $"GET /pagamento/pagamentos/{id}").Corpo!.AsObject();

    // um pedido do preparo: a última resposta do cenário fica como estava
    public async Task<Resposta> Preparo(Pessoa quem, string metodo, string caminho, JsonObject? corpo, int status)
    {
        var ultima = mundo.Ultima;
        var r = Exigir(await mundo.EnviarComo(quem, metodo, caminho, corpo?.ToJsonString()), status, $"{metodo} {caminho} de {quem.Nome}");
        mundo.Guardar(ultima);
        return r;
    }

    public static string? Campo(JsonNode? no, string campo) =>
        no?[campo] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String ? v.GetValue<string>() : null;

    public static decimal? Valor(JsonNode? no, string campo) =>
        no?[campo] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number ? v.GetValue<decimal>() : null;

    // ── outubro de 2026 de todas as contas (RF-190, RF-191, RN-307) ────────
    // As 9 faturas vencidas do exemplo (6 de instituições, a do Pedro e mais 2
    // de autônomos; 14.329,80) e, no mês inteiro, as 119 pagas (198.120,50):
    // 128 contas, uma fatura cada, com as formas e os valores que fecham o
    // resumo do RF-190. Cada conta é criada e paga pela API; por isso o mês
    // inteiro só é montado para o cenário do resumo. O relógio termina em
    // 2026-11-14, ao meio-dia UTC.
    public async Task Outubro(bool inteiro)
    {
        if (outubro && (outubroInteiro || !inteiro)) return;
        if (outubro) throw new InvalidOperationException("preparo: outubro já foi montado só com as vencidas neste cenário");
        (outubro, outubroInteiro) = (true, inteiro);
        await Comecar("2026-10-31T12:00:00Z");

        var pedro = await mundo.Pedro();
        await Estado.Cobranca(await mundo.ContaDoPedro(), "PREIS", "MOD-03", "2026-10", 147.00m);
        await Estado.Cobranca(await mundo.ContaDoPedro(), "PREIS", "MOD-05", "2026-10", 147.00m);
        var numero = 0;
        for (var i = 0; i < 6; i++) await NovaContaComCobranca("INSTITUICAO", numero++, 2192.30m);
        for (var i = 0; i < 2; i++) await NovaContaComCobranca("AUTONOMO", numero++, 441.00m);
        var pagas = new List<(Pessoa Master, string Forma)>();
        if (inteiro)
        {
            for (var i = 0; i < 29; i++) pagas.Add((await NovaContaComCobranca("AUTONOMO", numero++, i == 0 ? 324.00m : 317.00m), "PIX"));
            for (var i = 0; i < 45; i++) pagas.Add((await NovaContaComCobranca("INSTITUICAO", numero++, i == 0 ? 2508.00m : 2468.00m), "PIX"));
            for (var i = 0; i < 25; i++) pagas.Add((await NovaContaComCobranca("INSTITUICAO", numero++, 1650.02m), "CARTAO_CREDITO"));
            for (var i = 0; i < 5; i++) pagas.Add((await NovaContaComCobranca("INSTITUICAO", numero++, 1314.00m), "CARTAO_DEBITO"));
            for (var i = 0; i < 15; i++) pagas.Add((await NovaContaComCobranca("INSTITUICAO", numero++, 2000.00m), "BOLETO"));
        }

        await Fechar("2026-10");
        var fatura = await FaturaDa(pedro, "2026-10");
        Anotar("FAT-000371", fatura);
        pagadores[fatura] = pedro;

        if (pagas.Count > 0) await Saltar("2026-11-05T13:00:00Z");
        foreach (var (master, forma) in pagas)
        {
            var id = await Pagar(master, await FaturaDa(master, "2026-10"), forma);
            if (forma != "CARTAO_CREDITO") await Confirmar(id);
            // 119 contas: a sessão de cada uma não fica guardada no cenário
            mundo.Sessoes.RemoveAll(s => s.Pessoa == master);
        }

        await Vencer("2026-10");
        await Saltar("2026-11-14T12:00:00Z");
        // o card do dia é montado pelo agendamento (RN-307)
        await Agendamento();
    }

    // uma conta nova, só para a fatura de outubro: "PG" e três letras
    async Task<Pessoa> NovaContaComCobranca(string tipo, int numero, decimal valor)
    {
        var sigla = $"PG{Letras(numero)}".ToUpperInvariant();
        var (nome, documento) = tipo == "INSTITUICAO"
            ? ($"Escola do Pagamento {Letras(numero)}", Cnpj(30000000 + numero))
            : ($"Educador do Pagamento {Letras(numero)}", Cpf(700000000 + numero));
        var conta = await mundo.NovaConta(sigla, ContaNova(tipo, nome, documento, $"pagamento_{Letras(numero)}", sigla));
        await Estado.Cobranca(conta, sigla, "MOD-03", "2026-10", valor);
        return await mundo.MasterDe(sigla);
    }

    // CPF com os dígitos verificadores certos, um por número
    public static string Cpf(int numero)
    {
        var d = $"{numero:D9}".Select(c => c - '0').ToList();
        foreach (var peso in new[] { 10, 11 })
        {
            var resto = d.Select((digito, i) => digito * (peso - i)).Sum() * 10 % 11;
            d.Add(resto == 10 ? 0 : resto);
        }
        var t = string.Concat(d);
        return $"{t[..3]}.{t[3..6]}.{t[6..9]}-{t[9..]}";
    }

    [GeneratedRegex(@"(?:FAT|PAG|EST)-\d{6}")]
    private static partial Regex Numero();

    [GeneratedRegex(@"^PAG-\d{6}$")]
    private static partial Regex NumeroDePagamento();

    [GeneratedRegex(@"^EST-\d{6}$")]
    private static partial Regex NumeroDeEstorno();
}
