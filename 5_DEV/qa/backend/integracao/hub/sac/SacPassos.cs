using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Sac;

// Passos dos Cenários de API de 1_Requisitos/18_modulo-sac.md (v06), contrato
// sac-contrato-api v03 e modelo sac-documentos v03. Os cenários que entraram
// na v06 (RN-292 baixar, RN-293 sem login, RN-295 e RN-297 do contrato,
// RF-181 sem prazos salvos, RNF-230 ping e RNF-232 a RNF-234) ficam em
// SacContratoPassos.cs, AnexosGuardadosPassos.cs e PartidaDoSacPassos.cs. Os chamados, anexos e
// mensagens dos exemplos vêm de ChamadosDoExemplo.cs.
//
// Reaproveitados: "o token da conta master da EBFGA, com …" e "o token do
// administrador do deboaz, com …" (Sujeito, hub/usuarios), "envia GET …" e
// "envia PUT … com:" (com o token do Sujeito), "a resposta é …" (suporte),
// "a resposta é 422 DADOS_INVALIDOS com o campo "…"" (hub/usuarios), a
// auditoria (suporte/PassosDeAuditoria.cs) e "a resposta é 200 e a conexão
// fica aberta" (hub/usuarios/AoVivoPassos.cs).
//
// "<quem> envia <MÉTODO> /sac/…<resto>" é um passo só: o chamado do caminho é
// preparado no estado de que o pedido precisa (se um Dado ainda não o criou) e
// o resto da frase diz o corpo.
[Binding]
public sealed partial class SacPassos(Mundo mundo, ChamadosDoExemplo sac, Execucoes execucoes, AoVivo aoVivo, AnexosDoCenario anexos)
{
    // quem pede ao SAC nas frases "<quem> envia <MÉTODO> /sac/…" (as duas expressões abaixo)
    static readonly TimeSpan CincoSegundos = TimeSpan.FromSeconds(5);
    static readonly TimeSpan Folga = TimeSpan.FromSeconds(2);

    // o status pedido em duas frases (RF-179: "com o status" e "E a mensagem")
    (string Caminho, string Status)? statusPendente;
    // o texto que o Quando pôs na conversa, para o Então procurar
    string textoEnviado = "";
    string notaDoCaio = "";
    string mensagemDoCaio = "";
    readonly List<string> depoisDaQueda = [];
    Fluxo? conversa;

    // ── Dado ───────────────────────────────────────────────────────────────
    // os chamados da EBFGA no exemplo: SAC-000039 em atendimento e SAC-000042
    // aguardando a conta, e 3 resolvidos no mês (RF-172, RN-295, RNF-230).
    // A versao 57 do card do exemplo é o histórico da escola: sai da comparação
    [Given(@"^o token da conta master da EBFGA$")]
    public async Task TokenDaMaster()
    {
        await sac.MundoDaEbfga();
        mundo.Sujeito = await mundo.Master();
        await mundo.TokenDe(mundo.Sujeito);
        mundo.CamposSemConferir.Add("versao");
    }

    [Given(@"^o token do Caio, com o papel ""MEMBRO_TIME"" e ""MOD-02"" com ""U""$")]
    public async Task TokenDoCaio()
    {
        await sac.MundoDaFila();
        mundo.Sujeito = await sac.Caio();
        await mundo.TokenDe(mundo.Sujeito);
    }

    [Given(@"^o anexo ""(anx-\w+)"" enviado antes por POST /sac/anexos$")]
    public async Task AnexoAntes(string exemplo) => await sac.Anexo(mundo.Sujeito ?? await mundo.Master(), exemplo);

    [Given(@"^o SAC-000042 ""ABERTO"", sem responsável$")]
    public async Task AbertoSemResponsavel() => await sac.Chamado("SAC-000042", "ABERTO");

    [Given(@"^o chamado SAC-000042 ""(ABERTO|FECHADO|RESOLVIDO)""$")]
    [Given(@"^o SAC-000042 ""(RESOLVIDO)""$")]
    public async Task ChamadoNoEstado(string estado) => await sac.Chamado("SAC-000042", estado);

    [Given(@"^o SAC-000042 com a Ana Lima como responsável$")]
    public async Task ComAAna()
    {
        var id = await sac.Abrir(await mundo.Master(), "SAC-000042");
        await sac.Levar(id, "EM_ATENDIMENTO", await sac.AnaLima());
    }

    // resolvido há 2 dias pelo relógio de teste; os 10 em andamento depois dele
    [Given(@"^o SAC-000042 ""RESOLVIDO"" há 2 dias, com a EBFGA no limite de 10 em andamento$")]
    public async Task ResolvidoHaDoisDias()
    {
        var hoje = DateTimeOffset.Parse("2026-11-12T13:00:00Z");
        await sac.Saltar(hoje.AddDays(-2));
        await sac.Chamado("SAC-000042", "RESOLVIDO");
        await sac.Saltar(hoje);
        await DezEmAndamento();
    }

    [Given(@"^a EBFGA com 10 chamados em ABERTO, EM_ATENDIMENTO ou AGUARDANDO_CLIENTE$")]
    public async Task DezEmAndamento()
    {
        var master = await mundo.Master();
        var estados = new[] { "ABERTO", "EM_ATENDIMENTO", "AGUARDANDO_CLIENTE" };
        for (var i = 0; i < 10; i++)
        {
            var id = await sac.Abrir(master, assunto: $"Em andamento número {i + 1}");
            await sac.Levar(id, estados[i % 3]);
        }
    }

    // RN-290: Aguardando cliente desde a data do exemplo, pelo relógio de teste
    [Given(@"^o SAC-000039 ""AGUARDANDO_CLIENTE"" desde (\S+)$")]
    public async Task AguardandoDesde(string desde)
    {
        await sac.Saltar(DateTimeOffset.Parse(desde).AddSeconds(-30));
        await sac.Chamado("SAC-000039", "AGUARDANDO_CLIENTE");
    }

    // RN-296: fechado pelo prazo (7 dias depois de resolvido, sem avaliação),
    // e o relógio n dias depois do fechamento; ou encerrado pela conta
    [Given(@"^o chamado fechado há (\d+) dias, sem avaliação$")]
    public async Task FechadoHa(int dias)
    {
        var resolvido = DateTimeOffset.Parse("2026-11-02T13:00:00Z");
        await sac.Saltar(resolvido);
        var id = await sac.Abrir(await mundo.Master());
        await sac.Levar(id, "RESOLVIDO");
        var fechado = resolvido.AddDays(7).AddMinutes(1);
        await sac.Saltar(fechado);
        await sac.Agendamento();
        var lido = await sac.Ler(id, await mundo.Master());
        Assert.True(lido["status"]?.GetValue<string>() == "FECHADO", $"preparo: 7 dias depois de resolvido, o chamado devia fechar: {lido.ToJsonString()}");
        await sac.Saltar(fechado.AddDays(dias));
        sac.DoDado = id;
    }

    [Given(@"^o chamado encerrado pela conta, sem resposta$")]
    public async Task EncerradoPelaConta()
    {
        var id = await sac.Abrir(await mundo.Master());
        await sac.Levar(id, "ENCERRADO");
        sac.DoDado = id;
    }

