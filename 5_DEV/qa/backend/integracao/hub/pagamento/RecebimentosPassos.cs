using System.Globalization;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Hub.Sac;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// O recebedor (o administrador do deboaz) nos Cenários de API de
// 1_Requisitos/19_modulo-pagamento.md (v05): recebimentos (RF-190), faturas de
// todas as contas (RF-191), pagamento e estornos (RF-192, RN-306),
// configuração (RF-193) e o card ao vivo (RN-307). Os pedidos do Quando são os
// de PagamentoPassos.cs; os números do card, de CardDoPagamentoPassos.cs.
public sealed partial class PagamentoPassos
{
    // RF-191, ordem das vencidas: A, B e C, pelos números reais
    readonly List<string> ordem = [];

    // ── Quando ─────────────────────────────────────────────────────────────
    // RF-193: o corpo do GET com o boleto desligado, no If-Match da versão lida (a "4" do exemplo)
    [Given(@"^a configuração lida pelo administrador, como a versão 4 acima$")]
    public async Task ConfiguracaoLida() => configuracaoLida = await fat.Configuracao();

    [When(@"^o mesmo corpo, com ""BOLETO"": false$")]
    public async Task MesmoCorpoSemBoleto()
    {
        var (quem, metodo, caminho) = pendente ?? throw new InvalidOperationException("nenhum PUT da configuração à espera do corpo");
        var lida = configuracaoLida ?? await fat.Configuracao();
        versaoEnviada = lida["versao"]!.GetValue<int>();
        await Executar(quem, metodo, caminho, FaturasDoExemplo.CorpoDaConfiguracao(lida, c => c["formas"]!["BOLETO"] = false).ToJsonString(),
            versaoEnviada.ToString(CultureInfo.InvariantCulture));
    }

    // RN-306: o valor do pedido de estorno da frase anterior
    [When(@"^o valor (\d+\.\d+)$")]
    public async Task ValorDoEstorno(string valor)
    {
        var (quem, metodo, caminho) = pendente ?? throw new InvalidOperationException("nenhum pedido de estorno à espera do valor");
        await Executar(quem, metodo, caminho, FaturasDoExemplo.CorpoDoEstorno(decimal.Parse(valor, CultureInfo.InvariantCulture)).ToJsonString());
    }

    [When(@"^o administrador envia outro POST de estorno do PAG-000773$")]
    public async Task OutroEstorno() =>
        await Executar(mundo.Admin(), "POST", $"/pagamento/recebimentos/pagamentos/{fat.Real("PAG-000773")}/estornos",
            FaturasDoExemplo.CorpoDoEstorno(100.00m).ToJsonString());

    // RF-192: o estorno de 1.473,92 do crédito pago e a resposta do banco
    [When(@"^o banco (confirma|recusa) o EST-000012$")]
    public async Task BancoRespondeAoEstorno(string resposta)
    {
        var pagamento = await fat.CreditoPago("PAG-000773");
        var estorno = fat.Conhecido("EST-000012") ? fat.Real("EST-000012") : await fat.Estornar(pagamento, exemplo: "EST-000012");
        var tipo = resposta == "confirma" ? BancoFalso.EstornoConcluido : BancoFalso.EstornoRecusado;
        Respostas.Confere(await Avisar(BancoFalso.Aviso(tipo, await banco.IdExterno(estorno), fat.Agora())), 200);
    }

    // RF-192: o crédito pago às 13:42 de 2026-12-03 pela Rita (a execução
    // "trc-1d6e3f08"), o estorno pedido ao meio-dia de 2026-12-04 e
    // confirmado pelo banco às 15:10
    [Given(@"^o EST-000012 confirmado pelo banco em (\S+)$")]
    public async Task EstornoConfirmado(string quando)
    {
        await fat.Fat402();
        await fat.Saltar("2026-12-03T13:42:00Z");
        Execucao("trc-1d6e3f08", await fat.CreditoPago("PAG-000773"));
        await fat.Saltar("2026-12-04T12:00:00Z");
        var estorno = await fat.Estornar(fat.Real("PAG-000773"), exemplo: "EST-000012");
        var em = FaturasDoExemplo.Hora(quando);
        await fat.Saltar(em);
        Exigir(await banco.Avisar(estorno, BancoFalso.EstornoConcluido, em), 200, $"POST /pagamento/notificacoes (estorno {estorno} concluído)");
    }

