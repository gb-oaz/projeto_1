using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using MongoDB.Driver;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// O intermediador nos Cenários de API de 1_Requisitos/19_modulo-pagamento.md
// (v05) e no contrato pagamento-contrato-api v03: as formas e a chave
// pública (RF-184), o QR Code e o boleto em PDF (RF-185, RF-188), a coleção
// "avisosDoBanco" (RN-305), o intermediador de cada ambiente e o dono de
// cada aviso (RNF-242), as frases do X-Trace-Id (RNF-243) e os números
// somados (RNF-245, pendente: não há suíte de carga).
//
// Exceções à regra "o banco só prepara" (README, seção 6), porque o
// requisito as nomeia e a API não as mostra: a suíte LÊ a coleção
// "avisosDoBanco" (RN-305, RNF-242) e o campo "intermediador" de
// "pagamentos" (RNF-242), e GRAVA "intermediador": "MERCADO_PAGO" num
// pagamento do falso para fazer de conta que ele veio do intermediador
// anterior. O Mercado Pago só aparece numa instância própria (Partida.cs),
// com segredos de teste, e só recebe avisos: a suíte nunca chama o serviço.
public sealed partial class PagamentoPassos
{
    const string AvisosDoBanco = "avisosDoBanco";
    // o id do pagamento do exemplo do contrato, no Mercado Pago
    const string IdDoMercadoPago = "1325001234";

    // RF-185: o SVG lido no primeiro Então
    string? qrCodeSvg;
    // RNF-242: o caso do Esquema "de qual intermediador é o aviso" e a resposta dele
    (string Caso, Resposta Resposta)? avisoRecebido;
    // RN-305, ocorrências para o time: o traceId de cada aviso e a execução do 401
    readonly Dictionary<string, string> ocorrencias = [];
    int execucaoRecusada = -1;

    // ── apoio ──────────────────────────────────────────────────────────────
    static async Task<List<BsonDocument>> Documentos(IMongoCollection<BsonDocument> colecao) =>
        await colecao.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

    static bool? Booleano(BsonDocument d, string campo) =>
        d.TryGetValue(campo, out var v) && v.IsBoolean ? v.AsBoolean : null;

    // o aviso que "avisosDoBanco" guardou com o traceId da resposta
    static async Task<BsonDocument> AvisoGuardado(IMongoCollection<BsonDocument> colecao, Resposta r)
    {
        var traceId = Auditoria.TraceIdDoCabecalho(r);
        var avisos = await Documentos(colecao);
        var aviso = avisos.FirstOrDefault(d => Auditoria.Texto(d, "traceId") == traceId);
        Assert.True(aviso is not null, $"esperava em \"{AvisosDoBanco}\" o aviso com o traceId {traceId} do X-Trace-Id, vieram {Auditoria.Resumo(avisos)}");
        return aviso;
    }

    static IMongoCollection<BsonDocument> DoBackend(string colecao) =>
        new MongoClient(Ambiente.MongoUrl).GetDatabase(Ambiente.MongoBanco).GetCollection<BsonDocument>(colecao);

    static void ConfereAviso(BsonDocument aviso, string intermediador, bool assinaturaValida, string resultado, string? idExterno = null)
    {
        Assert.True(Auditoria.Texto(aviso, "intermediador") == intermediador && Booleano(aviso, "assinaturaValida") == assinaturaValida
            && Auditoria.Texto(aviso, "resultado") == resultado && (idExterno is null || Auditoria.Texto(aviso, "idExterno") == idExterno),
            $"esperava o aviso de {intermediador}, assinaturaValida {assinaturaValida.ToString().ToLowerInvariant()}, resultado {resultado}"
            + (idExterno is null ? "" : $" e idExterno {idExterno}") + $": {aviso.ToJson()}");
    }

