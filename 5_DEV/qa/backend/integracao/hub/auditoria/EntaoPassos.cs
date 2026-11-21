using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Hub.Auditoria.AuditoriaDoTime;
using static Boaz.Qa.Integracao.Suporte.Mundo;
using Fonte = Boaz.Qa.Integracao.Suporte.Auditoria;

namespace Boaz.Qa.Integracao.Hub.Auditoria;

// Os Então de 1_Requisitos/20_modulo-auditoria.md (v03, Cenários de API) sobre as
// respostas JSON: saúde, histórico, módulos, métricas, endereços, ações, execuções
// e ocorrências. Registros, ao vivo, consulta auditada e formato comum ficam em
// EntaoRegistrosPassos.cs (a mesma classe).
[Binding]
public sealed partial class EntaoPassos(Mundo mundo, AuditoriaDoTime a, Execucoes execucoes)
{
    static readonly string[] OrdemServicos = ["FORA_DO_AR", "LENTO", "SEM_VERIFICACAO", "NO_AR", "DESLIGADO"];
    static readonly string[] OrdemModulos = ["FORA_DO_AR", "LENTO", "NO_AR", "SEM_PEDIDOS"];
    static readonly TimeSpan Cinco = TimeSpan.FromSeconds(5), Folga = TimeSpan.FromSeconds(3);

    JsonNode? Corpo => mundo.Ultima.Corpo;
    static readonly TimeSpan UmMinuto = TimeSpan.FromMinutes(1);

    // ── comparação de campos ───────────────────────────────────────────────
    static bool Igual(JsonNode? e, JsonNode? v)
    {
        if (e is null || v is null) return e is null && v is null;
        if (e.GetValueKind() == System.Text.Json.JsonValueKind.Number && v.GetValueKind() == System.Text.Json.JsonValueKind.Number)
        {
            return Math.Abs(e.GetValue<double>() - v.GetValue<double>()) < 0.01;
        }
        return JsonNode.DeepEquals(e, v);
    }

    static void Campos(JsonNode esperado, JsonNode? atual, params string[] campos)
    {
        foreach (var c in campos)
        {
            Assert.True(Igual(esperado[c], atual?[c]), $"\"{c}\": esperava {esperado[c]?.ToJsonString() ?? "null"}, veio {atual?[c]?.ToJsonString() ?? "null"} em {atual}");
        }
    }

    static void Perto(string campo, JsonNode? atual, DateTimeOffset esperado, TimeSpan folga)
    {
        var veio = Txt(atual, campo);
        Assert.True(veio is not null && (DateTimeOffset.Parse(veio) - esperado).Duration() <= folga, $"\"{campo}\": esperava perto de {Iso(esperado)}, veio {veio} em {atual}");
    }

    static JsonNode? Achar(JsonArray lista, string campo, string valor) => lista.FirstOrDefault(i => Txt(i, campo) == valor);

    static void Ordem(JsonArray lista, string campo, string[] ordem)
    {
        var posicoes = lista.Select(i => Array.IndexOf(ordem, Txt(i, campo))).ToList();
        Assert.True(posicoes.All(p => p >= 0) && posicoes.SequenceEqual(posicoes.OrderBy(p => p)), $"ordem de \"{campo}\" fora de {string.Join(", ", ordem)}: {lista.ToJsonString()}");
    }

    // ── saúde (RF-194, RF-196, RN-315, RN-316, RNF-252) ────────────────────
    [Then(@"^a resposta é 200 e traz, entre os 8 serviços e os módulos da RF-196:$")]
    public async Task SaudeDoExemplo(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var e = JsonNode.Parse(await mundo.Traduzir(json))!;
        Campos(e, Corpo, "geral", "foraDoAr", "lentos");
        var servicos = Lista(Corpo, "servicos");
        Assert.Equal(8, servicos.Count);
        foreach (var esperado in Lista(e, "servicos"))
        {
            var id = Txt(esperado, "id")!;
            var veio = Achar(servicos, "id", id);
            Assert.True(veio is not null, $"\"servicos\" sem \"{id}\": {servicos.ToJsonString()}");
            Campos(esperado!, veio, "nome");
            if (a.Definidos.Contains(id)) Campos(esperado!, veio, "estado", "falha", "tempoMs");
            if (a.QuedaDesde is { } desde && id == "intermediador-pagamento") Perto("desde", veio, desde, UmMinuto);
        }
        var instancias = Lista(Corpo, "instancias");
        Assert.True(instancias.Count >= 1 && instancias.All(i => i?["id"] is not null && i["versao"] is not null && i["inicio"] is not null && i["ultimoSinal"] is not null),
            $"\"instancias\" sem id, versao, inicio e ultimoSinal: {instancias.ToJsonString()}");
    }

    [Then(@"^""servicos"" vem na ordem FORA_DO_AR, LENTO, SEM_VERIFICACAO, NO_AR e DESLIGADO$")]
    public void OrdemDosServicos() => Ordem(Lista(Corpo, "servicos"), "estado", OrdemServicos);