    // RF-191: A da EBFGA, B e C do Pedro e da COLSA; os três emitidos no mesmo
    // fechamento. O vencimento é sempre dia 10 (RF-193): o de A, dia 8, só se
    // prepara no documento da fatura (modelo pagamento-documentos v03)
    [Given(@"^três faturas vencidas de ""(\d{4}-\d{2})"": A, com vencimento em (\d{4}-\d{2}-\d{2})$")]
    public async Task TresVencidas(string competencia, string vencimentoDeA)
    {
        await fat.Saltar($"{competencia}-28T12:00:00Z");
        var donos = new List<(Pessoa Dono, string Conta, string Sigla)>
        {
            (await mundo.Master(), await mundo.Conta(), "EBFGA"),
            (await mundo.Pedro(), await mundo.ContaDoPedro(), "PREIS"),
            (await mundo.MasterDe("COLSA"), await mundo.IdDaConta("COLSA"), "COLSA"),
        };
        foreach (var (_, conta, sigla) in donos) await Estado.Cobranca(conta, sigla, "MOD-03", competencia, 500.00m);
        await fat.Fechar(competencia);
        var ids = new List<string>();
        foreach (var (dono, _, _) in donos) ids.Add(await fat.FaturaDa(dono, competencia));
        await Estado.Atualizar("faturas", ids[0], "vencimento", vencimentoDeA);
        ordem.Add(ids[0]);
        ordem.AddRange(ids.Skip(1).Order(StringComparer.Ordinal));
    }

    [Given(@"^B e C, com vencimento em 2026-11-10, B emitida antes de C e com número menor$")]
    public async Task BeCVencidas()
    {
        await fat.Vencer("2026-10");
        await fat.Saltar("2026-11-14");
    }

    [When(@"^o administrador pede as vencidas de ""(\d{4}-\d{2})"", como no cenário acima$")]
    public async Task PedeAsVencidas(string competencia) =>
        await Executar(mundo.Admin(), "GET", $"/pagamento/recebimentos/faturas?competencia={competencia}&situacao=VENCIDA&pagina=1", null);

    // ── Então · RF-190 recebimentos ────────────────────────────────────────
    [Then(@"^a resposta é 200 com as 12 competências de ""(\d{4}-\d{2})"" a ""(\d{4}-\d{2})"", a mais nova primeiro$")]
    public void DozeCompetencias(string primeira, string ultima)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var competencias = (mundo.Ultima.Corpo as JsonArray ?? []).Select(c => Campo(c, "competencia")).ToList();
        var mes = DateTime.ParseExact(ultima, "yyyy-MM", CultureInfo.InvariantCulture);
        var esperadas = Enumerable.Range(0, 12).Select(i => mes.AddMonths(-i).ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(primeira, esperadas[^1]);
        Assert.True(competencias.SequenceEqual(esperadas), $"esperava as 12 competências de {ultima} a {primeira}: {mundo.Ultima}");
    }

    [Then(@"^cada uma com competencia, faturado, recebido, vencido e inadimplencia$")]
    public void CamposDaCompetencia() =>
        Assert.All((mundo.Ultima.Corpo as JsonArray ?? []).OfType<JsonObject>(), c =>
            Assert.True(new[] { "competencia", "faturado", "recebido", "vencido", "inadimplencia" }.All(c.ContainsKey),
                $"esperava competencia, faturado, recebido, vencido e inadimplencia: {c.ToJsonString()}"));