    // RF-179: o pedido sem login vira o chamado; o número sai do alvo do
    // CONCLUIDO de ESQUECI_SENHA_MASTER (17 RN-274: "ajuda-<n>" = "SAC-<n>")
    [Given(@"^o pedido ""ajuda-000087"" de POST /contas/esqueci-senha-master, com 202$")]
    public async Task PedidoSemLogin() => await PedidoDeSenha();

    async Task<string> PedidoDeSenha()
    {
        if (sac.Conhecido("SAC-000087")) return sac.Real("SAC-000087");
        await mundo.Conta();
        Exigir(await execucoes.Executar(() => mundo.Enviar("POST", "/contas/esqueci-senha-master", new JsonObject
        {
            ["instituicao"] = "Escola Bento Freitas",
            ["contato"] = "(51) 99999-0000",
        }.ToJsonString())), 202, "POST /contas/esqueci-senha-master");
        var alvo = Auditoria.Texto(await execucoes.Etapa("CONCLUIDO", "ESQUECI_SENHA_MASTER"), "alvo") ?? "";
        Assert.True(Pedido().IsMatch(alvo), $"preparo: o alvo do pedido devia ser \"ajuda-<número>\", veio \"{alvo}\"");
        mundo.Apelidos["ajuda-000087"] = () => Task.FromResult(alvo);
        var id = "SAC-" + alvo["ajuda-".Length..];
        sac.Anotar("SAC-000087", id);
        return id;
    }

    // RNF-230: a conta master conectada à conversa do SAC-000042 recebe a
    // mensagem do Caio ("msg-000318") e a conexão cai; o Caio manda mais duas
    [Given(@"^a conexão caída depois do evento ""msg-000318""$")]
    public async Task ConexaoCaida()
    {
        await sac.MundoDaEbfga();
        var id = sac.Real("SAC-000042");
        var master = await mundo.Master();
        var caio = await sac.Caio();
        // fora do AoVivo do cenário: esta conexão cai aqui, antes do Quando
        using (var fluxo = await Fluxo.Abrir("conta master", $"/sac/chamados/{id}/ao-vivo", await mundo.TokenDe(master)))
        {
            Assert.True(fluxo.Status == 200, $"preparo: GET /sac/chamados/{id}/ao-vivo: {fluxo}");
            var primeira = await sac.Mensagem(id, caio, "Primeira, antes da queda");
            mundo.Apelidos["msg-000318"] = () => Task.FromResult(primeira);
            Assert.True(await fluxo.Esperar(e => e.Tipo == "mensagem" && e.Id == primeira, CincoSegundos + Folga) is not null,
                $"preparo: o evento da {primeira} devia chegar: {fluxo}");
        }
        depoisDaQueda.Add(await sac.Mensagem(id, caio, "Segunda, durante a queda"));
        depoisDaQueda.Add(await sac.Mensagem(id, caio, "Terceira, durante a queda"));
    }

    // ── Quando: o pedido ao SAC ────────────────────────────────────────────
    [When(@"^(a conta master da EBFGA|a conta master|a conta dona|o Caio, com ""MOD-02"" com ""U"",?|o Caio, com ""MOD-02"" só com ""U"",|o Caio, só com ""U"" no MOD-02|o Caio|o Pedro Reis|o Rui, membro do time sem ""MOD-02""|a staff Maria|o administrador do deboaz|o administrador) envia (GET|POST|PUT|PATCH|DELETE) (/sac/[^\s,]+)((?:.*[^:])?)$")]
    public async Task Envia(string quem, string metodo, string caminho, string resto) =>
        await Pedir(await QuemPede(quem), quem, metodo, caminho, resto.Trim(), null);

    [When(@"^(a conta master da EBFGA|a conta master|a conta dona|o Caio, com ""MOD-02"" com ""U"",?|o Caio, com ""MOD-02"" só com ""U"",|o Caio, só com ""U"" no MOD-02|o Caio|o Pedro Reis|o Rui, membro do time sem ""MOD-02""|a staff Maria|o administrador do deboaz|o administrador) envia (POST|PUT) (/sac/\S+) com:$")]
    public async Task EnviaCom(string quem, string metodo, string caminho, string json) =>
        await Pedir(await QuemPede(quem), quem, metodo, caminho, "", json);

    // RF-171: com o relógio de teste 20 s antes da hora do exemplo, as datas
    // da resposta (abertoEm e o prazoResposta de 8 h úteis, RNF-229) valem com
    // folga de 1 min; quem pede entra de novo, no relógio novo
    [When(@"^envia POST /sac/chamados em (\S+) com:$")]
    public async Task AbreNaHora(string quando, string json)
    {
        await sac.Saltar(DateTimeOffset.Parse(quando).AddSeconds(-20));
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
        await Pedir(mundo.Sujeito ?? await mundo.Master(), "a conta master", "POST", "/sac/chamados", "", json);
    }

    [When(@"^a conta master envia uma mensagem com 6 anexos$")]
    public async Task SeisAnexos()
    {
        var master = await mundo.Master();
        var id = await sac.Chamado("SAC-000042", "AGUARDANDO_CLIENTE");
        var anexos = new List<string>();
        for (var i = 0; i < 6; i++) anexos.Add(await sac.Anexo(master));
        await Executar(master, "POST", $"/sac/chamados/{id}/mensagens", ChamadosDoExemplo.Mensagem("Seguem as telas", false, [.. anexos]).ToJsonString());
    }

    [When(@"^o Caio envia o status ""(\w+)"" sem a mensagem$")]
    public async Task StatusSemMensagem(string status)
    {
        var id = await sac.Chamado("SAC-000042", "EM_ATENDIMENTO");
        await Executar(await sac.Caio(), "POST", $"/sac/chamados/{id}/status", new JsonObject { ["status"] = status }.ToJsonString());
    }

    // a segunda frase do status do RF-179
    [When(@"^a mensagem ""(.+)""$")]
    public async Task ComAMensagem(string mensagem)
    {
        var (caminho, status) = statusPendente ?? throw new InvalidOperationException("nenhum status pedido antes da mensagem");
        textoEnviado = mensagem;
        await Executar(await sac.Caio(), "POST", caminho, new JsonObject { ["status"] = status, ["mensagem"] = mensagem }.ToJsonString());
    }

    // RN-290 e RN-297: os 7 dias passam pelo relógio de teste e o agendamento
    // do SAC roda na hora (gancho POST /_teste/sac/agendamento)
    [When(@"^chega (\S+) sem mensagem da conta$")]
    public async Task Chega(string quando)
    {
        await sac.Saltar(DateTimeOffset.Parse(quando).AddSeconds(30));
        await sac.Agendamento();
    }

    [When(@"^o SAC-000039 passa a ""RESOLVIDO"" por 7 dias sem resposta$")]
    public async Task SeteDiasSemResposta()
    {
        await AguardandoDesde("2026-11-05T13:00:00Z");
        await sac.Saltar(DateTimeOffset.Parse("2026-11-12T13:00:30Z"));
        await execucoes.Executar(async () =>
        {
            await sac.Agendamento();
            return mundo.Ultima;
        });
    }