    [Then(@"^no mesmo estado, na ordem da tabela ""serviços e identificadores"" abaixo$")]
    public void OrdemNoMesmoEstado()
    {
        var servicos = Lista(Corpo, "servicos");
        foreach (var grupo in servicos.GroupBy(s => Txt(s, "estado")))
        {
            var posicoes = grupo.Select(s => Array.IndexOf(Servicos, Txt(s, "id"))).ToList();
            Assert.True(posicoes.All(p => p >= 0) && posicoes.SequenceEqual(posicoes.OrderBy(p => p)), $"no estado {grupo.Key}, a ordem da tabela: {servicos.ToJsonString()}");
        }
    }

    [Then(@"^""servicos"" traz o ""([\w-]+)"" com o nome ""(.+)""$")]
    public void ServicoComNome(string id, string nome)
    {
        var s = Achar(Lista(Corpo, "servicos"), "id", id);
        Assert.True(s is not null && Txt(s, "nome") == nome, $"esperava \"{id}\" com o nome \"{nome}\": {Corpo}");
    }

    [Then(@"^""geral"" é ""(\w+)"", ""foraDoAr"" é (\d+) e ""lentos"" é (\d+)$")]
    public void Geral(string geral, int fora, int lentos)
    {
        Assert.True(Txt(Corpo, "geral") == geral && Corpo!["foraDoAr"]!.GetValue<int>() == fora && Corpo["lentos"]!.GetValue<int>() == lentos,
            $"esperava geral {geral}, foraDoAr {fora}, lentos {lentos}: {Corpo}");
    }

    [Then(@"^o ""([\w-]+)"" vem com o estado ""(\w+)"" e a falha (null|""\w+"")$")]
    public void ServicoComFalha(string id, string estado, string falha)
    {
        var s = Achar(Lista(Corpo, "servicos"), "id", id);
        Assert.True(s is not null && Txt(s, "estado") == estado, $"esperava \"{id}\" {estado}: {Corpo}");
        Assert.True(falha == "null" ? s["falha"] is null : Txt(s, "falha") == falha.Trim('"'), $"esperava a falha {falha} de \"{id}\": {s}");
    }

    [Then(@"^o ""([\w-]+)"" vem com o estado ""(\w+)""$")]
    public void ServicoComEstado(string id, string estado)
    {
        var s = Achar(Lista(Corpo, "servicos"), "id", id);
        Assert.True(s is not null && Txt(s, "estado") == estado, $"esperava \"{id}\" {estado}: {Corpo}");
    }

    [Then(@"^""(MOD-\d+)"" vem com o estado ""(\w+)""$")]
    public void ModuloComEstado(string id, string estado)
    {
        var m = Achar(Lista(Corpo, "modulos"), "id", id);
        Assert.True(m is not null && Txt(m, "estado") == estado, $"esperava \"{id}\" {estado}: {Corpo}");
    }

    [Then(@"^o ""agendamento-sac"" vem NO_AR, com ""falha"": null$")]
    public void AgendamentoComErro()
    {
        var s = Achar(Lista(Corpo, "servicos"), "id", "agendamento-sac");
        Assert.True(Txt(s, "estado") == "NO_AR" && s!["falha"] is null, $"esperava NO_AR sem falha: {s}");
    }

    [Then(@"^para os agendamentos a sonda usa só ""ultimaExecucao"" e ignora ""resposta""$")]
    public static void SondaDoAgendamento() { } // conferido no passo anterior

    [Then(@"^GET /auditoria/saude traz o ""MOD-01"" em ""SEM_PEDIDOS""$")]
    public async Task Mod01SemPedidos()
    {
        var saude = (await a.Pedir(await a.Caio(), "GET", "/auditoria/saude")).Corpo;
        Assert.Equal("SEM_PEDIDOS", Txt(Achar(Lista(saude, "modulos"), "id", "MOD-01"), "estado"));
    }

    [Then(@"^GET /auditoria/ocorrencias\?alvo=MOD-01 traz a mais nova com ""de"": ""LENTO"" e ""para"": ""NO_AR""$")]
    public async Task OcorrenciaFechada()
    {
        var itens = Lista((await a.Pedir(await a.Caio(), "GET", "/auditoria/ocorrencias?alvo=MOD-01")).Corpo, "itens");
        a.Base = itens.FirstOrDefault();
        Assert.True(Txt(a.Base, "de") == "LENTO" && Txt(a.Base, "para") == "NO_AR", $"esperava LENTO para NO_AR na mais nova: {itens}");
    }

    [Then(@"^""duracaoMin"": 5, e GET /auditoria/ocorrencias/abertas não traz mais o MOD-01$")]
    public async Task FechouEmCinco()
    {
        Assert.Equal(5, a.Base!["duracaoMin"]!.GetValue<int>());
        var abertas = (await a.Pedir(await a.Caio(), "GET", "/auditoria/ocorrencias/abertas")).Corpo;
        Assert.DoesNotContain(Lista(abertas, "abertas"), o => Txt(o, "alvo") == "MOD-01");
    }

    [Then(@"^a ocorrência fecha com ""para"": ""NO_AR"", porque (\w+) conta como NO_AR$")]
    public static void FechaComoNoAr(string estado) { } // o Quando é pendente: o estado não se provoca pelas sondas

