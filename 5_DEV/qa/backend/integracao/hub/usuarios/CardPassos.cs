using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos do card do Usuários: RN-189-api e RN-280 de 1_Requisitos/17_modulo-usuario.md
// e, pela instância do Usuários, RN-276-api e RN-278 de 1_Requisitos/15_modulos.md
// (modulos.feature). Contrato: usuarios-contrato-api v13, GET /usuarios/card.
//
// Os números vêm do que a API cria (contas, staff, participantes, pendentes,
// desativados). O que só nasce com o tempo é preparado pelo banco
// (suporte/Estado.cs), como os outros passos fazem com a versão das permissões:
// o último acesso ("Sem acesso há 30 dias", "Master sem acesso há 30 dias"), a
// hora do pedido do código de liberação (selo, "Códigos vencidos") e os meses
// anteriores do card guardado. Nada é conferido pelo banco: só pela resposta.
//
// A consulta de cada número (RN-280: de qual coleção, com qual filtro) é
// provada pelo efeito: o card lido antes e depois de criar o que o filtro conta
// e o que ele não conta, e a diferença de cada número.
[Binding]
public sealed class CardPassos(Mundo mundo)
{
    const string SemRelogio = "a suíte não controla o relógio do backend: o fechamento da 00:00 de Brasília fica nos testes do backend";
    const string SoNoBanco = "documento do banco: conferido nos testes do backend";

    // o número da linha de Exemplos (RN-280) e os cards de antes
    string numero = "";
    readonly Dictionary<string, JsonNode> antes = [];
    readonly Dictionary<string, JsonNode> depois = [];
    // pendentes, criados no Dado, que o Então faz vencer (refeitos na leitura)
    string pendenteHoje = "", pendenteVencido = "";
    // o intervalo entre o último acesso do Lucas e o GET do exemplo (15 RN-278)
    DateOnly ultimoAcessoDoLucas;
    int criados;

    // ── RN-189-api · card da instituição ───────────────────────────────────
    // 37 staff e 298 participantes ativos; 4 pendentes: 2 com o código que vence
    // hoje (o selo 2) e 2 com o código vencido ("Códigos vencidos" 2); 14 dos
    // participantes sem acesso há mais de 30 dias e 6 participantes desativados
    // (expandida). Os meses de antes (301, 310, 318, 323) só existem com o tempo
    [Given(@"^a conta master da EBFGA, com (\d+) staff e (\d+) participantes ativos e (\d+) pendentes$")]
    public async Task InstituicaoCompleta(int staff, int participantes, int pendentes)
    {
        var master = await mundo.Master();
        var (staffIds, participanteIds) = await Povoar(master, staff, participantes, "m");
        var agora = DateTime.UtcNow;
        await Estado.UltimoAcesso([.. staffIds, .. participanteIds.Skip(14)], agora);
        await Estado.UltimoAcesso(participanteIds.Take(14), agora.AddDays(-31));
        for (var i = 0; i < pendentes; i++)
        {
            var id = await NovoPendente(master, $"pend{Letras(i)}");
            if (i < 2) await Estado.CodigoVenceHoje(id);
            else await Estado.PedidoHa(id, TimeSpan.FromHours(73));
        }
        for (var i = 0; i < 6; i++) await Desativar(master, await NovoParticipante(master, $"desa{Letras(i)}"));
        await Estado.MesesAnterioresDoCard(await mundo.Conta(), "INSTITUICAO", [301, 310, 318, 323]);
        mundo.Sujeito = master;
    }

