using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// O banco e o rastreio nos Cenários de API de 1_Requisitos/19_modulo-pagamento.md
// (v05): o aviso do banco (RN-305, RF-187, RF-188, RN-303), o pagamento ao
// vivo (RNF-238), a auditoria (RN-309) e o traceId (RNF-243, RNF-244). O
// banco é sempre o intermediador falso (BancoFalso.cs); o aviso entra pelo
// endereço do contrato. "avisosDoBanco" e o intermediador de cada ambiente
// (RNF-242) ficam em IntermediadorPassos.cs.
//
// O traceIdOrigem e o historico de cada documento só o banco de dados
// mostra, sem o requisito os nomear como coleção: ficam nos testes do
// backend. Aqui a frase confere o que a API, o fluxo ao vivo, o
// intermediador falso e as coleções "auditoria" e "avisosDoBanco" mostram.
public sealed partial class PagamentoPassos
{
    // pagamento e fatura antes do aviso (RN-305: "nenhum pagamento ou fatura muda")
    (string Id, JsonObject Pagamento, JsonObject Fatura)? antes;
    // no relógio monotônico (Cronometro), para medir a chegada do evento ao vivo
    TimeSpan avisoEnviadoEm;
    // o X-Trace-Id recebido no pedido (RNF-243) e a marca da execução do Dado (RNF-244)
    string origem = "";
    int execucaoDoPagamento = -1;

    // ── apoio ──────────────────────────────────────────────────────────────
    // o PIX da FAT-000402 aguardando; "pix_8c1e5d" é o id que o banco falso
    // deu à cobrança e "trc-2e7d90aa" o X-Trace-Id da execução que a criou
    async Task<string> PixDoExemplo()
    {
        var pix = await fat.Pix("PAG-000781");
        var externo = await banco.IdExterno(pix);
        mundo.Apelidos["pix_8c1e5d"] = () => Task.FromResult(externo);
        Execucao("trc-2e7d90aa", pix);
        return pix;
    }

    // o aviso é o pedido do cenário; "trc-6b0c42f1" do exemplo é o X-Trace-Id da resposta dele
    async Task<Resposta> Avisar(JsonObject aviso, bool assinaturaValida = true)
    {
        avisoEnviadoEm = Cronometro.Agora;
        var r = await execucoes.Executar(async () => mundo.Guardar(await banco.Avisar(aviso, assinaturaValida)));
        mundo.Apelidos["trc-6b0c42f1"] = () => Task.FromResult(r.Cabecalho("X-Trace-Id") ?? "sem X-Trace-Id");
        return r;
    }

    async Task<Resposta> Avisar(string pagamento, string tipo, DateTimeOffset em, string? traceId = null) =>
        await Avisar(BancoFalso.Aviso(tipo, await banco.IdExterno(pagamento), em, traceId));

    async Task Guardar(string pagamento) =>
        antes = (pagamento, await fat.Pagamento(pagamento), await fat.Fatura(fat.FaturaDo(pagamento)));

    // o POST que cria o PAG-000773 (crédito aprovado) é a execução "trc-1d6e3f08"
    async Task<string> ExecucaoQueCria()
    {
        await Pedir("a Rita", "POST", "/pagamento/faturas/FAT-000402/pagamentos", "", FaturasDoExemplo.Corpo("CARTAO_CREDITO").ToJsonString());
        Respostas.Confere(mundo.Ultima, 201);
        execucaoDoPagamento = execucoes.Resultados.Count - 1;
        var id = Texto(mundo.Ultima, "id");
        fat.Anotar("PAG-000773", id);
        Execucao("trc-1d6e3f08", id);
        return id;
    }

    // ── Dado ───────────────────────────────────────────────────────────────
    [Given(@"^a Rita com GET /pagamento/pagamentos/PAG-000781/ao-vivo aberto$")]
    public async Task AoVivoAberto()
    {
        var pix = await PixDoExemplo();
        var fluxo = await AbrirAoVivo(await fat.Rita(), $"/pagamento/pagamentos/{pix}/ao-vivo");
        Assert.True(fluxo.Status == 200, $"preparo: a conexão ao vivo do pagamento devia abrir: {fluxo}");
    }