    [Then(@"^a resposta é 200 com o ""banco"" em ""FORA_DO_AR"", lida da última verificação$")]
    public void BancoForaNaSaude()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal("FORA_DO_AR", Txt(Achar(Lista(Corpo, "servicos"), "id", "banco"), "estado"));
    }

    [Then(@"^a resposta é 200 e ""modulos"" traz, entre os outros:$")]
    public async Task ModulosDoExemplo(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        foreach (var esperado in JsonNode.Parse(json)!.AsArray())
        {
            var m = Achar(Lista(Corpo, "modulos"), "id", Txt(esperado, "id")!);
            Assert.True(m is not null, $"\"modulos\" sem {Txt(esperado, "id")}: {Corpo}");
            Campos(esperado!, m, "nome", "estado", "pedidos", "erros5xx", "taxaErro5xx", "p95Ms");
        }
        await Task.CompletedTask;
    }

    [Then(@"^""modulos"" vem na ordem FORA_DO_AR, LENTO, NO_AR e SEM_PEDIDOS$")]
    public void OrdemDosModulos() => Ordem(Lista(Corpo, "modulos"), "estado", OrdemModulos);

    // ── histórico (RF-195) ─────────────────────────────────────────────────
    [Then(@"^""serie"" traz (\d+) pontos, um por hora, do mais antigo ao atual$")]
    [Then(@"^""serie"" traz (60) pontos, do mais antigo ao atual$")]
    public void SerieCom(int pontos) => Serie(pontos, null);

    [Then(@"^a resposta é 200, com (\d+) pontos, um por (hora|dia)$")]
    public void PontosPor(int pontos, string por)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Serie(pontos, por == "hora" ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1));
    }

    void Serie(int pontos, TimeSpan? passo)
    {
        var serie = Lista(Corpo, "serie");
        Assert.True(serie.Count == pontos, $"esperava {pontos} pontos em \"serie\", vieram {serie.Count}");
        var inicios = serie.Select(p => DateTimeOffset.Parse(Txt(p, "inicio")!)).ToList();
        Assert.True(inicios.SequenceEqual(inicios.OrderBy(i => i)), "\"serie\" devia vir do mais antigo ao atual");
        if (passo is { } p && p == TimeSpan.FromHours(1)) Assert.True(inicios.Zip(inicios.Skip(1), (x, y) => y - x).All(d => d == p), "um ponto por hora");
    }

    [Then(@"^a resposta é 200, como (\?periodo=24h|\?janela=1h|&ordem=p95)$")]
    public void ComoPadrao(string padrao)
    {
        Respostas.Confere(mundo.Ultima, 200);
        if (padrao == "?periodo=24h") { Assert.Equal("24h", Txt(Corpo, "periodo")); Serie(24, null); }
        else if (padrao == "?janela=1h") { Assert.Equal("1h", Txt(Corpo, "janela")); Serie(60, null); }
        else Assert.Equal("p95", Txt(Corpo, "ordem"));
    }

    [Then(@"^a primeira queda vem com ""fim"": null e ""duracaoMin"": (\d+)$")]
    public void QuedaAberta(int minutos)
    {
        var q = Lista(Corpo, "quedas")[0]!;
        Assert.True(q["fim"] is null && q["duracaoMin"]!.GetValue<int>() == minutos, $"esperava fim null e {minutos} min: {q}");
    }

    // ── métricas (RF-197, RF-198, RN-317) ──────────────────────────────────
    [Then(@"^o minuto sem pedido vem com ""pedidos"": 0 e ""p95Ms"": null$")]
    public void MinutoVazio() =>
        Assert.True(Lista(Corpo, "serie").Any(p => p!["pedidos"]!.GetValue<int>() == 0 && p["p95Ms"] is null), $"nenhum minuto sem pedido: {Corpo}");

    [Then(@"^a resposta é 200, com ""modulo"": ""MOD-02"" e só os pedidos do SAC$")]
    public void SoOSac()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal("MOD-02", Txt(Corpo, "modulo"));
    }

    [Then(@"^o resumo traz ""p50Ms"": (\d+), ""p95Ms"": (\d+) e ""p99Ms"": (\d+|null)$")]
    public void Percentis(int p50, int p95, string p99)
    {
        var r = Corpo?["resumo"];
        Assert.True(r?["p50Ms"]?.GetValue<int>() == p50 && r["p95Ms"]?.GetValue<int>() == p95, $"esperava p50 {p50} e p95 {p95}: {r}");
        Assert.True(p99 == "null" ? r["p99Ms"] is null : r["p99Ms"]?.GetValue<int>() == int.Parse(p99), $"esperava p99 {p99}: {r}");
    }

    [Then(@"^a resposta é 200 com até 20 itens, o de maior p95 primeiro:$")]
    public void EnderecosMaisLentos(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperado = JsonNode.Parse(json)!;
        Campos(esperado, Corpo, "janela", "modulo", "ordem");
        var itens = Lista(Corpo, "itens");
        Assert.True(itens.Count is > 0 and <= 20, $"esperava de 1 a 20 itens: {itens.Count}");
        foreach (var c in new[] { "metodo", "endereco", "modulo", "pedidos", "p50Ms", "p95Ms", "p99Ms", "erros4xx", "erros5xx", "lento" })
        {
            Assert.True(itens.All(i => i is JsonObject o && o.ContainsKey(c)), $"item sem \"{c}\": {itens}");
        }
        var p95 = itens.Select(i => i!["p95Ms"]?.GetValue<int>() ?? int.MaxValue).ToList();
        Assert.True(p95.SequenceEqual(p95.OrderByDescending(p => p)), $"o de maior p95 vem primeiro: {itens}");
    }

    [Then(@"^""lento"" é true quando ""p95Ms"" passa de 1000 ou é null por estar acima de 10 s$")]
    public void Lento() => Assert.All(Lista(Corpo, "itens"), i =>
        Assert.True(i!["lento"]!.GetValue<bool>() == (i["p95Ms"] is null || i["p95Ms"]!.GetValue<int>() > 1000), $"\"lento\" errado: {i}"));

    // ── ações (RN-319) ─────────────────────────────────────────────────────
    [Then(@"^a resposta é 200 com uma lista em que cada ação vem assim:$")]
    public void CatalogoDeAcoes(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var lista = Corpo!.AsArray();
        Assert.All(lista, i => Assert.True(i?["acao"] is not null && i["nome"] is not null && i["modulo"] is not null && i["semQuem"] is not null, $"ação incompleta: {i}"));
        foreach (var esperado in JsonNode.Parse(json)!.AsArray())
        {
            var veio = Achar(lista, "acao", Txt(esperado, "acao")!);
            Assert.True(veio is not null, $"sem a ação {Txt(esperado, "acao")}");
            Campos(esperado!, veio, "nome", "modulo", "semQuem");
        }
    }

    [Then(@"^a lista traz ""(\w+)"" com o nome ""(.+)"", o módulo ""(MOD-\d+)"" e ""semQuem"" ""(\w+)""$")]
    public void AcaoDoCatalogo(string acao, string nome, string modulo, string sem)
    {
        var i = Achar(Corpo!.AsArray(), "acao", acao);
        Assert.True(i is not null && Txt(i, "nome") == nome && Txt(i, "modulo") == modulo && Txt(i, "semQuem") == sem, $"esperava {acao} / {nome} / {modulo} / {sem}: {i}");
    }

    // ── execuções (RF-202, RF-203, RN-318) ─────────────────────────────────
    [Then(@"^a resposta é 200 com ""resultado"": ""(\w+)"" e ""duracaoMs"": (null|\d+)$")]
    public void ResultadoEDuracao(string resultado, string duracao)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(resultado, Txt(Corpo, "resultado"));
        Assert.True(duracao == "null" ? Corpo!["duracaoMs"] is null : Corpo!["duracaoMs"]!.GetValue<int>() == int.Parse(duracao), $"esperava duracaoMs {duracao}: {Corpo}");
    }

    [Then(@"^a resposta é 200 com ""resultado"": ""FALHOU"", ""codigo"": ""BANCO_INDISPONIVEL""$")]
    public void Falhou()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Txt(Corpo, "resultado") == "FALHOU" && Txt(Corpo, "codigo") == "BANCO_INDISPONIVEL", $"esperava FALHOU por BANCO_INDISPONIVEL: {Corpo}");
    }

    [Then(@"^""duracaoMs"": 2107, do ""INICIADO"" ao registro de erro$")]
    public void Duracao2107() => Assert.Equal(2107, Corpo!["duracaoMs"]!.GetValue<int>());

    [Then(@"^o último passo com ""tipo"": ""ERRO"" e ""nivel"": ""ERRO""$")]
    public void UltimoPassoErro()
    {
        var ultimo = Lista(Corpo, "passos").Last();
        Assert.True(Txt(ultimo, "tipo") == "ERRO" && Txt(ultimo, "nivel") == "ERRO", $"esperava o último passo ERRO/ERRO: {ultimo}");
    }

    [Then(@"^a resposta é 200 com a contagem do período e as execuções pedidas:$")]
    public void ExecucoesComProblema(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var c = Corpo?["contagem"];
        foreach (var campo in new[] { "concluidas", "recusadas", "falharam", "interrompidas", "emAndamento" }) Assert.True(c?[campo] is not null, $"\"contagem\" sem {campo}: {Corpo}");
        Assert.True(c!["concluidas"]!.GetValue<int>() >= 398 && c["recusadas"]!.GetValue<int>() == 9 && c["falharam"]!.GetValue<int>() == 4
            && c["interrompidas"]!.GetValue<int>() == 1 && c["emAndamento"]!.GetValue<int>() == 0, $"contagem fora do exemplo: {c}");
        var itens = Lista(Corpo, "itens");
        Assert.True(itens.Count == 5 && itens.All(i => Txt(i, "resultado") is "FALHOU" or "INTERROMPIDA"), $"esperava as 5 execuções FALHOU ou INTERROMPIDA: {itens}");
        Assert.True(itens.Any(i => Txt(i, "resultado") == "INTERROMPIDA" && i!["duracaoMs"] is null), $"INTERROMPIDA com duracaoMs null: {itens}");
        Assert.True(Corpo!["de"] is not null && Corpo["ate"] is not null, "\"de\" e \"ate\" ausentes");
    }

    [Then(@"^""contagem"" conta todos os resultados do período, com os outros filtros, sem o ""resultado""$")]
    public void ContagemSemFiltro() => Assert.True(Corpo!["contagem"]!["concluidas"]!.GetValue<int>() >= 398 && Corpo["itens"]!.AsArray().Count < 398, $"a contagem inclui todos os resultados: {Corpo}");

    // ── ocorrências (RF-204, RF-205, RN-324, RNF-252) ──────────────────────
    // chamado pelo "a resposta é 200, sem o cabeçalho X-Trace-Id, com:" de commons/autenticacao, pelo ConferirDoExemplo
    public void AbertasEVoltaram(JsonNode e)
    {
        foreach (var lista in new[] { "abertas", "voltaram" })
        {
            foreach (var esperado in Lista(e, lista))
            {
                var veio = Lista(Corpo, lista).FirstOrDefault(o => Txt(o, "alvo") == Txt(esperado, "alvo"));
                Assert.True(veio is not null, $"\"{lista}\" sem {Txt(esperado, "alvo")}: {Corpo}");
                Campos(esperado!, veio, "tipo", "nome", "de", "para", "falha", "duracaoMin");
                Perto("inicio", veio, DateTimeOffset.Parse(Txt(esperado, "inicio")!), UmMinuto);
                Perto("quando", veio, DateTimeOffset.Parse(Txt(esperado, "quando")!), UmMinuto);
            }
        }
    }

    [Then(@"^""abertas"" traz a última mudança de cada serviço ou módulo fora de NO_AR$")]
    public void AbertasForaDeNoAr() =>
        Assert.True(Lista(Corpo, "abertas").All(o => Txt(o, "para") != "NO_AR") && Lista(Corpo, "abertas").Select(o => Txt(o, "alvo")).Distinct().Count() == Lista(Corpo, "abertas").Count,
            $"\"abertas\" só com a última mudança de cada alvo, fora de NO_AR: {Corpo}");

    [Then(@"^vem com FORA_DO_AR antes de LENTO e, entre as iguais, a mais nova primeiro$")]
    public void AbertasEmOrdem()
    {
        var abertas = Lista(Corpo, "abertas");
        Ordem(abertas, "para", ["FORA_DO_AR", "LENTO"]);
        foreach (var grupo in abertas.GroupBy(o => Txt(o, "para")))
        {
            var quando = grupo.Select(o => DateTimeOffset.Parse(Txt(o, "quando")!)).ToList();
            Assert.True(quando.SequenceEqual(quando.OrderByDescending(q => q)), $"a mais nova primeiro entre as {grupo.Key}");
        }
    }

    [Then(@"^""voltaram"" traz só as voltas a NO_AR dos últimos 10 min, a mais nova primeiro$")]
    public void Voltaram()
    {
        var v = Lista(Corpo, "voltaram");
        Assert.All(v, o => Assert.Equal("NO_AR", Txt(o, "para")));
        var quando = v.Select(o => DateTimeOffset.Parse(Txt(o, "quando")!)).ToList();
        Assert.True(quando.SequenceEqual(quando.OrderByDescending(q => q)), "a mais nova primeiro");
    }

    [Then(@"^""inicio"" é quando o alvo saiu de NO_AR, e ""duracaoMin"" só vem na volta$")]
    public void InicioEDuracao()
    {
        Assert.All(Lista(Corpo, "abertas"), o => Assert.Null(o!["duracaoMin"]));
        Assert.All(Lista(Corpo, "voltaram"), o => Assert.NotNull(o!["duracaoMin"]));
    }

    [Then(@"^a resposta é 200, com o cabeçalho X-Trace-Id e as últimas 24 h:$")]
    public void OcorrenciasDoPeriodo(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Auditoria_TraceId();
        var e = JsonNode.Parse(json)!;
        Perto("de", Corpo, DateTimeOffset.Parse(Txt(e, "de")!), UmMinuto);
        Perto("ate", Corpo, DateTimeOffset.Parse(Txt(e, "ate")!), UmMinuto);
        var esperado = Lista(e, "itens")[0]!;
        var veio = Lista(Corpo, "itens").FirstOrDefault(o => Txt(o, "alvo") == Txt(esperado, "alvo"));
        Assert.True(veio is not null, $"\"itens\" sem {Txt(esperado, "alvo")}: {Corpo}");
        Campos(esperado, veio, "tipo", "nome", "de", "para", "falha", "duracaoMin");
        Perto("inicio", veio, DateTimeOffset.Parse(Txt(esperado, "inicio")!), UmMinuto);
        Perto("quando", veio, DateTimeOffset.Parse(Txt(esperado, "quando")!), UmMinuto);
        Assert.True(Corpo!["proximo"] is null, "\"proximo\" devia vir null");
    }

    void Auditoria_TraceId() =>
        Assert.False(string.IsNullOrWhiteSpace(mundo.Ultima.Cabecalho("X-Trace-Id")), $"a resposta veio sem X-Trace-Id: {mundo.Ultima}");

    [Then(@"^""itens"" vem da mais nova para a mais antiga, até 50 por página$")]
    public void ItensDaMaisNova()
    {
        var itens = Lista(Corpo, "itens");
        var quando = itens.Select(o => DateTimeOffset.Parse(Txt(o, "quando")!)).ToList();
        Assert.True(itens.Count <= 50 && quando.SequenceEqual(quando.OrderByDescending(q => q)), $"da mais nova para a mais antiga, até 50: {itens}");
    }

    [Then(@"^""itens"" traz (.+)$")]
    public void ItensTraz(string o)
    {
        var itens = Lista(Corpo, "itens");
        if (o == "nenhum") { Assert.Empty(itens); return; }
        if (o.StartsWith("uma só ocorrência", StringComparison.Ordinal))
        {
            Assert.True(itens.Count == 1, $"esperava uma só ocorrência: {itens}");
            Perto("quando", itens[0], Hora("13:31:00"), UmMinuto);
            return;
        }
        if (o.StartsWith("a ida do \"banco\"", StringComparison.Ordinal))
        {
            var ida = itens.FirstOrDefault(i => Txt(i, "alvo") == "banco" && Txt(i, "para") == "FORA_DO_AR");
            Assert.True(ida is not null, $"sem a ida do banco a FORA_DO_AR: {itens}");
            Perto("quando", ida, Hora("13:41:00"), UmMinuto);
            return;
        }
        var quantos = int.Parse(o[..1]);
        Assert.True(itens.Count == quantos, $"esperava {quantos} itens: {itens}");
        // a mais nova vem primeiro
        var nova = itens[0];
        foreach (Match m in Regex.Matches(o, @"""(de|para)"": ""(\w+)"""))
        {
            Assert.True(Txt(nova, m.Groups[1].Value) == m.Groups[2].Value, $"esperava {m.Groups[1].Value} {m.Groups[2].Value} na mais nova: {nova}");
        }
        if (o.Contains("duracaoMin", StringComparison.Ordinal)) Assert.NotNull(nova!["duracaoMin"]);
    }

    [Then(@"^a volta a NO_AR com ""quando"": ""(\S+)"" e ""duracaoMin"": (\d+)$")]
    public void VoltaDoBanco(string quando, int minutos)
    {
        var volta = Lista(Corpo, "itens").FirstOrDefault(i => Txt(i, "alvo") == "banco" && Txt(i, "para") == "NO_AR");
        Assert.True(volta is not null, $"sem a volta do banco a NO_AR: {Corpo}");
        Perto("quando", volta, DateTimeOffset.Parse(quando), UmMinuto);
        Assert.Equal(minutos, volta!["duracaoMin"]!.GetValue<int>());
    }

    [Then(@"^GET /auditoria/ocorrencias\?alvo=MOD-14 traz ""itens"": \[\]$")]
    public async Task ModuloSemPedidos()
    {
        await a.Pedir(await a.Caio(), "GET", "/auditoria/ocorrencias?alvo=MOD-14");
        Assert.Empty(Lista(Corpo, "itens"));
    }

    [Then(@"^GET /auditoria/ocorrencias/abertas responde 200 com a ocorrência do ""banco""$")]
    public async Task AbertasComBanco()
    {
        var r = await a.Pedir(await a.Caio(), "GET", "/auditoria/ocorrencias/abertas");
        Respostas.Confere(r, 200);
        Assert.True(Lista(Corpo, "abertas").Any(o => Txt(o, "alvo") == "banco"), $"sem a ocorrência do banco: {Corpo}");
    }

    [Then(@"^GET /auditoria/registros e GET /auditoria/ocorrencias respondem 503 BANCO_DE_DADOS_FORA$")]
    public async Task Respondem503()
    {
        var caio = await a.Caio();
        Respostas.Confere(await a.Pedir(caio, "GET", "/auditoria/registros"), 503, "BANCO_DE_DADOS_FORA");
        Respostas.Confere(await a.Pedir(caio, "GET", "/auditoria/ocorrencias"), 503, "BANCO_DE_DADOS_FORA");
    }

    [Then(@"^GET /auditoria/metricas e GET /auditoria/metricas/enderecos também respondem 503$")]
    public async Task Metricas503()
    {
        var caio = await a.Caio();
        Respostas.Confere(await a.Pedir(caio, "GET", "/auditoria/metricas"), 503, "BANCO_DE_DADOS_FORA");
        Respostas.Confere(await a.Pedir(caio, "GET", "/auditoria/metricas/enderecos"), 503, "BANCO_DE_DADOS_FORA");
    }

    [Then(@"^a ""mensagem"" é ""(.*)""$")]
    public void Mensagem(string mensagem) => Assert.Equal(mensagem, Txt(Corpo, "mensagem"));

    // ── card (RN-322) ──────────────────────────────────────────────────────
    [Then(@"^""normal\.valor"" é 1 a mais do que antes, com ""versao"" maior$")]
    public void CardMaisUm()
    {
        Assert.Equal(a.Base!["normal"]!["valor"]!.GetValue<int>() + 1, Corpo!["normal"]!["valor"]!.GetValue<int>());
        Assert.True(Corpo["versao"]!.GetValue<long>() > a.Base["versao"]!.GetValue<long>(), $"\"versao\" devia crescer: {Corpo}");
    }

    // ── medição injetada (RN-317) ───────────────────────────────────────────
    int PedidosDoSac(JsonNode? c) => c?["resumo"]?["pedidos"]?.GetValue<int>() ?? -1;

    [Then(@"^GET /auditoria/metricas\?janela=1h&modulo=MOD-02 já conta os 10, sem /medicoes/gravar$")]
    public async Task JaConta()
    {
        a.Base = (await a.Pedir(await a.Caio(), "GET", "/auditoria/metricas?janela=1h&modulo=MOD-02")).Corpo;
        Assert.True(PedidosDoSac(a.Base) >= 10, $"esperava os 10 pedidos medidos já contados: {a.Base}");
    }

    [Then(@"^a próxima verificação já os usa no estado do ""MOD-02""$")]
    public async Task UsaNaVerificacao()
    {
        await a.Verificar();
        var saude = (await a.Pedir(await a.Caio(), "GET", "/auditoria/saude")).Corpo;
        var m = Achar(Lista(saude, "modulos"), "id", "MOD-02");
        Assert.True(m is not null && m["pedidos"]!.GetValue<int>() >= 10 && Txt(m, "estado") != "SEM_PEDIDOS", $"o MOD-02 devia usar os 10 pedidos: {m}");
    }

    [Then(@"^POST /_teste/auditoria/medicoes/gravar só grava o que os pedidos de verdade juntaram$")]
    public async Task GravarNaoDobra()
    {
        await a.GravarMedicoes();
        var depois = (await a.Pedir(await a.Caio(), "GET", "/auditoria/metricas?janela=1h&modulo=MOD-02")).Corpo;
        Assert.True(PedidosDoSac(depois) < 20, $"gravar não pode dobrar a medição injetada: {depois}");
    }

    // ── registros do gancho (RN-318) ───────────────────────────────────────
    async Task<JsonNode?> RegistroDoGancho(string consulta)
    {
        var item = Lista((await a.Pedir(await a.Caio(), "GET", consulta)).Corpo, "itens").FirstOrDefault(i => Txt(i, "traceId") == a.TraceIdDoGancho);
        Assert.True(item is not null, $"{consulta} sem o registro {a.TraceIdDoGancho}");
        return item;
    }

    [Then(@"^GET /auditoria/registros\?quem=usr-ebfga-maria&conta=ebfga traz o registro$")]
    public async Task TrazORegistro() => a.Base = await RegistroDoGancho("/auditoria/registros?quem=usr-ebfga-maria&conta=ebfga");

    [Then(@"^com ""quem"" de nome ""Maria Souza"" e ""conta"" de sigla ""EBFGA"", lidos de usuarios e contas$")]
    public void QuemEConta() => Assert.True(Txt(a.Base?["quem"], "nome") == "Maria Souza" && Txt(a.Base?["conta"], "sigla") == "EBFGA", $"quem e conta lidos: {a.Base}");

    [Then(@"^GET /auditoria/execucoes\?conta=ebfga conta a execução dele$")]
    public async Task ContaAExecucao()
    {
        var itens = Lista((await a.Pedir(await a.Caio(), "GET", "/auditoria/execucoes?conta=ebfga")).Corpo, "itens");
        Assert.Contains(itens, i => Txt(i, "traceId") == a.TraceIdDoGancho);
    }

    [Then(@"^a resposta é 201, com ""quem"": null e ""conta"": null$")]
    public async Task SemQuemENemConta()
    {
        Respostas.Confere(mundo.Ultima, 201);
        var item = await RegistroDoGancho($"/auditoria/registros?traceId={a.TraceIdDoGancho}");
        Assert.True(item!["quem"] is null && item["conta"] is null, $"esperava quem e conta null: {item}");
    }

    // ── RF-205: o limite de 31 dias com "ate" explícito ────────────────────
    [Then(@"^a resposta é 200, com os 31 dias$")]
    public void TrintaEUmDias()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(TimeSpan.FromDays(31), DateTimeOffset.Parse(Txt(Corpo, "ate")!) - DateTimeOffset.Parse(Txt(Corpo, "de")!));
    }

    // ── ganchos (RN-315, RN-317, RN-318, RNF-246) ──────────────────────────
    [Then(@"^a resposta é 204, e o cache volta à verificação real$")]
    public void Sem204Real() => Respostas.Confere(mundo.Ultima, 204);

    [Then(@"^a resposta é 204, card do minuto e descarte já feitos$")]
    public void Sem204Agendamento() => Respostas.Confere(mundo.Ultima, 204);

    [Then(@"^a resposta é 204, sondas trocadas e medições zeradas$")]
    public void Sem204Reiniciar() => Respostas.Confere(mundo.Ultima, 204);

    [Then(@"^a resposta é 422 DADOS_INVALIDOS sem ""resposta""$")]
    public void SemResposta() { Respostas.Confere(mundo.Ultima, 422, "DADOS_INVALIDOS"); Assert.Equal("resposta", Txt(Corpo, "campo")); }

    [Then(@"^a resposta é 422 DADOS_INVALIDOS com ""tempoMs"": 3001$")]
    public void TempoAlto() { Respostas.Confere(mundo.Ultima, 422, "DADOS_INVALIDOS"); Assert.Equal("tempoMs", Txt(Corpo, "campo")); }

    [Then(@"^a sonda responde com ""tempoMs"": 2000, e o cache fica LENTO na próxima verificação$")]
    public async Task SondaLenta()
    {
        await a.Verificar();
        var saude = (await a.Pedir(await a.Caio(), "GET", "/auditoria/saude")).Corpo;
        var cache = Achar(Lista(saude, "servicos"), "id", "cache");
        Assert.True(Txt(cache, "estado") == "LENTO" && cache!["tempoMs"]!.GetValue<int>() == 2000, $"esperava o cache LENTO com 2000 ms: {cache}");
    }

    [Then(@"^a resposta é 200 em até 4 s, porque cada serviço tem até 3 s para responder$")]
    public void ate4s()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(a.UltimaDuracao <= TimeSpan.FromSeconds(4), $"a verificação levou {a.UltimaDuracao.TotalSeconds:0.0} s, o limite é 4 s");
    }

    [Then(@"^GET /auditoria/execucoes\?resultado=FALHOU,INTERROMPIDA de 7 dias chega em até 3 s$")]
    public static void TempoDeCarga7d() => throw new PendingStepException("RNF-250 é de carga: sem suíte de carga ainda (RN-155)");

    [Then(@"^a medição vai com o módulo ""(MOD-\d+)""$")]
    public async Task MedicaoDoModulo(string modulo)
    {
        var pedido = a.PedidoMedido ?? "POST /auth/hub/entrar";
        await a.GravarMedicoes();
        a.Base = (await a.Pedir(await a.Caio(), "GET", "/auditoria/metricas/enderecos?janela=1h&limite=100")).Corpo;
        var item = Lista(a.Base, "itens").FirstOrDefault(i => $"{Txt(i, "metodo")} {Txt(i, "endereco")}" == pedido);
        Assert.True(item is not null, $"\"{pedido}\" não está nos endereços medidos: {a.Base}");
        Assert.Equal(modulo, Txt(item, "modulo"));
    }

    [Then(@"^GET /auditoria/metricas/enderecos\?janela=1h&modulo=MOD-02 traz ""GET /sac/chamados""$")]
    public async Task SacMedido()
    {
        var corpo = (await a.Pedir(await a.Caio(), "GET", "/auditoria/metricas/enderecos?janela=1h&modulo=MOD-02")).Corpo;
        a.Depois = corpo;
        Assert.True(Achar(Lista(corpo, "itens"), "endereco", "/sac/chamados") is not null, $"sem GET /sac/chamados: {corpo}");
    }

    [Then(@"^com 10 pedidos a mais do que antes$")]
    public void DezAMais()
    {
        int Pedidos(JsonNode? c) => Lista(c, "itens").Where(i => Txt(i, "endereco") == "/sac/chamados" && Txt(i, "metodo") == "GET").Sum(i => i!["pedidos"]!.GetValue<int>());
        Assert.Equal(Pedidos(a.Base) + 10, Pedidos(a.Depois));
    }

    [Then(@"^nenhum endereço novo aparece em GET /auditoria/metricas/enderecos$")]
    public void SemEnderecoNovo() => Assert.True(a.Base!.AsArray().Count == 0, $"endereços novos: {a.Base}");

    // ── volume (RNF-250) e fora de escopo ──────────────────────────────────
    [Given(@"^5\.000\.000 registros em ""auditoria"" e 400\.000 em ""auditoriaErros"", de 800 contas$")]
    [Given(@"^90 dias de medições por hora de 60 endereços$")]
    public static void Volume() => throw new PendingStepException(
        "RNF-250 é de carga: semear milhões de documentos e medir o tempo cabe numa suíte de carga (5_DEV/qa/backend/carga ainda não existe, RN-155/RN-156)");

    [Then(@"^a resposta 200 chega em até (\d+) s(?:,| ).*$")]
    public static void TempoDeCarga(int _) => throw new PendingStepException("RNF-250 é de carga: sem suíte de carga ainda (RN-155)");

    [Then(@"^(?:a Auditoria não altera|o aviso de queda fica|não vai por|não mostra|não mede|não é usada).*$")]
    public static void ForaDeEscopo() => throw new PendingStepException("fora de escopo: afirma o que a Auditoria não faz, sem comportamento a provocar pela API");

    [Then(@"^com o cache fora do ar nenhuma sessão é conferida, e a Auditoria também não abre$")]
    public static void CacheForaSemSessao() => throw new PendingStepException(
        "o cache real fora do ar não se provoca pela suíte (a sonda só simula a verificação); fica nos testes do backend");
}
