using System.Globalization;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// O formato comum de 1_Requisitos/19_modulo-pagamento.md (v05): a mensagem
// de cada código de erro (contrato pagamento-contrato-api v03, "MENSAGENS DE
// ERRO") e o "MOD-01" do administrador. Cada resposta do esquema é provocada
// de verdade, pelo pedido que a produz nos outros cenários do 19; a frase do
// Então confere só a "mensagem" do corpo.
public sealed partial class PagamentoPassos
{
    // ── Quando · a resposta de cada código ─────────────────────────────────
    [When(@"^a resposta do Pagamento é (.+)$")]
    public async Task RespostaDoPagamento(string resposta)
    {
        var rita = await fat.Rita();
        switch (resposta)
        {
            case "403 ACESSO_NEGADO":
                // a Maria não tem o "MOD-01" (RN-298)
                await Executar(await mundo.Maria(), "GET", "/pagamento/faturas", null);
                break;
            case "404 FATURA_INEXISTENTE":
                await Executar(rita, "GET", "/pagamento/faturas/FAT-999999", null);
                break;
            case "404 PAGAMENTO_INEXISTENTE":
                await Executar(rita, "GET", "/pagamento/pagamentos/PAG-999999", null);
                break;
            case "404 BOLETO_INEXISTENTE":
                await Executar(rita, "GET", $"/pagamento/pagamentos/{await fat.Pix("PAG-000772")}/boleto", null);
                break;
            case "409 FATURA_PAGA":
                await PagarComPix(rita, await fat.Fat318());
                break;
            case "409 FATURA_CANCELADA":
            {
                var fatura = await fat.Fat371();
                await fat.Saltar("2026-11-14");
                await fat.Cancelar(fatura);
                await PagarComPix(await mundo.Pedro(), fatura);
                break;
            }
            case "409 PAGAMENTO_EM_ABERTO":
                await fat.Boleto("PAG-000785");
                await PagarComPix(rita, fat.Real("FAT-000402"));
                break;
            case "409 PAGAMENTO_CONFIRMADO":
                await Executar(rita, "POST", $"/pagamento/pagamentos/{await fat.CreditoPago("PAG-000773")}/cancelar", null);
                break;
            case "409 PAGAMENTO_NAO_CONFIRMADO":
                // o comprovante do boleto ainda não compensado (RF-189)
                await Executar(rita, "GET", $"/pagamento/pagamentos/{await fat.Boleto("PAG-000785")}/comprovante", null);
                break;
            case "409 ESTORNO_EM_ANDAMENTO":
            {
                var pagamento = await fat.CreditoPago("PAG-000773");
                await fat.Estornar(pagamento, exemplo: "EST-000012");
                await Estornar(pagamento, 100.00m);
                break;
            }
            case "409 ESTORNO_PELO_SAC":
                await fat.Garantir("PAG-000790");
                await Estornar(fat.Real("PAG-000790"), 100.00m);
                break;
            case "412 CONFIGURACAO_MUDOU":
            {
                // uma versão a mais, para a anterior existir
                await fat.Configurar(_ => { });
                var atual = await fat.Configuracao();
                await Executar(mundo.Admin(), "PUT", Configuracao, FaturasDoExemplo.CorpoDaConfiguracao(atual).ToJsonString(),
                    (atual["versao"]!.GetValue<int>() - 1).ToString(CultureInfo.InvariantCulture));
                break;
            }
            case "422 FORMA_INDISPONIVEL":
                await fat.Forma("BOLETO", false);
                await Executar(rita, "POST", $"/pagamento/faturas/{await fat.Fat402()}/pagamentos", FaturasDoExemplo.Corpo("BOLETO").ToJsonString());
                break;
            case "422 FORMA_OBRIGATORIA":
            {
                var lida = await fat.Configuracao();
                var corpo = FaturasDoExemplo.CorpoDaConfiguracao(lida, c => { foreach (var forma in Formas) c["formas"]![forma] = false; });
                await Executar(mundo.Admin(), "PUT", Configuracao, corpo.ToJsonString(), lida["versao"]!.ToJsonString());
                break;
            }
            case "428 VERSAO_AUSENTE":
                await Executar(mundo.Admin(), "PUT", Configuracao, FaturasDoExemplo.CorpoDaConfiguracao(await fat.Configuracao()).ToJsonString());
                break;
            case "401 ASSINATURA_INVALIDA":
                await AvisoComAssinaturaInvalida();
                break;
            case "503 BANCO_INDISPONIVEL":
            {
                var fatura = await fat.Fat402();
                await banco.Proximo(BancoFalso.SemResposta);
                await PagarComPix(rita, fatura);
                break;
            }
            case "422 DADOS_INVALIDOS, campo \"forma\"":
                await Executar(rita, "POST", $"/pagamento/faturas/{await fat.Fat402()}/pagamentos", "{}");
                break;
            case "422 ESTORNO_ACIMA_DO_SALDO, 2698.33":
                await ComEstornoAnterior("1000.00");
                await Estornar(fat.Real("PAG-000773"), 2698.34m);
                break;
            case "409 FATURA_PAGA, ao cancelar":
                await Executar(mundo.Admin(), "POST", $"/pagamento/recebimentos/faturas/{await fat.Fat318()}/cancelar",
                    new JsonObject { ["motivo"] = FaturasDoExemplo.MotivoDoCancelamento }.ToJsonString());
                break;
            default:
                throw new ArgumentException($"resposta do Pagamento sem preparo: {resposta}", nameof(resposta));
        }
        var (status, codigo) = (int.Parse(resposta[..3], CultureInfo.InvariantCulture), resposta[4..].Split(',')[0]);
        Respostas.Confere(mundo.Ultima, status, codigo);
    }

