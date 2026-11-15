using System.Globalization;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Hub.Sac;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// O recebedor (o administrador do deboaz) nos Cenários de API de
// 1_Requisitos/19_modulo-pagamento.md (v04): recebimentos (RF-190), faturas de
// todas as contas (RF-191), estorno (RF-192, RN-306), configuração (RF-193) e
// o card ao vivo (RN-307). Os pedidos do Quando são os de PagamentoPassos.cs.
public sealed partial class PagamentoPassos
{
    const string VencidasDeOutubro = "/pagamento/recebimentos/faturas?competencia=2026-10&situacao=VENCIDA&pagina=1";

    // quantos itens da ordem das vencidas as frases já conferiram (RF-191)
    int conferidos;

    // ── Quando ─────────────────────────────────────────────────────────────
    // RF-193: o corpo do GET com o boleto desligado, no If-Match da versão lida (a "4" do exemplo)
    [When(@"^o mesmo corpo, com ""BOLETO"": false$")]
    public async Task MesmoCorpoSemBoleto()
    {
        var (quem, metodo, caminho) = pendente ?? throw new InvalidOperationException("nenhum PUT da configuração à espera do corpo");
        var lida = await fat.Configuracao();
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
    [Then(@"^a resposta é 200 com (\d+) itens, o vencimento mais antigo primeiro$")]
    public void ItensPeloVencimento(int quantos)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var itens = Itens();
        Assert.True(itens.Count == quantos && mundo.Ultima.Corpo?["total"]?.GetValue<int>() == quantos, $"esperava {quantos} itens: {mundo.Ultima}");
        PeloVencimentoEPeloNumero(itens);
    }

    // os números reais não são os do exemplo: vale a ordem
    static void PeloVencimentoEPeloNumero(List<JsonObject> itens)
    {
        var ordem = itens.Select(i => (Vencimento: Campo(i, "vencimento"), Id: Campo(i, "id"))).ToList();
        var esperada = ordem.OrderBy(i => i.Vencimento, StringComparer.Ordinal).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
        Assert.True(ordem.SequenceEqual(esperada),
            $"esperava do vencimento mais antigo e, no mesmo vencimento, do menor número, vieram {string.Join(", ", ordem)}");
    }

    // o cenário só tem Então: as 9 vencidas de outubro e o pedido do administrador
    [Then(@"^os 9 itens vêm do vencimento mais antigo e, no mesmo vencimento, do menor número:$")]
    public async Task OrdemDasVencidas()
    {
        await fat.Outubro(inteiro: false);
        await Executar(mundo.Admin(), "GET", VencidasDeOutubro, null);
        ItensPeloVencimento(9);
    }

    // as duas linhas de números do exemplo: 5 e 4 itens, todos vencidos, a FAT-000371 entre eles
    [Then(@"^((?:FAT-\d{6}, )+FAT-\d{6})$")]
    [Then(@"^((?:FAT-\d{6}, )+FAT-\d{6} e FAT-\d{6})$")]
    public void NumerosDaOrdem(string numeros)
    {
        var quantos = numeros.Split("FAT-").Length - 1;
        var itens = Itens();
        var trecho = itens.Skip(conferidos).Take(quantos).ToList();
        Assert.True(trecho.Count == quantos && trecho.All(i => Campo(i, "status") == "VENCIDA"),
            $"esperava mais {quantos} faturas vencidas depois das {conferidos} primeiras: {mundo.Ultima}");
        conferidos += quantos;
        if (conferidos == itens.Count) Assert.Contains(fat.Real("FAT-000371"), itens.Select(i => Campo(i, "id")));
    }

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
    // o contrato não tem endereço que mostre o estorno: o "CONCLUIDO" aparece
    // no "devolvido" da fatura; o "RECUSADO", no devolvido que não muda
    [Then(@"^o PAG-000773 fica ""(\w+)"" e o estorno fica ""(CONCLUIDO|RECUSADO)""$")]
    public async Task DepoisDoBanco(string status, string estorno)
    {
        var id = fat.Real("PAG-000773");
        var pagamento = await fat.Pagamento(id);
        Assert.True(Campo(pagamento, "status") == status, $"esperava o PAG-000773 em {status}: {pagamento.ToJsonString()}");
        var fatura = await fat.Fatura(fat.FaturaDo(id));
        var devolvido = estorno == "CONCLUIDO" ? 1473.92m : 0m;
        Assert.True(Valor(fatura, "devolvido") == devolvido, $"esperava a fatura com devolvido {devolvido}: {fatura.ToJsonString()}");
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
    // "a versão 5" do exemplo é a seguinte à que foi lida e enviada no If-Match
    [Then(@"^a resposta é 200 com a versão 5 e o boleto false$")]
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
