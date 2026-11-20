using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// Passos dos Cenários de API de 1_Requisitos/19_modulo-pagamento.md (v05),
// contrato pagamento-contrato-api v03 e modelo pagamento-documentos v03. Este
// arquivo traz os Dados e o pagador (RF-182 a RF-189, RN-298, RN-299, RN-301,
// RN-303, RN-304, RN-310, RN-311, RN-313, RNF-239, RNF-241); o recebedor e o
// card ficam em RecebimentosPassos.cs e o aviso do banco, o ao vivo e o
// rastreio em BancoPassos.cs (a mesma classe, em três arquivos). As faturas e
// os pagamentos dos exemplos vêm de FaturasDoExemplo.cs e o banco é o
// intermediador falso de BancoFalso.cs: nenhum cenário chama serviço externo.
//
// Reaproveitados: "envia GET …" e "envia POST … com:" (com o token do
// Sujeito, commons), "a resposta é …" e "a resposta é 201 com:" (suporte),
// "a resposta é 422 DADOS_INVALIDOS com o campo "…"" (hub/usuarios), "a
// resposta é …, com o cabeçalho X-Trace-Id" (suporte/PassosDeAuditoria.cs),
// "o corpo traz o código, a mensagem e, quando houver, o campo:", "a resposta
// é 200 com o status "…"", o card e o Content-Type do ao vivo (hub/sac).
//
// "<quem> envia <MÉTODO> /pagamento/…<resto>" é um passo só: o que o caminho
// cita é preparado no estado de que o pedido precisa (se um Dado ainda não o
// criou) e o resto da frase diz o corpo, a hora ou o estado.
[Binding]
public sealed partial class PagamentoPassos(Mundo mundo, Api api, FaturasDoExemplo fat, BancoFalso banco, Execucoes execucoes,
    AoVivo aoVivo, Partida partida, ScenarioContext cenario)
{
    const string Configuracao = "/pagamento/recebimentos/configuracao";
    static readonly TimeSpan CincoSegundos = TimeSpan.FromSeconds(5);
    static readonly TimeSpan Folga = TimeSpan.FromSeconds(2);
    static readonly string[] Formas = ["PIX", "CARTAO_CREDITO", "CARTAO_DEBITO", "BOLETO"];

    // o pedido que espera a frase seguinte ("E o valor 2698.33", "E o mesmo corpo, com …")
    (Pessoa Quem, string Metodo, string Caminho)? pendente;
    // a versão enviada no If-Match do PUT da configuração (RF-193: "a versão lida + 1")
    int versaoEnviada;
    // a configuração lida pelo administrador antes do PUT (RF-193)
    JsonObject? configuracaoLida;
    // as permissões da Rita lidas pela conta master antes do PUT (RN-299)
    int versaoDasPermissoes;

    // ── Dado · quem pede ───────────────────────────────────────────────────
    // a Rita com a FAT-000402 da EBFGA a pagar, em 2026-12-03 (exemplo-pagamento)
    [Given(@"^o token da staff Rita, da EBFGA, com ""MOD-01"" com ""U""$")]
    [Given(@"^o token do cartão ""tok_9f2c71"", gerado pela tela direto no banco$")]
    public async Task TokenDaRita()
    {
        await fat.Fat402();
        mundo.Sujeito = await fat.Rita();
        await mundo.TokenDe(mundo.Sujeito);
    }

    // RN-307: o card do dia, com as faturas de setembro e de outubro pagas (o
    // "Pago no ano" de 39.810,44). A versao 12 do exemplo é o histórico da
    // escola: sai da comparação, como no card do SAC
    [Given(@"^o token da staff Rita, da EBFGA, com ""MOD-01"" com ""U"", em 2026-12-03$")]
    public async Task TokenDaRitaNoDia()
    {
        await fat.Fat247();
        await fat.Fat318();
        await TokenDaRita();
        await fat.Agendamento();
        mundo.CamposSemConferir.Add("versao");
    }

    // RF-193: a versao 4 do exemplo é o histórico da configuração; sai da comparação
    [Given(@"^o token do administrador do deboaz$")]
    public async Task TokenDoAdministrador()
    {
        mundo.Sujeito = mundo.Admin();
        await mundo.TokenDe(mundo.Sujeito);
        mundo.CamposSemConferir.Add("versao");
    }

    // RF-190 e RF-191: outubro de 2026 de todas as contas, visto em 2026-11-14.
    // O mês inteiro (128 contas) só para o resumo; para as vencidas, as 9 delas
    [Given(@"^o token do administrador do deboaz, em 2026-11-14$")]
    public async Task TokenDoAdministradorNoDia()
    {
        await fat.Outubro(inteiro: cenario.ScenarioInfo.Tags.Contains("RF-190"));
        mundo.Sujeito = mundo.Admin();
        await mundo.TokenDe(mundo.Sujeito);
    }

    [Given(@"^o token do administrador do deboaz e o PAG-000773 ""PAGO"", de 3698\.33$")]
    public async Task TokenDoAdministradorEPago()
    {
        await fat.CreditoPago("PAG-000773");
        mundo.Sujeito = mundo.Admin();
        await mundo.TokenDe(mundo.Sujeito);
    }

    // ── Dado · faturas ─────────────────────────────────────────────────────
    // RF-183, RN-310, RN-311: a taxa e a tolerância ditas são salvas antes do vencimento
    [Given(@"^a FAT-000371(?:, do Pedro|, de 294\.00)?,? vencida em 2026-11-10(.*)$")]
    public async Task Fat371Vencida(string resto)
    {
        var tolerancia = ToleranciaDe().Match(resto) is { Success: true } t ? int.Parse(t.Groups[1].Value) : 0;
        await fat.Fat371(resto.Contains("0,1 % ao dia", StringComparison.Ordinal) ? 0.1m : 0m, tolerancia);
    }

    // RN-301: a fatura do Pedro vencida em 2026-11-10, com a taxa salva antes do vencimento
    [Given(@"^a FAT-000371, do Pedro, ""VENCIDA"" desde 2026-11-11, de 294\.00, com 0,1 % ao dia$")]
    public async Task Fat371VencidaComJuros() => await fat.Fat371(0.1m);

    [Given(@"^a FAT-000402 ""A_PAGAR"" com vencimento em 2026-12-10$")]
    [Given(@"^a FAT-000402 ""A_PAGAR"" com vencimento em 2026-12-10, sem fatura vencida$")]
    [Given(@"^a FAT-000402 sem pagamento em aberto$")]
    public async Task Fat402APagar() => await fat.Fat402();

    // RN-301: a fatura que não aceita pagamento, no status da linha
    [Given(@"^a (FAT-000318|FAT-000371), da conta dona, com o status ""(PAGA|CANCELADA)""$")]
    public async Task FaturaQueNaoAceita(string exemplo, string status)
    {
        if (exemplo == "FAT-000318")
        {
            await fat.Fat318();
            return;
        }
        var fatura = await fat.Fat371();
        await fat.Saltar("2026-11-14");
        await fat.Cancelar(fatura);
    }

    // RN-303: o boleto cancelado para trocar de forma e o PIX que quitou a fatura
    [Given(@"^a FAT-000402 ""PAGA"" pelo PIX PAG-000781 e o boleto PAG-000790 ""CANCELADO""$")]
    public async Task PagaPeloPix()
    {
        await fat.CancelarPagamento(await fat.Boleto("PAG-000790"));
        await fat.Confirmar(await fat.Pix("PAG-000781"));
    }

    // RN-304: 12:40 o crédito recusado, 13:00 o PIX que expira às 13:30 e
    // 13:42 o crédito pago; o traceId de cada um é o X-Trace-Id do POST que o criou
    [Given(@"^a FAT-000402 com o crédito recusado, o PIX expirado e o crédito pago de 2026-12-03$")]
    public async Task TresTentativas()
    {
        var fatura = await fat.Fat402();
        await fat.Saltar("2026-12-03T12:40:00Z");
        await banco.Proximo(BancoFalso.Recusar);
        Execucao("trc-52f0e7c4", await fat.Pagar(await fat.Rita(), fatura, "CARTAO_CREDITO", "PAG-000771", "RECUSADO"));
        await banco.Normal();
        await fat.Saltar("2026-12-03T13:00:00Z");
        Execucao("trc-83b5c1a9", await fat.Pix("PAG-000772"));
        await fat.Chegar("2026-12-03T13:30:00Z");
        await fat.Saltar("2026-12-03T13:42:00Z");
        Execucao("trc-1d6e3f08", await fat.CreditoPago("PAG-000773"));
    }

    // RN-313: a conta do Pedro com a fatura vencida; os juros do exemplo (2,94
    // em 10 dias) são os de 0,1 % ao dia
    [Given(@"^a conta-pedro-reis com a FAT-000371 ""VENCIDA""$")]
    public async Task ContaDoPedroDevendo() => await fat.Fat371();

    [Given(@"^a conta-pedro-reis com a FAT-000371 ""VENCIDA"" de 294\.00 e juros de 0,1 % ao dia$")]
    public async Task ContaDoPedroDevendoComJuros() => await fat.Fat371(0.1m);

    // RN-313: a taxa de 0,1 % ao dia vem na frase seguinte, salva antes do vencimento
    [Given(@"^a conta-pedro-reis desativada com a FAT-000371 ""VENCIDA"" de 294\.00$")]
    public async Task ContaDoPedroDesativada()
    {
        await fat.Fat371(0.1m);
        await EstadoDaContaDoPedro("DESATIVADA");
    }

    // a taxa já foi salva antes da emissão (RN-310: vale a do vencimento): confere
    [Given(@"^os juros de 0,1 % ao dia, vigentes no vencimento, em 2026-11-10$")]
    public async Task JurosVigentesNoVencimento()
    {
        // a conta está desativada: quem lê é o recebedor, com o mesmo corpo do pagador (RF-191)
        var fatura = await fat.FaturaDoRecebedor(fat.Real("FAT-000371"));
        Assert.True(fatura["juros"] is JsonObject juros && Valor(juros, "taxaAoDia") == 0.1m,
            $"preparo: esperava a FAT-000371 com a taxa de 0,1 % ao dia: {fatura.ToJsonString()}");
    }

    [Given(@"^a conta-pedro-reis ""ATIVA"" com a FAT-000371 ""VENCIDA"" há 30 dias$")]
    public async Task ContaDoPedroHaTrintaDias()
    {
        await fat.Fat371();
        await fat.Saltar("2026-12-10T12:00:00Z");
    }

    // ── Dado · pagamentos ──────────────────────────────────────────────────
    [Given(@"^o boleto PAG-000785 da FAT-000402 ""AGUARDANDO""$")]
    [Given(@"^o PAG-000785 ""AGUARDANDO"", com vencimento em 2026-12-10$")]
    // RF-184: o boleto leva o endereço da Rua Sete (FaturasDoExemplo.Corpo)
    [Given(@"^o boleto PAG-000785 da EBFGA gerado com o endereço da Rua Sete$")]
    public async Task BoletoAguardando() => await fat.Boleto("PAG-000785");

    // RF-188: o PIX gerado às 13:00 e expirado às 13:30, sem boleto na fatura
    [Given(@"^o PIX PAG-000772 da FAT-000402, já ""EXPIRADO"", sem boleto em aberto$")]
    public async Task PixExpirado() => await PixSemPagamento();

    [Given(@"^o PAG-000774 ""AGUARDANDO""$")]
    public async Task DebitoAguardando() => await fat.Debito("PAG-000774");

    // RF-185: gerado às 13:00, o PIX vale 30 min; o agendamento das 13:30 o expira
    [Given(@"^o PAG-000772 sem pagamento até 2026-12-03T13:30:00Z$")]
    public async Task PixSemPagamento()
    {
        await fat.Pix("PAG-000772");
        await fat.Chegar("2026-12-03T13:30:00Z");
    }

    // RN-305: "pix_8c1e5d" do exemplo é o id que o banco falso deu à cobrança
    [Given(@"^o PAG-000781 ""AGUARDANDO"", com o identificador externo ""pix_8c1e5d"" no banco$")]
    public async Task PixNoBanco() => await PixDoExemplo();

    [Given(@"^o PAG-000655, ""PAGO"", da FAT-000318$")]
    public async Task PagoDaFat318() => await fat.Fat318();

    [Given(@"^o EST-000012 ""PENDENTE"" do PAG-000773$")]
    public async Task EstornoPendente() => await fat.Estornar(await fat.CreditoPago("PAG-000773"), exemplo: "EST-000012");

    // RN-306: o que já foi devolvido é um estorno anterior, concluído pelo banco
    [Given(@"^o PAG-000773 de 3698\.33, com (\d+\.\d{2}) já estornado$")]
    public async Task ComEstornoAnterior(string devolvido)
    {
        var pagamento = await fat.CreditoPago("PAG-000773");
        var valor = decimal.Parse(devolvido, CultureInfo.InvariantCulture);
        if (valor == 0m) return;
        var estorno = await fat.Estornar(pagamento, valor);
        Exigir(await banco.Avisar(estorno, BancoFalso.EstornoConcluido, fat.Agora()), 200, $"POST /pagamento/notificacoes (estorno {estorno} concluído)");
    }

    [Given(@"^o boleto desligado pelo administrador do deboaz$")]
    public async Task BoletoDesligado() => await fat.Forma("BOLETO", false);

    // ── Quando · o pedido do cenário ───────────────────────────────────────
    [When(@"^(a Rita|a staff Rita, com ""MOD-01"" com ""U""|a staff Rita|o Pedro Reis, sem faturas,|o Pedro Reis|o Pedro|a conta master da EBFGA|a conta master|a conta dona|o administrador do deboaz|o administrador, com ""2026-13""|o administrador|o admin|o membro do time Caio|o participante Lucas|a staff Maria, sem ""MOD-01""|a staff Maria|ele|alguém) envia (GET|POST|PUT) (/pagamento/[^\s,]+)(.*(?<!com:))$")]
    public async Task Envia(string quem, string metodo, string caminho, string resto) =>
        await Pedir(quem, metodo, caminho, resto.TrimStart(',', ' ').Trim(), null);

    [When(@"^(a Rita|o administrador) envia (POST|PUT) (/pagamento/[^\s,]+) com:$")]
    public async Task EnviaCom(string quem, string metodo, string caminho, string json) =>
        await Pedir(quem, metodo, caminho, "", json);

    // com o relógio de teste 20 s antes da hora do exemplo, as datas da
    // resposta (criadoEm, expiraEm: a validade do PIX e do débito) valem com
    // folga de 1 min; quem pede entra de novo, no relógio novo
    [When(@"^a Rita envia POST (/pagamento/[^\s,]+) em (\S+) com:$")]
    [When(@"^envia POST (/pagamento/[^\s,]+) em (\S+) com:$")]
    public async Task EnviaNaHora(string caminho, string quando, string json)
    {
        await fat.Garantir(caminho);
        await fat.Saltar(FaturasDoExemplo.Hora(quando).AddSeconds(-20));
        mundo.FolgaDasDatas = TimeSpan.FromMinutes(1);
        await Pedir("a Rita", "POST", caminho, "", json);
    }

    async Task Pedir(string quem, string metodo, string caminho, string resto, string? json)
    {
        const string Sucesso = "e a resposta é de sucesso";
        var sucesso = resto.EndsWith(Sucesso, StringComparison.Ordinal);
        if (sucesso) resto = resto[..^Sucesso.Length].Trim();

        // RN-298, RNF-243: sem o cabeçalho Authorization
        if (quem == "alguém")
        {
            await execucoes.Executar(() => mundo.Enviar(metodo, caminho));
            return;
        }
        await Preparar(quem, metodo, caminho, resto);
        var pessoa = await QuemPede(quem, caminho);

        // "em 2026-11-14", "em 2026-11-13T12:00:00Z": o relógio vai para a hora
        // dita; o aviso de atraso é criado pelo agendamento (RN-311)
        // RN-301: "com "PIX" em 2026-11-14", a hora no fim da frase
        if (EmNoFim().Match(resto) is { Success: true } fim) resto = $"em {fim.Groups[2].Value} {fim.Groups[1].Value}".Trim();
        if (Em().Match(resto) is { Success: true } em)
        {
            await fat.Saltar(em.Groups[1].Value);
            resto = resto[em.Length..].TrimStart(',', ' ');
            if (resto == "já paga") await fat.Pagar(pessoa, fat.Real("FAT-000371"), "CARTAO_CREDITO", status: "PAGO");
            // o aviso de atraso (RN-311) e o card do dia (RN-307) são do agendamento
            if (caminho is "/pagamento/avisos" or "/pagamento/card") await fat.Agendamento();
        }
        if (quem.EndsWith("com \"2026-13\"", StringComparison.Ordinal)) caminho = caminho.Replace("competencia=2026-10", "competencia=2026-13");
        var alvo = await mundo.Traduzir(caminho);
        await GuardarParaNadaMuda(caminho);

        if (metodo == "GET")
        {
            if (caminho.Contains("/ao-vivo", StringComparison.Ordinal)) await AbrirAoVivo(pessoa, alvo);
            else await Executar(pessoa, "GET", alvo, null);
            return;
        }
        // RF-193: "com If-Match "4"" e, na frase seguinte, o corpo
        // RN-306: o pedido de estorno e, na frase seguinte, o valor
        if (caminho == Configuracao && resto.StartsWith("com If-Match da versão lida", StringComparison.Ordinal)
            || caminho.EndsWith("/estornos", StringComparison.Ordinal) && cenario.ScenarioInfo.Title == "valor do estorno")
        {
            pendente = (pessoa, metodo, alvo);
            return;
        }
        (string? Corpo, string? IfMatch) pedido = json is not null ? (await mundo.Traduzir(json), null) : await CorpoDoPedido(caminho, resto);
        await Executar(pessoa, metodo, alvo, pedido.Corpo, pedido.IfMatch);
        if (sucesso) Assert.True(mundo.Ultima.Status is >= 200 and < 300, $"esperava sucesso, veio {mundo.Ultima}");
    }

    // RN-298, "a resposta é 403 ACESSO_NEGADO e nada muda" (suporte/Respostas.cs):
    // a fatura e o pagamento que o caminho cita, vistos pelo administrador
    // antes do pedido, comparados depois. Só os pedidos de quem não pode
    // (participante, staff sem o papel do recebedor) leem antes, para não
    // somar leituras aos outros cenários
    async Task GuardarParaNadaMuda(string caminho)
    {
        if (cenario.ScenarioInfo.Title != "pedidos de quem não pode") return;
        var vistos = new List<(string Caminho, string Antes)>();
        foreach (Match m in Regex.Matches(caminho, @"(FAT|PAG)-\d{6}"))
        {
            var real = fat.Real(m.Value);
            var ver = m.Groups[1].Value == "FAT" ? $"/pagamento/recebimentos/faturas/{real}" : $"/pagamento/recebimentos/pagamentos/{real}";
            vistos.Add((ver, Exigir(await fat.Ler(mundo.Admin(), ver), 200, $"GET {ver}").Texto));
        }
        mundo.NadaMudou = async () =>
        {
            foreach (var (ver, antes) in vistos)
            {
                var agora = Exigir(await fat.Ler(mundo.Admin(), ver), 200, $"GET {ver}");
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(antes), agora.Corpo), $"{ver} mudou: era {antes}, ficou {agora.Texto}");
            }
        };
    }

    async Task<Pessoa> QuemPede(string quem, string caminho) => quem switch
    {
        _ when quem.Contains("Rita", StringComparison.Ordinal) => await fat.Rita(),
        _ when quem.StartsWith("o Pedro", StringComparison.Ordinal) => await mundo.Pedro(),
        // RN-301: a conta dona da fatura do caminho
        "a conta dona" => caminho.Contains("FAT-000371", StringComparison.Ordinal) ? await mundo.Pedro() : await mundo.Master(),
        _ when quem.StartsWith("a conta master", StringComparison.Ordinal) => await mundo.Master(),
        "o membro do time Caio" => await mundo.Caio(),
        "o participante Lucas" => await mundo.Lucas(),
        _ when quem.StartsWith("a staff Maria", StringComparison.Ordinal) => await mundo.Maria(),
        _ => mundo.Admin(),
    };

    // o que o pedido precisa encontrar, quando nenhum Dado o criou
    async Task Preparar(string quem, string metodo, string caminho, string resto)
    {
        var rita = quem.Contains("Rita", StringComparison.Ordinal);
        // RN-298, RNF-243: a lista da Rita é a da EBFGA, com a FAT-000402
        if (rita && (caminho == "/pagamento/faturas" || caminho.StartsWith("/pagamento/faturas?", StringComparison.Ordinal))) await fat.Fat402();
        // RF-182: uma fatura de cada status
        if (quem == "a conta master" && caminho.StartsWith("/pagamento/faturas?situacao=", StringComparison.Ordinal)) await fat.TodosOsStatus();
        // RN-307: o selo 9 do administrador são as 9 faturas vencidas de outubro
        if (quem == "o administrador do deboaz" && caminho == "/pagamento/card") await fat.Outubro(inteiro: false);
        // RF-191: as três faturas da EBFGA e a do Pedro, que a busca deixa de fora
        if (caminho.StartsWith("/pagamento/recebimentos/faturas?busca=EBFGA", StringComparison.Ordinal))
        {
            await fat.Fat247();
            await fat.Fat318();
            await fat.Fat371();
            await fat.Fat402();
        }
        // RF-183: a fatura do Pedro cancelada pelo administrador em 2026-11-14
        if (resto == "cancelada em 2026-11-14")
        {
            var fatura = await fat.Fat371();
            await fat.Saltar("2026-11-14");
            await fat.Cancelar(fatura);
        }
        // RF-184: "cartao" depende do intermediador do ambiente; as frases seguintes o conferem
        if (caminho == "/pagamento/formas") mundo.CamposSemConferir.Add("cartao");
        // RF-193: o boleto que já estava aguardando quando a forma é desligada
        if (metodo == "PUT" && caminho == Configuracao && resto.StartsWith("com If-Match da versão lida", StringComparison.Ordinal)) await fat.Boleto("PAG-000785");
        await fat.Garantir(caminho);
    }

    // o corpo que o resto da frase descreve
    async Task<(string? Corpo, string? IfMatch)> CorpoDoPedido(string caminho, string resto)
    {
        if (caminho.StartsWith("/pagamento/faturas/", StringComparison.Ordinal) && caminho.EndsWith("/pagamentos", StringComparison.Ordinal))
        {
            return ((await CorpoDoPagamento(resto)).ToJsonString(), null);
        }
        if (caminho.StartsWith("/pagamento/recebimentos/faturas/", StringComparison.Ordinal) && caminho.EndsWith("/cancelar", StringComparison.Ordinal))
        {
            return (resto == "sem o motivo" ? "{}" : new JsonObject { ["motivo"] = FaturasDoExemplo.MotivoDoCancelamento }.ToJsonString(), null);
        }
        if (caminho.EndsWith("/estornos", StringComparison.Ordinal)) return (FaturasDoExemplo.CorpoDoEstorno().ToJsonString(), null);
        if (caminho == Configuracao) return await CorpoDaConfiguracao(resto);
        // cancelar o pagamento e o "confirmar" que não existe: sem corpo
        return (null, null);
    }

    // RF-184, RNF-239, RNF-241; sem dizer a forma (RN-309, RNF-243), vai o PIX
    async Task<JsonObject> CorpoDoPagamento(string resto)
    {
        switch (resto)
        {
            case "":
                return FaturasDoExemplo.Corpo("PIX");
            case "sem a forma" or "com sem a forma":
                return new JsonObject();
            case "com a forma \"CHEQUE\"":
                return FaturasDoExemplo.Corpo("CHEQUE");
            case "com a forma \"BOLETO\", desligada":
                await fat.Forma("BOLETO", false);
                return FaturasDoExemplo.Corpo("BOLETO");
            case "com \"PIX\", com o boleto \"AGUARDANDO\" aberto":
                await fat.Boleto("PAG-000785");
                return FaturasDoExemplo.Corpo("PIX");
            case "com \"PIX\"":
                // RNF-241: "E o banco não responde em 15 s" vem depois do pedido na frase, e antes dele no banco
                if (cenario.ScenarioInfo.Tags.Contains("RNF-241")) await banco.Proximo(BancoFalso.SemResposta);
                return FaturasDoExemplo.Corpo("PIX");
        }
        // RNF-239: os dados do cartão nunca chegam ao deboaz; só o token
        if (DadoDoCartao().Match(resto) is { Success: true } dado)
        {
            var corpo = FaturasDoExemplo.Corpo("CARTAO_CREDITO");
            corpo["cartao"]![dado.Groups[1].Value] = dado.Groups[1].Value switch
            {
                "numero" => "4111111111111111",
                "cvv" => "123",
                _ => "12/30",
            };
            return corpo;
        }
        throw new ArgumentException($"pedido de pagamento sem preparo: {resto}", nameof(resto));
    }

    // RF-193: o corpo é o do GET, sem "versao", com a mudança da linha; o
    // If-Match é a versão lida (a "4" do exemplo)
    async Task<(string? Corpo, string? IfMatch)> CorpoDaConfiguracao(string resto)
    {
        if (resto == "com If-Match da lida - 1")
        {
            // uma versão a mais, para a anterior existir
            await fat.Configurar(_ => { });
            var atual = await fat.Configuracao();
            return (FaturasDoExemplo.CorpoDaConfiguracao(atual).ToJsonString(), (atual["versao"]!.GetValue<int>() - 1).ToString(CultureInfo.InvariantCulture));
        }
        var lida = await fat.Configuracao();
        // RF-193: o corpo válido sem o cabeçalho If-Match (428 VERSAO_AUSENTE)
        if (resto == "com sem If-Match") return (FaturasDoExemplo.CorpoDaConfiguracao(lida).ToJsonString(), null);
        Action<JsonObject>? mudar = resto switch
        {
            "" or "com o corpo válido" => null,
            "com as quatro formas false" => c => { foreach (var forma in Formas) c["formas"]![forma] = false; },
            "com sem \"formas.BOLETO\"" => c => c["formas"]!.AsObject().Remove("BOLETO"),
            _ when CampoDaConfiguracao().Match(resto) is { Success: true } campo => c =>
            {
                var partes = campo.Groups[1].Value.Split('.');
                JsonNode no = c;
                foreach (var parte in partes[..^1]) no = no[parte]!;
                no[partes[^1]] = decimal.Parse(campo.Groups[2].Value, CultureInfo.InvariantCulture);
            },
            _ => throw new ArgumentException($"configuração sem preparo: {resto}", nameof(resto)),
        };
        return (FaturasDoExemplo.CorpoDaConfiguracao(lida, mudar).ToJsonString(), lida["versao"]!.ToJsonString());
    }

    // o login (auditado) vem antes da marca: só o pedido em teste conta
    async Task Executar(Pessoa pessoa, string metodo, string caminho, string? corpo, string? ifMatch = null,
        IReadOnlyDictionary<string, string>? cabecalhos = null)
    {
        var sessao = await mundo.SessaoParaPedido(pessoa);
        if (cabecalhos is null) await execucoes.Executar(() => mundo.EnviarComo(pessoa, metodo, caminho, corpo, ifMatch));
        else await execucoes.Executar(async () => mundo.Guardar(await api.Enviar(metodo, caminho, corpo, sessao.Acesso, doPedido: cabecalhos)));
        await Anotar(pessoa, metodo, caminho);
    }

    // o pagamento que o pedido do cenário criou fica anotado; o que o banco
    // gera (código PIX, linha do boleto, endereço da confirmação do débito) é
    // conferido pela forma e sai da comparação com o JSON do exemplo
    async Task Anotar(Pessoa pessoa, string metodo, string caminho)
    {
        var r = mundo.Ultima;
        if (metodo != "POST" || r.Status != 201 || PagamentosDaFatura().Match(caminho) is not { Success: true } fatura
            || r.Corpo is not JsonObject corpo || Campo(corpo, "id") is not { } id) return;
        fat.Criado(r, fatura.Groups[1].Value, pessoa);
        if (corpo["pix"] is JsonObject pix)
        {
            mundo.CamposSemConferir.Add("pix");
            Assert.True(Campo(pix, "copiaECola")?.StartsWith("000201", StringComparison.Ordinal) == true
                && Campo(pix, "qrCodeSvg") is { } svg && svg.StartsWith("<svg", StringComparison.Ordinal) && svg.TrimEnd().EndsWith("</svg>", StringComparison.Ordinal),
                $"esperava o pix com copiaECola (\"000201…\") e qrCodeSvg (\"<svg…</svg>\"): {r}");
        }
        if (corpo["boleto"] is JsonObject boleto)
        {
            mundo.CamposSemConferir.Add("boleto");
            Assert.True(Data().IsMatch(Campo(boleto, "vencimento") ?? "") && Campo(boleto, "linhaDigitavel") is { Length: > 0 }
                && Campo(boleto, "codigoBarras") is { Length: > 0 } && Campo(boleto, "pdf") == $"/pagamento/pagamentos/{id}/boleto",
                $"esperava o boleto com vencimento, linhaDigitavel, codigoBarras e pdf \"/pagamento/pagamentos/{id}/boleto\": {r}");
            // a 3 dias ou mais do vencimento da fatura, o boleto vence com ela (RF-188: "2026-12-10")
            var vencimento = Campo(await fat.Fatura(fatura.Groups[1].Value, pessoa), "vencimento")!;
            if (DateOnly.Parse(vencimento, CultureInfo.InvariantCulture).DayNumber - DateOnly.FromDateTime(fat.Agora().UtcDateTime).DayNumber >= 3)
            {
                Assert.True(Campo(boleto, "vencimento") == vencimento, $"esperava o boleto com o vencimento da fatura ({vencimento}): {r}");
            }
        }
        if (corpo["debito"] is JsonObject debito)
        {
            mundo.CamposSemConferir.Add("debito");
            Assert.True(Uri.TryCreate(Campo(debito, "confirmacaoUrl"), UriKind.Absolute, out var url) && url.Scheme == "https",
                $"esperava o debito com a confirmacaoUrl do banco, em https: {r}");
        }
    }

    // a abertura passa por Execucoes: a resposta guarda os cabeçalhos do fluxo (X-Trace-Id, RNF-243)
    async Task<Fluxo> AbrirAoVivo(Pessoa pessoa, string caminho)
    {
        var token = await mundo.TokenDe(pessoa);
        Fluxo? fluxo = null;
        await execucoes.Executar(async () =>
        {
            fluxo = await aoVivo.Abrir(pessoa.Nome, caminho, token);
            if (fluxo.Status != 200) await fluxo.EsperarFim(Folga);
            return mundo.Guardar(new Resposta(fluxo.Status, fluxo.Status == 200 ? "" : fluxo.Texto, [], fluxo.Cabecalhos));
        });
        return fluxo!;
    }

    // o número real do exemplo: o que um Dado criou ou o que o pedido do cenário acabou de gerar
    async Task<string> Num(string exemplo) => fat.Conhecido(exemplo) ? fat.Real(exemplo) : await mundo.Traduzir(exemplo);

    // "trc-1d6e3f08" do exemplo é o X-Trace-Id da execução que criou o pagamento
    void Execucao(string exemplo, string pagamento) =>
        mundo.Apelidos[exemplo] = () => Task.FromResult(fat.ExecucaoDe(pagamento) ?? "sem X-Trace-Id");

    async Task EstadoDaContaDoPedro(string estado) =>
        await fat.Preparo(mundo.Admin(), "POST", $"/contas/{await mundo.ContaDoPedro()}/estado", new JsonObject { ["estado"] = estado }, 200);

    static string? Campo(JsonNode? no, string campo) => FaturasDoExemplo.Campo(no, campo);
    static decimal? Valor(JsonNode? no, string campo) => FaturasDoExemplo.Valor(no, campo);

    List<JsonObject> Itens() => [.. (mundo.Ultima.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>()];

    // ── Quando · outros pedidos do pagador ─────────────────────────────────
    // formato-comum: o pedido sem a forma; "trc-7f3a9c21" do exemplo é o X-Trace-Id da resposta
    [When(@"^um pedido ao Pagamento é recusado$")]
    public async Task PedidoRecusado()
    {
        mundo.Apelidos["trc-7f3a9c21"] = () => Task.FromResult(mundo.Ultima.Cabecalho("X-Trace-Id") ?? "sem X-Trace-Id");
        await Pedir("a Rita", "POST", "/pagamento/faturas/FAT-000402/pagamentos", "sem a forma", null);
    }

    // RF-186: o banco recusa, analisa ou não aceita o token
    [When(@"^o banco recusa o cartão$")]
    public async Task BancoRecusaOCartao()
    {
        await banco.Proximo(BancoFalso.Recusar);
        await Pedir("a Rita", "POST", "/pagamento/faturas/FAT-000402/pagamentos", "", FaturasDoExemplo.Corpo("CARTAO_CREDITO").ToJsonString());
    }

    [When(@"^a Rita envia o pagamento com (um cartão em análise no banco|o token ""tok_vencido"", já usado|o token sem o ""metodo""|sem o cartao)$")]
    public async Task PagamentoComCartao(string cartao)
    {
        var corpo = FaturasDoExemplo.Corpo("CARTAO_CREDITO");
        if (cartao.StartsWith("um cartão em análise", StringComparison.Ordinal)) await banco.Proximo(BancoFalso.EmAnalise);
        else if (cartao == "sem o cartao") corpo.Remove("cartao");
        else if (cartao == "o token sem o \"metodo\"") corpo["cartao"]!.AsObject().Remove("metodo");
        else corpo["cartao"]!["token"] = "tok_vencido";
        await Pedir("a Rita", "POST", "/pagamento/faturas/FAT-000402/pagamentos", "", corpo.ToJsonString());
    }

    // RF-186: o token vem do intermediador com "metodo" e "emissor", e o
    // corpo do pedido já os leva (FaturasDoExemplo.Corpo); a suíte não gera
    // token: o banco falso aceita os do exemplo
    [When(@"^""metodo"" e ""emissor"" vêm do intermediador junto com o token; ""emissor"" pode faltar$")]
    public void MetodoEEmissor() =>
        Assert.True(mundo.UltimoPedido.Corpo?.Contains("\"metodo\"", StringComparison.Ordinal) == true,
            $"o pedido devia levar o \"metodo\" do cartão: {mundo.UltimoPedido.Corpo}");

    // RF-188: o boleto da FAT-000402 com o endereço da linha
    [When(@"^a Rita envia o boleto da FAT-000402 (sem o endereco|com o CEP ""(?:[^""]+)""|com a UF ""(?:[^""]+)"")$")]
    public async Task BoletoComEndereco(string endereco)
    {
        var corpo = FaturasDoExemplo.Corpo("BOLETO");
        var valor = endereco.Split('"') is { Length: 3 } partes ? partes[1] : "";
        if (endereco == "sem o endereco") corpo.Remove("endereco");
        else if (endereco.StartsWith("com o CEP", StringComparison.Ordinal)) corpo["endereco"]!["cep"] = valor;
        else corpo["endereco"]!["uf"] = valor;
        await Pedir("a Rita", "POST", "/pagamento/faturas/FAT-000402/pagamentos", "", corpo.ToJsonString());
    }

    // RN-303: os dois pedidos saem juntos, cada um com a sua sessão
    [When(@"^a conta master e a Rita enviam POST /pagamento/faturas/FAT-000402/pagamentos juntas$")]
    public async Task DoisPedidosJuntos()
    {
        var fatura = await fat.Fat402();
        var tokens = new[] { await mundo.TokenDe(await mundo.Master()), await mundo.TokenDe(await fat.Rita()) };
        var corpo = FaturasDoExemplo.Corpo("PIX").ToJsonString();
        juntas = await Task.WhenAll(tokens.Select(token => api.Enviar("POST", $"/pagamento/faturas/{fatura}/pagamentos", corpo, token)));
    }

    Resposta[] juntas = [];

    // RN-299: as permissões da Rita lidas antes, e o PUT com a versão lida
    [Given(@"^as permissões da Rita lidas pela conta master, na versão que estiverem$")]
    public async Task PermissoesDaRitaLidas() =>
        versaoDasPermissoes = (await mundo.Permissoes(await fat.RitaSemPagamento(), await mundo.Master()))["versao"]!.GetValue<int>();

    [When(@"^a conta master envia PUT /usuarios/usr-ebfga-rita/permissoes com If-Match dessa versão e:$")]
    public async Task DelegaARita(string json) =>
        await Executar(await mundo.Master(), "PUT", $"/usuarios/{(await fat.RitaSemPagamento()).Id}/permissoes", await mundo.Traduzir(json),
            versaoDasPermissoes.ToString(CultureInfo.InvariantCulture));

    [Then(@"^a resposta é 200 com a versão lida \+ 1 e ""MOD-01"" com ""U""$")]
    public void DelegadaNaVersaoSeguinte()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(versaoDasPermissoes + 1, mundo.Ultima.Corpo?["versao"]?.GetValue<int>());
        Assert.True(Campo(mundo.Ultima.Corpo?["mods"], "MOD-01") == "U", $"esperava \"MOD-01\" com \"U\": {mundo.Ultima}");
    }

    // RN-299 e 17-b RF-161: a linha do administrador é a de hub/usuarios;
    // "usr-ebfga-rita" é a Rita ainda sem o Pagamento. Aqui, as permissões que
    // a pessoa já tem, mais o "MOD-01" com as abas da linha
    [When(@"^(a conta master|a Rita, com Adm\.) envia PUT /usuarios/(usr-[\w-]+)/permissoes com ""MOD-01"" com ""(\w+)""$")]
    public async Task ConcessaoRecusada(string quem, string id, string abas)
    {
        // a Bia está abaixo da Rita (criada por ela): par dela, a recusa seria
        // o 403 da hierarquia (17-b RN-252), não a do Pagamento
        Pessoa por, pessoa;
        if (quem == "a conta master") (por, pessoa) = (await mundo.Master(), await fat.RitaSemPagamento());
        else
        {
            por = await RitaComAdministrar();
            pessoa = await fat.Bia(criador: por);
        }
        var atuais = await mundo.Permissoes(pessoa, por);
        var mods = (JsonObject)atuais["mods"]!.DeepClone();
        mods["MOD-01"] = abas;
        await Executar(por, "PUT", $"/usuarios/{await mundo.Traduzir(id)}/permissoes", new JsonObject { ["mods"] = mods }.ToJsonString(),
            atuais["versao"]!.ToJsonString());
    }

    // "a Rita, com Adm.": com Administrar usuários (MOD-03 com "CU"), como o Rui do 17-b
    async Task<Pessoa> RitaComAdministrar()
    {
        var rita = await fat.Rita();
        await mundo.Conceder(rita, new() { ["MOD-03"] = "CU" });
        return rita;
    }

    // RN-313: o estado da conta é do 17 (RF-113)
    [When(@"^o administrador do deboaz envia POST (/contas/conta-pedro-reis/estado) com:$")]
    public async Task EstadoDaConta(string caminho, string json) =>
        await Executar(mundo.Admin(), "POST", await mundo.Traduzir(caminho), json);

    [When(@"^a conta volta ao estado ""ATIVA"" e o Pedro envia GET (/pagamento/avisos) em (\S+)$")]
    public async Task ReativadaEAvisos(string caminho, string quando)
    {
        await fat.Saltar(quando);
        await EstadoDaContaDoPedro("ATIVA");
        await fat.Agendamento();
        await Executar(await mundo.Pedro(), "GET", caminho, null);
    }

    [When(@"^o Pedro envia POST /auth/hub/entrar com a senha certa$")]
    public async Task PedroEntra()
    {
        var pedro = await mundo.Pedro();
        await mundo.Enviar("POST", "/auth/hub/entrar", new JsonObject
        {
            ["entrada"] = pedro.Entrada,
            ["senha"] = pedro.Senha,
            ["aparelho"] = Navegador,
        }.ToJsonString());
    }

    // RN-313: a exclusão é o estado "ENCERRADA" do 17 RF-113 (409 FATURA_EM_ABERTO)
    [When(@"^o administrador do deboaz envia POST (/contas/conta-pedro-reis/estado) com ""(\w+)""$")]
    public async Task EstadoDaContaDoPedroPedido(string caminho, string estado) =>
        await Executar(mundo.Admin(), "POST", await mundo.Traduzir(caminho), new JsonObject { ["estado"] = estado }.ToJsonString());

    // RF-187, RF-188, RN-301: a hora chega e o agendamento roda
    [When(@"^chega (\S+) sem (?:a confirmação|compensação|pagamento)$")]
    public async Task ChegaAHora(string quando) => await execucoes.Executar(() => fat.Chegar(quando));

    // RN-310: a taxa nova vale para as faturas que vencerem depois
    [When(@"^o administrador salva ""jurosAoDia"": (\S+) em (\S+)$")]
    public async Task SalvaJuros(string taxa, string quando)
    {
        await fat.Saltar(quando);
        await fat.Juros(decimal.Parse(taxa, CultureInfo.InvariantCulture));
    }

    // ── Então · formato comum ──────────────────────────────────────────────
    [Then(@"^o ""traceId"" do corpo é o mesmo do cabeçalho X-Trace-Id.*$")]
    public void TraceIdDoCorpo()
    {
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        Assert.Matches("^trc-[0-9a-f]{8}$", traceId);
        Assert.True(Campo(mundo.Ultima.Corpo, "traceId") == traceId, $"esperava o traceId {traceId} no corpo: {mundo.Ultima}");
    }

    [Then(@"^a fatura vai no endereço pelo número, como ""FAT-000402"", e o pagamento como ""PAG-000771""$")]
    public async Task EnderecoPeloNumero()
    {
        var fatura = await fat.Fat402();
        Assert.Matches("^FAT-[0-9]{6}$", fatura);
        Assert.Equal(fatura, Campo(await fat.Fatura(fatura), "id"));
        var pagamento = await fat.Pix("PAG-000771");
        Assert.Matches("^PAG-[0-9]{6}$", pagamento);
        Assert.Equal(pagamento, Campo(await fat.Pagamento(pagamento), "id"));
    }

    [Then(@"^toda data e hora segue a ISO 8601 em UTC, como ""2026-12-03T13:00:00Z""$")]
    public async Task DatasEmUtc()
    {
        var fatura = await fat.Fatura(await fat.Fat402());
        var datas = Datas(fatura).Concat(Datas(await fat.Pagamento(await fat.Pix("PAG-000771")))).ToList();
        Assert.Contains(datas, d => d.Campo == "emitidaEm");
        Assert.Contains(datas, d => d.Campo == "expiraEm");
        Assert.All(datas, d => Assert.True(Utc().IsMatch(d.Valor), $"{d.Campo}: esperava ISO 8601 em UTC, veio \"{d.Valor}\""));
    }

    [Then(@"^o vencimento e a competência vão sem hora, como ""(\S+)"" e ""(\S+)""$")]
    public async Task SemHora(string vencimento, string competencia)
    {
        var fatura = await fat.Fatura(await fat.Fat402());
        Assert.Equal((vencimento, competencia), (Campo(fatura, "vencimento"), Campo(fatura, "competencia")));
    }

    // duas casas no texto da resposta, e não só no número lido
    [Then(@"^todo valor vai em reais, com duas casas, como (\d+\.\d{2})$")]
    public async Task DuasCasas(string valor)
    {
        var fatura = await fat.Fat402();
        var r = Exigir(await fat.Ler(fat.Pagador(fatura), $"/pagamento/faturas/{fatura}"), 200, $"GET /pagamento/faturas/{fatura}");
        Assert.Equal(decimal.Parse(valor, CultureInfo.InvariantCulture), Valor(r.Corpo, "valor"));
        var valores = ValorNoTexto().Matches(r.Texto).Select(m => m.Groups[1].Value).ToList();
        Assert.True(valores.Count > 0 && valores.All(v => DuasCasasDecimais().IsMatch(v)),
            $"esperava todo valor com duas casas, vieram {string.Join(", ", valores)}: {r}");
    }

    [Then(@"^status e forma vão em maiúsculas, como ""(\w+)"" e ""(\w+)""$")]
    public async Task EmMaiusculas(string status, string forma)
    {
        var fatura = await fat.Fat402();
        Assert.Equal(status, Campo(await fat.Fatura(fatura), "status"));
        // o PIX das frases de cima sai para o crédito do exemplo entrar (RN-303)
        if (fat.Conhecido("PAG-000771")) await fat.CancelarPagamento(fat.Real("PAG-000771"));
        Assert.Equal(forma, Campo(await fat.Pagamento(await fat.CreditoPago("PAG-000773")), "forma"));
    }

    [Then(@"^o pagador usa ""MOD-01"" com ""U""; o recebedor é só o papel ""ADMINISTRADOR""$")]
    public async Task PagadorERecebedor()
    {
        var rita = await fat.Rita();
        var mods = Jwt.Carga(await mundo.TokenDe(rita))["mods"];
        Assert.True(Campo(mods, "MOD-01") == "U", $"esperava \"MOD-01\" com \"U\" no token da Rita, veio {mods?.ToJsonString()}");
        Respostas.Confere(await fat.Ler(rita, "/pagamento/faturas"), 200);
        Respostas.Confere(await fat.Ler(rita, Configuracao), 403, "ACESSO_NEGADO");
        Respostas.Confere(await fat.Ler(mundo.Admin(), Configuracao), 200);
    }

    [Then(@"^toda resposta do Pagamento, de sucesso ou de erro, traz o cabeçalho X-Trace-Id$")]
    public async Task TodaRespostaComTraceId()
    {
        var rita = await fat.Rita();
        var sucesso = await fat.Ler(rita, "/pagamento/formas");
        Respostas.Confere(sucesso, 200);
        var erro = await fat.Ler(rita, "/pagamento/faturas/FAT-999999");
        Respostas.Confere(erro, 404, "FATURA_INEXISTENTE");
        Assert.All(new[] { sucesso, erro }, r => Assert.Matches("^trc-[0-9a-f]{8}$", Auditoria.TraceIdDoCabecalho(r)));
    }

    // ── Então · RF-182 lista ───────────────────────────────────────────────
    [Then(@"^a resposta é 200 com itens, total e resumo, até 50 itens por página$")]
    public void ItensTotalEResumo()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var corpo = mundo.Ultima.Corpo;
        Assert.True(corpo?["itens"] is JsonArray { Count: > 0 and <= 50 } && corpo["total"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number
            && corpo["resumo"] is JsonObject, $"esperava itens (até 50), total e resumo: {mundo.Ultima}");
    }

    [Then(@"^as faturas ""VENCIDA"" vêm antes das ""A_PAGAR"", a competência mais nova primeiro$")]
    public void VencidasPrimeiro() => VencidasAntes(Itens());

    static void VencidasAntes(List<JsonObject> itens)
    {
        var ordem = itens.Where(i => Campo(i, "status") is "VENCIDA" or "A_PAGAR").Select(i => (Status: Campo(i, "status"), Competencia: Campo(i, "competencia"))).ToList();
        var esperada = ordem.OrderBy(i => i.Status == "VENCIDA" ? 0 : 1).ThenByDescending(i => i.Competencia, StringComparer.Ordinal).ToList();
        Assert.True(ordem.SequenceEqual(esperada), $"esperava as VENCIDA antes das A_PAGAR, a competência mais nova primeiro, vieram {string.Join(", ", ordem)}");
    }

    [Then(@"^o item da (FAT-\d{6}) é:$")]
    public async Task ItemDaFatura(string exemplo, string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var id = await Num(exemplo);
        var item = Itens().FirstOrDefault(i => Campo(i, "id") == id);
        Assert.True(item is not null, $"esperava a {id} em itens: {mundo.Ultima}");
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), item);
    }

    [Then(@"^o resumo é:$")]
    public async Task Resumo(string json) =>
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), mundo.Ultima.Corpo?["resumo"]);

    [Then(@"^a resposta é 200 com as faturas (""A_PAGAR"" e ""VENCIDA""|""PAGA""|de todos os status)$")]
    public void FaturasDaSituacao(string quais)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var status = Itens().Select(i => Campo(i, "status")).ToList();
        string[] esperados = quais switch
        {
            "\"PAGA\"" => ["PAGA"],
            "de todos os status" => ["A_PAGAR", "VENCIDA", "PAGA", "CANCELADA"],
            _ => ["A_PAGAR", "VENCIDA"],
        };
        Assert.True(status.Count > 0 && status.All(s => esperados.Contains(s)) && esperados.All(e => status.Contains(e)),
            $"esperava as faturas {string.Join(", ", esperados)}, vieram {string.Join(", ", status)}");
        VencidasAntes(Itens());
    }

    [Then(@"^a resposta é 200 com itens vazio e total 0$")]
    public void SemFaturas()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Corpo?["itens"] is JsonArray { Count: 0 } && mundo.Ultima.Corpo["total"]?.GetValue<int>() == 0,
            $"esperava itens vazio e total 0: {mundo.Ultima}");
    }

    // RN-298: a lista da Rita só traz faturas da EBFGA
    [Then(@"^a resposta é 200, da EBFGA$")]
    public async Task DaEbfga()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var ids = Itens().Select(i => Campo(i, "id")).ToList();
        Assert.Contains(fat.Real("FAT-000402"), ids);
        var conta = await mundo.Conta();
        foreach (var id in ids)
        {
            Assert.Equal(conta, Campo((await fat.Fatura(id!, await fat.Rita()))["conta"], "id"));
        }
    }

    // ── Então · RF-183 detalhe ─────────────────────────────────────────────
    [Then(@"^a resposta é 200 com o status ""VENCIDA"", o valor (\d+\.\d{2}) e:$")]
    public async Task VencidaComJuros(string valor, string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.Equal(("VENCIDA", decimal.Parse(valor, CultureInfo.InvariantCulture)), (Campo(mundo.Ultima.Corpo, "status"), Valor(mundo.Ultima.Corpo, "valor")));
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), mundo.Ultima.Corpo);
    }

    [Then(@"^itens traz o ""(MOD-\d+)"" (\S+) com (\d+\.\d{2}) e o ""(MOD-\d+)"" (\S+) com (\d+\.\d{2})$")]
    public void ItensDaFatura(string modulo1, string nome1, string valor1, string modulo2, string nome2, string valor2)
    {
        var itens = (mundo.Ultima.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>()
            .Select(i => (Campo(i, "moduloId"), Campo(i, "nome"), Valor(i, "valor"))).ToList();
        var esperados = new[]
        {
            (modulo1, nome1, decimal.Parse(valor1, CultureInfo.InvariantCulture)),
            (modulo2, nome2, decimal.Parse(valor2, CultureInfo.InvariantCulture)),
        };
        Assert.True(itens.Count == 2 && esperados.All(e => itens.Contains(e)), $"esperava os itens {string.Join(" e ", esperados)}: {mundo.Ultima}");
    }

    // a taxa que vale é a do dia do vencimento (RN-310): com a taxa 0 salva
    // agora, a fatura de novembro do Pedro vence em 2026-12-10 sem juros
    [Then(@"^com a taxa 0, juros vem null e totalAPagar (\d+\.\d{2})$")]
    public async Task SemJuros(string total)
    {
        await fat.Juros(0m);
        var pedro = await mundo.Pedro();
        var fatura = await fat.Emitir(pedro, await mundo.ContaDoPedro(), "PREIS", "2026-11", ("MOD-03", 147.00m), ("MOD-05", 147.00m));
        await fat.Vencer("2026-11");
        await fat.Saltar("2026-12-14");
        var lida = await fat.Fatura(fatura, pedro);
        Assert.True(Campo(lida, "status") == "VENCIDA" && lida.ContainsKey("juros") && lida["juros"] is null
            && Valor(lida, "totalAPagar") == decimal.Parse(total, CultureInfo.InvariantCulture),
            $"esperava a fatura vencida com juros null e totalAPagar {total}: {lida.ToJsonString()}");
    }

    [Then(@"^a resposta é 200 com o status ""CANCELADA"", ""canceladaEm"" e o motivo$")]
    public void CanceladaComData()
    {
        Cancelada();
        Assert.True(Utc().IsMatch(Campo(mundo.Ultima.Corpo, "canceladaEm") ?? ""), $"esperava canceladaEm: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 200 com o status ""CANCELADA"" e o motivo$")]
    public void Cancelada()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo(mundo.Ultima.Corpo, "status") == "CANCELADA" && Campo(mundo.Ultima.Corpo, "motivo") is { Length: > 0 },
            $"esperava a fatura CANCELADA com o motivo: {mundo.Ultima}");
    }

    [Then(@"^""motivo"" é ""(.+)""$")]
    public void Motivo(string motivo) =>
        Assert.True(Campo(mundo.Ultima.Corpo, "motivo") == motivo, $"esperava o motivo \"{motivo}\": {mundo.Ultima}");

    // a fatura de outra conta responde como a que não existe: o mesmo 404 e a
    // mesma mensagem, sem campo a mais que diga se ela existe
    [Then(@"^a resposta é 404 FATURA_INEXISTENTE, sem dizer se a fatura existe$")]
    public void DeOutraContaOuInexistente()
    {
        Respostas.Confere(mundo.Ultima, 404, "FATURA_INEXISTENTE");
        var corpo = mundo.Ultima.Corpo as JsonObject;
        Assert.True(Campo(corpo, "mensagem") == "Fatura não encontrada."
            && corpo!.Select(c => c.Key).All(c => c is "codigo" or "mensagem" or "traceId"),
            $"esperava só o código, a mensagem \"Fatura não encontrada.\" e o traceId: {mundo.Ultima}");
    }

    // ── Então · pagamentos (RF-184 a RF-189) ───────────────────────────────
    [Then(@"^POST /pagamento/faturas/FAT-000402/pagamentos com ""PIX"" passa a ser aceito$")]
    public async Task PixAceito() => await fat.Pagar(await fat.Rita(), fat.Real("FAT-000402"), "PIX", status: "AGUARDANDO");

    [Then(@"^a resposta é 200 com o mesmo corpo e o status atual$")]
    public async Task MesmoCorpo()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var criado = (JsonObject)fat.CorpoDoCriado(await Num("PAG-000772")).DeepClone();
        Comparacao.Confere(criado, mundo.Ultima.Corpo, folga: TimeSpan.Zero);
    }

    [Then(@"^a resposta é 200 com o status ""EXPIRADO"" e sem o ""pix""$")]
    public void ExpiradoSemPix()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo(mundo.Ultima.Corpo, "status") == "EXPIRADO" && mundo.Ultima.Corpo?["pix"] is null,
            $"esperava o PIX EXPIRADO, sem o \"pix\": {mundo.Ultima}");
    }

    [Then(@"^a fatura FAT-000402 segue ""A_PAGAR"" e aceita um novo POST de pagamento$")]
    public async Task SegueAPagarEAceita()
    {
        await SegueAPagar();
        await PixAceito();
    }

    [Then(@"^a fatura FAT-000402 segue ""A_PAGAR""$")]
    public async Task SegueAPagar()
    {
        var fatura = await fat.Fatura(fat.Real("FAT-000402"));
        Assert.True(Campo(fatura, "status") == "A_PAGAR", $"esperava a fatura A_PAGAR: {fatura.ToJsonString()}");
    }

    [Then(@"^a resposta é 201 com o status ""RECUSADO"" e o motivo ""(\w+)""$")]
    public void Recusado(string motivo)
    {
        Respostas.Confere(mundo.Ultima, 201);
        Assert.Equal(("RECUSADO", motivo), (Campo(mundo.Ultima.Corpo, "status"), Campo(mundo.Ultima.Corpo, "motivo")));
    }

    [Then(@"^a resposta é 201 com o status ""(PROCESSANDO)""$")]
    public void CriadoComStatus(string status)
    {
        Respostas.Confere(mundo.Ultima, 201);
        Assert.True(Campo(mundo.Ultima.Corpo, "status") == status, $"esperava o status {status}: {mundo.Ultima}");
    }

    // "cartao.token", "cartao.numero": o campo com o caminho dentro do corpo
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com o campo ""(\w+(?:\.\w+)+)""$")]
    public void ComOCampo(int status, string codigo, string campo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        Assert.True(Campo(mundo.Ultima.Corpo, "campo") == campo, $"esperava o campo \"{campo}\": {mundo.Ultima}");
    }

    [Then(@"^GET /pagamento/pagamentos/(PAG-\d{6}) traz o status ""(\w+)""$")]
    public async Task PagamentoNoStatus(string exemplo, string status)
    {
        var pagamento = await fat.Pagamento(await Num(exemplo));
        Assert.True(Campo(pagamento, "status") == status, $"esperava o {exemplo} em {status}: {pagamento.ToJsonString()}");
    }

    [Then(@"^GET /pagamento/pagamentos/(PAG-\d{6}) traz o status ""PAGO"" e pagoEm de (\d{4}-\d{2}-\d{2})$")]
    public async Task PagoNoDia(string exemplo, string dia)
    {
        var pagamento = await fat.Pagamento(await Num(exemplo));
        Assert.True(Campo(pagamento, "status") == "PAGO" && Campo(pagamento, "pagoEm")?.StartsWith(dia, StringComparison.Ordinal) == true,
            $"esperava o {exemplo} PAGO com pagoEm de {dia}: {pagamento.ToJsonString()}");
    }

    [Then(@"^GET /pagamento/pagamentos/(PAG-\d{6}) traz o status ""PAGO"" e ""duplicidade"": true$")]
    public async Task PagoEmDuplicidade(string exemplo)
    {
        var pagamento = await fat.Pagamento(await Num(exemplo));
        Assert.True(Campo(pagamento, "status") == "PAGO" && pagamento["duplicidade"]?.GetValueKind() == System.Text.Json.JsonValueKind.True,
            $"esperava o {exemplo} PAGO com \"duplicidade\": true: {pagamento.ToJsonString()}");
    }

    [Then(@"^a resposta é 200 com Content-Type ""([^""]+)""$")]
    public void ComOTipo(string tipo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var veio = mundo.Ultima.Cabecalho("Content-Type");
        Assert.True(veio?.Split(';')[0].Trim() == tipo, $"esperava Content-Type \"{tipo}\", veio \"{veio}\"");
        // o PDF começa com a assinatura do formato
        if (tipo == "application/pdf")
        {
            Assert.True(mundo.Ultima.Bytes is { Length: > 4 } b && System.Text.Encoding.ASCII.GetString(b, 0, 5) == "%PDF-", "esperava o conteúdo de um PDF");
        }
    }

    // o nome do arquivo leva o número real da fatura
    [Then(@"^Content-Disposition ""attachment; filename=\\""(boleto|comprovante)-(FAT-\d{6})\.pdf\\""""$")]
    public async Task ComONome(string documento, string exemplo)
    {
        var veio = mundo.Ultima.Cabecalho("Content-Disposition");
        var esperado = $"{documento}-{await Num(exemplo)}.pdf";
        Assert.True(veio is not null && ContentDispositionHeaderValue.TryParse(veio, out var v) && v.DispositionType == "attachment"
            && v.FileName?.Trim('"') == esperado, $"esperava Content-Disposition attachment com o nome \"{esperado}\", veio \"{veio}\"");
    }

    // ── Então · RN-299 delegação ───────────────────────────────────────────
    [Then(@"^o token novo da Rita tem ""MOD-01"" com ""U""$")]
    public async Task TokenNovoDaRita()
    {
        var sessao = await mundo.Entrar(await fat.RitaSemPagamento(), Navegador);
        var mods = Jwt.Carga(sessao.Acesso)["mods"];
        Assert.True(Campo(mods, "MOD-01") == "U", $"esperava \"MOD-01\" com \"U\" no token novo da Rita, veio {mods?.ToJsonString()}");
    }

    // ── Então · RN-301 status da fatura ────────────────────────────────────
    [Then(@"^a resposta é 201 com o valor (\d+\.\d{2}): (\d+\.\d{2}) da fatura e (\d+\.\d{2}) de juros de \d+ dias$")]
    public void ValorComJuros(string valor, string daFatura, string juros)
    {
        Respostas.Confere(mundo.Ultima, 201);
        static decimal D(string v) => decimal.Parse(v, CultureInfo.InvariantCulture);
        Assert.True(Valor(mundo.Ultima.Corpo, "valor") == D(valor) && Valor(mundo.Ultima.Corpo, "valorFatura") == D(daFatura)
            && Valor(mundo.Ultima.Corpo, "juros") == D(juros), $"esperava o valor {valor} = {daFatura} da fatura + {juros} de juros: {mundo.Ultima}");
    }

    // RN-301: a recusa não mexe na fatura
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) e a fatura segue ""(\w+)""$")]
    public async Task RecusaSemMudarAFatura(int status, string codigo, string situacao)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        var exemplo = cenario.ScenarioInfo.Arguments["id"] as string ?? throw new InvalidOperationException("o exemplo não diz a fatura");
        var fatura = await fat.Fatura(fat.Real(exemplo));
        Assert.True(Campo(fatura, "status") == situacao && fatura["pagamentoEmAberto"] is null,
            $"esperava a {exemplo} \"{situacao}\", sem pagamento em aberto: {fatura.ToJsonString()}");
    }

    [Then(@"^GET /pagamento/faturas/(FAT-\d{6}) traz o status ""VENCIDA"" e o mesmo valor (\d+\.\d{2})$")]
    public async Task ViraVencida(string exemplo, string valor)
    {
        var fatura = await fat.Fatura(await Num(exemplo));
        Assert.True(Campo(fatura, "status") == "VENCIDA" && Valor(fatura, "valor") == decimal.Parse(valor, CultureInfo.InvariantCulture),
            $"esperava a {exemplo} VENCIDA com o valor {valor}: {fatura.ToJsonString()}");
    }

    [Then(@"^POST /auth/hub/entrar da Maria, staff da EBFGA, segue com 200: a conta não é desativada$")]
    public async Task MariaEntra()
    {
        var maria = await mundo.Maria();
        mundo.Sessoes.RemoveAll(s => s.Pessoa == maria);
        await mundo.Entrar(maria, Navegador);
    }

    // ── Então · RN-303 sem cobrança em dobro ───────────────────────────────
    [Then(@"^só uma recebe 201$")]
    public void SoUmaCriada() =>
        Assert.True(juntas.Count(r => r.Status == 201) == 1, $"esperava um só 201, vieram {string.Join(" e ", juntas.Select(r => r.ToString()))}");

    [Then(@"^a outra recebe 409 PAGAMENTO_EM_ABERTO$")]
    public void AOutraRecusada()
    {
        var outra = juntas.Where(r => r.Status != 201).ToList();
        Assert.True(outra.Count == 1, $"esperava uma só recusa, vieram {string.Join(" e ", juntas.Select(r => r.ToString()))}");
        Respostas.Confere(outra[0], 409, "PAGAMENTO_EM_ABERTO");
    }

    [Then(@"^a FAT-000402 segue ""PAGA"", com pagaEm do PAG-000781$")]
    public async Task SeguePaga()
    {
        var fatura = await fat.Fatura(fat.Real("FAT-000402"));
        var pix = await fat.Pagamento(fat.Real("PAG-000781"));
        Assert.True(Campo(fatura, "status") == "PAGA" && Campo(fatura, "pagaEm") is { } pagaEm && pagaEm == Campo(pix, "pagoEm"),
            $"esperava a fatura PAGA com pagaEm do PIX ({Campo(pix, "pagoEm")}): {fatura.ToJsonString()}");
    }

    [Then(@"^GET (/pagamento/recebimentos/resumo\?competencia=\d{4}-\d{2}) traz ""duplicidades"": (\d+)$")]
    public async Task Duplicidades(string caminho, int duplicidades)
    {
        var r = Exigir(await fat.Ler(mundo.Admin(), caminho), 200, $"GET {caminho}");
        Assert.True(r.Corpo?["duplicidades"]?.GetValue<int>() == duplicidades, $"esperava \"duplicidades\": {duplicidades}: {r}");
    }

    // ── Então · RN-304 tentativas ──────────────────────────────────────────
    [Then(@"^pagamentos traz os (\d+), o mais novo primeiro, cada um com o mesmo valor (\d+\.\d{2}):$")]
    public async Task Tentativas(int quantos, string valor, string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var esperados = JsonNode.Parse(await mundo.Traduzir(json))!.AsArray();
        var pagamentos = (mundo.Ultima.Corpo?["pagamentos"]?.AsArray() ?? []).OfType<JsonObject>().ToList();
        Assert.True(pagamentos.Count == quantos, $"esperava {quantos} pagamentos: {mundo.Ultima}");
        // na ordem do exemplo, do mais novo para o mais antigo
        for (var i = 0; i < quantos; i++) Comparacao.Confere(esperados[i], pagamentos[i], $"$.pagamentos[{i}]");
        var criados = pagamentos.Select(p => DateTimeOffset.Parse(Campo(p, "criadoEm")!, CultureInfo.InvariantCulture)).ToList();
        Assert.True(criados.SequenceEqual(criados.OrderByDescending(c => c)), $"esperava o mais novo primeiro: {mundo.Ultima}");
        Assert.All(pagamentos, p => Assert.Equal(decimal.Parse(valor, CultureInfo.InvariantCulture), Valor(p, "valor")));
    }

    // ── Então · RN-310 juros ───────────────────────────────────────────────
    [Then(@"^a resposta é 201 com o valor (\d+\.\d{2}) e:$")]
    public async Task CriadoComValor(string valor, string json)
    {
        Respostas.Confere(mundo.Ultima, 201);
        Assert.Equal(decimal.Parse(valor, CultureInfo.InvariantCulture), Valor(mundo.Ultima.Corpo, "valor"));
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), mundo.Ultima.Corpo);
    }

    // um dia depois, os juros da fatura cresceram e o valor do pagamento é o mesmo
    [Then(@"^o valor do pagamento não muda depois de gerado, mesmo que vire o dia$")]
    public async Task ValorNaoMuda()
    {
        var (id, valor) = (Texto(mundo.Ultima, "id"), Valor(mundo.Ultima.Corpo, "valor"));
        await fat.Saltar(fat.Agora().AddDays(1));
        var depois = await fat.Pagamento(id);
        Assert.True(Valor(depois, "valor") == valor, $"esperava o pagamento ainda com {valor}: {depois.ToJsonString()}");
    }

    [Then(@"^GET /pagamento/faturas/(FAT-\d{6}) segue com ""taxaAoDia"": (\S+)$")]
    public async Task TaxaDoVencimento(string exemplo, string taxa)
    {
        var fatura = await fat.Fatura(await Num(exemplo));
        Assert.True(Valor(fatura["juros"], "taxaAoDia") == decimal.Parse(taxa, CultureInfo.InvariantCulture),
            $"esperava a taxa {taxa} do dia do vencimento: {fatura.ToJsonString()}");
    }

    // ── Então · RN-311 e RN-313 avisos e conta ─────────────────────────────
    [Then(@"^total é (\d+)$")]
    public void Total(int total)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Corpo?["total"]?.GetValue<int>() == total, $"esperava total {total}: {mundo.Ultima}");
    }

    [Then(@"^a resposta é 200 com total (\d+) e o item(?:, com \d+ dias de juros, de \d{2}/\d{2} a \d{2}/\d{2})?:$")]
    public async Task TotalEItem(int total, string json)
    {
        Total(total);
        var esperado = JsonNode.Parse(await mundo.Traduzir(json));
        Assert.True(Itens().Any(i => Comparacao.Contem(esperado, i)), $"esperava o item {esperado!.ToJsonString()}: {mundo.Ultima}");
    }

    // a fatura da conta desativada segue na lista do administrador, com o atraso contando
    [Then(@"^GET (/pagamento/recebimentos/faturas\?\S+) em (\S+) traz:$")]
    public async Task ListaDoAdministradorTraz(string caminho, string quando, string json)
    {
        await fat.Saltar(quando);
        var r = Exigir(await fat.Ler(mundo.Admin(), caminho), 200, $"GET {caminho}");
        var esperado = JsonNode.Parse(await mundo.Traduzir(json));
        Assert.True((r.Corpo?["itens"]?.AsArray() ?? []).Any(i => Comparacao.Contem(esperado, i)), $"esperava o item {esperado!.ToJsonString()}: {r}");
    }

    // ── Então · RNF-239 e RNF-241 ──────────────────────────────────────────
    // o que a API mostra: nenhum pagamento nasce do pedido recusado. O que não
    // vai para o banco nem para o log só os testes do backend conferem
    [Then(@"^nada desse pedido é guardado nem escrito em log$")]
    public async Task NadaGuardado()
    {
        var fatura = await fat.Fatura(fat.Real("FAT-000402"));
        Assert.True(fatura["pagamentos"] is JsonArray { Count: 0 }, $"o pedido recusado não devia criar pagamento: {fatura.ToJsonString()}");
    }

    // o banco falso já está sem responder desde antes do pedido (CorpoDoPagamento);
    // os 15 s são do adaptador HTTP do intermediador, medidos nos testes do backend
    [When(@"^o banco não responde em 15 s$")]
    public void BancoSemResposta() => Assert.True(banco.Mudou, "o banco falso devia estar sem responder desde antes do pedido");

    [Then(@"^nenhum pagamento fica ""AGUARDANDO"" e a FAT-000402 segue ""A_PAGAR""$")]
    public async Task NadaAguardando()
    {
        var id = fat.Real("FAT-000402");
        var fatura = await fat.Fatura(id);
        var abertos = (fatura["pagamentos"]?.AsArray() ?? []).Where(p => Campo(p, "status") is "AGUARDANDO" or "PROCESSANDO").ToList();
        Assert.True(Campo(fatura, "status") == "A_PAGAR" && abertos.Count == 0, $"esperava a fatura A_PAGAR, sem pagamento em aberto: {fatura.ToJsonString()}");
        var lista = Exigir(await fat.Ler(await fat.Rita(), "/pagamento/faturas?situacao=A_PAGAR"), 200, "GET /pagamento/faturas?situacao=A_PAGAR");
        var item = (lista.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>().FirstOrDefault(i => Campo(i, "id") == id);
        Assert.True(item is not null && item.ContainsKey("pagamentoEmAberto") && item["pagamentoEmAberto"] is null,
            $"esperava a {id} na lista com pagamentoEmAberto null: {lista}");
    }

    // ── fora de escopo ─────────────────────────────────────────────────────
    [Then(@"^não há PIX Automático, débito automático nem cobrança recorrente no cartão$")]
    [Then(@"^não há parcelamento, cartão guardado para a próxima vez, outra moeda nem banco do exterior$")]
    [Then(@"^não há divisão do valor entre recebedores nem nota fiscal$")]
    [Then(@"^preço, desconto e plano anual são do Catálogo, e o cálculo de cada cobrança também$")]
    [Then(@"^participante, responsável e acesso anônimo não pagam nada no deboaz$")]
    [Then(@"^não há aviso por e-mail, SMS ou WhatsApp: toda comunicação é no próprio módulo$")]
    [Then(@"^a falta de pagamento, sozinha, não desativa a conta nem bloqueia a entrada$")]
    [Then(@"^desativar a conta não apaga a fatura em aberto.*$")]
    [Then(@"^multa por atraso não existe: só os juros ao dia da Configuração$")]
    public static void ForaDeEscopo() => throw new PendingStepException("fora de escopo: nada a testar");

    // os campos de data e hora (…Em) em qualquer nível
    static IEnumerable<(string Campo, string Valor)> Datas(JsonNode? no)
    {
        switch (no)
        {
            case JsonObject o:
                foreach (var (campo, valor) in o)
                {
                    if (campo.EndsWith("Em", StringComparison.Ordinal) && valor?.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    {
                        yield return (campo, valor.GetValue<string>());
                    }
                    foreach (var d in Datas(valor)) yield return d;
                }
                break;
            case JsonArray a:
                foreach (var item in a) foreach (var d in Datas(item)) yield return d;
                break;
        }
    }

    [GeneratedRegex(@"^em (\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}Z)?)")]
    private static partial Regex Em();

    [GeneratedRegex(@"^(com .+) em (\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}Z)?)$")]
    private static partial Regex EmNoFim();

    [GeneratedRegex(@"tolerância de (\d+) dias")]
    private static partial Regex ToleranciaDe();

    [GeneratedRegex(@"^com o cartão cartao\.(numero|cvv|validade)$")]
    private static partial Regex DadoDoCartao();

    [GeneratedRegex(@"^com ""([\w.]+)"": (\d+(?:\.\d+)?)$")]
    private static partial Regex CampoDaConfiguracao();

    [GeneratedRegex(@"^/pagamento/faturas/(FAT-\d{6})/pagamentos$")]
    private static partial Regex PagamentosDaFatura();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex Data();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$")]
    private static partial Regex Utc();

    [GeneratedRegex(@"""(?:valor|totalAPagar|devolvido)""\s*:\s*(-?\d+(?:\.\d+)?)")]
    private static partial Regex ValorNoTexto();

    [GeneratedRegex(@"^-?\d+\.\d{2}$")]
    private static partial Regex DuasCasasDecimais();
}