    // ── Então · RF-191 faturas de todas as contas ──────────────────────────
    // as 9 vencidas de outubro do exemplo (FaturasDoExemplo.Outubro), e nada mais
    [Then(@"^a resposta é 200 só com faturas ""(\w+)"" de ""(\d{4}-\d{2})"" e total com quantas são$")]
    public void SoAsDaSituacao(string situacao, string competencia)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var itens = Itens();
        Assert.True(itens.Count == 9 && mundo.Ultima.Corpo?["total"]?.GetValue<int>() == itens.Count
            && itens.All(i => Campo(i, "status") == situacao && Campo(i, "competencia") == competencia),
            $"esperava as 9 faturas {situacao} de {competencia} e o total delas: {mundo.Ultima}");
    }

    [Then(@"^A vem antes de B, e B antes de C$")]
    public void ABeC()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var vieram = Itens().Select(i => Campo(i, "id")).ToList();
        Assert.True(vieram.SequenceEqual(ordem), $"esperava {string.Join(", ", ordem)} (A, B e C), vieram {string.Join(", ", vieram)}");
    }

    // RF-191: o detalhe do recebedor é o mesmo do pagador
    [Then(@"^a resposta é 200 com o mesmo corpo de GET /pagamento/faturas/(FAT-\d{6}) da conta$")]
    public async Task MesmoCorpoDaConta(string exemplo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var doRecebedor = mundo.Ultima.Corpo;
        var daConta = await fat.Fatura(fat.Real(exemplo));
        Assert.True(JsonNode.DeepEquals(daConta, doRecebedor), $"esperava o corpo da conta {daConta.ToJsonString()}, veio {doRecebedor?.ToJsonString()}");
    }

    [Then(@"^a FAT-999999 dá 404 FATURA_INEXISTENTE$")]
    public async Task DetalheQueNaoExiste() =>
        Respostas.Confere(await fat.Ler(mundo.Admin(), "/pagamento/recebimentos/faturas/FAT-999999"), 404, "FATURA_INEXISTENTE");

    [Then(@"^a resposta é 200 com a FAT-000402, a FAT-000318 e a FAT-000247$")]
    public void AsTresDaEbfga()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperadas = new[] { "FAT-000402", "FAT-000318", "FAT-000247" }.Select(fat.Real).Order().ToList();
        var vieram = Itens().Select(i => Campo(i, "id")).Order().ToList();
        Assert.True(vieram.SequenceEqual(esperadas), $"esperava as faturas {string.Join(", ", esperadas)}: {mundo.Ultima}");
    }

    [Then(@"^a busca vale para a sigla, o nome da conta e o número da fatura$")]
    public async Task BuscaPorNomeENumero()
    {
        var porNome = Exigir(await fat.Ler(mundo.Admin(), "/pagamento/recebimentos/faturas?busca=Bento&situacao=TODAS"), 200, "busca pelo nome da conta");
        Assert.True((porNome.Corpo?["itens"]?.AsArray() ?? []).Select(i => Campo(i, "id")).Order()
            .SequenceEqual(new[] { "FAT-000402", "FAT-000318", "FAT-000247" }.Select(fat.Real).Order()), $"a busca por \"Bento\" devia trazer as 3 da EBFGA: {porNome}");
        var numero = fat.Real("FAT-000402");
        var porNumero = Exigir(await fat.Ler(mundo.Admin(), $"/pagamento/recebimentos/faturas?busca={numero}&situacao=TODAS"), 200, "busca pelo número");
        Assert.True((porNumero.Corpo?["itens"]?.AsArray() ?? []).Select(i => Campo(i, "id")).SequenceEqual([numero]),
            $"a busca por \"{numero}\" devia trazer só ela: {porNumero}");
    }

    [Then(@"^GET /pagamento/faturas/(FAT-\d{6}) do Pedro traz o status ""CANCELADA"" e o motivo$")]
    public async Task CanceladaParaOPedro(string exemplo)
    {
        var fatura = await fat.Fatura(await Num(exemplo), await mundo.Pedro());
        Assert.True(Campo(fatura, "status") == "CANCELADA" && Campo(fatura, "motivo") == FaturasDoExemplo.MotivoDoCancelamento,
            $"esperava a fatura CANCELADA com o motivo \"{FaturasDoExemplo.MotivoDoCancelamento}\": {fatura.ToJsonString()}");
    }

    // ── Então · RF-192 e RN-306 estorno ────────────────────────────────────
    [Then(@"^GET /pagamento/recebimentos/pagamentos/(PAG-\d{6}) traz o status ""(\w+)""$")]
    public async Task StatusParaORecebedor(string exemplo, string status)
    {
        var pagamento = await fat.PagamentoDoRecebedor(fat.Real(exemplo));
        Assert.True(Campo(pagamento, "status") == status, $"esperava o {exemplo} em {status}: {pagamento.ToJsonString()}");
    }

    [Then(@"^o (EST-\d{6}) em ""estornos"" com o status ""(\w+)""$")]
    public async Task EstornoNaLista(string exemplo, string status)
    {
        var pagamento = await fat.PagamentoDoRecebedor(fat.Real("PAG-000773"));
        var estorno = (pagamento["estornos"]?.AsArray() ?? []).FirstOrDefault(e => Campo(e, "id") == fat.Real(exemplo));
        Assert.True(estorno is not null && Campo(estorno, "status") == status,
            $"esperava o {exemplo} em \"estornos\" com o status {status}: {pagamento.ToJsonString()}");
    }

    [Then(@"^GET /pagamento/pagamentos/(PAG-\d{6}) da conta traz a mesma lista ""estornos""$")]
    public async Task MesmosEstornosParaAConta(string exemplo)
    {
        var id = fat.Real(exemplo);
        var (doRecebedor, daConta) = (await fat.PagamentoDoRecebedor(id), await fat.Pagamento(id));
        Assert.True(daConta["estornos"] is JsonArray && JsonNode.DeepEquals(doRecebedor["estornos"], daConta["estornos"]),
            $"esperava os mesmos \"estornos\": {doRecebedor["estornos"]?.ToJsonString()} e {daConta["estornos"]?.ToJsonString()}");
    }

    [Then(@"^a resposta é 422 ESTORNO_ACIMA_DO_SALDO, com o saldo (\d+\.\d{2})$")]
    public void AcimaDoSaldo(string saldo)
    {
        Respostas.Confere(mundo.Ultima, 422, "ESTORNO_ACIMA_DO_SALDO");
        Assert.True(Valor(mundo.Ultima.Corpo, "saldo") == decimal.Parse(saldo, CultureInfo.InvariantCulture), $"esperava o saldo {saldo}: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 409 ESTORNO_PELO_SAC, porque o PAG-000790 é boleto$")]
    public async Task PeloSac()
    {
        Respostas.Confere(mundo.Ultima, 409, "ESTORNO_PELO_SAC");
        Assert.Equal("BOLETO", Campo(await fat.Pagamento(fat.Real("PAG-000790")), "forma"));
    }

    // RN-312: o Pagamento não abre chamado; quem pagou abre, pelo endereço do SAC (18 RF-171)
    [Then(@"^nenhum chamado é aberto: quem pagou abre pelo POST /sac/chamados, na categoria ""(\w+)""$")]
    public async Task NenhumChamado(string categoria)
    {
        var master = await mundo.Master();
        var antes = Exigir(await fat.Ler(master, "/sac/chamados"), 200, "GET /sac/chamados da conta master");
        Assert.True(antes.Corpo?["total"]?.GetValue<int>() == 0, $"o estorno recusado não devia abrir chamado: {antes}");
        var aberto = await mundo.EnviarComo(master, "POST", "/sac/chamados",
            ChamadosDoExemplo.Corpo("Estorno do boleto da fatura", categoria).ToJsonString());
        Respostas.Confere(aberto, 201);
    }

    [Then(@"^o administrador que tenta abrir em nome da conta recebe 403, como no 18 RF-171-api$")]
    public async Task AdministradorNaoAbre() =>
        Respostas.Confere(await mundo.EnviarComo(mundo.Admin(), "POST", "/sac/chamados",
            ChamadosDoExemplo.Corpo("Estorno do boleto da fatura", "COBRANCA").ToJsonString()), 403);

    // ── Então · RF-193 configuração ────────────────────────────────────────
    // "a 5" do exemplo é a seguinte à que foi lida e enviada no If-Match
    [Then(@"^a resposta é 200 com a versão lida \+ 1, a 5, e o boleto false$")]
    public void BoletoDesligadoNaVersaoNova()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Corpo?["versao"]?.GetValue<int>() == versaoEnviada + 1
            && mundo.Ultima.Corpo["formas"]?["BOLETO"]?.GetValueKind() == System.Text.Json.JsonValueKind.False,
            $"esperava a versão {versaoEnviada + 1} com o boleto false: {mundo.Ultima}");
    }

    [Then(@"^GET /pagamento/formas da conta master deixa de trazer ""(\w+)""$")]
    public async Task FormaDesligada(string forma)
    {
        var r = Exigir(await fat.Ler(await mundo.Master(), "/pagamento/formas"), 200, "GET /pagamento/formas");
        var formas = (r.Corpo?["formas"]?.AsArray() ?? []).Select(f => f?.GetValue<string>()).ToList();
        Assert.True(formas.Count == 3 && !formas.Contains(forma), $"esperava as formas sem {forma}: {r}");
    }

    // o boleto gerado antes de a forma ser desligada segue aguardando e o banco ainda o compensa
    [Then(@"^os boletos ""AGUARDANDO"" seguem válidos até pagos ou expirados$")]
    public async Task BoletosSeguemValidos()
    {
        var boleto = fat.Real("PAG-000785");
        Assert.Equal("AGUARDANDO", Campo(await fat.Pagamento(boleto), "status"));
        await fat.Confirmar(boleto);
        Assert.Equal("PAGO", Campo(await fat.Pagamento(boleto), "status"));
    }

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+), com o campo citado quando há$")]
    public void RecusaDaConfiguracao(int status, string codigo) => Respostas.Confere(mundo.Ultima, status, codigo);

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+), ""([\w.]+)"", com o campo citado quando há$")]
    public void RecusaDaConfiguracaoComCampo(int status, string codigo, string campo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        Assert.True(Campo(mundo.Ultima.Corpo, "campo") == campo, $"esperava o campo \"{campo}\": {mundo.Ultima}");
    }

    // ── Então · RN-307 card ao vivo ────────────────────────────────────────
    [Then(@"^o 1º evento ""card"" chega logo ao abrir, com o corpo de GET /pagamento/card$")]
    public async Task PrimeiroCard()
    {
        var fluxo = aoVivo.Ultimo;
        var card = await fluxo.EsperarTipo("card", Folga);
        Assert.True(card is not null, $"o primeiro \"card\" não chegou em {Folga.TotalSeconds} s: {fluxo}");
        Assert.True(fluxo.Eventos.FirstOrDefault(e => e.Tipo != "ping") == card, $"o primeiro evento devia ser o \"card\": {fluxo}");
        var lido = Exigir(await fat.Ler(await mundo.Master(), "/pagamento/card"), 200, "GET /pagamento/card").Corpo;
        Comparacao.Confere(lido, card.Corpo);
    }
}