    async Task PagarComPix(Pessoa quem, string fatura) =>
        await Executar(quem, "POST", $"/pagamento/faturas/{fatura}/pagamentos", FaturasDoExemplo.Corpo("PIX").ToJsonString());

    async Task Estornar(string pagamento, decimal valor) =>
        await Executar(mundo.Admin(), "POST", $"/pagamento/recebimentos/pagamentos/{pagamento}/estornos",
            FaturasDoExemplo.CorpoDoEstorno(valor).ToJsonString());

    [Then(@"^a ""mensagem"" do corpo é ""(.*)""$")]
    public void MensagemDoCorpo(string mensagem) =>
        Assert.True(Campo(mundo.Ultima.Corpo, "mensagem") == mensagem.Replace("\\\"", "\"", StringComparison.Ordinal),
            $"esperava a mensagem \"{mensagem}\": {mundo.Ultima}");

    // ── Então · o "MOD-01" do administrador ────────────────────────────────
    // 17-b RN-233: o token do administrador tem "MOD-01" com "U" só para o
    // card; as rotas do recebedor conferem o papel, e as do pagador o recusam
    [Then(@"^o ""MOD-01"" com ""U"" do administrador só abre o card; as rotas do recebedor checam o papel$")]
    public async Task ModuloDoAdministrador()
    {
        var admin = mundo.Admin();
        var mods = Jwt.Carga(await mundo.TokenDe(admin))["mods"];
        Assert.True(Campo(mods, "MOD-01") == "U", $"esperava \"MOD-01\" com \"U\" no token do administrador, veio {mods?.ToJsonString()}");
        Respostas.Confere(await fat.Ler(admin, "/pagamento/card"), 200);
        Respostas.Confere(await fat.Ler(admin, "/pagamento/faturas"), 403, "ACESSO_NEGADO");
        // a Rita tem o mesmo "MOD-01" com "U", mas não o papel
        Respostas.Confere(await fat.Ler(await fat.Rita(), "/pagamento/recebimentos/resumo?competencia=2026-10"), 403, "ACESSO_NEGADO");
    }
}