    [Given(@"^a execução ""trc-1d6e3f08"" do POST que cria o PAG-000773 da FAT-000402$")]
    public async Task ExecucaoDoPost() => await ExecucaoQueCria();

    // RNF-243: o banco sem resposta; "trc-9a20d5be" é o traceId do corpo do 503
    [Given(@"^o 503 BANCO_INDISPONIVEL da FAT-000402 com o traceId ""trc-9a20d5be""$")]
    public async Task BancoIndisponivel()
    {
        var fatura = await fat.Fat402();
        await banco.Proximo(BancoFalso.SemResposta);
        var r = await mundo.EnviarComo(await fat.Rita(), "POST", $"/pagamento/faturas/{fatura}/pagamentos", FaturasDoExemplo.Corpo("PIX").ToJsonString());
        Exigir(r, 503, $"POST /pagamento/faturas/{fatura}/pagamentos com o banco sem resposta");
        origem = Campo(r.Corpo, "traceId") ?? throw new InvalidOperationException($"preparo: o 503 veio sem o traceId no corpo: {r}");
        mundo.Apelidos["trc-9a20d5be"] = () => Task.FromResult(origem);
        await banco.Normal();
    }

    // RNF-244: a busca do time pelo traceId é no banco de dados (índices do modelo de documentos), sem endereço no contrato
    [Given(@"^o traceId ""trc-1d6e3f08""$")]
    [Then(@"^encontra o PAG-000773, a FAT-000402, a auditoria e os erros com esse traceId$")]
    public static void BuscaPeloTraceId() => throw new PendingStepException(
        "o contrato não tem endereço de busca pelo traceId: o time procura no banco de dados; os índices ficam nos testes do backend");

    // ── Quando · o banco ───────────────────────────────────────────────────
    // RF-187: a confirmação ou a recusa do débito que estava aguardando
    [When(@"^o banco confirma às (\d{2}:\d{2})$")]
    public async Task BancoConfirmaNaHora(string hora)
    {
        var em = FaturasDoExemplo.Hora($"2026-12-03T{hora}:00Z");
        await fat.Saltar(em);
        Respostas.Confere(await Avisar(fat.UltimoPagamento!, BancoFalso.Confirmado, em), 200);
    }

    [When(@"^o banco recusa$")]
    public async Task BancoRecusa() =>
        Respostas.Confere(await Avisar(fat.UltimoPagamento!, BancoFalso.Recusado, fat.Agora()), 200);

    // RF-188: a compensação do boleto, no dia dito
    [When(@"^o banco confirma a compensação de (\d{4}-\d{2}-\d{2})$")]
    public async Task BancoCompensaNoDia(string dia)
    {
        await fat.Saltar(dia);
        Respostas.Confere(await Avisar(fat.Real("PAG-000785"), BancoFalso.Confirmado, FaturasDoExemplo.Hora(dia)), 200);
    }

    // RN-303: o boleto cancelado é pago mesmo assim, com a fatura já paga
    [When(@"^o banco confirma a compensação do (PAG-\d{6})$")]
    public async Task BancoCompensa(string exemplo) =>
        Respostas.Confere(await Avisar(fat.Real(exemplo), BancoFalso.Confirmado, fat.Agora()), 200);

    // RN-305: o aviso do PIX, pago às 13:12 UTC de 2026-12-03
    [When(@"^o banco envia POST /pagamento/notificacoes com a assinatura válida, pago às (\d{2}:\d{2})$")]
    public async Task AvisoValido(string hora)
    {
        var em = FaturasDoExemplo.Hora($"2026-12-03T{hora}:00Z");
        await fat.Saltar(em);
        await Avisar(fat.Real("PAG-000781"), BancoFalso.Confirmado, em);
    }

