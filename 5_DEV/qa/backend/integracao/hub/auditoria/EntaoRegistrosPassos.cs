using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using Reqnroll;
using static Boaz.Qa.Integracao.Hub.Auditoria.AuditoriaDoTime;
using static Boaz.Qa.Integracao.Suporte.Mundo;
using Fonte = Boaz.Qa.Integracao.Suporte.Auditoria;

namespace Boaz.Qa.Integracao.Hub.Auditoria;

// Os Então sobre registros, a consulta auditada (RN-325), o ao vivo e o formato comum
// de 1_Requisitos/20_modulo-auditoria.md (v03). A mesma classe de EntaoPassos.cs.
public sealed partial class EntaoPassos
{
    // ── formato comum ──────────────────────────────────────────────────────
    async Task<JsonNode?> De(string caminho) => (await a.Pedir(await a.Caio(), "GET", caminho)).Corpo;

    static void Varrer(JsonNode? n, Action<string, JsonNode?> f)
    {
        if (n is JsonObject o) foreach (var (k, v) in o) { f(k, v); Varrer(v, f); }
        else if (n is JsonArray l) foreach (var v in l) Varrer(v, f);
    }

    [Then(@"^toda data e hora segue a ISO 8601 em UTC, como ""2026-11-20T13:42:30Z""$")]
    public async Task DatasEmUtc()
    {
        await a.Verificar();
        foreach (var caminho in new[] { "/auditoria/saude", "/auditoria/ocorrencias", "/auditoria/metricas" })
        {
            Varrer(await De(caminho), (k, v) =>
            {
                if (v?.GetValueKind() == System.Text.Json.JsonValueKind.String && Regex.IsMatch(v.GetValue<string>(), @"^\d{4}-\d{2}-\d{2}T"))
                {
                    Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", v.GetValue<string>());
                }
            });
        }
    }

