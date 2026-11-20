using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Hub.Pagamento;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// O estado da conta com o Pagamento, nos Cenários de API de
// 1_Requisitos/17_modulo-usuario.md (v110): a própria conta se desativa, o
// que ela não pode, e a fatura em aberto que segue devida ou impede o
// encerramento (RF-113). As faturas são as do 19 (hub/pagamento/
// FaturasDoExemplo.cs): a FAT-000402 da EBFGA e a FAT-000371 do Pedro Reis.
[Binding]
public sealed class EstadoDaContaPassos(Mundo mundo, FaturasDoExemplo fat)
{
    // o token do Dado ("esse token") e a fatura em aberto da conta de quem o tem
    Sessao? sessao;
    string? faturaEmAberto;
    // a conta e a fatura antes do pedido, para "não muda" e "seguem como estavam"
    JsonObject? contaAntes;
    JsonObject? faturaAntes;

    async Task<JsonObject> LerConta(string contaId) =>
        Exigir(await fat.Ler(mundo.Admin(), $"/contas/{contaId}"), 200, $"GET /contas/{contaId}").Corpo!.AsObject();

    // ── Dado ───────────────────────────────────────────────────────────────
    [Given(@"^a conta-ebfga com a FAT-000402 ""A_PAGAR""$")]
    public async Task EbfgaComFatura()
    {
        var fatura = await fat.Fat402();
        faturaAntes = await fat.FaturaDoRecebedor(fatura);
        Assert.True(Campo(faturaAntes, "status") == "A_PAGAR", $"preparo: a FAT-000402 devia estar A_PAGAR: {faturaAntes.ToJsonString()}");
        contaAntes = await LerConta(await mundo.Conta());
    }

    // a conta master e o autônomo já têm uma fatura em aberto (a "segue
    // devida" do Esquema), criada antes de entrar: o relógio da fatura vem antes
    [Given(@"^o token de (a conta master da EBFGA|o autônomo Pedro Reis|a staff Maria, com ""MOD-03"" com ""CU"")$")]
    public async Task TokenDe(string quem)
    {
        Pessoa pessoa;
        switch (quem)
        {
            case "a conta master da EBFGA":
                faturaEmAberto = await fat.Fat402();
                pessoa = await mundo.Master();
                break;
            case "o autônomo Pedro Reis":
                faturaEmAberto = await fat.Fat371(vencida: false);
                pessoa = await mundo.Pedro();
                break;
            default:
                pessoa = await mundo.Maria();
                await mundo.Conceder(pessoa, new() { ["MOD-03"] = "CU" });
                break;
        }
        mundo.Sujeito = pessoa;
        sessao = await mundo.Entrar(pessoa, Navegador);
    }

    // ── Quando ─────────────────────────────────────────────────────────────
    [When(@"^envia POST /contas/(conta-[\w-]+)/estado com o estado ""(\w+)""$")]
    public async Task EnviaOEstado(string conta, string estado)
    {
        var contaId = await mundo.Traduzir(conta);
        contaAntes = await LerConta(contaId);
        var token = sessao?.Acesso ?? throw new InvalidOperationException("o Dado não deu o token");
        await mundo.Enviar("POST", $"/contas/{contaId}/estado", new JsonObject { ["estado"] = estado }.ToJsonString(), token);
    }

    // ── Então ──────────────────────────────────────────────────────────────
    [Then(@"^o próximo pedido com esse token recebe 401 SESSAO_TERMINADA$")]
    public async Task TokenTerminado()
    {
        var token = sessao?.Acesso ?? throw new InvalidOperationException("o Dado não deu o token");
        Respostas.Confere(await mundo.Enviar("GET", "/auth/sessoes", token: token), 401, "SESSAO_TERMINADA");
    }

    // devida: a fatura segue a pagar (ou vencida), sem ser cancelada pela desativação
    [Then(@"^a fatura em aberto da (conta-[\w-]+) segue devida$")]
    public async Task FaturaSegueDevida(string conta)
    {
        var fatura = await fat.FaturaDoRecebedor(faturaEmAberto ?? throw new InvalidOperationException($"o Dado não criou a fatura em aberto da {conta}"));
        Assert.True(Campo(fatura, "status") is "A_PAGAR" or "VENCIDA", $"a fatura em aberto da {conta} devia seguir devida: {fatura.ToJsonString()}");
    }

    [Then(@"^a resposta é 403 ACESSO_NEGADO e o estado da (conta-[\w-]+) não muda$")]
    public async Task EstadoNaoMuda(string conta)
    {
        Respostas.Confere(mundo.Ultima, 403, "ACESSO_NEGADO");
        var agora = await LerConta(await mundo.Traduzir(conta));
        Assert.True(Campo(agora, "estado") == Campo(contaAntes, "estado"), $"o estado da {conta} mudou: era {Campo(contaAntes, "estado")}, ficou {agora.ToJsonString()}");
    }

    [Then(@"^GET /pagamento/recebimentos/faturas/FAT-000402 segue com o status ""(\w+)""$")]
    public async Task FaturaSegue(string status)
    {
        var fatura = await fat.FaturaDoRecebedor(fat.Real("FAT-000402"));
        Assert.True(Campo(fatura, "status") == status, $"esperava a FAT-000402 com o status {status}: {fatura.ToJsonString()}");
    }

    [Then(@"^a conta-ebfga e a FAT-000402 seguem como estavam$")]
    public async Task SeguemComoEstavam()
    {
        var conta = await LerConta(await mundo.Conta());
        var fatura = await fat.FaturaDoRecebedor(fat.Real("FAT-000402"));
        Assert.True(JsonNode.DeepEquals(contaAntes, conta), $"a conta-ebfga mudou: era {contaAntes?.ToJsonString()}, ficou {conta.ToJsonString()}");
        Assert.True(JsonNode.DeepEquals(faturaAntes, fatura), $"a FAT-000402 mudou: era {faturaAntes?.ToJsonString()}, ficou {fatura.ToJsonString()}");
    }

    static string? Campo(JsonNode? no, string campo) => FaturasDoExemplo.Campo(no, campo);
}