    [When(@"^chega POST /pagamento/notificacoes (com a assinatura inválida|repetido, já processado|de pagamento externo desconhecido)$")]
    public async Task AvisoSemEfeito(string caso)
    {
        var pix = await PixDoExemplo();
        switch (caso)
        {
            case "com a assinatura inválida":
                await Guardar(pix);
                await Avisar(BancoFalso.Aviso(BancoFalso.Confirmado, await banco.IdExterno(pix), fat.Agora()), assinaturaValida: false);
                break;
            case "repetido, já processado":
                // o primeiro vale; o segundo é o mesmo aviso, com o mesmo id
                await fat.Confirmar(pix);
                await Guardar(pix);
                await Avisar(banco.UltimoAviso!);
                break;
            default:
                await Guardar(pix);
                await Avisar(BancoFalso.Aviso(BancoFalso.Confirmado, "pix_desconhecido", fat.Agora()));
                break;
        }
    }

    // RNF-243 e RN-309
    [When(@"^o banco envia POST /pagamento/notificacoes com assinatura inválida$")]
    public async Task AvisoComAssinaturaInvalida() =>
        await Avisar(BancoFalso.Aviso(BancoFalso.Confirmado, await banco.IdExterno(await PixDoExemplo()), fat.Agora()), assinaturaValida: false);

    [When(@"^o banco envia POST /pagamento/notificacoes e a resposta é de sucesso$")]
    public async Task AvisoComSucesso()
    {
        var r = await Avisar(await PixDoExemplo(), BancoFalso.Confirmado, fat.Agora());
        Assert.True(r.Status is >= 200 and < 300, $"esperava sucesso, veio {r}");
    }

    // RNF-244: o banco devolve, ou não, o traceId da criação nos metadados do aviso
    [When(@"^o banco envia POST /pagamento/notificacoes do ""pix_8c1e5d"" (sem traceId|com ""trc-2e7d90aa"" de volta nos dados)$")]
    public async Task AvisoComOuSemTraceId(string como)
    {
        var pix = await PixDoExemplo();
        await Avisar(pix, BancoFalso.Confirmado, fat.Agora(), como == "sem traceId" ? null : fat.ExecucaoDe(pix));
    }

    [When(@"^o aviso do banco confirma o (PAG-\d{6})$")]
    [When(@"^o aviso do banco da execução ""trc-6b0c42f1"" confirma o (PAG-\d{6})$")]
    public async Task AvisoConfirma(string exemplo) =>
        Respostas.Confere(await Avisar(fat.Real(exemplo), BancoFalso.Confirmado, fat.Agora()), 200);

    // ── Quando · rastreio ──────────────────────────────────────────────────
    // RNF-243: o corpo que não é JSON vai como veio
    [When(@"^a Rita envia POST …/FAT-000402/pagamentos com o corpo ""(.*)""$")]
    public async Task CorpoQueNaoEJson(string corpo) =>
        await Executar(await fat.Rita(), "POST", $"/pagamento/faturas/{await fat.Fat402()}/pagamentos", corpo);

    // RNF-243: a nova tentativa leva o traceId do 503 (ou um valor fora do formato) no cabeçalho
    [When(@"^a Rita envia o pagamento de novo com o cabeçalho X-Trace-Id ""(.+)""$")]
    public async Task DeNovoComTraceId(string recebido) =>
        await Executar(await fat.Rita(), "POST", $"/pagamento/faturas/{fat.Real("FAT-000402")}/pagamentos", FaturasDoExemplo.Corpo("PIX").ToJsonString(),
            cabecalhos: new Dictionary<string, string> { ["X-Trace-Id"] = await mundo.Traduzir(recebido) });