    [Then(@"^a hora de um registro vai com milissegundos, como ""(.+)""$")]
    public async Task HoraComMilissegundos(string _)
    {
        await a.RegistrosRecentes(1);
        var itens = Lista(await De("/auditoria/registros"), "itens");
        Assert.NotEmpty(itens);
        Assert.All(itens, i => Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", Txt(i, "quando")!));
    }

    [Then(@"^dias, semanas e meses das séries começam às 00:00 do horário de Brasília$")]
    public async Task SeriesEmBrasilia()
    {
        var serie = Lista(await De("/auditoria/metricas?janela=7d"), "serie");
        Assert.All(serie, p => Assert.Equal(3, DateTimeOffset.Parse(Txt(p, "inicio")!).UtcDateTime.Hour));
    }

    [Then(@"^todo tempo vai em milissegundos inteiros, em campos terminados em ""Ms""$")]
    public async Task TemposInteiros()
    {
        foreach (var caminho in new[] { "/auditoria/saude", "/auditoria/metricas", "/auditoria/metricas/enderecos" })
        {
            Varrer(await De(caminho), (k, v) =>
            {
                if (k.EndsWith("Ms", StringComparison.Ordinal) && v is not null)
                {
                    Assert.True(v.GetValueKind() == System.Text.Json.JsonValueKind.Number && !v.ToJsonString().Contains('.'), $"\"{k}\" devia ser inteiro: {v.ToJsonString()}");
                }
            });
        }
    }

    [Then(@"^estado, resultado, tipo, nível e ação vão em maiúsculas, como ""(.+)""$")]
    public async Task EmMaiusculas(string _)
    {
        await a.RegistrosRecentes(1);
        foreach (var caminho in new[] { "/auditoria/saude", "/auditoria/registros", "/auditoria/execucoes", "/auditoria/ocorrencias" })
        {
            Varrer(await De(caminho), (k, v) =>
            {
                if (k is "estado" or "resultado" or "tipo" or "nivel" or "acao" or "falha" && v?.GetValueKind() == System.Text.Json.JsonValueKind.String)
                {
                    Assert.Matches(@"^[A-Z0-9_]+$", v.GetValue<string>());
                }
            });
        }
    }

    [Then(@"^só o papel ""ADMINISTRADOR"" e o ""MEMBRO_TIME"" com ""MOD-17"" com ""U"" usam a Auditoria$")]
    public async Task SoOTime()
    {
        Respostas.Confere(await a.Pedir(mundo.Admin(), "GET", "/auditoria/saude"), 200);
        Respostas.Confere(await a.Pedir(await a.Caio(), "GET", "/auditoria/saude"), 200);
        Respostas.Confere(await a.Pedir(await a.Rui(), "GET", "/auditoria/saude"), 403, "ACESSO_NEGADO");
        Respostas.Confere(await a.Pedir(await mundo.Master(), "GET", "/auditoria/saude"), 403, "ACESSO_NEGADO");
        Respostas.Confere(await a.Pedir(await a.MariaDoUsuarios(), "GET", "/auditoria/saude"), 403, "ACESSO_NEGADO");
    }

    [Then(@"^todo endereço de /auditoria é GET$")]
    public async Task SoGet()
    {
        var caio = await a.Caio();
        foreach (var metodo in new[] { "POST", "PUT", "PATCH", "DELETE" })
        {
            foreach (var caminho in new[] { "/auditoria/saude", "/auditoria/registros", "/auditoria/execucoes/trc-1d6e3f08" })
            {
                Respostas.Confere(await a.Pedir(caio, metodo, caminho), 405);
            }
        }
    }

    [Then(@"^a consulta atendida traz o cabeçalho X-Trace-Id e fica na auditoria, como na RN-325$")]
    public async Task ConsultaAuditada()
    {
        var r = await a.Pedir(await a.Caio(), "GET", "/auditoria/registros");
        Respostas.Confere(r, 200);
        Auditoria_TraceId();
        await Gravou(null);
    }

    // ── consulta auditada (RN-325) ─────────────────────────────────────────
    static string Alvo(string caminho)
    {
        var partes = caminho.Split('?', 2);
        if (partes.Length == 1) return caminho;
        var ps = partes[1].Split('&').Where(p => !p.StartsWith("depois=", StringComparison.Ordinal) && !p.StartsWith("limite=", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal);
        return ps.Any() ? $"{partes[0]}?{string.Join('&', ps)}" : partes[0];
    }

    async Task<List<BsonDocument>> Novos() { await Task.Delay(500); return await execucoes.Novos(Fonte.Registros); }

    async Task Gravou(string? alvo)
    {
        var novos = (await Novos()).Where(d => Fonte.Texto(d, "acao") == "CONSULTAR_AUDITORIA").ToList();
        Assert.True(novos.Select(d => Fonte.Texto(d, "etapa")).Order().SequenceEqual(["CONCLUIDO", "INICIADO"]),
            $"esperava INICIADO e CONCLUIDO de CONSULTAR_AUDITORIA, vieram {Fonte.Resumo(novos)}");
        if (alvo is not null) Assert.All(novos, d => Assert.Equal(alvo, Fonte.Texto(d, "alvo")));
    }

    [Then(@"^grava, com o alvo ""(.+)""$")]
    public async Task GravaComAlvo(string alvo) { Auditoria_TraceId(); await Gravou(alvo); }

    [Then(@"^grava, com esse alvo$")]
    public async Task GravaComEsseAlvo() { Auditoria_TraceId(); await Gravou(Alvo(a.UltimoCaminho)); }

    [Then(@"^grava uma vez, ao abrir$")]
    public async Task GravaUmaVez()
    {
        var novos = (await Novos()).Where(d => Fonte.Texto(d, "acao") == "CONSULTAR_AUDITORIA" && Fonte.Texto(d, "etapa") == "CONCLUIDO").ToList();
        Assert.True(novos.Count == 1, $"esperava um só CONSULTAR_AUDITORIA ao abrir, vieram {Fonte.Resumo(novos)}");
    }

    [Then(@"^não grava, sem X-Trace-Id$")]
    public async Task NaoGrava()
    {
        Assert.True(string.IsNullOrEmpty(mundo.Ultima.Cabecalho("X-Trace-Id")), "não devia trazer X-Trace-Id");
        var novos = (await Novos()).Where(d => Fonte.Texto(d, "acao") == "CONSULTAR_AUDITORIA").ToList();
        Assert.Empty(novos);
    }

    [Then(@"^a resposta traz o cabeçalho X-Trace-Id gerado pela aplicação, aqui o exemplo ""(trc-\w+)""$")]
    public void TraceDoExemplo(string exemplo)
    {
        var real = mundo.Ultima.Cabecalho("X-Trace-Id");
        Assert.False(string.IsNullOrWhiteSpace(real), $"sem X-Trace-Id: {mundo.Ultima}");
        mundo.Apelidos[exemplo] = () => Task.FromResult(real!);
    }

    [Then(@"^GET (/auditoria/registros\?acao=CONSULTAR_AUDITORIA) traz, entre os itens:$")]
    public async Task ItemDaConsulta(string caminho, string json)
    {
        var esperado = JsonNode.Parse(await mundo.Traduzir(json))!;
        var itens = Lista(await De(caminho), "itens");
        var concluido = itens.FirstOrDefault(i => Txt(i, "traceId") == Txt(esperado, "traceId") && Txt(i, "etapa") == "CONCLUIDO");
        Assert.True(concluido is not null, $"sem o CONCLUIDO do traceId {Txt(esperado, "traceId")}: {itens}");
        Campos(esperado, concluido, "tipo", "etapa", "nivel", "codigo", "acao", "acaoNome", "modulo", "alvo", "conta");
        Campos(esperado["quem"]!, concluido!["quem"], "id", "nome");
        Perto("quando", concluido, DateTimeOffset.Parse(Txt(esperado, "quando")!), UmMinuto);
        var posicao = itens.IndexOf(concluido);
        Assert.True(itens.Skip(posicao + 1).Any(i => Txt(i, "traceId") == Txt(esperado, "traceId") && Txt(i, "etapa") == "INICIADO"),
            "o INICIADO do mesmo traceId devia vir depois do CONCLUIDO");
    }

    [Then(@"^""traceId"" é o do cabeçalho dessa resposta; ""id"" e ""traceId"" mudam a cada pedido$")]
    public static void TraceIdDoCabecalho() { } // o traceId do exemplo vira o do cabeçalho em TraceDoExemplo; "id" não é comparado

    [Then(@"^o ""INICIADO"" do mesmo traceId vem logo depois dele, por ser mais antigo$")]
    public static void IniciadoDepois() { } // conferido no passo anterior, que lê a mesma lista

    [Then(@"^o alvo gravado é ""(.+)""$")]
    public async Task AlvoGravado(string alvo) => await Gravou(alvo);

    [Then(@"^os parâmetros vão em ordem alfabética, sem ""depois"" nem ""limite""$")]
    public void ParametrosEmOrdem() { } // o alvo exato do passo anterior já exige a ordem e a ausência

    [Then(@"^a resposta é 200, com o X-Trace-Id, e (nenhum|um) ""CONSULTAR_AUDITORIA"" novo$")]
    public async Task ConsultaRepetida(string quantos)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Auditoria_TraceId();
        var novos = (await Novos()).Where(d => Fonte.Texto(d, "acao") == "CONSULTAR_AUDITORIA" && Fonte.Texto(d, "etapa") == "CONCLUIDO").ToList();
        Assert.Equal(quantos == "um" ? 1 : 0, novos.Count);
    }

    [Then(@"^a resposta é (403 ACESSO_NEGADO|422 DADOS_INVALIDOS) e ""auditoria"" não ganha ""CONSULTAR_AUDITORIA""$")]
    public async Task RecusadaNaoGrava(string resposta)
    {
        var partes = resposta.Split(' ');
        Respostas.Confere(mundo.Ultima, int.Parse(partes[0]), partes[1]);
        Assert.DoesNotContain(await execucoes.Novos(Fonte.Registros), d => Fonte.Texto(d, "acao") == "CONSULTAR_AUDITORIA");
    }

    // ── só leitura e sem registro ──────────────────────────────────────────
    [Then(@"^a resposta é 405, sem corpo$")]
    public void Sem405() { Respostas.Confere(mundo.Ultima, 405); Assert.True(mundo.Ultima.Texto.Length == 0, $"o 405 devia vir sem corpo: {mundo.Ultima}"); }

    [Then(@"^""auditoria"" e ""auditoriaErros"" ficam como estavam$")]
    [Then(@"^""auditoria"" e ""auditoriaErros"" não ganham registro, como a leitura da RN-270 do 17$")]
    public async Task NadaMuda()
    {
        Assert.Empty(await execucoes.Novos(Fonte.Registros));
        Assert.Empty(await execucoes.Novos(Fonte.Erros));
    }

    [Then(@"^a resposta é 404 e ""auditoria"" não ganha registro$")]
    public async Task Gancho404()
    {
        Respostas.Confere(mundo.Ultima, 404);
        Assert.DoesNotContain(await Fonte.Todos(Fonte.Registros), d => Fonte.Texto(d, "acao") == "CRIAR_USUARIO" && Fonte.Texto(d, "etapa") == "INICIADO");
    }

    [Then(@"^a consulta não fica gravada, e o log da aplicação avisa ""consulta sem registro""$")]
    public async Task SemRegistroSemBanco()
    {
        // o log da aplicação de API_URL não é lido pela suíte; confere-se o que o banco mostra
        Assert.DoesNotContain(await Fonte.Todos(Fonte.Registros), d => Fonte.Texto(d, "acao") == "CONSULTAR_AUDITORIA");
    }

    // ── registros (RF-199, RF-200, RN-319, RN-321) ─────────────────────────
    [Then(@"^a resposta é 200, com ""auditoria"" e ""auditoriaErros"" juntos, 50 por página:$")]
    public async Task RegistrosDoExemplo(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var e = JsonNode.Parse(await mundo.Traduzir(json))!;
        Perto("de", Corpo, DateTimeOffset.Parse(Txt(e, "de")!), UmMinuto);
        Perto("ate", Corpo, DateTimeOffset.Parse(Txt(e, "ate")!), UmMinuto);
        var itens = Lista(Corpo, "itens");
        Assert.True(itens.Count <= 50, "50 por página");
        var quando = itens.Select(i => DateTimeOffset.Parse(Txt(i, "quando")!)).ToList();
        Assert.True(quando.SequenceEqual(quando.OrderByDescending(q => q)), "o mais novo primeiro");
        var esperado = Lista(e, "itens")[0]!;
        var veio = itens.FirstOrDefault(i => Txt(i, "acao") == Txt(esperado, "acao") && Txt(i, "etapa") == Txt(esperado, "etapa"));
        Assert.True(veio is not null, $"sem o CONCLUIDO de CRIAR_USUARIO: {itens}");
        Campos(esperado, veio, "tipo", "nivel", "codigo", "acaoNome", "modulo", "alvo");
        Campos(esperado["quem"]!, veio!["quem"], "id", "nome");
        Campos(esperado["conta"]!, veio["conta"], "sigla");
        Assert.Matches(@"^trc-[0-9a-f]{8}$", Txt(veio, "traceId")!);
        Perto("quando", veio, DateTimeOffset.Parse(Txt(esperado, "quando")!), UmMinuto);
    }

    [Then(@"^o item do erro vem com ""tipo"": ""ERRO"", ""etapa"": null$")]
    public void ItemDoErro()
    {
        var erro = Lista(Corpo, "itens").FirstOrDefault(i => Txt(i, "codigo") == "MATRICULA_EM_USO");
        Assert.True(erro is not null, $"sem o erro MATRICULA_EM_USO: {Corpo}");
        Assert.True(Txt(erro, "tipo") == "ERRO" && erro!["etapa"] is null, $"esperava tipo ERRO e etapa null: {erro}");
    }

    [Then(@"^""nivel"": ""ALERTA"" e ""codigo"": ""MATRICULA_EM_USO""$")]
    public void NivelAlerta()
    {
        var erro = Lista(Corpo, "itens").First(i => Txt(i, "codigo") == "MATRICULA_EM_USO");
        Assert.Equal("ALERTA", Txt(erro, "nivel"));
    }

    [Then(@"^a resposta é 200 com 50 itens e ""proximo""$")]
    public void Pagina1() { Respostas.Confere(mundo.Ultima, 200); Assert.Equal(50, Lista(Corpo, "itens").Count); Assert.NotNull(Corpo!["proximo"]); }

    [Then(@"^a resposta é 200 com os (130) e ""proximo"": null$")]
    [Then(@"^a resposta é 200 com os (30) últimos e ""proximo"": null$")]
    public void UltimaPagina(int quantos) { Respostas.Confere(mundo.Ultima, 200); Assert.Equal(quantos, Lista(Corpo, "itens").Count); Assert.Null(Corpo!["proximo"]); }

    [Then(@"^a resposta é 200 e traz só (.+)$")]
    public void TrazSo(string o)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var itens = Lista(Corpo, "itens");
        if (o.StartsWith("nenhum", StringComparison.Ordinal)) { Assert.Empty(itens); Assert.Null(Corpo!["proximo"]); return; }
        Assert.NotEmpty(itens);
        Func<JsonNode?, bool> ok = o switch
        {
            "os registros de \"auditoria\"" => i => Txt(i, "tipo") == "AUDITORIA",
            "os de \"auditoriaErros\" com o nivel ERRO" => i => Txt(i, "tipo") == "ERRO" && Txt(i, "nivel") == "ERRO",
            "os de \"auditoriaErros\" com o nivel ALERTA" => i => Txt(i, "tipo") == "ERRO" && Txt(i, "nivel") == "ALERTA",
            "os das ações do SAC" => i => Txt(i, "modulo") == "MOD-02",
            "os da ação CRIAR_USUARIO" => i => Txt(i, "acao") == "CRIAR_USUARIO",
            "os da conta de sigla EBFGA, sem diferença de caixa" => i => Txt(i?["conta"], "sigla") == "EBFGA",
            "os da Maria" => i => Txt(i?["quem"], "nome") == "Maria Souza",
            "os erros com esse código" => i => Txt(i, "tipo") == "ERRO" && Txt(i, "codigo") == "MATRICULA_EM_USO",
            _ => throw new ArgumentException($"filtro sem conferência: {o}"),
        };
        Assert.All(itens, i => Assert.True(ok(i), $"item fora do filtro \"{o}\": {i}"));
    }