    // a versao do exemplo (418) é a do histórico da escola, que a API não
    // reproduz: confere-se que vem um número; o resto, igual ao exemplo
    [Then(@"^a resposta é 200 com, além de series:$")]
    public async Task CardSemSeries(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperado = JsonNode.Parse(await mundo.Traduzir(json))!.AsObject();
        esperado.Remove("versao");
        Comparacao.Confere(esperado, mundo.Ultima.Corpo);
        Assert.True(mundo.Ultima.Corpo?["versao"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number,
            $"versao devia ser um número: {mundo.Ultima}");
    }

    [Then(@"^series traz dia, semana e mes com (\d+) números cada$")]
    public void SeriesCom(int quantos) => SeriesDe(mundo.Ultima.Corpo, quantos);

    [Then(@"^series\.mes termina em (.+)$")]
    public void MesTerminaEm(string lista)
    {
        var esperado = lista.Replace(" e ", ", ").Split(", ").Select(int.Parse).ToList();
        var mes = mundo.Ultima.Corpo?["series"]?["mes"]?.AsArray().Select(n => n!.GetValue<int>()).ToList() ?? [];
        Assert.True(mes.Count >= esperado.Count && mes.TakeLast(esperado.Count).SequenceEqual(esperado),
            $"series.mes devia terminar em {string.Join(", ", esperado)}, veio [{string.Join(", ", mes)}]");
    }

    // ── RN-189-api · perfil e dado macro ───────────────────────────────────
    // cada um com os números do exemplo: 112 contas ativas (DEBOAZ), 37 staff e
    // 298 participantes (EBFGA), 3 staff e 38 participantes criados pela Maria
    // (41) e 32 participantes do Pedro. O Caio recebe o Usuários (MOD-03 · U),
    // que o card exige
    [When(@"^(o administrador do deboaz|o membro do time Caio|a conta master da EBFGA|a staff Maria|o educador autônomo Pedro) envia GET /usuarios/card$")]
    public async Task PedeOCard(string quem)
    {
        Pessoa por;
        switch (quem)
        {
            case "a conta master da EBFGA":
                await Povoar(por = await mundo.Master(), 37, 298, "m");
                break;
            case "a staff Maria":
                por = await mundo.Maria();
                await mundo.Conceder(por, new() { ["MOD-03"] = "CU" });
                await Povoar(por, 3, 38, "s");
                break;
            case "o educador autônomo Pedro":
                await Povoar(por = await mundo.Pedro(), 0, 32, "a");
                break;
            case "o membro do time Caio":
                await ContasAtivas(112);
                por = await mundo.Caio();
                await mundo.Conceder(por, new() { ["MOD-03"] = "U" });
                break;
            default:
                await ContasAtivas(112);
                por = mundo.Admin();
                break;
        }
        await mundo.EnviarComo(por, "GET", "/usuarios/card");
    }

    [Then(@"^perfil é ""(\w+)"" e a reduzida traz (\d+) e ""(.+)""$")]
    public void PerfilEReduzida(string perfil, int valor, string rotulo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(new JsonObject
        {
            ["perfil"] = perfil,
            ["reduzida"] = new JsonObject { ["valor"] = valor, ["rotulo"] = rotulo },
        }, mundo.Ultima.Corpo);
    }

    // ── RN-189-api, RN-281 e 15 RN-276 · quem não tem o card ───────────────
    [When(@"^(o participante Lucas Andrade|o Lucas Andrade|um staff sem o Usuários) envia GET /usuarios/card$")]
    public async Task SemOCard(string quem) =>
        await mundo.EnviarComo(quem == "um staff sem o Usuários" ? await mundo.StaffSemUsuarios() : await mundo.Lucas(), "GET", "/usuarios/card");

    // a ficha do Usuários (registro de módulos, 15_modulos) já não tem o
    // participante entre os perfis do card: nada a preparar
    [Given(@"^a ficha do Usuários sem o participante entre os perfis do card$")]
    public static void FichaSemParticipante() { }

    // ── RN-280 · números do perfil DEBOAZ ──────────────────────────────────
    [When(@"^o card do perfil DEBOAZ conta (.+)$")]
    public void DeboazConta(string n) => numero = n;

    // contas novas: A (instituição ativa, master sem acesso há 31 dias), B
    // (autônomo ativo, master com acesso hoje), C (instituição suspensa, master
    // sem acesso), D (instituição desativada) e E (autônomo encerrado). Cada
    // número tem de subir só pelo que o filtro conta
    [Then(@"^lê as contas de tipo INSTITUICAO ou AUTONOMO com (.+)$")]
    public async Task LeAsContas(string filtro)
    {
        var admin = mundo.Admin();
        var cardAntes = await Card(admin);
        var agora = DateTime.UtcNow;
        var a = await ContaQa(1, "INSTITUICAO");
        var b = await ContaQa(2, "AUTONOMO");
        var c = await ContaQa(3, "INSTITUICAO");
        var d = await ContaQa(4, "INSTITUICAO");
        var e = await ContaQa(5, "AUTONOMO");
        await MudarEstado(c, "SUSPENSA");
        await MudarEstado(d, "DESATIVADA");
        await MudarEstado(e, "ENCERRADA");
        await Estado.UltimoAcessoMaster(a, agora.AddDays(-31));
        await Estado.UltimoAcessoMaster(b, agora);
        await Estado.UltimoAcessoMaster(c, agora.AddDays(-31));
        var cardDepois = await Card(admin);
        var esperados = numero switch
        {
            "o selo e Master sem acesso há 30 dias" => new Dictionary<string, int> { ["o selo"] = 1, ["Master sem acesso há 30 dias"] = 1 },
            "o dado macro" => new() { ["o dado macro"] = 2 },
            "instituições" or "autônomos" or "suspensas" or "Desativadas" or "Encerradas" => new() { [numero] = 1 },
            _ => throw new InvalidOperationException($"número do perfil DEBOAZ sem exemplo: {numero}"),
        };
        foreach (var (nome, diferenca) in esperados)
        {
            AssertDiferenca(cardAntes, cardDepois, nome, diferenca, $"DEBOAZ, contas com {filtro}");
        }
    }

    // ── RN-280 · números das contas ────────────────────────────────────────
    // os Dado descrevem a consulta; o Então a prova pelo que cada card mostra
    [Given(@"^o perfil INSTITUICAO ou AUTONOMO contando os usuarios do contaId da conta$")]
    [Given(@"^o perfil STAFF contando os usuarios com criadoPor igual à staff$")]
    [Given(@"^no STAFF, o selo só com as geracoesSenha de pedidaPor igual à staff$")]
    public static void ConsultaDoCard() { }

    [When(@"^o card conta (.+)$")]
    public void CardConta(string n) => numero = n;

    // o que cada card tem de ganhar, pelo que o Então cria: na EBFGA, a conta
    // master e a Maria criam, cada uma, 1 staff ativo, 1 participante ativo, 1
    // participante sem acesso há 31 dias, 1 participante desativado, 1 pendente
    // com o código que vence hoje e 1 com o código vencido; o Pedro cria 1
    // participante ativo, 1 sem acesso e 1 desativado
    static readonly Dictionary<string, Dictionary<string, int>> Diferencas = new()
    {
        ["o selo"] = new() { ["INSTITUICAO"] = 2, ["STAFF"] = 1 },
        ["o dado macro"] = new() { ["INSTITUICAO"] = 6, ["STAFF"] = 3, ["AUTONOMO"] = 2 },
        ["staff"] = new() { ["INSTITUICAO"] = 2, ["STAFF"] = 1 },
        ["participantes"] = new() { ["INSTITUICAO"] = 4, ["STAFF"] = 2 },
        ["pendentes"] = new() { ["INSTITUICAO"] = 4, ["STAFF"] = 2 },
        ["criados no mês"] = new() { ["AUTONOMO"] = 3 },
        ["Sem acesso há 30 dias"] = new() { ["INSTITUICAO"] = 2, ["STAFF"] = 1, ["AUTONOMO"] = 1 },
        ["Desativados"] = new() { ["INSTITUICAO"] = 2, ["STAFF"] = 1, ["AUTONOMO"] = 1 },
        ["Códigos vencidos"] = new() { ["INSTITUICAO"] = 2, ["STAFF"] = 1 },
    };

    [Then(@"^lê (?!as contas de tipo )(.+)$")]
    public async Task LeOsUsuarios(string consulta)
    {
        if (!Diferencas.TryGetValue(numero, out var esperados)) throw new InvalidOperationException($"número sem exemplo: {numero}");
        var master = await mundo.Master();
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "CU" });
        var pedro = await mundo.Pedro();
        var quem = new Dictionary<string, Pessoa> { ["INSTITUICAO"] = master, ["STAFF"] = maria, ["AUTONOMO"] = pedro };
        foreach (var (perfil, pessoa) in quem) antes[perfil] = await Card(pessoa);