    async Task<BsonDocument> DocumentoDoPagamento(string id)
    {
        var doc = (await Documentos(DoBackend("pagamentos"))).FirstOrDefault(d => Auditoria.Texto(d, "_id") == id);
        Assert.True(doc is not null, $"esperava o pagamento {id} na coleção \"pagamentos\"");
        return doc;
    }

    // RNF-242: a instância de homologação sem o relógio de teste, com ou sem os segredos do Mercado Pago
    async Task ForaDeProducao(bool comAsChaves)
    {
        partida.KeyVaultCompleto("hml");
        partida.KeyVault["Teste__Relogio"] = "";
        foreach (var nome in Partida.ChavesDoMercadoPago.Keys) partida.KeyVault[nome] = comAsChaves ? Partida.ChavesDoMercadoPago[nome] : null;
        await partida.IniciarPronta($"homologação, sem o relógio de teste, {(comAsChaves ? "com" : "sem")} as chaves do Mercado Pago");
    }

    async Task<JsonNode?> CartaoNaPartida()
    {
        var token = await partida.Token(ContasIniciais.Master);
        var r = Exigir(await partida.Api.Enviar("GET", "/pagamento/formas", token: token), 200, "GET /pagamento/formas da conta master da QAINS");
        return r.Corpo?["cartao"];
    }

