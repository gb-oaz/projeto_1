using System.Globalization;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Pagamento;

// Os números do card do Pagamento (19_modulo-pagamento.md v05, RN-307): a
// versão que sobe a cada mudança de fatura, os pontos de cada série, a
// tendência do deboaz e o selo da instituição. O pedido é o GET
// /pagamento/card de PagamentoPassos.cs; o card do dia sai do agendamento.
public sealed partial class PagamentoPassos
{
    // RN-307, pontos de cada série: quem lê e qual série
    (string Perfil, string Serie)? serieLida;

    // ── Dado ───────────────────────────────────────────────────────────────
    // o recebido de cada mês é uma fatura da EBFGA paga no cartão de crédito
    // (aprovado na hora) no mês: outubro paga em 2026-11-05 e novembro em
    // 2026-12-03, quando o administrador lê o card
    [Given(@"^o recebido de (\d+\.\d{2}) no mês e de (\d+\.\d{2}) no mês anterior$")]
    public async Task RecebidoNoMesENoAnterior(string atual, string anterior)
    {
        var (master, conta, rita) = (await mundo.Master(), await mundo.Conta(), await fat.Rita());
        static decimal D(string v) => decimal.Parse(v, CultureInfo.InvariantCulture);
        await fat.Saltar("2026-10-28T12:00:00Z");
        if (D(anterior) > 0m)
        {
            var outubro = await fat.Emitir(master, conta, "EBFGA", "2026-10", ("MOD-03", D(anterior)));
            await fat.Saltar("2026-11-05T13:00:00Z");
            await fat.Pagar(rita, outubro, "CARTAO_CREDITO", status: "PAGO");
        }
        var novembro = await fat.Emitir(master, conta, "EBFGA", "2026-11", ("MOD-03", D(atual)));
        await fat.Saltar("2026-12-03T13:00:00Z");
        await fat.Pagar(rita, novembro, "CARTAO_CREDITO", status: "PAGO");
        await fat.Agendamento();
    }

    // ── Quando · RN-307 pontos de cada série ───────────────────────────────
    // a história da EBFGA: agosto emitida e cancelada (2.100,00), setembro e
    // outubro pagas (19.905,22 cada, em 2026-10-05 e 2026-11-05) e novembro
    // paga no crédito em 2026-12-03 (3.698,33), quando o card é lido
    [When(@"^(o deboaz|a instituição) lê a série ""(dia|semana|mes)"" do card$")]
    public async Task LeASerie(string perfil, string serie)
    {
        await fat.Saltar("2026-08-28T12:00:00Z");
        var (master, conta, rita) = (await mundo.Master(), await mundo.Conta(), await fat.Rita());
        var agosto = await fat.Emitir(master, conta, "EBFGA", "2026-08", ("MOD-03", 2100.00m));
        await fat.Cancelar(agosto);
        await fat.Fat247();
        await fat.Fat318();
        await fat.Fat402();
        await fat.CreditoPago("PAG-000773");
        await fat.Agendamento();
        serieLida = (perfil, serie);
        await Executar(perfil == "o deboaz" ? mundo.Admin() : rita, "GET", "/pagamento/card", null);
    }

    // ── Então ──────────────────────────────────────────────────────────────
    // RN-307: muda uma fatura que o card lê (a FAT-000402 é paga) e lê de novo
    [Then(@"^a cada mudança de uma fatura que o card lê, ""versao"" passa à versão lida \+ 1$")]
    public async Task VersaoDoCardSobe()
    {
        var quem = await fat.Rita();
        var lida = Exigir(await fat.Ler(quem, "/pagamento/card"), 200, "GET /pagamento/card").Corpo!["versao"]!.GetValue<int>();
        await fat.CreditoPago("PAG-000773");
        var depois = Exigir(await fat.Ler(quem, "/pagamento/card"), 200, "GET /pagamento/card").Corpo;
        Assert.True(depois?["versao"]?.GetValue<int>() == lida + 1, $"esperava a versão {lida + 1} depois de a FAT-000402 ser paga: {depois?.ToJsonString()}");
    }

    // os pontos do exemplo, do mais antigo ao atual: o último é o dia, a
    // semana ou o mês de 2026-12-03 (a competência 2026-11 para a instituição)
    [Then(@"^cada ponto é (.+), do mais antigo ao atual$")]
    public void PontosDaSerie(string ponto)
    {
        var (perfil, serie) = serieLida ?? throw new InvalidOperationException("o Quando não leu a série");
        Respostas.Confere(mundo.Ultima, 200);
        var pontos = (mundo.Ultima.Corpo?["series"]?[serie]?.AsArray() ?? []).Select(p => p?.GetValue<decimal>() ?? -1m).ToList();
        Assert.True(pontos.Count == 12, $"esperava 12 pontos em series.{serie}: {mundo.Ultima}");
        const decimal Novembro = 3698.33m, Pagas = 19905.22m;
        var texto = string.Join(", ", pontos.Select(p => p.ToString(CultureInfo.InvariantCulture)));
        switch (perfil, serie)
        {
            // um pagamento por dia pago: só o de hoje cai nos 12 últimos dias
            case (_, "dia"):
                Assert.True(pontos[^1] == Novembro && pontos.Take(11).All(p => p == 0m),
                    $"{ponto}: esperava {Novembro} hoje e 0 nos 11 dias antes, vieram {texto}");
                break;
            // os 7 dias até hoje têm só o pagamento de hoje
            case (_, "semana"):
                Assert.True(pontos[^1] == Novembro && pontos.All(p => p >= 0m), $"{ponto}: esperava {Novembro} no último ponto, vieram {texto}");
                break;
            // pela data do pagamento: outubro, novembro e dezembro
            case ("o deboaz", "mes"):
                Assert.True(pontos.Skip(9).SequenceEqual([Pagas, Pagas, Novembro]),
                    $"{ponto}: esperava {Pagas}, {Pagas} e {Novembro} nos três últimos meses, vieram {texto}");
                break;
            // pela competência: agosto cancelada (0), setembro, outubro e novembro
            default:
                var ultimo = pontos.FindLastIndex(p => p != 0m);
                Assert.True(ultimo >= 3 && pontos.Skip(ultimo - 3).Take(4).SequenceEqual([0m, Pagas, Pagas, Novembro]) && pontos.Skip(ultimo + 1).All(p => p == 0m),
                    $"{ponto}: esperava 0 (agosto, cancelada), {Pagas}, {Pagas} e {Novembro} até a última competência, vieram {texto}");
                break;
        }
    }

    [Then(@"^""normal\.rotulo"" é ""(.+)""$")]
    public void RotuloDaNormal(string rotulo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(Campo(mundo.Ultima.Corpo?["normal"], "rotulo") == rotulo, $"esperava \"normal.rotulo\" \"{rotulo}\": {mundo.Ultima}");
    }

    [Then(@"^""selo"" é (\d+), porque conta a pagar em até 5 dias úteis$")]
    public void SeloDaInstituicao(int selo)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Corpo?["selo"]?.GetValue<int>() == selo, $"esperava o selo {selo}: {mundo.Ultima}");
    }
}