    // RN-309 e RNF-244: a virada para "VENCIDA" é do agendamento, sem pedido de ninguém
    [When(@"^o agendamento vira a FAT-000371 para ""VENCIDA"" em 2026-11-11T03:00:00Z$")]
    [When(@"^sistema envia a virada para ""VENCIDA"" e o aviso de atraso e a resposta é de sucesso$")]
    public async Task AgendamentoViraVencida()
    {
        var fatura = await fat.Fat371(vencida: false);
        await execucoes.Executar(() => fat.Chegar("2026-11-11T03:00:00Z"));
        var lida = await fat.Fatura(fatura);
        Assert.True(Campo(lida, "status") == "VENCIDA", $"esperava a fatura VENCIDA depois do agendamento: {lida.ToJsonString()}");
    }

    [When(@"^o deboaz pede ao intermediador a cobrança do PAG-000773$")]
    public async Task CobrancaNoIntermediador() => await ExecucaoQueCria();

    // ── Então · RN-305 aviso do banco ──────────────────────────────────────
    [Then(@"^o PAG-000781 fica ""PAGO"" e a FAT-000402 ""PAGA"", com pagaEm ""(\S+)""$")]
    public async Task PagoEPaga(string pagaEm)
    {
        var pagamento = await fat.Pagamento(fat.Real("PAG-000781"));
        var fatura = await fat.Fatura(fat.Real("FAT-000402"));
        Assert.True(Campo(pagamento, "status") == "PAGO" && Campo(fatura, "status") == "PAGA"
            && DateTimeOffset.TryParse(Campo(fatura, "pagaEm"), out var veio) && veio == FaturasDoExemplo.Hora(pagaEm),
            $"esperava o pagamento PAGO e a fatura PAGA com pagaEm {pagaEm}: {pagamento.ToJsonString()} {fatura.ToJsonString()}");
    }