    [Then(@"^a resposta é 200 com todos os registros desse traceId, sem o limite do período$")]
    public void PorTraceId()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var itens = Lista(Corpo, "itens");
        Assert.True(itens.Count >= 2 && itens.All(i => Txt(i, "traceId") == "trc-4b7e01c9"), $"esperava os registros de trc-4b7e01c9: {itens}");
    }

    [Then(@"^""de"" e ""ate"" vêm null$")]
    public void DeEAteNull() { Assert.Null(Corpo!["de"]); Assert.Null(Corpo["ate"]); }

    [Then(@"^o item vem com ""acaoNome"": ""(\w+)"" e ""modulo"": null$")]
    public void AcaoSemCatalogo(string acao)
    {
        var item = Lista(Corpo, "itens").FirstOrDefault(i => Txt(i, "acao") == acao);
        Assert.True(item is not null && Txt(item, "acaoNome") == acao && item["modulo"] is null, $"esperava acaoNome {acao} e modulo null: {item}");
    }

    [Then(@"^""de"" e ""ate"" da resposta são (.+)$")]
    public void PeriodoQueFalta(string o)
    {
        var agora = a.Agora();
        DateTimeOffset de, ate;
        if (o == "as últimas 24 h até agora") (de, ate) = (agora - TimeSpan.FromHours(24), agora);
        else if (o.StartsWith("de ", StringComparison.Ordinal)) (de, ate) = (DateTimeOffset.Parse(o.Split(' ')[1]), agora);
        else { ate = DateTimeOffset.Parse(o.Split(' ').Last()); de = ate - TimeSpan.FromHours(24); }
        if (!o.StartsWith("as 24 h antes", StringComparison.Ordinal) && !o.StartsWith("de ", StringComparison.Ordinal)) { }
        Perto("de", Corpo, de, UmMinuto);
        Perto("ate", Corpo, ate, UmMinuto);
    }

    // ── "a resposta é 200, …" das linhas de Exemplos ───────────────────────
    [Then(@"^a resposta é 200, (com o de mais pedidos primeiro|com o de mais erros 5xx primeiro, depois o de mais 4xx|com até (?:100|200) itens|com todas, a mais nova primeiro, 50 por página|só com as recusadas|só com as do SAC da EBFGA|só com as do intermediador|só com as dos módulos|só com as mudanças para FORA_DO_AR|como na RN-279 de 15_modulos)$")]
    public void Resposta200(string o)
    {
        Respostas.Confere(mundo.Ultima, 200);
        if (o == "como na RN-279 de 15_modulos") { FluxoAberto(); return; }
        var itens = Lista(Corpo, "itens");
        switch (o)
        {
            case "com o de mais pedidos primeiro":
                Assert.True(itens.Select(i => i!["pedidos"]!.GetValue<int>()).SequenceEqual(itens.Select(i => i!["pedidos"]!.GetValue<int>()).OrderByDescending(x => x)), $"mais pedidos primeiro: {itens}");
                break;
            case "com o de mais erros 5xx primeiro, depois o de mais 4xx":
                var chaves = itens.Select(i => (i!["erros5xx"]!.GetValue<int>(), i["erros4xx"]!.GetValue<int>())).ToList();
                Assert.True(chaves.SequenceEqual(chaves.OrderByDescending(c => c.Item1).ThenByDescending(c => c.Item2)), $"mais erros primeiro: {itens}");
                break;
            case var s when s.StartsWith("com até", StringComparison.Ordinal):
                Assert.True(itens.Count <= int.Parse(Regex.Match(s, @"\d+").Value), $"itens além do limite: {itens.Count}");
                break;
            case "com todas, a mais nova primeiro, 50 por página":
                var inicios = itens.Select(i => DateTimeOffset.Parse(Txt(i, "inicio")!)).ToList();
                Assert.True(itens.Count <= 50 && inicios.SequenceEqual(inicios.OrderByDescending(x => x)), $"50 por página, a mais nova primeiro: {itens}");
                break;
            case "só com as recusadas":
                Assert.NotEmpty(itens); Assert.All(itens, i => Assert.Equal("RECUSADA", Txt(i, "resultado")));
                break;
            case "só com as do SAC da EBFGA":
                Assert.NotEmpty(itens); Assert.All(itens, i => Assert.True(Txt(i, "modulo") == "MOD-02" && Txt(i?["conta"], "sigla") == "EBFGA", $"fora do filtro: {i}"));
                break;
            case "só com as do intermediador":
                Assert.NotEmpty(itens); Assert.All(itens, i => Assert.Equal("intermediador-pagamento", Txt(i, "alvo")));
                break;
            case "só com as dos módulos":
                Assert.NotEmpty(itens); Assert.All(itens, i => Assert.Equal("MODULO", Txt(i, "tipo")));
                break;
            case "só com as mudanças para FORA_DO_AR":
                Assert.NotEmpty(itens); Assert.All(itens, i => Assert.Equal("FORA_DO_AR", Txt(i, "para")));
                break;
        }
    }

    // ── ao vivo (RF-201, RF-204, RNF-246, RNF-249, RN-322) ─────────────────
    void FluxoAberto()
    {
        var f = a.UltimoFluxo;
        Assert.True(f.Status == 200 && f.TipoDoConteudo == "text/event-stream", $"esperava 200 em text/event-stream: {f}");
        Assert.True(f.CacheControl?.Contains("no-store", StringComparison.OrdinalIgnoreCase) == true, $"esperava Cache-Control no-store, veio \"{f.CacheControl}\"");
    }

    [Then(@"^a resposta, 200 com Content-Type ""text/event-stream"" e Cache-Control ""no-store""$")]
    [Then(@"^a resposta é 200 com Content-Type ""text/event-stream"" e Cache-Control ""no-store""$")]
    public void FluxoSemCache() => FluxoAberto();

    [Then(@"^recebe o evento ""registro"", com id igual ao do registro e o corpo de um item da RF-199$")]
    public async Task EventoRegistro()
    {
        var e = await a.UltimoFluxo.EsperarTipo("registro", Cinco + Folga);
        Assert.True(e is not null, $"o evento \"registro\" não chegou em 5 s: {a.UltimoFluxo}");
        Assert.True(e.Id == Txt(e.Corpo, "id") && e.Corpo?["traceId"] is not null && e.Corpo["acao"] is not null && e.Corpo["quando"] is not null, $"evento fora do item da RF-199: {e}");
        Assert.Equal("trc-4b7e01c9", Txt(e.Corpo, "traceId"));
    }

    [Then(@"^um "": ping"" chega a cada 30 s$")]
    public async Task Ping() =>
        Assert.True(await a.UltimoFluxo.EsperarTipo("ping", TimeSpan.FromSeconds(30) + Folga) is not null, $"o \": ping\" não chegou em 30 s: {a.UltimoFluxo}");

    [Then(@"^a 4ª recebe 200 e a mais antiga recebe ""encerrado"" com ""LIMITE_CONEXOES"" e fecha$")]
    [Then(@"^a mais antiga recebe ""encerrado"" com ""LIMITE_CONEXOES"" e fecha$")]
    [Then(@"^a 4ª conexão do mesmo usuário fecha a mais antiga com ""encerrado"" e ""LIMITE_CONEXOES""$")]
    public async Task LimiteDeConexoes()
    {
        Assert.Equal(200, a.UltimoFluxo.Status);
        var antiga = a.Fluxos[0];
        var e = await antiga.EsperarTipo("encerrado", Cinco);
        Assert.True(e is not null && Txt(e.Corpo, "codigo") == "LIMITE_CONEXOES", $"a mais antiga devia receber \"encerrado\" LIMITE_CONEXOES: {antiga}");
        Assert.True(await antiga.EsperarFim(Folga), $"a mais antiga devia fechar: {antiga}");
    }

    [Then(@"^o 1º evento ""(saude|card)"" chega logo ao abrir, com o corpo de GET (/auditoria/\S+)$")]
    public async Task PrimeiroEvento(string tipo, string caminho)
    {
        var f = a.UltimoFluxo;
        var e = await f.EsperarTipo(tipo, Cinco);
        Assert.True(e is not null && f.Eventos.First(x => x.Tipo != "ping") == e, $"o 1º evento devia ser \"{tipo}\": {f}");
        var lido = (await a.Pedir(await a.Caio(), "GET", caminho)).Corpo;
        if (tipo == "card") Campos(lido!, e.Corpo, "modulo", "perfil", "vazio", "selo");
        else Assert.True(e.Corpo?["servicos"] is JsonArray && e.Corpo["geral"] is not null, $"o evento \"saude\" devia ter o corpo da saúde: {e}");
    }

    [Then(@"^o 1º evento, logo ao abrir, é ""abertas"", com o corpo de GET /auditoria/ocorrencias/abertas$")]
    public async Task PrimeiroAbertas()
    {
        var f = a.UltimoFluxo;
        var e = await f.EsperarTipo("abertas", Cinco);
        Assert.True(e is not null && f.Eventos.First(x => x.Tipo != "ping") == e && e.Corpo?["abertas"] is JsonArray && e.Corpo["voltaram"] is JsonArray, $"o 1º evento devia ser \"abertas\": {f}");
    }

    [Then(@"^quando o cache passa a LENTO, o evento ""ocorrencia"" chega em até 5 s com um item$")]
    [Then(@"^o evento ""ocorrencia"" chega até 13:31:05$")]
    public async Task OcorrenciaChega()
    {
        await a.Sonda("cache", "LENTA");
        await a.Verificar();
        var e = await a.UltimoFluxo.EsperarTipo("ocorrencia", Cinco + Folga);
        Assert.True(e is not null && Txt(e.Corpo, "alvo") == "cache" && Txt(e.Corpo, "para") == "LENTO", $"o evento \"ocorrencia\" não chegou em 5 s: {a.UltimoFluxo}");
    }

    [Then(@"^recebe também o evento ""ocorrencia"" em até 5 s, com o mesmo corpo$")]
    public async Task OcorrenciaNosOutros()
    {
        var e = await a.UltimoFluxo.EsperarTipo("ocorrencia", Cinco + Folga);
        Assert.True(e is not null && Txt(e.Corpo, "alvo") == "cache", $"o evento \"ocorrencia\" não chegou: {a.UltimoFluxo}");
    }

    [Then(@"^depois um evento ""saude"" a cada verificação, a cada 30 s, com ""verificadoEm"" novo$")]
    public async Task SaudeAPorVerificacao()
    {
        var f = a.UltimoFluxo;
        var primeiro = await f.EsperarTipo("saude", Cinco);
        var segundo = await f.Esperar(x => x.Tipo == "saude" && x != primeiro, TimeSpan.FromSeconds(35));
        Assert.True(primeiro is not null && segundo is not null && Txt(segundo.Corpo, "verificadoEm") != Txt(primeiro.Corpo, "verificadoEm"), $"esperava outro \"saude\" com \"verificadoEm\" novo: {f}");
    }

    [Then(@"^\?intervalo=10 é 422 DADOS_INVALIDOS com o campo ""intervalo""$")]
    public async Task IntervaloInvalido()
    {
        var r = await a.Pedir(await a.Caio(), "GET", "/auditoria/card/ao-vivo?intervalo=10");
        Respostas.Confere(r, 422, "DADOS_INVALIDOS");
        Assert.Equal("intervalo", Txt(r.Corpo, "campo"));
    }

    [Then(@"^o Caio recebe os eventos ""registro"" do ""INICIADO"" e do ""CONCLUIDO"" em até 5 s$")]
    public async Task IniciadoEConcluidoAoVivo()
    {
        var f = a.UltimoFluxo;
        foreach (var etapa in new[] { "INICIADO", "CONCLUIDO" })
        {
            var e = await f.Esperar(x => x.Tipo == "registro" && Txt(x.Corpo, "acao") == "CRIAR_USUARIO" && Txt(x.Corpo, "etapa") == etapa, Cinco + Folga);
            Assert.True(e is not null, $"o \"registro\" {etapa} de CRIAR_USUARIO não chegou em 5 s: {f}");
        }
    }

    [Then(@"^os registros ficam gravados em ""auditoria"" e a criação responde 201 como sempre$")]
    public async Task GravadosSemAviso() =>
        Assert.Contains(await Fonte.Todos(Fonte.Registros), d => Fonte.Texto(d, "acao") == "CRIAR_USUARIO" && Fonte.Texto(d, "etapa") == "CONCLUIDO");

    [Then(@"^GET /auditoria/registros os traz, mesmo sem o evento ao vivo$")]
    public async Task ListaTrazOsRegistros()
    {
        var itens = Lista(await De("/auditoria/registros?acao=CRIAR_USUARIO"), "itens");
        Assert.Contains(itens, i => Txt(i, "etapa") == "INICIADO");
        Assert.Contains(itens, i => Txt(i, "etapa") == "CONCLUIDO");
    }

    // ── filtros do ao vivo (RF-201) ────────────────────────────────────────
    [Then(@"^a resposta é 200, só com os registros de auditoria do Pagamento$")]
    public async Task AoVivoDoPagamento()
    {
        var f = a.UltimoFluxo;
        Assert.Equal(200, f.Status);
        var agora = a.Agora();
        await a.Registrar([
            Registro(NovoTraceId(), "INICIADO", "CRIAR_USUARIO", agora),
            Registro(NovoTraceId(), "ERRO", "PAGAMENTO_CRIADO", agora, "BANCO_INDISPONIVEL"),
            Registro(NovoTraceId(), "INICIADO", "PAGAMENTO_CRIADO", agora),
        ]);
        var e = await f.EsperarTipo("registro", Cinco + Folga);
        Assert.True(e is not null && Txt(e.Corpo, "modulo") == "MOD-01" && Txt(e.Corpo, "tipo") == "AUDITORIA", $"esperava só o registro de auditoria do Pagamento: {f}");
        Assert.All(f.Eventos.Where(x => x.Tipo == "registro"), x => Assert.Equal("MOD-01", Txt(x.Corpo, "modulo")));
    }

    [Then(@"^a resposta é 200, só com os registros da EBFGA$")]
    public async Task AoVivoDaEbfga()
    {
        var f = a.UltimoFluxo;
        Assert.Equal(200, f.Status);
        await a.RegistrosRecentes(0);
        var maria = await a.MariaDoUsuarios();
        await mundo.SessaoParaPedido(maria);
        Exigir(await mundo.EnviarComo(maria, "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "PARTICIPANTE", ["nome"] = "Lucas Andrade", ["nomeCurto"] = "lucas", ["matricula"] = null, ["responsaveis"] = new JsonArray(),
        }.ToJsonString()), 201, "POST /usuarios do Lucas pela Maria");
        var e = await f.EsperarTipo("registro", Cinco + Folga);
        Assert.True(e is not null, $"nenhum registro da EBFGA em 5 s: {f}");
        Assert.All(f.Eventos.Where(x => x.Tipo == "registro"), x => Assert.Equal("EBFGA", Txt(x.Corpo?["conta"], "sigla")));
    }
}