    // RNF-230: a conversa ao vivo do SAC-000042
    [When(@"^envia GET /sac/chamados/(SAC-\d{6})/ao-vivo com Accept ""text/event-stream""$")]
    public async Task Conecta(string exemplo)
    {
        var id = await sac.Chamado(exemplo, "AGUARDANDO_CLIENTE");
        conversa = await AbrirAoVivo(mundo.Sujeito ?? await mundo.Master(), $"/sac/chamados/{id}/ao-vivo");
    }

    [When(@"^a conta master reconecta com o cabeçalho Last-Event-ID ""(msg-\d{6})""$")]
    public async Task Reconecta(string exemplo)
    {
        var ultimo = await mundo.Traduzir(exemplo);
        conversa = await AbrirAoVivo(await mundo.Master(), $"/sac/chamados/{sac.Real("SAC-000042")}/ao-vivo", ultimo);
    }

    // ── Então ──────────────────────────────────────────────────────────────
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com o campo (\w+)$")]
    public void ComOCampoSemAspas(int status, string codigo, string campo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        Assert.True(mundo.Ultima.Corpo?["campo"]?.GetValue<string>() == campo, $"esperava o campo \"{campo}\": {mundo.Ultima}");
    }

    [Then(@"^a resposta é 200 com o status ""(\w+)""$")]
    public void ComOStatus(string status)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo("status") == status, $"esperava o status {status}: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 200 com o status ""(\w+)"" e o mesmo responsavel$")]
    public void ComOMesmoResponsavel(string status)
    {
        ComOStatus(status);
        Assert.True(Campo("responsavel") == "Caio Lima", $"esperava o mesmo responsável, o Caio Lima: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 200 com o status ""(\w+)"" e resolvidoEm$")]
    public void ComResolvidoEm(string status)
    {
        ComOStatus(status);
        Assert.True(DateTimeOffset.TryParse(Campo("resolvidoEm"), out _), $"esperava resolvidoEm: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 201 e o chamado passa a ""(\w+)""$")]
    public async Task AvaliadoEFechado(string status)
    {
        Respostas.Confere(mundo.Ultima, 201);
        var lido = await sac.Ler(sac.Real("SAC-000042"), await mundo.Master());
        Assert.True(lido["status"]?.GetValue<string>() == status, $"esperava o chamado em {status}: {lido.ToJsonString()}");
    }

    [Then(@"^a resposta é 409 JA_ASSUMIDO com o responsavel ""(.+)""$")]
    public void JaAssumido(string responsavel)
    {
        Respostas.Confere(mundo.Ultima, 409, "JA_ASSUMIDO");
        Assert.True(Campo("responsavel") == responsavel, $"esperava o responsavel \"{responsavel}\": {mundo.Ultima}");
    }

    [Then(@"^com ""substituir"" true no corpo, a resposta é 200 com o Caio como responsável$")]
    public async Task Substituir()
    {
        await Executar(await sac.Caio(), "POST", $"/sac/chamados/{sac.Real("SAC-000042")}/assumir",
            new JsonObject { ["substituir"] = true }.ToJsonString());
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo("responsavel") == "Caio Lima", $"esperava o Caio Lima como responsável: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 409 LIMITE_CHAMADOS e nenhum chamado é aberto$")]
    public async Task NenhumAberto()
    {
        Respostas.Confere(mundo.Ultima, 409, "LIMITE_CHAMADOS");
        var lista = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", "/sac/chamados?situacao=EM_ANDAMENTO"), 200, "GET /sac/chamados");
        Assert.True(lista.Corpo?["total"]?.GetValue<int>() == 10, $"a EBFGA devia continuar com 10 em andamento: {lista}");
    }

    // ── formato comum ──────────────────────────────────────────────────────
    [When(@"^um pedido ao SAC é recusado$")]
    public async Task PedidoRecusado()
    {
        var corpo = ChamadosDoExemplo.Corpo();
        corpo["assunto"] = "Erro";
        await Executar(await mundo.Master(), "POST", "/sac/chamados", corpo.ToJsonString());
    }

    [Then(@"^o corpo traz o código, a mensagem e, quando houver, o campo:$")]
    public async Task CorpoDoErro(string json)
    {
        Assert.True(mundo.Ultima.Status is >= 400 and < 600, $"esperava uma recusa: {mundo.Ultima}");
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), mundo.Ultima.Corpo);
    }

    [Then(@"^o chamado vai no endereço pelo número, como ""SAC-000042""$")]
    public async Task EnderecoPeloNumero()
    {
        var id = await sac.Chamado("SAC-000042", "EM_ATENDIMENTO");
        Assert.Matches("^SAC-[0-9]{6}$", id);
        var lido = await sac.Ler(id, await mundo.Master());
        Assert.Equal(id, lido["id"]?.GetValue<string>());
    }

    [Then(@"^toda data e hora segue a ISO 8601 em UTC, como ""2026-11-12T13:00:00Z""$")]
    public async Task DatasEmUtc()
    {
        var lido = await sac.Ler(await sac.Chamado("SAC-000042", "EM_ATENDIMENTO"), await mundo.Master());
        var datas = Datas(lido).ToList();
        Assert.Contains(datas, d => d.Campo == "abertoEm");
        Assert.All(datas, d => Assert.True(Utc().IsMatch(d.Valor), $"{d.Campo}: esperava ISO 8601 em UTC, veio \"{d.Valor}\""));
    }

    [Then(@"^status e categoria vão em maiúsculas, como ""(\w+)"" e ""(\w+)""$")]
    public async Task EmMaiusculas(string status, string categoria)
    {
        var lido = await sac.Ler(await sac.Chamado("SAC-000042", "EM_ATENDIMENTO"), await mundo.Master());
        Assert.Equal((status, categoria), (lido["status"]?.GetValue<string>(), lido["categoria"]?.GetValue<string>()));
    }

    [Then(@"^só a conta master, o autônomo, o administrador e o membro com ""MOD-02"" usam o SAC$")]
    public async Task QuemUsa()
    {
        foreach (var (pessoa, status) in new[]
        {
            (await mundo.Master(), 200), (await mundo.Pedro(), 200), (mundo.Admin(), 200), (await sac.Caio(), 200),
            (await sac.RuiSemSac(), 403), (await mundo.Maria(), 403),
        })
        {
            var r = await mundo.EnviarComo(pessoa, "GET", "/sac/chamados");
            Assert.True(r.Status == status, $"GET /sac/chamados de {pessoa.Nome}: esperava {status}, veio {r}");
        }
    }

    // ── listas (RF-172, RF-174, RF-179) ────────────────────────────────────
    [Then(@"^a resposta é 200 com itens e total, até 50 por página$")]
    public void ItensETotal()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Corpo?["total"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number, $"esperava o total: {mundo.Ultima}");
        Assert.InRange(Itens().Count, 0, 50);
    }

    [Then(@"^a resposta é 200 com os (\d+) chamados de todas as contas, até 50 por página$")]
    public void ChamadosDeTodas(int quantos)
    {
        ItensETotal();
        Assert.True(mundo.Ultima.Corpo!["total"]!.GetValue<int>() == quantos, $"esperava total {quantos}: {mundo.Ultima}");
        Assert.True(sac.Fila["andamento"].Order().SequenceEqual(Ids().Order()),
            $"esperava os {quantos} em andamento das 3 contas: {mundo.Ultima}");
    }

    [Then(@"^o mais antigo esperando o time vem primeiro$")]
    public void MaisAntigoPrimeiro() => Assert.True(Ids().FirstOrDefault() == sac.PrimeiroDaFila,
        $"o primeiro devia ser {sac.PrimeiroDaFila}, aberto antes e ainda sem resposta: {mundo.Ultima}");

    [Then(@"^cada item traz (?:também )?(.+)$")]
    public void CadaItemTraz(string campos)
    {
        var nomes = campos.Replace(" e ", ", ").Split(", ", StringSplitOptions.TrimEntries);
        Assert.NotEmpty(Itens());
        Assert.All(Itens(), item => Assert.All(nomes, n => Assert.True(item.ContainsKey(n), $"o item devia trazer {n}: {item.ToJsonString()}")));
    }

    [Then(@"^itens traz (o SAC-000042 e o SAC-000039, o atualizado por último primeiro|os ""ABERTO"" sem responsável|os chamados do Caio|os 2 com o prazo vencido|os 4 à espera da conta|os fechados com nota 1 ou 2|os da Escola Bento Freitas, de todo status)$")]
    public void ItensTraz(string quais)
    {
        Respostas.Confere(mundo.Ultima, 200);
        if (quais.StartsWith("o SAC-000042", StringComparison.Ordinal))
        {
            Assert.Equal(new string?[] { sac.Real("SAC-000042"), sac.Real("SAC-000039") }, Ids());
            return;
        }
        var lista = quais switch
        {
            "os \"ABERTO\" sem responsável" => "nenhum",
            "os chamados do Caio" => "eu",
            "os 2 com o prazo vencido" => "atrasado",
            "os 4 à espera da conta" => "aguardando",
            "os fechados com nota 1 ou 2" => "nota",
            _ => "EBFGA",
        };
        Assert.True(sac.Fila[lista].Order().SequenceEqual(Ids().Order()),
            $"esperava {string.Join(", ", sac.Fila[lista].Order())}; vieram {string.Join(", ", Ids().Order())}");
    }

    [Then(@"^o item do (SAC-\d{6}) traz:$")]
    public async Task ItemDo(string _, string json) => await ItemTraz(json);

    [Then(@"^itens traz:$")]
    public async Task ItemTraz(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperado = JsonNode.Parse(await mundo.Traduzir(json));
        Assert.True(Itens().Any(i => Comparacao.Contem(esperado, i)),
            $"nenhum item igual a {esperado!.ToJsonString()}: {mundo.Ultima}");
    }

    // ── conversa (RF-173, RF-175, RF-176, RF-179) ──────────────────────────
    [Then(@"^a resposta é 200 com o chamado, as mensagens e a linhaDoTempo, sem as notas internas$")]
    public void ChamadoSemNotas()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var corpo = mundo.Ultima.Corpo!;
        Assert.Equal(sac.Real("SAC-000042"), corpo["id"]?.GetValue<string>());
        Assert.True(corpo["linhaDoTempo"] is JsonArray { Count: > 0 }, $"esperava a linhaDoTempo: {mundo.Ultima}");
        var mensagens = ChamadosDoExemplo.Mensagens(corpo);
        Assert.Contains(mensagens, m => m["texto"]?.GetValue<string>() == ChamadosDoExemplo.Pergunta);
        Assert.DoesNotContain(mensagens, m => ChamadosDoExemplo.Interna(m) || m["texto"]?.GetValue<string>() == ChamadosDoExemplo.NotaDoTime);
        Assert.DoesNotContain(ChamadosDoExemplo.NotaDoTime, mundo.Ultima.Texto);
    }

    [Then(@"^o próximo GET /sac/chamados traz o (SAC-\d{6}) com novaResposta false$")]
    public async Task Lido(string exemplo)
    {
        var lista = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", "/sac/chamados"), 200, "GET /sac/chamados");
        var item = (lista.Corpo?["itens"]?.AsArray() ?? []).FirstOrDefault(i => i?["id"]?.GetValue<string>() == sac.Real(exemplo));
        Assert.True(item?["novaResposta"]?.GetValueKind() == System.Text.Json.JsonValueKind.False, $"esperava novaResposta false: {lista}");
    }

    [Then(@"^GET /sac/chamados/SAC-000042 da conta master não traz a nota$")]
    public async Task SemANota()
    {
        var lido = await sac.Ler(sac.Real("SAC-000042"), await mundo.Master());
        Assert.DoesNotContain(textoEnviado, lido.ToJsonString());
        Assert.DoesNotContain(ChamadosDoExemplo.Mensagens(lido), ChamadosDoExemplo.Interna);
    }

    [Then(@"^a mensagem entra na conversa, visível para a conta$")]
    public async Task NaConversa()
    {
        var lido = await sac.Ler(sac.Real("SAC-000042"), await mundo.Master());
        Assert.True(ChamadosDoExemplo.Mensagens(lido).Any(m => m["texto"]?.GetValue<string>() == textoEnviado && !ChamadosDoExemplo.Interna(m)),
            $"a conta devia ver \"{textoEnviado}\" na conversa: {lido.ToJsonString()}");
    }

    [Then(@"^a resposta é 200 e a mensagem é gravada como nota interna$")]
    public async Task ComoNotaInterna()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var lido = await sac.Ler(sac.Real("SAC-000087"), await sac.Caio());
        Assert.True(ChamadosDoExemplo.Mensagens(lido).Any(m => m["texto"]?.GetValue<string>() == textoEnviado && ChamadosDoExemplo.Interna(m)),
            $"\"{textoEnviado}\" devia ser nota interna: {lido.ToJsonString()}");
    }

    // RN-289: o pedido sem login resolvido não fica atrasado, nem depois do
    // prazo de 4 h úteis (o relógio de teste anda 4 dias)
    [Then(@"^o prazo de primeira resposta fica cumprido$")]
    public async Task PrazoCumprido()
    {
        await sac.Saltar(sac.Agora().AddDays(4));
        var lido = await sac.Ler(sac.Real("SAC-000087"), await sac.Caio());
        Assert.True(lido["atrasado"]?.GetValueKind() == System.Text.Json.JsonValueKind.False, $"o prazo devia estar cumprido (atrasado false): {lido.ToJsonString()}");
    }

    [Then(@"^com ""interna"": true a resposta é 201$")]
    public async Task ComNotaInterna()
    {
        await Executar(await sac.Caio(), "POST", $"/sac/chamados/{sac.Real("SAC-000087")}/mensagens",
            ChamadosDoExemplo.Mensagem("Contato confirmado por telefone", true).ToJsonString());
        Respostas.Confere(mundo.Ultima, 201);
    }

    // ── prazos (RN-290, RN-297) ────────────────────────────────────────────
    [Then(@"^GET /sac/chamados/(SAC-\d{6}) traz o status ""(\w+)""$")]
    public async Task GetTrazOStatus(string exemplo, string status)
    {
        var lido = await sac.Ler(sac.Real(exemplo), await mundo.Master());
        Assert.True(lido["status"]?.GetValue<string>() == status, $"esperava {status}: {lido.ToJsonString()}");
    }

    // o autor do evento do prazo: "sistema" (o nome ou o tipo SISTEMA do modelo de documentos)
    [Then(@"^a linhaDoTempo traz o evento com o autor ""sistema""$")]
    public async Task EventoDoSistema()
    {
        var lido = await sac.Ler(sac.Real("SAC-000039"), await mundo.Master());
        var eventos = lido["linhaDoTempo"]?.AsArray() ?? [];
        Assert.True(eventos.Any(e => e?["autor"] is { } autor
                && (autor.GetValueKind() == System.Text.Json.JsonValueKind.String ? autor.GetValue<string>() : autor["tipo"]?.GetValue<string>())
                    ?.Equals("sistema", StringComparison.OrdinalIgnoreCase) == true),
            $"esperava um evento do sistema na linhaDoTempo: {lido.ToJsonString()}");
    }

    [Then(@"^""auditoria"" ganha a ação ""(\w+)"", sem quem, com o alvo ""(SAC-\d{6})""$")]
    public async Task AuditadoSemQuem(string acao, string exemplo)
    {
        var novos = (await execucoes.Novos(Auditoria.Registros)).Where(d => Auditoria.Texto(d, "acao") == acao).ToList();
        Assert.True(novos.Count > 0, $"esperava a ação {acao} em \"auditoria\"");
        Assert.All(novos, d => Assert.True(Auditoria.Texto(d, "quem") is null, $"{acao} devia ir sem quem: {d}"));
        Assert.Contains(novos, d => Auditoria.Texto(d, "alvo") == sac.Real(exemplo));
    }

    // ── anexos (RN-292) ────────────────────────────────────────────────────
    // 24 h e 1 min depois, pelo relógio de teste, e o agendamento do SAC na hora
    [Then(@"^o anexo não usado num chamado ou mensagem em 24 h é descartado$")]
    public async Task Descartado()
    {
        var id = Texto(mundo.Ultima, "id");
        await sac.Saltar(sac.Agora().AddHours(24).AddMinutes(1));
        await sac.Agendamento();
        var r = await mundo.EnviarComo(await mundo.Master(), "GET", $"/sac/anexos/{id}");
        Respostas.Confere(r, 404, "ANEXO_INEXISTENTE");
    }

    // ── card (RN-295) ──────────────────────────────────────────────────────
    [Then(@"^traz também a expandida e as series de dia, semana e mes, (\d+) números cada$")]
    public void ExpandidaESeries(int pontos)
    {
        var corpo = mundo.Ultima.Corpo!;
        Assert.True(corpo["expandida"] is JsonObject, $"esperava a expandida: {mundo.Ultima}");
        foreach (var recorte in new[] { "dia", "semana", "mes" })
        {
            var serie = corpo["series"]?[recorte] as JsonArray;
            Assert.True(serie?.Count == pontos && serie.All(n => n?.GetValueKind() == System.Text.Json.JsonValueKind.Number),
                $"series.{recorte} devia ter {pontos} números: {mundo.Ultima}");
        }
    }

    [Then(@"^a resposta é 200 com o perfil ""(\w+)""$")]
    public void ComOPerfil(string perfil)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo("perfil") == perfil && Campo("modulo") == "MOD-02", $"esperava o card MOD-02 do perfil {perfil}: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 200 com o perfil ""(\w+)"" e o selo (\d+)$")]
    public void ComOPerfilEOSelo(string perfil, int selo)
    {
        ComOPerfil(perfil);
        Assert.True(mundo.Ultima.Corpo?["selo"]?.GetValue<int>() == selo, $"esperava o selo {selo}: {mundo.Ultima}");
    }

    // 15 RN-279: 200 em text/event-stream e o primeiro "card", com id = versao,
    // igual ao GET /sac/card
    [Then(@"^a resposta é 200, como em RN-279 de 15_modulos$")]
    public async Task ComoORn279()
    {
        var fluxo = aoVivo.Ultimo;
        Assert.True(fluxo.Status == 200 && fluxo.TipoDoConteudo == "text/event-stream", $"esperava 200 em text/event-stream: {fluxo}");
        var card = await fluxo.EsperarTipo("card", CincoSegundos + Folga);
        Assert.True(card is not null, $"esperava o primeiro evento \"card\": {fluxo}");
        Assert.True(card.Corpo?["modulo"]?.GetValue<string>() == "MOD-02" && card.Id == card.Corpo?["versao"]?.ToJsonString(),
            $"o \"card\" devia ser o do SAC, com id igual à versao: {card}");
    }

    // ── configuração (RF-181) ──────────────────────────────────────────────
    [Then(@"^a resposta é 200 e GET /sac/configuracao traz os prazos novos$")]
    public async Task PrazosNovos()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var enviados = JsonNode.Parse(mundo.UltimoPedido.Corpo!)!["prazos"]!;
        var lida = Exigir(await mundo.EnviarComo(mundo.Admin(), "GET", "/sac/configuracao"), 200, "GET /sac/configuracao");
        foreach (var prazo in enviados.AsArray())
        {
            Assert.True(lida.Corpo?["prazos"]?.AsArray().Any(p => Comparacao.Contem(prazo, p)) == true,
                $"esperava {prazo!.ToJsonString()} em GET /sac/configuracao: {lida}");
        }
    }

    [Then(@"^a resposta é 200 com os prazos de cada categoria$")]
    public void PrazosDeCadaCategoria()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var categorias = (mundo.Ultima.Corpo?["prazos"]?.AsArray() ?? [])
            .Where(p => p?["horasUteis"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number)
            .Select(p => p!["categoria"]?.GetValue<string>()).ToHashSet();
        string[] todas = ["ACESSO_E_SENHA", "COBRANCA", "MODULO_COM_PROBLEMA", "DUVIDA_DE_USO", "SUGESTAO_AO_DEBOAZ", "SENHA_CONTA_MASTER"];
        Assert.True(categorias.SetEquals(todas), $"esperava os prazos das 6 categorias (RN-289): {mundo.Ultima}");
    }

    [Then(@"^a conta master da EBFGA recebe 403 ACESSO_NEGADO no mesmo pedido$")]
    public async Task MasterNoMesmoPedido()
    {
        var (metodo, caminho, corpo) = mundo.UltimoPedido;
        Respostas.Confere(await mundo.EnviarComo(await mundo.Master(), metodo, caminho, corpo), 403, "ACESSO_NEGADO");
    }

    // ── ao vivo (RNF-230) ──────────────────────────────────────────────────
    [Then(@"^quando o Caio envia uma mensagem, o evento ""mensagem"" chega em até (\d+) s$")]
    public async Task MensagemAoVivo(int segundos)
    {
        var fluxo = conversa ?? throw new InvalidOperationException("nenhuma conversa ao vivo aberta");
        var desde = fluxo.Eventos.Count;
        mensagemDoCaio = await sac.Mensagem(sac.Real("SAC-000042"), await sac.Caio(), "Pode tentar de novo agora?");
        var enviada = DateTimeOffset.UtcNow;
        var evento = await fluxo.Esperar(e => e.Tipo == "mensagem", TimeSpan.FromSeconds(segundos) + Folga, desde);
        Assert.True(evento is not null && evento.Quando - enviada <= TimeSpan.FromSeconds(segundos) + Folga,
            $"o evento \"mensagem\" devia chegar em até {segundos} s: {fluxo}");
    }

    // o id do evento é o da mensagem (requisito), e o corpo é o item da
    // mensagem em GET /sac/chamados/{id}
    [Then(@"^o evento tem o id da mensagem, como ""msg-000319"", e o mesmo corpo de GET do chamado$")]
    public async Task EventoComOId()
    {
        var evento = conversa!.Eventos.FirstOrDefault(e => e.Tipo == "mensagem" && e.Id == mensagemDoCaio);
        Assert.True(evento is not null, $"esperava o evento \"mensagem\" com id {mensagemDoCaio}: {conversa}");
        var lido = await sac.Ler(sac.Real("SAC-000042"), await mundo.Master());
        var item = ChamadosDoExemplo.Mensagens(lido).FirstOrDefault(m => m["id"]?.GetValue<string>() == mensagemDoCaio);
        Assert.True(item is not null, $"a mensagem {mensagemDoCaio} devia estar em GET do chamado: {lido.ToJsonString()}");
        Comparacao.Confere(item, evento.Corpo, exato: true);
    }

    [Then(@"^a nota interna do Caio não chega à conta$")]
    public async Task NotaNaoChega()
    {
        var desde = conversa!.Eventos.Count;
        notaDoCaio = await sac.Mensagem(sac.Real("SAC-000042"), await sac.Caio(), ChamadosDoExemplo.NotaDoTime, interna: true);
        var chegou = await conversa.Esperar(e => e.Id == notaDoCaio || e.Dados.Contains(ChamadosDoExemplo.NotaDoTime), CincoSegundos + Folga, desde);
        Assert.True(chegou is null, $"a nota interna não devia chegar à conta: {chegou}");
    }

    [Then(@"^recebe primeiro as mensagens enviadas depois da ""msg-000318"", na ordem$")]
    public async Task RecebeAsDaQueda()
    {
        var fluxo = conversa!;
        Assert.True(fluxo.Status == 200, $"a reconexão devia ser aceita: {fluxo}");
        await fluxo.Esperar(_ => fluxo.Eventos.Count(e => e.Tipo == "mensagem") >= depoisDaQueda.Count, CincoSegundos + Folga);
        var ids = fluxo.Eventos.Where(e => e.Tipo == "mensagem").Select(e => e.Id).Take(depoisDaQueda.Count).ToList();
        Assert.True(ids.SequenceEqual(depoisDaQueda), $"esperava {string.Join(", ", depoisDaQueda)} primeiro, na ordem: {fluxo}");
    }

    // ── fora de escopo ─────────────────────────────────────────────────────
    [Then(@"^o SAC não tem chat ao vivo, telefone nem atendimento por robô$")]
    [Then(@"^o staff, o participante e o responsável não abrem chamado$")]
    [Then(@"^a conversa com pais e alunos é do módulo Mensagens$")]
    [Then(@"^o time deboaz não abre chamado em nome da conta$")]
    public static void ForaDeEscopo() => throw new PendingStepException("fora de escopo: nada a testar");

    // ── o pedido ───────────────────────────────────────────────────────────
    async Task<Pessoa> QuemPede(string quem) => quem switch
    {
        _ when quem.StartsWith("a conta master", StringComparison.Ordinal) || quem == "a conta dona" => await mundo.Master(),
        _ when quem.StartsWith("o Caio", StringComparison.Ordinal) => await sac.Caio(),
        "o Pedro Reis" => await mundo.Pedro(),
        "a staff Maria" => await mundo.Maria(),
        _ when quem.StartsWith("o Rui", StringComparison.Ordinal) => await sac.RuiSemSac(),
        _ => mundo.Admin(),
    };

    async Task Pedir(Pessoa pessoa, string quem, string metodo, string caminho, string resto, string? json)
    {
        // RNF-233 na instância de Partida e RNF-234 "… em <data e hora>"
        if (await anexos.Atender(pessoa, metodo, caminho, resto)) return;
        // RN-297: o 500 não se provoca de fora (o banco caindo no meio do pedido)
        if (resto == "com falha geral")
        {
            throw new PendingStepException(
                "derrubar o banco no meio de um pedido não dá para provocar de fora, numa suíte caixa-preta: o 500 ERRO_INESPERADO " +
                "com X-Trace-Id e auditoriaErros ERRO fica nos testes do backend (SacContratoApiTests, a porta de prazos falhando)");
        }
        // RN-297: o corpo que não é JSON vai como veio
        if (CorpoCru().Match(resto) is { Success: true } cru)
        {
            await Executar(pessoa, metodo, await mundo.Traduzir(caminho), cru.Groups[1].Value);
            return;
        }
        // RN-289-api: "… em 2026-11-12T16:00:00Z", o pedido sai nessa hora do relógio de teste
        DateTimeOffset? naHora = null;
        if (EmHoraUtc().Match(resto) is { Success: true } hora)
        {
            naHora = DateTimeOffset.Parse(hora.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            resto = "";
        }
        var doTime = quem.StartsWith("o Caio", StringComparison.Ordinal);
        var partes = caminho.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /sac/chamados/{id}/{acao}/{m}
        var exemplo = partes.Length > 2 && partes[1] == "chamados" ? partes[2] : null;
        var acao = partes.Length > 3 ? partes[3] : "";
        // RF-173: o anexo que o corpo do exemplo cita ("anx-81bc09") é enviado antes por quem pede
        if (json is not null)
        {
            foreach (var anexo in AnexoNoTexto().Matches(json).Select(m => m.Value).Distinct())
            {
                if (!mundo.Apelidos.ContainsKey(anexo)) await sac.Anexo(pessoa, anexo);
            }
        }
        string? corpo = json is null ? null : await mundo.Traduzir(json);

        if (exemplo is not null) await Preparar(exemplo, acao, doTime, resto, partes.Length > 4 ? partes[4] : null);
        else if (caminho == "/sac/card" && doTime) await CincoEsperandoOTime();
        else if (caminho.StartsWith("/sac/chamados?", StringComparison.Ordinal) && doTime && !caminho.Contains("categoria=") && !sac.FilaDoCenario)
        {
            await sac.MundoDaFila();
        }
        else if (caminho == "/sac/anexos/anx-55d0e2") await AnexoDoPedro();
        // RF-172 e RF-174: a conta lista os chamados do exemplo da EBFGA
        else if (metodo == "GET" && caminho.StartsWith("/sac/chamados", StringComparison.Ordinal) && !doTime) await sac.MundoDaEbfga();

        corpo ??= await Corpo(pessoa, metodo, caminho, acao, resto);
        if (statusPendente is not null && corpo is null && acao == "status") return;
        var alvo = await mundo.Traduzir(caminho);
        if (caminho.EndsWith("/ao-vivo", StringComparison.Ordinal) || caminho.Contains("/ao-vivo?", StringComparison.Ordinal))
        {
            await AbrirAoVivo(pessoa, alvo);
            return;
        }
        if (caminho == "/sac/anexos")
        {
            await EnviarAnexo(pessoa, resto);
            return;
        }
        if (caminho == "/sac/chamados" && resto.StartsWith("ou /mensagens", StringComparison.Ordinal))
        {
            corpo = ChamadosDoExemplo.Corpo("Chamado com anexo", "MODULO_COM_PROBLEMA", await AnexoQueNaoPodeSerUsado(resto)).ToJsonString();
        }
        if (corpo is not null) textoEnviado = TextoDo(corpo);
        if (naHora is { } quando) await sac.Saltar(quando);
        await Executar(pessoa, metodo, alvo, corpo);
        Anotar(caminho, acao);
        if (resto == "com sucesso") Assert.True(mundo.Ultima.Status is >= 200 and < 300, $"o pedido devia dar certo: {mundo.Ultima}");
    }

    // o estado de que o pedido precisa, se o chamado ainda não existe
    async Task Preparar(string exemplo, string acao, bool doTime, string resto, string? mensagem)
    {
        if (exemplo == "SAC-000087")
        {
            await PedidoDeSenha();
            return;
        }
        if (resto == "com os 10 em andamento")
        {
            await sac.Chamado(exemplo, "RESOLVIDO");
            await DezEmAndamento();
            return;
        }
        if (resto == "com a nota 4, já avaliado")
        {
            var resolvido = await sac.Chamado(exemplo, "RESOLVIDO");
            await sac.Avaliar(resolvido, sac.DonoDe(resolvido), 5);
            return;
        }
        if (resto == ", depois de 7 dias")
        {
            // o relógio vai 7 dias e 10 s além de agora, logo depois de resolvido
            await sac.Chamado(exemplo, "RESOLVIDO");
            await sac.Saltar(sac.Agora().AddDays(7).AddSeconds(10));
            return;
        }
        var estado = (exemplo, acao) switch
        {
            ("SAC-000031", _) => "ENCERRADO",
            ("SAC-000045", _) => "ABERTO",
            (_, "assumir") => "ABERTO",
            (_, "status") => "EM_ATENDIMENTO",
            (_, "reabrir" or "avaliacao") => "RESOLVIDO",
            (_, "encerrar") => "EM_ATENDIMENTO",
            (_, "mensagens") when doTime => "EM_ATENDIMENTO",
            _ => "AGUARDANDO_CLIENTE",
        };
        var novo = !sac.Conhecido(exemplo) && sac.DoDado is null;
        var id = await sac.Chamado(exemplo, estado);
        // RF-173: a nota interna do time no chamado que a conta abre
        if (novo && acao == "" && exemplo == "SAC-000042")
        {
            await sac.Mensagem(id, await sac.Caio(), ChamadosDoExemplo.NotaDoTime, interna: true);
        }
        // RN-293: a mensagem "msg-000318" que o pedido tenta mudar
        if (mensagem is not null && !mundo.Apelidos.ContainsKey(mensagem))
        {
            var msg = await sac.Mensagem(id, await mundo.Master(), "Turma 7A");
            mundo.Apelidos[mensagem] = () => Task.FromResult(msg);
        }
    }

    // o corpo que o resto da frase descreve
    async Task<string?> Corpo(Pessoa pessoa, string metodo, string caminho, string acao, string resto)
    {
        if (metodo == "GET" || acao is "encerrar") return null;
        if (metodo is "PATCH" or "PUT" && caminho.Contains("/mensagens/", StringComparison.Ordinal))
        {
            return new JsonObject { ["texto"] = "Turma 7B" }.ToJsonString();
        }
        if (metodo == "DELETE") return null;
        if (caminho == "/sac/configuracao")
        {
            var horas = HorasUteis().Match(resto) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 4;
            return new JsonObject
            {
                ["prazos"] = new JsonArray(new JsonObject { ["categoria"] = "ACESSO_E_SENHA", ["horasUteis"] = horas }),
            }.ToJsonString();
        }
        if (caminho == "/sac/chamados")
        {
            var corpo = ChamadosDoExemplo.Corpo();
            switch (resto)
            {
                case "com sem a categoria" or "sem a categoria": corpo.Remove("categoria"); break;
                case "com a categoria \"SENHA_CONTA_MASTER\"": corpo["categoria"] = "SENHA_CONTA_MASTER"; break;
                case "com o assunto com 4 caracteres": corpo["assunto"] = "Erro"; break;
                case "com a descrição com 5.001 caracteres": corpo["descricao"] = new string('a', 5001); break;
                case "com o relacionadoA \"SAC-000045\", do Pedro":
                    corpo["relacionadoA"] = await sac.Chamado("SAC-000045", "ABERTO");
                    break;
            }
            return corpo.ToJsonString();
        }
        return acao switch
        {
            "mensagens" => resto switch
            {
                "com o texto \"Oi\"" => ChamadosDoExemplo.Mensagem("Oi").ToJsonString(),
                "com o texto vazio" => ChamadosDoExemplo.Mensagem("").ToJsonString(),
                "com \"interna\": true" => ChamadosDoExemplo.Mensagem(pessoa == await mundo.Master() ? "Nota da conta" : "Nota interna do Caio", true).ToJsonString(),
                "com \"interna\": false" => ChamadosDoExemplo.Mensagem("Olá, recebemos o seu pedido").ToJsonString(),
                _ => ChamadosDoExemplo.Mensagem("Turma 7A").ToJsonString(),
            },
            "assumir" => null,
            "status" => resto switch
            {
                "com \"RESOLVIDO\"" => Status("RESOLVIDO", ChamadosDoExemplo.Solucao),
                "com \"FECHADO\"" => Status("FECHADO", "Fechando"),
                "com o status \"RESOLVIDO\"" => Pendente(await mundo.Traduzir(caminho), "RESOLVIDO"),
                _ => Status("AGUARDANDO_CLIENTE", ChamadosDoExemplo.Pergunta),
            },
            "reabrir" => new JsonObject { ["mensagem"] = "Ainda aparece o erro na 8B" }.ToJsonString(),
            "avaliacao" => resto switch
            {
                "com sem a nota" => new JsonObject { ["comentario"] = "Sem nota" }.ToJsonString(),
                "com um comentário de 501 letras" => new JsonObject { ["nota"] = 4, ["comentario"] = new string('a', 501) }.ToJsonString(),
                _ when NotaDoResto().Match(resto) is { Success: true } m => new JsonObject { ["nota"] = int.Parse(m.Groups[1].Value) }.ToJsonString(),
                _ => new JsonObject { ["nota"] = 5, ["comentario"] = "Resolvido em 4 h, obrigado" }.ToJsonString(),
            },
            _ => null,
        };
    }

    static string Status(string status, string mensagem) => new JsonObject { ["status"] = status, ["mensagem"] = mensagem }.ToJsonString();

    string? Pendente(string caminho, string status)
    {
        // o caminho chega traduzido: "SAC-000087" já é o número real
        statusPendente = (caminho, status);
        return null;
    }

    // RN-292: "erro.png" de 2 MB, "foto.jpg" de 11 MB, "tela.mp4", "boleto.pdf",
    // um PDF com o nome "foto.png" (o tipo vem do conteúdo), "nota.jpg" com
    // ameaça, e a verificação fora do ar
    async Task EnviarAnexo(Pessoa pessoa, string resto)
    {
        var (nome, conteudo) = resto switch
        {
            "com o arquivo \"erro.png\" de 2 MB" => ("erro.png", Arquivos.Png(2 * Arquivos.MB)),
            "com \"foto.jpg\" de 11 MB" => ("foto.jpg", Arquivos.Jpg(11 * Arquivos.MB)),
            "com \"tela.mp4\"" => ("tela.mp4", Arquivos.Mp4()),
            "com \"boleto.pdf\"" => ("boleto.pdf", Arquivos.Pdf()),
            "com um PDF chamado \"foto.png\"" => ("foto.png", Arquivos.Pdf()),
            "com \"nota.jpg\" com ameaça" => ("nota.jpg", Arquivos.JpgComAmeaca()),
            _ => ("erro.png", Arquivos.Png(4096)),
        };
        if (resto == "com a verificação fora do ar") await sac.VerificacaoForaDoAr();
        await mundo.SessaoParaPedido(pessoa);
        var r = await execucoes.Executar(() => sac.EnviarAnexo(pessoa, nome, conteudo));
        if (r.Status == 201 && r.Corpo?["id"]?.GetValue<string>() is { } id && Anexo().IsMatch(id))
        {
            mundo.Apelidos["anx-7f3a21"] = () => Task.FromResult(id);
        }
    }

    // RN-292: o anexo de cada linha, preparado pela API (o descartado, pelo
    // relógio de teste e o agendamento do SAC)
    async Task<string> AnexoQueNaoPodeSerUsado(string resto)
    {
        var master = await mundo.Master();
        if (resto.Contains("anx-000000", StringComparison.Ordinal)) return "anx-000000";
        if (resto.Contains("anx-55d0e2", StringComparison.Ordinal)) return await sac.Anexo(await mundo.Pedro());
        if (resto.Contains("anx-7f3a21", StringComparison.Ordinal))
        {
            var usado = await sac.Anexo(master);
            await sac.Abrir(master, "SAC-000042", ChamadosDoExemplo.Assunto, "MODULO_COM_PROBLEMA", usado);
            return usado;
        }
        var descartado = await sac.Anexo(master);
        await sac.Saltar(sac.Agora().AddHours(24).AddMinutes(1));
        await sac.Agendamento();
        return descartado;
    }

    // RN-288: o anexo do chamado do Pedro (SAC-000045)
    async Task AnexoDoPedro()
    {
        if (mundo.Apelidos.ContainsKey("anx-55d0e2")) return;
        var pedro = await mundo.Pedro();
        var anexo = await sac.Anexo(pedro, "anx-55d0e2");
        await sac.Abrir(pedro, "SAC-000045", "Erro no relatório do autônomo", "MODULO_COM_PROBLEMA", anexo);
    }

    // RN-295: o selo do time é o número de chamados esperando o time
    async Task CincoEsperandoOTime()
    {
        var master = await mundo.Master();
        for (var i = 0; i < 5; i++) await sac.Abrir(master, assunto: $"Esperando o time número {i + 1}");
    }

    // a abertura passa por Execucoes: o fluxo não é auditado (RN-297) e a
    // resposta guarda os cabeçalhos dele (X-Trace-Id)
    async Task<Fluxo> AbrirAoVivo(Pessoa pessoa, string caminho, string? ultimoId = null)
    {
        var token = await mundo.TokenDe(pessoa);
        Fluxo? fluxo = null;
        await execucoes.Executar(async () =>
        {
            fluxo = await aoVivo.Abrir(pessoa.Nome, caminho, token, ultimoId);
            if (fluxo.Status != 200) await fluxo.EsperarFim(Folga);
            return mundo.Guardar(new Resposta(fluxo.Status, fluxo.Status == 200 ? "" : fluxo.Texto, [], fluxo.Cabecalhos));
        });
        return fluxo!;
    }

    // o login (ENTRAR_HUB, auditado) vem antes da marca: só o pedido em teste conta
    async Task Executar(Pessoa pessoa, string metodo, string caminho, string? corpo)
    {
        await mundo.SessaoParaPedido(pessoa);
        await execucoes.Executar(() => mundo.EnviarComo(pessoa, metodo, caminho, corpo));
    }

    // número gerado de um chamado ou mensagem novos: o do exemplo vira o real
    void Anotar(string caminho, string acao)
    {
        if (mundo.Ultima.Status != 201 || mundo.Ultima.Corpo?["id"]?.GetValue<string>() is not { } id) return;
        if (caminho == "/sac/chamados" && Chamado().IsMatch(id) && !sac.Conhecido("SAC-000042")) sac.Anotar("SAC-000042", id);
        if (acao == "mensagens" && Mensagem().IsMatch(id) && !mundo.Apelidos.ContainsKey("msg-000318"))
        {
            mundo.Apelidos["msg-000318"] = () => Task.FromResult(id);
        }
    }

    string? Campo(string nome) => mundo.Ultima.Corpo?[nome] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String
        ? v.GetValue<string>() : null;

    List<JsonObject> Itens() => [.. (mundo.Ultima.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>()];

    List<string?> Ids() => [.. Itens().Select(i => i["id"]?.GetValue<string>())];

    static string TextoDo(string corpo) =>
        JsonNode.Parse(corpo) is JsonObject o ? o["texto"]?.GetValue<string>() ?? o["mensagem"]?.GetValue<string>() ?? "" : "";

    // os campos de data e hora (…Em, prazoResposta) em qualquer nível
    static IEnumerable<(string Campo, string Valor)> Datas(JsonNode? no)
    {
        switch (no)
        {
            case JsonObject o:
                foreach (var (campo, valor) in o)
                {
                    if ((campo.EndsWith("Em", StringComparison.Ordinal) || campo == "prazoResposta")
                        && valor?.GetValueKind() == System.Text.Json.JsonValueKind.String) yield return (campo, valor.GetValue<string>());
                    foreach (var d in Datas(valor)) yield return d;
                }
                break;
            case JsonArray a:
                foreach (var item in a) foreach (var d in Datas(item)) yield return d;
                break;
        }
    }

    [GeneratedRegex(@"^em ""?(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)""?$")]
    private static partial Regex EmHoraUtc();

    [GeneratedRegex(@"^com o corpo ""(.*)""$")]
    private static partial Regex CorpoCru();

    [GeneratedRegex(@"^ajuda-\d{6}$")]
    private static partial Regex Pedido();

    [GeneratedRegex(@"^SAC-\d{6}$")]
    private static partial Regex Chamado();

    [GeneratedRegex(@"^msg-\d{6}$")]
    private static partial Regex Mensagem();

    [GeneratedRegex(@"anx-[0-9a-f]{6}")]
    private static partial Regex AnexoNoTexto();

    [GeneratedRegex(@"^anx-\w+$")]
    private static partial Regex Anexo();

    [GeneratedRegex(@"horasUteis (\d+)")]
    private static partial Regex HorasUteis();

    [GeneratedRegex(@"^com a nota (\d+)")]
    private static partial Regex NotaDoResto();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$")]
    private static partial Regex Utc();
}