    [Then(@"^a resposta é (\d{3}) e nenhum pagamento ou fatura muda$")]
    public async Task NadaMuda(int status) => await NadaMudaComCodigo(status, null);

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) e nenhum pagamento ou fatura muda$")]
    public async Task NadaMudaComCodigo(int status, string? codigo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        var (id, pagamento, fatura) = antes ?? throw new InvalidOperationException("o Quando não guardou o pagamento e a fatura de antes do aviso");
        var (agora, faturaAgora) = (await fat.Pagamento(id), await fat.Fatura(fat.FaturaDo(id)));
        Assert.True(JsonNode.DeepEquals(pagamento, agora), $"o pagamento mudou: era {pagamento.ToJsonString()}, ficou {agora.ToJsonString()}");
        Assert.True(JsonNode.DeepEquals(fatura, faturaAgora), $"a fatura mudou: era {fatura.ToJsonString()}, ficou {faturaAgora.ToJsonString()}");
    }

    // RN-305: o aviso repetido não soma a fatura recebida duas vezes
    async Task NadaEmDobro()
    {
        var r = Exigir(await fat.Ler(mundo.Admin(), "/pagamento/recebimentos/resumo?competencia=2026-11"), 200, "GET /pagamento/recebimentos/resumo");
        var recebido = r.Corpo?["recebido"];
        Assert.True(Valor(recebido, "valor") == 3698.33m && recebido?["faturas"]?.GetValue<int>() == 1 && r.Corpo?["duplicidades"]?.GetValue<int>() == 0,
            $"esperava a fatura recebida uma vez só (3698.33, 1 fatura, 0 duplicidades): {r}");
    }

    [Then(@"^a resposta é 404, porque o endereço não existe$")]
    public void EnderecoQueNaoExiste() => Respostas.Confere(mundo.Ultima, 404);

    [Then(@"^só o aviso do banco ou a consulta do deboaz ao banco confirma um pagamento$")]
    public async Task SegueAguardando()
    {
        var pagamento = await fat.Pagamento(fat.Real("PAG-000781"));
        Assert.True(Campo(pagamento, "status") == "AGUARDANDO", $"o pagador não confirma: o PIX devia seguir AGUARDANDO: {pagamento.ToJsonString()}");
    }

    // ── Então · RNF-238 pagamento ao vivo ──────────────────────────────────
    [Then(@"^o evento ""pagamento"" com o status ""(\w+)"" chega em até (\d+) s$")]
    public async Task EventoDoPagamento(string status, int segundos)
    {
        var fluxo = aoVivo.Ultimo;
        var evento = await fluxo.Esperar(e => e.Tipo == "pagamento" && Campo(e.Corpo, "status") == status, TimeSpan.FromSeconds(segundos) + Folga);
        Assert.True(evento is not null, $"o evento \"pagamento\" com o status {status} não chegou: {fluxo}");
        Assert.True(evento.Quando - avisoEnviadoEm <= TimeSpan.FromSeconds(segundos),
            $"o evento chegou {(evento.Quando - avisoEnviadoEm).TotalSeconds:F1} s depois do aviso, esperava até {segundos} s");
    }

    [Then(@"^o fluxo fecha depois do status final: ""PAGO"", ""RECUSADO"", ""EXPIRADO"" ou ""CANCELADO""$")]
    public async Task FluxoFecha()
    {
        var fluxo = aoVivo.Ultimo;
        Assert.True(await fluxo.EsperarFim(CincoSegundos + Folga), $"o fluxo devia fechar depois do status final: {fluxo}");
    }

    [Then(@"^cada evento traz o traceId da execução que mudou o status.*$")]
    public void TraceIdDoEvento()
    {
        var eventos = aoVivo.Ultimo.Eventos.Where(e => e.Tipo == "pagamento").ToList();
        Assert.True(eventos.Count > 0, $"nenhum evento \"pagamento\": {aoVivo.Ultimo}");
        Assert.All(eventos, e => Assert.Matches("^trc-[0-9a-f]{8}$", Campo(e.Corpo, "traceId") ?? ""));
        var pago = eventos.Last(e => Campo(e.Corpo, "status") == "PAGO");
        Assert.Equal(Suporte.Auditoria.TraceIdDoCabecalho(banco.RespostaDoAviso!), Campo(pago.Corpo, "traceId"));
    }

    [Then(@"^o evento ""pagamento"" traz:$")]
    public async Task EventoTraz(string json)
    {
        var fluxo = aoVivo.Ultimo;
        var evento = await fluxo.EsperarTipo("pagamento", CincoSegundos + Folga);
        Assert.True(evento is not null, $"o evento \"pagamento\" não chegou: {fluxo}");
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), evento.Corpo);
    }

    // ── Então · RN-309 auditoria ───────────────────────────────────────────
    // "como na RN-178": o INICIADO e o CONCLUIDO da ação, com o mesmo traceId,
    // que é o do X-Trace-Id quando há resposta (o agendamento não tem)
    [Then(@"^a coleção ""auditoria"" ganha os registros da ação ""(\w+)"", como na RN-178$")]
    public async Task Auditado(string acao)
    {
        var novos = await DaAcao(acao);
        var etapas = novos.Select(d => Suporte.Auditoria.Texto(d, "etapa")).ToList();
        Assert.True(etapas.Contains("INICIADO") && etapas.Contains("CONCLUIDO"),
            $"esperava INICIADO e CONCLUIDO de {acao} em \"auditoria\", vieram {Suporte.Auditoria.Resumo(novos)}");
        var traceId = Suporte.Auditoria.MesmoTraceId(novos);
        if (acao != "FATURA_VENCIDA") Assert.Equal(Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima), traceId);
    }

    async Task<List<BsonDocument>> DaAcao(string acao, int? execucao = null) =>
        [.. (execucao is { } i ? await execucoes.Novos(Suporte.Auditoria.Registros, i) : await execucoes.Novos(Suporte.Auditoria.Registros))
            .Where(d => Suporte.Auditoria.Texto(d, "acao") == acao)];

    // ── Então · RNF-243 X-Trace-Id ─────────────────────────────────────────
    [Then(@"^a resposta é 200, na abertura, com o cabeçalho X-Trace-Id$")]
    public void AberturaComTraceId()
    {
        Assert.True(aoVivo.Ultimo.Status == 200 && aoVivo.Ultimo.TipoDoConteudo == "text/event-stream", $"esperava o fluxo aberto: {aoVivo.Ultimo}");
        Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima);
    }

    [Then(@"^o valor segue o formato ""trc-"" mais 8 hexadecimais, como ""trc-1d6e3f08""$")]
    public void FormatoDoTraceId() => Assert.Matches("^trc-[0-9a-f]{8}$", Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima));

    // mais uma execução, de outro pedido: nenhum traceId se repete
    [Then(@"^cada execução tem o seu, diferente do de qualquer outra$")]
    public async Task UmPorExecucao()
    {
        var outra = await fat.Ler(await fat.Rita(), "/pagamento/formas");
        var traceIds = execucoes.Resultados.Append(outra).Select(Suporte.Auditoria.TraceIdDoCabecalho).ToList();
        Assert.True(traceIds.Distinct().Count() == traceIds.Count, $"esperava um traceId por execução, vieram {string.Join(", ", traceIds)}");
    }

    // "trc-7f3a9c21" do exemplo é o X-Trace-Id da resposta: o corpo traz o mesmo
    [Then(@"^a resposta é (\d{3}) com o cabeçalho X-Trace-Id ""trc-7f3a9c21"" e:$")]
    public async Task ErroComOMesmoTraceId(int status, string json)
    {
        Respostas.Confere(mundo.Ultima, status);
        var traceId = Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        Assert.Matches("^trc-[0-9a-f]{8}$", traceId);
        Comparacao.Confere(JsonNode.Parse(json.Replace("trc-7f3a9c21", traceId, StringComparison.Ordinal)), mundo.Ultima.Corpo);
    }

    // o 503 é o do banco sem resposta; o 500 não se provoca de fora, numa suíte
    // caixa-preta: fica nos testes do backend
    [Then(@"^o 503 BANCO_INDISPONIVEL e o 500 também trazem o traceId no corpo$")]
    public async Task IndisponivelComTraceId()
    {
        await banco.Proximo(BancoFalso.SemResposta);
        var r = await mundo.EnviarComo(await fat.Rita(), "POST", $"/pagamento/faturas/{fat.Real("FAT-000402")}/pagamentos",
            FaturasDoExemplo.Corpo("PIX").ToJsonString());
        Respostas.Confere(r, 503, "BANCO_INDISPONIVEL");
        Assert.True(Campo(r.Corpo, "traceId") == Suporte.Auditoria.TraceIdDoCabecalho(r), $"esperava o traceId do cabeçalho no corpo do 503: {r}");
    }

    [Then(@"^a resposta traz um X-Trace-Id novo, ""trc-1d6e3f08""$")]
    public void TraceIdNovo()
    {
        var traceId = Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        Assert.Matches("^trc-[0-9a-f]{8}$", traceId);
        Assert.True(traceId != origem, $"a execução nova devia ter o seu traceId, veio o recebido ({origem})");
    }

    // o traceIdOrigem fica no pagamento, sem campo no contrato que o mostre:
    // aqui, a nova tentativa é aceita; o campo, nos testes do backend
    [Then(@"^a execução nova guarda ""trc-9a20d5be"" como traceIdOrigem$")]
    [Then(@"^o valor fora do formato é ignorado e o pedido segue sem recusa$")]
    public void NovaTentativaAceita() => Respostas.Confere(mundo.Ultima, 201);

    // ── Então · RNF-244 rastreio das transações ────────────────────────────
    // o traceId da criação aparece nas tentativas da fatura (RN-304)
    [Then(@"^o PAG-000773 guarda o traceId ""trc-1d6e3f08"" de quando foi criado$")]
    public async Task TraceIdDaCriacao()
    {
        var id = fat.Real("PAG-000773");
        var fatura = await fat.Fatura(fat.FaturaDo(id));
        var tentativa = (fatura["pagamentos"]?.AsArray() ?? []).FirstOrDefault(p => Campo(p, "id") == id);
        Assert.True(Campo(tentativa, "traceId") is { } traceId && traceId == fat.ExecucaoDe(id),
            $"esperava o {id} com o traceId da criação ({fat.ExecucaoDe(id)}): {fatura.ToJsonString()}");
    }

    // o historico de cada documento, avisosDoBanco e o traceIdOrigem
    [Then(@"^cada mudança de status do PAG-000773 entra no histórico com o traceId de quem a fez$")]
    [Then(@"^o mesmo vale para a fatura, o estorno e o aviso do banco$")]
    public static void HistoricoSoNoBanco()
    {
        // só o banco de dados mostra: conferido nos testes do backend
    }

    // a auditoria da execução que criou o pagamento e o erro de uma execução
    // recusada (a fatura já paga), cada um com o traceId do seu X-Trace-Id
    [Then(@"^a auditoria da RN-309 e os erros da 17 RN-270 dessa execução têm ""trc-1d6e3f08""$")]
    public async Task AuditoriaEErrosDaExecucao()
    {
        var id = fat.Real("PAG-000773");
        var registros = await DaAcao("PAGAMENTO_CRIADO", execucaoDoPagamento);
        Assert.True(registros.Count > 0 && registros.All(d => Suporte.Auditoria.TraceId(d) == fat.ExecucaoDe(id)),
            $"esperava a auditoria PAGAMENTO_CRIADO com o traceId {fat.ExecucaoDe(id)}, vieram {Suporte.Auditoria.Resumo(registros)}");
        await Executar(await fat.Rita(), "POST", $"/pagamento/faturas/{fat.FaturaDo(id)}/pagamentos", FaturasDoExemplo.Corpo("PIX").ToJsonString());
        Respostas.Confere(mundo.Ultima, 409, "FATURA_PAGA");
        Assert.Equal(Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima), Suporte.Auditoria.TraceId(await execucoes.UmNovoErro()));
    }

    // o que o intermediador falso recebeu na cobrança
    [Then(@"^o pedido leva o cabeçalho X-Trace-Id ""trc-1d6e3f08""$")]
    public async Task TraceIdNoPedidoAoBanco()
    {
        var id = fat.Real("PAG-000773");
        var pedido = await banco.PedidoDe(id);
        Assert.True(Campo(pedido, "traceId") == fat.ExecucaoDe(id), $"esperava o pedido ao banco com o X-Trace-Id {fat.ExecucaoDe(id)}: {pedido.ToJsonString()}");
    }

    [Then(@"^os metadados da cobrança levam o pagamento ""PAG-000773"" e o traceId ""trc-1d6e3f08""$")]
    public async Task MetadadosDaCobranca()
    {
        var id = fat.Real("PAG-000773");
        var metadados = (await banco.PedidoDe(id))["metadados"];
        Assert.True(Campo(metadados, "pagamento") == id && Campo(metadados, "traceId") == fat.ExecucaoDe(id),
            $"esperava os metadados com o pagamento {id} e o traceId {fat.ExecucaoDe(id)}: {metadados?.ToJsonString()}");
    }

    // o aviso é outra execução: o X-Trace-Id dele é novo, diferente do da criação
    [Then(@"^a resposta é 200 com o X-Trace-Id ""trc-6b0c42f1"", novo$")]
    public void AvisoComTraceIdNovo()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var traceId = Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        Assert.Matches("^trc-[0-9a-f]{8}$", traceId);
        Assert.True(traceId != fat.ExecucaoDe(fat.Real("PAG-000781")), $"o aviso devia ter um traceId novo, veio o da criação ({traceId})");
    }

    // avisosDoBanco (IntermediadorPassos.cs) e a auditoria do aviso levam o traceId da resposta
    [Then(@"^avisosDoBanco guarda ""trc-6b0c42f1"", o id externo ""pix_8c1e5d"" e o ""PAG-000781""$")]
    public async Task AvisoGuardado()
    {
        var pix = fat.Real("PAG-000781");
        var aviso = await AvisoGuardado(DoBackend(AvisosDoBanco), mundo.Ultima);
        Assert.True(Suporte.Auditoria.Texto(aviso, "idExterno") == await banco.IdExterno(pix) && Suporte.Auditoria.Texto(aviso, "pagamentoId") == pix,
            $"esperava o aviso com o id externo do {pix} e o pagamentoId {pix}: {aviso.ToJson()}");
        var registros = await DaAcao("AVISO_DO_BANCO");
        Assert.True(registros.Count > 0 && registros.All(d => Suporte.Auditoria.TraceId(d) == Suporte.Auditoria.TraceIdDoCabecalho(mundo.Ultima)),
            $"esperava a auditoria AVISO_DO_BANCO com o traceId do aviso, vieram {Suporte.Auditoria.Resumo(registros)}");
    }

    // o traceId da criação, devolvido pelo banco nos metadados, vira o traceIdOrigem do aviso
    [Then(@"^""trc-2e7d90aa"", da criação, fica como traceIdOrigem$")]
    public async Task TraceIdOrigemDoAviso()
    {
        var pix = fat.Real("PAG-000781");
        var aviso = await AvisoGuardado(DoBackend(AvisosDoBanco), mundo.Ultima);
        Assert.True(Suporte.Auditoria.Texto(aviso, "traceIdOrigem") == fat.ExecucaoDe(pix),
            $"esperava em \"avisosDoBanco\" o traceIdOrigem {fat.ExecucaoDe(pix)}, o da criação do {pix}: {aviso.ToJson()}");
    }

    // a ligação entre o id externo e o traceId fica no historico; a API mostra o pagamento confirmado pelo aviso
    [Then(@"^o PAG-000781 liga ""pix_8c1e5d"" a ""trc-6b0c42f1""$")]
    public async Task PagoPeloAviso()
    {
        var pagamento = await fat.Pagamento(fat.Real("PAG-000781"));
        Assert.True(Campo(pagamento, "status") == "PAGO", $"esperava o PAG-000781 PAGO pelo aviso: {pagamento.ToJsonString()}");
    }

    // a execução do agendamento tem o seu traceId, que não é o de nenhum pedido
    [Then(@"^essa execução tem traceId próprio, como ""trc-0f3b6a7d""$")]
    public async Task TraceIdDoAgendamento()
    {
        var registros = await DaAcao("FATURA_VENCIDA");
        Assert.True(registros.Count > 0, "esperava a auditoria FATURA_VENCIDA do agendamento");
        var traceId = Suporte.Auditoria.MesmoTraceId(registros);
        Assert.Matches("^trc-[0-9a-f]{8}$", traceId);
        Assert.DoesNotContain(traceId, execucoes.Resultados.Select(r => r.Cabecalho("X-Trace-Id")));
    }

    // a mudança de status fica no historico da fatura (só o banco mostra); a auditoria tem a fatura como alvo
    [Then(@"^ele fica na mudança de status da FAT-000371 e na auditoria FATURA_VENCIDA$")]
    public async Task TraceIdNaAuditoriaDaVirada()
    {
        var fatura = fat.Real("FAT-000371");
        var registros = await DaAcao("FATURA_VENCIDA");
        Assert.True(registros.Any(d => Suporte.Auditoria.Texto(d, "alvo") == fatura),
            $"esperava a auditoria FATURA_VENCIDA com o alvo {fatura}, vieram {Suporte.Auditoria.Resumo(registros)}");
    }
}

// As frases do exemplo citam "usr-ebfga-rita" e "conta-pedro-reis" também em
// passos de outras features (o PUT das permissões da RN-299 é o do 17-b): as
// faturas do exemplo nascem com o cenário, para os apelidos existirem
[Binding]
public sealed class PagamentoNoComeco(FaturasDoExemplo faturas)
{
    [BeforeScenario]
    [Scope(Feature = "Pagamento")]
    public void Apelidos() => _ = faturas;
}