    // ── Então · RF-184 formas e chave pública ──────────────────────────────
    [Then(@"^""chavePublica"" é a chave pública do intermediador, que a tela usa para gerar o token$")]
    public void ChavePublica()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var cartao = mundo.Ultima.Corpo?["cartao"];
        Assert.True(Campo(cartao, "intermediador") is "FALSO" or "MERCADO_PAGO" && Campo(cartao, "chavePublica") is { Length: > 0 },
            $"esperava \"cartao\" com o intermediador e a chave pública dele: {mundo.Ultima}");
    }

    // o backend do qa: homologação com o relógio de teste
    [Then(@"^fora de produção, com o intermediador falso, vem ""intermediador"": ""FALSO""$")]
    public void IntermediadorFalso()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo(mundo.Ultima.Corpo?["cartao"], "intermediador") == "FALSO", $"esperava \"cartao.intermediador\" \"FALSO\" no backend do qa: {mundo.Ultima}");
    }

    [Then(@"^""enderecoDoBoleto"" traz:$")]
    public void EnderecoDoBoleto(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(JsonNode.Parse(json), mundo.Ultima.Corpo?["enderecoDoBoleto"]);
    }

    // ── Então · RF-185 QR Code ─────────────────────────────────────────────
    // o PIX do exemplo, criado aqui (o cenário só tem o Então). O desenho é
    // conferido pela forma: SVG em vetor, com a área de um QR Code (21 módulos
    // ou mais, de 4 em 4); decodificar o QR Code não é feito aqui
    [Then(@"^""qrCodeSvg"" é o QR Code do ""copiaECola"" desenhado em vetor, com quadrados do SVG$")]
    public async Task QrCodeEmVetor()
    {
        var pix = fat.CorpoDoCriado(await fat.Pix("PAG-000772"))["pix"];
        qrCodeSvg = Campo(pix, "qrCodeSvg") ?? "";
        Assert.True(Campo(pix, "copiaECola") is { Length: > 0 }, $"esperava o \"copiaECola\" do PIX: {pix?.ToJsonString()}");
        var area = ViewBox().Match(qrCodeSvg);
        Assert.True(qrCodeSvg.StartsWith("<svg", StringComparison.Ordinal) && qrCodeSvg.Contains("xmlns=\"http://www.w3.org/2000/svg\"", StringComparison.Ordinal)
            && area.Success && int.Parse(area.Groups[1].Value) >= 21 && area.Groups[1].Value == area.Groups[2].Value
            && (qrCodeSvg.Contains("<rect", StringComparison.Ordinal) || qrCodeSvg.Contains("<path", StringComparison.Ordinal)),
            $"esperava um SVG quadrado com viewBox \"0 0 N N\" (N ≥ 21) e os módulos em <rect> ou <path>: {qrCodeSvg}");
    }

    [Then(@"^não é a imagem do banco embrulhada no SVG$")]
    public void SemImagemEmbrulhada()
    {
        var svg = qrCodeSvg ?? throw new InvalidOperationException("o Então anterior não leu o qrCodeSvg");
        Assert.True(!svg.Contains("<image", StringComparison.OrdinalIgnoreCase) && !svg.Contains("data:", StringComparison.OrdinalIgnoreCase)
            && !svg.Contains("base64", StringComparison.OrdinalIgnoreCase) && !svg.Contains("href", StringComparison.OrdinalIgnoreCase),
            $"o qrCodeSvg não pode embrulhar uma imagem (<image>, data:, base64, href): {svg}");
    }

    [GeneratedRegex(@"viewBox=""0 0 (\d+) (\d+)""")]
    private static partial Regex ViewBox();

    // ── Então · RF-188 boleto ──────────────────────────────────────────────
    // o nome e o documento do pagador saem no PDF do boleto recém-gerado
    [Then(@"^o boleto sai em nome da conta, ""(.+)"", com o documento (\S+)$")]
    public async Task BoletoEmNomeDaConta(string nome, string documento)
    {
        Respostas.Confere(mundo.Ultima, 201);
        var id = Texto(mundo.Ultima, "id");
        var r = Exigir(await fat.Ler(await fat.Rita(), $"/pagamento/pagamentos/{id}/boleto"), 200, $"GET /pagamento/pagamentos/{id}/boleto");
        var pdf = PdfLido.Ler(r.Bytes ?? []);
        Assert.True(pdf.Traz(nome) && pdf.Traz(documento), $"esperava o boleto em nome de \"{nome}\", documento {documento}: {pdf}");
    }

    // intercalado 2 de 5 com os 44 dígitos: 2 barras de início, 5 por par de
    // dígitos (22 pares) e 2 de fim, 114 barras desenhadas no mínimo
    [Then(@"^o PDF traz a linha digitável e o código de barras desenhado, no padrão intercalado 2 de 5$")]
    public async Task PdfDoBoleto()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var pdf = PdfLido.Ler(mundo.Ultima.Bytes ?? []);
        var boleto = (await fat.Pagamento(fat.Real("PAG-000785")))["boleto"];
        var linha = Campo(boleto, "linhaDigitavel") ?? "";
        var codigo = Campo(boleto, "codigoBarras") ?? "";
        Assert.True(linha.Length > 0 && pdf.Traz(linha), $"esperava a linha digitável \"{linha}\" no PDF: {pdf}");
        Assert.True(Regex.IsMatch(codigo, "^[0-9]{44}$"), $"esperava o código de barras com 44 dígitos, veio \"{codigo}\"");
        Assert.True(pdf.Retangulos >= 114 || pdf.Segmentos >= 114,
            $"esperava as 114 barras ou mais do intercalado 2 de 5 desenhadas (retângulos ou segmentos): {pdf}");
    }

    // ── Então · RN-305 avisosDoBanco ───────────────────────────────────────
    [Then(@"^a coleção ""avisosDoBanco"" guarda o aviso com o ""resultado"" ""(\w+)"" e o traceId$")]
    public async Task AvisoComResultado(string resultado)
    {
        var aviso = await AvisoGuardado(DoBackend(AvisosDoBanco), mundo.Ultima);
        ConfereAviso(aviso, "FALSO", resultado != "RECUSADO", resultado);
    }

    // os dois avisos sem efeito do Esquema, cada um com o seu traceId
    async Task Ocorrencias()
    {
        if (ocorrencias.Count > 0) return;
        await AvisoSemEfeito("com a assinatura inválida");
        execucaoRecusada = execucoes.Resultados.Count - 1;
        ocorrencias["RECUSADO"] = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        await AvisoSemEfeito("de pagamento externo desconhecido");
        ocorrencias["DESCONHECIDO"] = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
    }

    [Then(@"^os avisos ""RECUSADO"" e ""DESCONHECIDO"" de ""avisosDoBanco"" são o que o time deboaz confere$")]
    public async Task OcorrenciasParaOTime()
    {
        await Ocorrencias();
        var avisos = await Documentos(DoBackend(AvisosDoBanco));
        foreach (var (resultado, traceId) in ocorrencias)
        {
            Assert.True(avisos.Any(d => Auditoria.Texto(d, "resultado") == resultado && Auditoria.Texto(d, "traceId") == traceId),
                $"esperava em \"{AvisosDoBanco}\" o aviso {resultado} com o traceId {traceId}, vieram {Auditoria.Resumo(avisos)}");
        }
    }

    [Then(@"^o 401 ASSINATURA_INVALIDA também vai a ""auditoriaErros"", como na 17 RN-270$")]
    public async Task RecusadoNosErros()
    {
        await Ocorrencias();
        Respostas.Confere(execucoes.Resultados[execucaoRecusada], 401, "ASSINATURA_INVALIDA");
        var erros = await execucoes.Novos(Auditoria.Erros, execucaoRecusada);
        Assert.True(erros.Any(d => Auditoria.Texto(d, "traceId") == ocorrencias["RECUSADO"]),
            $"esperava em \"auditoriaErros\" o 401 com o traceId {ocorrencias["RECUSADO"]}, vieram {Auditoria.Resumo(erros)}");
    }

    [Then(@"^o aviso repetido não soma nada em dobro$")]
    public async Task RepetidoNaoSoma()
    {
        await AvisoSemEfeito("repetido, já processado");
        Respostas.Confere(mundo.Ultima, 200);
        ConfereAviso(await AvisoGuardado(DoBackend(AvisosDoBanco), mundo.Ultima), "FALSO", true, "REPETIDO");
        await NadaEmDobro();
    }

    // ── RNF-242 troca do intermediador ─────────────────────────────────────
    // o boleto nasce no falso e passa a dizer que veio do Mercado Pago: para o
    // backend do qa, que liga o falso, ele é do intermediador anterior
    [Given(@"^o PAG-000785, boleto ""AGUARDANDO"", que guarda ""intermediador"": ""MERCADO_PAGO""$")]
    public async Task BoletoDoMercadoPago()
    {
        var boleto = await fat.Boleto("PAG-000785");
        await Estado.Atualizar("pagamentos", boleto, "intermediador", "MERCADO_PAGO");
        await Guardar(boleto);
    }

    // a implantação do backend do qa já liga outro intermediador: o falso
    [When(@"^uma nova implantação do ambiente liga outro intermediador$")]
    public static void OutroIntermediador()
    {
        // nada a fazer: o backend do qa roda com o "FALSO" (Teste__Relogio=sim)
    }

    // o boleto e a fatura são lidos como antes, e um novo pagamento da
    // FAT-000402 recebe o mesmo código de quando o boleto estava aberto
    [Then(@"^faturas, status, códigos de erro e endereços do Pagamento não mudam$")]
    public async Task NadaMudaNoPagamento()
    {
        var (id, pagamento, fatura) = antes ?? throw new InvalidOperationException("o Dado não guardou o PAG-000785");
        var (agora, faturaAgora) = (await fat.Pagamento(id), await fat.Fatura(fat.FaturaDo(id)));
        Assert.True(JsonNode.DeepEquals(pagamento, agora), $"o boleto mudou: era {pagamento.ToJsonString()}, ficou {agora.ToJsonString()}");
        Assert.True(JsonNode.DeepEquals(fatura, faturaAgora), $"a fatura mudou: era {fatura.ToJsonString()}, ficou {faturaAgora.ToJsonString()}");
        await PagarComPix(await fat.Rita(), fat.FaturaDo(id));
        Respostas.Confere(mundo.Ultima, 409, "PAGAMENTO_EM_ABERTO");
    }

    // um PIX novo, da fatura do Pedro, vai ao falso e guarda "FALSO"
    [Then(@"^os novos pagamentos vão ao intermediador ligado e guardam o nome dele$")]
    public async Task NovoNoIntermediadorLigado()
    {
        await PagarComPix(await mundo.Pedro(), await fat.Fat371(vencida: false));
        Respostas.Confere(mundo.Ultima, 201);
        var id = Texto(mundo.Ultima, "id");
        Assert.True(Auditoria.Texto(await DocumentoDoPagamento(id), "intermediador") == "FALSO", $"esperava o {id} com \"intermediador\": \"FALSO\"");
        await banco.PedidoDe(id);
    }

    // o aviso do falso com o id externo do boleto não o acha: ele é do Mercado
    // Pago. A consulta dos pendentes (agendamento) iria ao Mercado Pago, que a
    // suíte não chama: não roda aqui
    [Then(@"^o PAG-000785 segue com o Mercado Pago: avisos e consultas dele vão a ele$")]
    public async Task SegueComOMercadoPago()
    {
        var (id, pagamento, _) = antes ?? throw new InvalidOperationException("o Dado não guardou o PAG-000785");
        Respostas.Confere(await Avisar(id, BancoFalso.Confirmado, fat.Agora()), 200);
        ConfereAviso(await AvisoGuardado(DoBackend(AvisosDoBanco), mundo.Ultima), "FALSO", true, "DESCONHECIDO");
        var agora = await fat.Pagamento(id);
        Assert.True(JsonNode.DeepEquals(pagamento, agora), $"o aviso do falso não podia mudar o boleto do Mercado Pago: ficou {agora.ToJsonString()}");
    }

    [Then(@"^isso vale até o PAG-000785 ficar ""PAGO"", ""EXPIRADO"" ou ""CANCELADO""$")]
    public static void AteOFim() => throw new PendingStepException(
        "o fim do PAG-000785 viria do Mercado Pago (aviso dele ou consulta do agendamento ao serviço), e a suíte nunca chama o serviço externo; " +
        "o que se confere de fora (aviso do falso sem efeito, novos pagamentos no falso) está nas frases anteriores");

    // ── RNF-242 intermediador de cada ambiente ─────────────────────────────
    // produção sobe com as chaves de teste e recusa o aviso do falso, mesmo
    // assinado com o segredo do falso e com o relógio de teste pedido
    [Then(@"^em produção o intermediador é sempre o ""MERCADO_PAGO"", com as chaves do Key Vault$")]
    public async Task ProducaoComOMercadoPago()
    {
        partida.KeyVaultCompleto("prd");
        partida.KeyVault["Teste__Relogio"] = "sim";
        partida.KeyVault["Pagamento__SegredoDoAviso"] = Ambiente.PagamentoSegredoDoAviso;
        await partida.IniciarPronta("produção, com as chaves do Mercado Pago");
        var r = await BancoFalso.AvisoDoFalso(partida.Api, BancoFalso.Aviso(BancoFalso.Confirmado, "pix_8c1e5d", DateTimeOffset.UtcNow));
        Respostas.Confere(r, 401, "ASSINATURA_INVALIDA");
    }

    [Then(@"^fora de produção é o ""MERCADO_PAGO"" de teste, ou o ""FALSO"" com o relógio de teste ligado$")]
    public async Task ForaDeProducaoUmOuOutro()
    {
        var formas = Exigir(await fat.Ler(await fat.Rita(), "/pagamento/formas"), 200, "GET /pagamento/formas no backend do qa");
        Assert.True(Campo(formas.Corpo?["cartao"], "intermediador") == "FALSO", $"esperava o \"FALSO\" no backend do qa, com o relógio de teste: {formas}");
        await ForaDeProducao(comAsChaves: true);
        var cartao = await CartaoNaPartida();
        Assert.True(Campo(cartao, "intermediador") == "MERCADO_PAGO" && Campo(cartao, "chavePublica") == Partida.ChavesDoMercadoPago["MercadoPago__PublicKey"],
            $"esperava o \"MERCADO_PAGO\" com a chave pública de teste em homologação sem o relógio de teste: {cartao?.ToJsonString()}");
    }

    [Then(@"^o ""FALSO"" também vale quando o ambiente não tem a chave de acesso do Mercado Pago$")]
    public async Task FalsoSemAChave()
    {
        await ForaDeProducao(comAsChaves: false);
        var cartao = await CartaoNaPartida();
        Assert.True(Campo(cartao, "intermediador") == "FALSO", $"esperava o \"FALSO\" em homologação sem a chave do Mercado Pago: {cartao?.ToJsonString()}");
    }

    [Then(@"^em produção, sem as chaves, a aplicação não inicia$")]
    public async Task ProducaoSemAsChaves()
    {
        partida.KeyVaultCompleto("prd");
        foreach (var nome in Partida.ChavesDoMercadoPago.Keys) partida.KeyVault[nome] = null;
        Assert.False(await partida.Iniciar(), $"a aplicação de produção sem as chaves do Mercado Pago não podia atender. Log:\n{partida.Log}");
        Assert.True(partida.CodigoDeSaida is not null and not 0, $"esperava a saída com código diferente de 0, veio {partida.CodigoDeSaida}. Log:\n{partida.Log}");
    }

    // ── RNF-242 de qual intermediador é o aviso ────────────────────────────
    // o Mercado Pago avisa a instância de homologação com as chaves de teste
    // (o backend do qa não tem o segredo do webhook) sobre um pagamento que ela
    // não tem: ela confere a assinatura e não acha o pagamento
    [When(@"^chega POST /pagamento/notificacoes com x-signature ""ts=…,v1=…"" e x-request-id$")]
    public async Task AvisoDoMercadoPago()
    {
        await ForaDeProducao(comAsChaves: true);
        var r = await BancoFalso.AvisoDoMercadoPago(partida.Api, IdDoMercadoPago, Partida.ChavesDoMercadoPago["MercadoPago__SegredoDoWebhook"]);
        avisoRecebido = ("MERCADO_PAGO", mundo.Guardar(r));
    }

    [When(@"^chega POST /pagamento/notificacoes com X-Assinatura ""sha256=…""$")]
    public async Task AvisoDoFalso()
    {
        var pix = await PixDoExemplo();
        await Guardar(pix);
        avisoRecebido = ("FALSO", await Avisar(pix, BancoFalso.Confirmado, fat.Agora()));
    }

    [When(@"^chega POST /pagamento/notificacoes com nenhum dos dois$")]
    public async Task AvisoSemAssinatura()
    {
        var pix = await PixDoExemplo();
        await Guardar(pix);
        var aviso = BancoFalso.Aviso(BancoFalso.Confirmado, await banco.IdExterno(pix), fat.Agora());
        avisoRecebido = ("nenhum", await execucoes.Executar(async () => mundo.Guardar(await banco.AvisarSemAssinatura(aviso))));
    }

    [Then(@"^o aviso é conferido como do (""MERCADO_PAGO""|""FALSO"", só fora de produção|nenhum: 401 ASSINATURA_INVALIDA)$")]
    public async Task AvisoConferido(string _)
    {
        var (caso, r) = avisoRecebido ?? throw new InvalidOperationException("o Quando não enviou o aviso");
        switch (caso)
        {
            case "MERCADO_PAGO":
                Respostas.Confere(r, 200);
                ConfereAviso(await AvisoGuardado(BancoDaPartida.Colecao(AvisosDoBanco), r), "MERCADO_PAGO", true, "DESCONHECIDO", IdDoMercadoPago);
                break;
            case "FALSO":
                Respostas.Confere(r, 200);
                ConfereAviso(await AvisoGuardado(DoBackend(AvisosDoBanco), r), "FALSO", true, "APLICADO");
                break;
            default:
                Respostas.Confere(r, 401, "ASSINATURA_INVALIDA");
                ConfereAviso(await AvisoGuardado(DoBackend(AvisosDoBanco), r), "FALSO", false, "RECUSADO");
                break;
        }
    }

    // o falso acha o PIX dele; o Mercado Pago não acha um pagamento que não
    // gerou; sem assinatura, nada é procurado
    [Then(@"^o pagamento é achado pelo id externo e pelo intermediador que o gerou$")]
    public async Task AchadoPeloIntermediador()
    {
        var (caso, r) = avisoRecebido ?? throw new InvalidOperationException("o Quando não enviou o aviso");
        if (caso == "MERCADO_PAGO")
        {
            var aviso = await AvisoGuardado(BancoDaPartida.Colecao(AvisosDoBanco), r);
            Assert.True(Auditoria.Texto(aviso, "pagamentoId") is null, $"o aviso do Mercado Pago não podia achar pagamento nenhum: {aviso.ToJson()}");
            return;
        }
        var (id, pagamento, _) = antes ?? throw new InvalidOperationException("o Quando não guardou o PIX");
        var guardado = await AvisoGuardado(DoBackend(AvisosDoBanco), r);
        var agora = await fat.Pagamento(id);
        if (caso == "FALSO")
        {
            Assert.True(Auditoria.Texto(guardado, "pagamentoId") == id && Auditoria.Texto(guardado, "idExterno") == await banco.IdExterno(id)
                && Campo(agora, "status") == "PAGO",
                $"esperava o aviso ligado ao {id} pelo id externo e o pagamento PAGO: {guardado.ToJson()} {agora.ToJsonString()}");
            return;
        }
        Assert.True(Auditoria.Texto(guardado, "pagamentoId") is null && JsonNode.DeepEquals(pagamento, agora),
            $"o aviso sem assinatura não podia achar nem mudar o {id}: {guardado.ToJson()} {agora.ToJsonString()}");
    }

    // ── RNF-243 frases do Esquema ──────────────────────────────────────────
    [Given(@"^que ""…/"" abrevia ""/pagamento/faturas/"" nos exemplos$")]
    public static void Abreviacao()
    {
        // só a leitura dos exemplos: "…/FAT-000402" é "/pagamento/faturas/FAT-000402"
    }

    [When(@"^a Rita envia POST …/(FAT-000318|FAT-000402)/pagamentos com ""forma"": ""PIX""$")]
    public async Task RitaPagaComPix(string exemplo) =>
        await PagarComPix(await fat.Rita(), exemplo == "FAT-000318" ? await fat.Fat318() : await fat.Fat402());

    // ── RNF-245 números somados (carga) ────────────────────────────────────
    [Given(@"^20\.000 faturas de 800 contas, em 24 competências$")]
    [Given(@"^a EBFGA com 24 faturas entre as 20\.000$")]
    [Then(@"^a resposta 200 chega em até 1 segundo, com competencia=2026-10 no resumo$")]
    [Then(@"^os valores e as contagens são somados no banco de dados, sem trazer as faturas à aplicação$")]
    [Then(@"^a resposta 200 chega em até 1 segundo e só as faturas da EBFGA são lidas$")]
    public static void NumerosSomados() => throw new PendingStepException(
        "RNF-245 é de carga (20.000 faturas, resposta em até 1 s): fica para a suíte de carga (5_DEV/qa/backend/carga, ainda não existe); " +
        "o card e o resumo com poucas faturas já estão nos cenários RN-307 e RF-190");
}