        var comAcesso = new List<string>();
        var semAcesso = new List<string>();
        foreach (var (por, p) in new[] { (master, "m"), (maria, "s") })
        {
            comAcesso.Add((await mundo.NovoStaff($"st{p}ativo", $"Staff Ativo {p}", por)).Id);
            comAcesso.Add(await NovoParticipante(por, $"pt{p}ativo"));
            semAcesso.Add(await NovoParticipante(por, $"pt{p}velho"));
            await Desativar(por, await NovoParticipante(por, $"pt{p}desat"));
            await Estado.CodigoVenceHoje(await NovoPendente(por, $"pd{p}hoje"));
            await Estado.PedidoHa(await NovoPendente(por, $"pd{p}venc"), TimeSpan.FromHours(73));
        }
        comAcesso.Add(await NovoParticipante(pedro, "ptaativo"));
        semAcesso.Add(await NovoParticipante(pedro, "ptavelho"));
        await Desativar(pedro, await NovoParticipante(pedro, "ptadesat"));
        await Estado.UltimoAcesso(comAcesso, DateTime.UtcNow);
        await Estado.UltimoAcesso(semAcesso, DateTime.UtcNow.AddDays(-31));

        foreach (var (perfil, diferenca) in esperados)
        {
            AssertDiferenca(antes[perfil], await Card(quem[perfil]), numero, diferenca, $"{perfil}, {consulta}");
        }
    }

    // ── RN-280 · criar participante atualiza os cards guardados ────────────
    // a Maria (com Administrar usuários) cria 38 participantes e a conta master
    // os outros 260; dois pendentes da conta master, com o código ainda longe
    // de vencer, para o Então fazê-los vencer
    [Given(@"^o card da EBFGA com (\d+) participantes e o card da staff Maria com (\d+)$")]
    public async Task CardsGuardados(int daEscola, int daMaria)
    {
        var master = await mundo.Master();
        var maria = await mundo.Maria();
        await mundo.Conceder(maria, new() { ["MOD-03"] = "CU" });
        var (_, dela) = await Povoar(maria, 0, daMaria, "s");
        var (_, outros) = await Povoar(master, 0, daEscola - daMaria, "m");
        await Estado.UltimoAcesso([.. dela, .. outros], DateTime.UtcNow);
        pendenteHoje = await NovoPendente(master, "pendhoje");
        pendenteVencido = await NovoPendente(master, "pendvenc");
        antes["INSTITUICAO"] = await Card(master);
        antes["STAFF"] = await Card(maria);
        Preparo(antes["INSTITUICAO"], "participantes", daEscola, "card da EBFGA");
        Preparo(antes["STAFF"], "participantes", daMaria, "card da Maria");
    }

    [When(@"^a Maria envia POST /usuarios e cria o participante Lucas Andrade com 201$")]
    public Task MariaCriaOLucas() => CriaOLucas(mundo.Maria());

    // criadoPor não sai na API (contrato v13): a gravação aparece no card da
    // Maria, que conta só os usuários com criadoPor igual a ela, já no GET seguinte
    [Then(@"^na mesma execução, o Lucas é gravado com criadoPor ""usr-ebfga-maria""$")]
    public async Task LucasDaMaria() =>
        AssertDiferenca(antes["STAFF"], await Card(await mundo.Maria()), "participantes", 1, "card da Maria, criadoPor dela");

    [Then(@"^""cardUsuarios"" passa a (\d+) no card da EBFGA e a (\d+) no card da Maria, e a versao sobe$")]
    public async Task CardsAtualizados(int daEscola, int daMaria)
    {
        depois["INSTITUICAO"] = await Card(await mundo.Master());
        depois["STAFF"] = await Card(await mundo.Maria());
        foreach (var (perfil, total) in new[] { ("INSTITUICAO", daEscola), ("STAFF", daMaria) })
        {
            Assert.True(Numero(depois[perfil], "participantes") == total,
                $"{perfil}: esperava {total} participantes, veio {depois[perfil].ToJsonString()}");
            Assert.True(Versao(depois[perfil]) > Versao(antes[perfil]),
                $"{perfil}: a versao devia subir de {Versao(antes[perfil])}, veio {depois[perfil].ToJsonString()}");
        }
    }

    // o tempo passa só no banco (último acesso do Lucas e hora do pedido dos
    // códigos): nenhuma execução da aplicação muda os números guardados
    [Then(@"^o selo, ""Sem acesso há 30 dias"" e ""Códigos vencidos"" não sobem a versao: são refeitos na leitura$")]
    public async Task RefeitosNaLeitura()
    {
        var master = await mundo.Master();
        var guardado = depois["INSTITUICAO"];
        await Estado.UltimoAcesso([(await mundo.Lucas()).Id], DateTime.UtcNow.AddDays(-31));
        await Estado.CodigoVenceHoje(pendenteHoje);
        await Estado.PedidoHa(pendenteVencido, TimeSpan.FromHours(73));
        var lido = await Card(master);
        Assert.True(Versao(lido) == Versao(guardado), $"a versao não devia subir de {Versao(guardado)}: {lido.ToJsonString()}");
        AssertDiferenca(guardado, lido, "o selo", 1, "código que vence hoje");
        AssertDiferenca(guardado, lido, "Sem acesso há 30 dias", 1, "Lucas sem acesso há 31 dias");
        AssertDiferenca(guardado, lido, "Códigos vencidos", 1, "código pedido há 73 h");
    }

    // ── 15 RN-276-api · campos do card ─────────────────────────────────────
    // o Usuários (MOD-03) é da conta: não há o que contratar
    [Given(@"^a conta master da EBFGA com o token de acesso e o ""MOD-03"" contratado$")]
    public async Task MasterComOUsuarios()
    {
        mundo.Sujeito = await mundo.Master();
        await mundo.TokenDe(mundo.Sujeito);
    }

    // a EBFGA do Dado ainda não tem staff nem participantes: vazio true e selo 0.
    // versao, atualizadoEm e vazio são conferidos também depois de uma mudança
    [Then(@"^a resposta é 200 e traz (\w+) com (.+)$")]
    public async Task TrazOCampo(string campo, string conteudo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var card = mundo.Ultima.Corpo!;
        var onde = $"{campo} ({conteudo})";
        switch (campo)
        {
            case "modulo":
                Assert.Equal("MOD-03", card["modulo"]?.GetValue<string>());
                break;
            case "perfil":
                Assert.Equal("INSTITUICAO", card["perfil"]?.GetValue<string>());
                break;
            case "versao":
                {
                    var v = Versao(card);
                    var mudou = await CardDepoisDeUmaMudanca();
                    Assert.True(Versao(mudou) > v, $"{onde}: a versao devia subir de {v}, veio {mudou.ToJsonString()}");
                    break;
                }
            case "atualizadoEm":
                {
                    Utc(card, onde);
                    var mudanca = DateTimeOffset.UtcNow;
                    var mudou = await CardDepoisDeUmaMudanca();
                    var quando = Utc(mudou, onde);
                    Assert.True(quando >= mudanca.AddMinutes(-1) && quando <= DateTimeOffset.UtcNow.AddMinutes(1),
                        $"{onde}: esperava a hora da criação do participante ({mudanca:O}), veio {quando:O}");
                    break;
                }
            case "vazio":
                Assert.True(card["vazio"]?.GetValueKind() == System.Text.Json.JsonValueKind.True, $"{onde}: sem usuários, esperava true: {card.ToJsonString()}");
                Assert.True((await CardDepoisDeUmaMudanca())["vazio"]?.GetValueKind() == System.Text.Json.JsonValueKind.False,
                    $"{onde}: com um participante, esperava false");
                break;
            case "selo":
                Assert.True(card["selo"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number && card["selo"]!.GetValue<int>() == 0,
                    $"{onde}: sem nada a avisar, esperava 0: {card.ToJsonString()}");
                break;
            case "reduzida":
                ValorERotulo(card["reduzida"], onde);
                break;
            case "normal":
                ValorERotulo(card["normal"], onde);
                Linhas(card["normal"]?["apoio"], onde);
                break;
            case "expandida":
                Linhas(card["expandida"]?["lista"], onde);
                break;
            case "series":
                SeriesDe(card, 12);
                break;
            default:
                throw new InvalidOperationException($"campo do card sem exemplo: {campo}");
        }
    }

    [Then(@"^a resposta traz a reduzida, a normal e a expandida, sem pedir o tamanho$")]
    public void TodosOsTamanhos()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal("/usuarios/card", mundo.UltimoPedido.Caminho);
        foreach (var tamanho in new[] { "reduzida", "normal", "expandida" })
        {
            Assert.True(mundo.Ultima.Corpo?[tamanho] is JsonObject, $"sem {tamanho}: {mundo.Ultima}");
        }
    }

    [Then(@"^não traz o ícone, que vem do Catálogo$")]
    public void SemIcone()
    {
        var chaves = Chaves(mundo.Ultima.Corpo).Where(c => c.Contains("icon", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(chaves.Count == 0, $"o card não devia trazer ícone, trouxe {string.Join(", ", chaves)}: {mundo.Ultima}");
    }

    // ── 15 RN-278 · atualizado na mesma execução ───────────────────────────
    // a versao do exemplo (418) é o histórico da escola: confere-se que sobe 1.
    // O João (com Administrar usuários) é criado antes de ler o card
    [Given(@"^o card do Usuários da EBFGA na versao \d+, com (\d+) participantes ativos$")]
    public async Task CardNaVersao(int participantes)
    {
        var master = await mundo.Master();
        var (_, ids) = await Povoar(master, 0, participantes, "m");
        await Estado.UltimoAcesso(ids, DateTime.UtcNow);
        await mundo.Conceder(await mundo.Joao(), new() { ["MOD-03"] = "CU" });
        antes["INSTITUICAO"] = await Card(master);
        Preparo(antes["INSTITUICAO"], "participantes", participantes, "card da EBFGA");
    }

    [When(@"^o staff João envia POST /usuarios e cria o participante Lucas Andrade com 201$")]
    public Task JoaoCriaOLucas() => CriaOLucas(mundo.Joao());

    [Then(@"^na mesma execução, o card guardado passa à versao \d+ com (\d+) participantes$")]
    public async Task VersaoSeguinte(int participantes)
    {
        depois["INSTITUICAO"] = await Card(await mundo.Master());
        var (v0, v1) = (Versao(antes["INSTITUICAO"]), Versao(depois["INSTITUICAO"]));
        Assert.True(v1 == v0 + 1, $"a versao devia passar de {v0} a {v0 + 1}, veio {v1}");
        Assert.True(Numero(depois["INSTITUICAO"], "participantes") == participantes,
            $"esperava {participantes} participantes: {depois["INSTITUICAO"].ToJsonString()}");
    }

    [Then(@"^o próximo GET /usuarios/card traz (\d+), lido do card guardado$")]
    public async Task ProximoGet(int participantes)
    {
        var card = await Card(await mundo.Master());
        Assert.True(Numero(card, "participantes") == participantes, $"esperava {participantes} participantes: {card.ToJsonString()}");
        Assert.True(Versao(card) == Versao(depois["INSTITUICAO"]), $"a leitura não devia mudar a versao: {card.ToJsonString()}");
    }

    // ── 15 RN-278 · fechamento do dia, da semana e do mês ──────────────────
    [Given(@"^o card do Usuários da EBFGA com 400 dias, 106 semanas e 24 meses guardados$")]
    [When(@"^chega 00:00 de \d{4}-\d{2}-\d{2}, .+, no horário de Brasília$")]
    [Then(@"^o card guardado abre o dia .+$")]
    [Then(@"^sai o dia, a semana e o mês mais antigos, para manter 400, 106 e 24$")]
    public static void Fechamento() => throw new PendingStepException(SemRelogio);

    // ── 15 RN-278 · números que mudam só com o tempo ───────────────────────
    // o Lucas é criado com acesso hoje e o card é lido; o Quando faz o tempo
    // passar só no banco: o último acesso dele vai para o passado, à mesma
    // distância do exemplo (2026-10-01 → 2026-11-08, 38 dias). Dois pendentes
    // com o código longe de vencer, para o selo e "Códigos vencidos"
    [Given(@"^o Lucas Andrade com o último acesso em (\d{4}-\d{2}-\d{2}), sem mudança desde então$")]
    public async Task LucasSemMudanca(string data)
    {
        ultimoAcessoDoLucas = DateOnly.Parse(data);
        var master = await mundo.Master();
        await Estado.UltimoAcesso([(await mundo.Lucas()).Id], DateTime.UtcNow);
        pendenteHoje = await NovoPendente(master, "pendhoje");
        pendenteVencido = await NovoPendente(master, "pendvenc");
        antes["INSTITUICAO"] = await Card(master);
    }

    [When(@"^a conta master envia GET /usuarios/card em (\d{4}-\d{2}-\d{2})$")]
    public async Task MasterLeDepois(string data)
    {
        var dias = DateOnly.Parse(data).DayNumber - ultimoAcessoDoLucas.DayNumber;
        await Estado.UltimoAcesso([(await mundo.Lucas()).Id], DateTime.UtcNow.AddDays(-dias));
        await mundo.EnviarComo(await mundo.Master(), "GET", "/usuarios/card");
        Respostas.Confere(mundo.Ultima, 200);
        depois["INSTITUICAO"] = mundo.Ultima.Corpo!;
    }

    [Then(@"^o Lucas conta em ""Sem acesso há 30 dias""$")]
    public void LucasSemAcesso() =>
        AssertDiferenca(antes["INSTITUICAO"], depois["INSTITUICAO"], "Sem acesso há 30 dias", 1, "Lucas sem acesso");

    // a parte da leitura: os códigos passam a vencer hoje e a estar vencidos só
    // no banco, e o card lido os mostra. O fechamento do dia fica com o relógio
    // (cenário anterior), fora do alcance da suíte
    [Then(@"^essa contagem, o selo e ""Códigos vencidos"" são refeitos na leitura e no fechamento do dia$")]
    public async Task RefeitosNaLeituraEnoFechamento()
    {
        await Estado.CodigoVenceHoje(pendenteHoje);
        await Estado.PedidoHa(pendenteVencido, TimeSpan.FromHours(73));
        var lido = await Card(await mundo.Master());
        AssertDiferenca(depois["INSTITUICAO"], lido, "o selo", 1, "código que vence hoje");
        AssertDiferenca(depois["INSTITUICAO"], lido, "Códigos vencidos", 1, "código pedido há 73 h");
        AssertDiferenca(antes["INSTITUICAO"], lido, "Sem acesso há 30 dias", 1, "Lucas sem acesso");
        depois["INSTITUICAO"] = lido;
    }

    [Then(@"^a versao não sobe por eles$")]
    public void VersaoIgual() =>
        Assert.True(Versao(depois["INSTITUICAO"]) == Versao(antes["INSTITUICAO"]),
            $"a versao não devia subir de {Versao(antes["INSTITUICAO"])}: {depois["INSTITUICAO"].ToJsonString()}");

    // ── exemplo-entidades · card guardado ──────────────────────────────────
    [Then(@"^o card da EBFGA em ""cardUsuarios"", com dias, semanas e meses só nos 2 últimos, é:$")]
    public static void CardGuardado(string _) => throw new PendingStepException(SoNoBanco);

    [Then(@"^dias guarda até 400, semanas até 106 e meses até 24, como na RN-278 de 15_modulos$")]
    public static void LimitesGuardados() => throw new PendingStepException(SemRelogio);

    // ── apoio ───────────────────────────────────────────────────────────────
    async Task<JsonNode> Card(Pessoa pessoa) =>
        Exigir(await mundo.EnviarComo(pessoa, "GET", "/usuarios/card"), 200, $"GET /usuarios/card de {pessoa.Nome}").Corpo!;

    // um participante novo da EBFGA, criado pela conta master, e o card depois dele
    async Task<JsonNode> CardDepoisDeUmaMudanca()
    {
        var master = await mundo.Master();
        await NovoParticipante(master, $"mud{Letras(criados++)}");
        return await Card(master);
    }

    async Task CriaOLucas(Task<Pessoa> quem)
    {
        await mundo.EnviarComo(await quem, "POST", "/usuarios", """
            {
              "tipo": "PARTICIPANTE",
              "nome": "Lucas Andrade",
              "nomeCurto": "lucas",
              "matricula": null,
              "responsaveis": [
                {
                  "nome": "Carla Andrade",
                  "parentesco": "MAE"
                }
              ]
            }
            """);
        Respostas.Confere(mundo.Ultima, 201);
    }

    // staff ativos (senha confirmada) e participantes criados por quem povoa
    // o prefixo separa os nomes curtos de quem povoa a mesma conta (a conta
    // master e a Maria na EBFGA)
    async Task<(List<string> Staff, List<string> Participantes)> Povoar(Pessoa por, int staff, int participantes, string prefixo)
    {
        var s = new List<string>();
        var p = new List<string>();
        for (var i = 0; i < staff; i++) s.Add((await mundo.NovoStaff($"{prefixo}st{Letras(i)}", $"Staff {prefixo}{Letras(i)}", por)).Id);
        for (var i = 0; i < participantes; i++) p.Add(await NovoParticipante(por, $"{prefixo}pt{Letras(i)}"));
        return (s, p);
    }

    async Task<string> NovoParticipante(Pessoa por, string nomeCurto) =>
        Texto(Exigir(await mundo.EnviarComo(por, "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "PARTICIPANTE",
            ["nome"] = $"Participante {nomeCurto}",
            ["nomeCurto"] = nomeCurto,
            ["matricula"] = null,
            ["responsaveis"] = new JsonArray(),
        }.ToJsonString()), 201, $"POST /usuarios do participante {nomeCurto}"), "id");

    // staff criado, sem gerar a senha: PENDENTE, com o código de liberação pedido agora
    async Task<string> NovoPendente(Pessoa por, string nomeCurto) =>
        Texto(Exigir(await mundo.EnviarComo(por, "POST", "/usuarios", new JsonObject
        {
            ["tipo"] = "STAFF",
            ["nome"] = $"Pendente {nomeCurto}",
            ["nomeCurto"] = nomeCurto,
            ["matricula"] = null,
        }.ToJsonString()), 201, $"POST /usuarios do pendente {nomeCurto}"), "id");

    async Task Desativar(Pessoa por, string id) =>
        Exigir(await mundo.EnviarComo(por, "POST", $"/usuarios/{id}/desativar"), 204, "POST /usuarios/{id}/desativar");

    // completa as contas ativas até o total do exemplo, contando as que a
    // inicialização criou (QAINS e QAAUT em homologação, RN-265)
    async Task ContasAtivas(int total)
    {
        await mundo.Conta();
        var ativas = Exigir(await mundo.EnviarComo(mundo.Admin(), "GET", "/contas?estado=ATIVA"), 200, "GET /contas?estado=ATIVA")
            .Corpo?["total"]?.GetValue<int>() ?? 1;
        for (var i = 1; i <= total - ativas; i++)
        {
            var sigla = $"CT{Letras(i)}".ToUpperInvariant();
            await mundo.NovaConta(sigla, ContaNova("INSTITUICAO", $"Escola {sigla}", Cnpj(30000000 + i), $"conta_{i}", sigla));
        }
    }

    // conta nova, confirmada (ATIVA), de instituição (CNPJ) ou de autônomo (CPF)
    async Task<string> ContaQa(int i, string tipo)
    {
        var sigla = $"QD{Letras(i)}".ToUpperInvariant();
        var documento = tipo == "INSTITUICAO" ? Cnpj(50000000 + i) : $"987.654.{i:D3}-{i % 100:D2}";
        return await mundo.NovaConta(sigla, ContaNova(tipo, $"Conta {sigla}", documento, $"qa_card_{i}", sigla));
    }

    async Task MudarEstado(string contaId, string estado) =>
        Exigir(await mundo.EnviarComo(mundo.Admin(), "POST", $"/contas/{contaId}/estado",
            new JsonObject { ["estado"] = estado }.ToJsonString()), 200, $"POST /contas/{{id}}/estado {estado}");

    // o número do card pelo nome: o selo, o dado macro (reduzida) ou uma linha
    // de normal.apoio ou de expandida.lista
    public static int Numero(JsonNode card, string nome)
    {
        switch (nome)
        {
            case "o selo":
                return card["selo"]!.GetValue<int>();
            case "o dado macro":
                return card["reduzida"]!["valor"]!.GetValue<int>();
        }
        var linhas = new List<JsonNode?>();
        if (card["normal"]?["apoio"] is JsonArray apoio) linhas.AddRange(apoio);
        if (card["expandida"]?["lista"] is JsonArray lista) linhas.AddRange(lista);
        var linha = linhas.FirstOrDefault(l => l?["nome"]?.GetValue<string>() == nome);
        return linha?["valor"]?.GetValue<int>()
            ?? throw new Xunit.Sdk.XunitException($"o card não traz \"{nome}\" em normal.apoio nem em expandida.lista: {card.ToJsonString()}");
    }

    public static int Versao(JsonNode card) => card["versao"]!.GetValue<int>();

    static void AssertDiferenca(JsonNode antes, JsonNode depois, string nome, int diferenca, string porque)
    {
        var (a, d) = (Numero(antes, nome), Numero(depois, nome));
        Assert.True(d - a == diferenca, $"\"{nome}\" ({porque}) devia ir de {a} a {a + diferenca}, foi a {d}: {depois.ToJsonString()}");
    }

    static void Preparo(JsonNode card, string nome, int esperado, string qual)
    {
        if (Numero(card, nome) != esperado) throw new InvalidOperationException($"preparo: {qual} com {Numero(card, nome)} {nome}, esperava {esperado}");
    }

    static void SeriesDe(JsonNode? card, int quantos)
    {
        foreach (var recorte in new[] { "dia", "semana", "mes" })
        {
            var serie = card?["series"]?[recorte] as JsonArray;
            Assert.True(serie is not null && serie.Count == quantos
                && serie.All(n => n?.GetValueKind() == System.Text.Json.JsonValueKind.Number),
                $"series.{recorte} devia ter {quantos} números: {card?.ToJsonString()}");
        }
    }

    static DateTimeOffset Utc(JsonNode card, string onde)
    {
        var texto = card["atualizadoEm"]?.GetValue<string>() ?? "";
        Assert.True(texto.EndsWith('Z') && DateTimeOffset.TryParse(texto, out _), $"{onde}: esperava data e hora em UTC, veio \"{texto}\"");
        return DateTimeOffset.Parse(texto);
    }

    static void ValorERotulo(JsonNode? no, string onde) =>
        Assert.True(no?["valor"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number
            && no["rotulo"]?.GetValue<string>() is { Length: > 0 }, $"{onde}: esperava valor e rotulo, veio {no?.ToJsonString()}");

    // até 3 linhas, cada uma com nome e valor
    static void Linhas(JsonNode? no, string onde) =>
        Assert.True(no is JsonArray linhas && linhas.Count <= 3 && linhas.All(l =>
            l?["nome"]?.GetValue<string>() is { Length: > 0 } && l["valor"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number),
            $"{onde}: esperava até 3 linhas com nome e valor, veio {no?.ToJsonString()}");

    static IEnumerable<string> Chaves(JsonNode? no) => no switch
    {
        JsonObject o => o.SelectMany(c => Chaves(c.Value).Prepend(c.Key)),
        JsonArray a => a.SelectMany(Chaves),
        _ => [],
    };
}
