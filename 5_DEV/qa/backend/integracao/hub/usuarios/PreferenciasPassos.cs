using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos da RN-283 de 1_Requisitos/17_modulo-usuario.md: GET e PUT
// /usuarios/eu/preferencias (contrato usuarios-contrato-api v14), sem módulo
// exigido, sem X-Trace-Id e sem auditoria. Os pedidos do Quando são os
// genéricos "envia GET …" (AutorizacaoPassos) e "envia PUT … com:"
// (AutenticacaoPassos), com o token de quem o Dado citou (Mundo.Sujeito).
[Binding]
public sealed class PreferenciasPassos(Mundo mundo)
{
    const string Caminho = "/usuarios/eu/preferencias";

    Sessao? outroAparelho;
    Auditoria.Marca? marca;

    // o participante entra pelo código, no quest (Mundo.SessaoParaPedido)
    [Given(@"^o participante Lucas Andrade com o token de acesso, sem escolha salva$")]
    public async Task LucasComToken()
    {
        var lucas = await mundo.Lucas();
        await mundo.SessaoParaPedido(lucas);
        mundo.Sujeito = lucas;
    }

    // as duas sessões (navegador e app) abrem antes da marca: entrar não é o
    // que o cenário confere
    [Given(@"^a conta master da EBFGA com o token de acesso, sem nenhum módulo exigido$")]
    public async Task MasterEmDoisAparelhos()
    {
        var master = await mundo.Master();
        await mundo.TokenDe(master);
        outroAparelho = await mundo.Entrar(master, App);
        mundo.Sujeito = master;
        marca = await Auditoria.Marcar();
    }

    [Then(@"^a resposta é (\d{3}), sem o cabeçalho X-Trace-Id, com:$")]
    public async Task SemTraceIdCom(int status, string json)
    {
        Respostas.Confere(mundo.Ultima, status);
        SemTraceId();
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), mundo.Ultima.Corpo, exato: true);
    }

    [Then(@"^a resposta é (\d{3}) com o mesmo corpo, sem o cabeçalho X-Trace-Id$")]
    public void MesmoCorpo(int status)
    {
        Respostas.Confere(mundo.Ultima, status);
        SemTraceId();
        Comparacao.Confere(JsonNode.Parse(mundo.UltimoPedido.Corpo!), mundo.Ultima.Corpo, exato: true);
    }

    [Then(@"^o GET /usuarios/eu/preferencias de outro aparelho dela traz ""(\w+)"" e (\d+)$")]
    public async Task OutroAparelho(string atualizacao, int intervalo)
    {
        var r = await mundo.Enviar("GET", Caminho, token: outroAparelho!.Acesso);
        Respostas.Confere(r, 200);
        Comparacao.Confere(new JsonObject { ["atualizacaoCards"] = atualizacao, ["intervaloAoVivo"] = intervalo }, r.Corpo, exato: true);
    }

    [Then(@"^as coleções ""auditoria"" e ""auditoriaErros"" não ganham registro, nem no GET nem no PUT$")]
    public async Task SemAuditoria()
    {
        foreach (var colecao in new[] { Auditoria.Registros, Auditoria.Erros })
        {
            var novos = await Auditoria.Novos(colecao, marca!);
            Assert.True(novos.Count == 0, $"\"{colecao}\" ganhou registro: {Auditoria.Resumo(novos)}");
        }
    }

    // o outro campo vai válido, para a recusa ser só pelo campo do exemplo
    [When(@"^a conta master envia PUT /usuarios/eu/preferencias com (\w+) igual a (.+)$")]
    public async Task ValorInvalido(string campo, string valor)
    {
        var corpo = new JsonObject { ["atualizacaoCards"] = "AO_VIVO", ["intervaloAoVivo"] = 15 };
        corpo[campo] = JsonNode.Parse(valor);
        await mundo.EnviarComo(await mundo.Master(), "PUT", Caminho, corpo.ToJsonString());
    }

    void SemTraceId() =>
        Assert.True(string.IsNullOrEmpty(mundo.Ultima.Cabecalho("X-Trace-Id")),
            $"a resposta não devia trazer X-Trace-Id, veio \"{mundo.Ultima.Cabecalho("X-Trace-Id")}\": {mundo.Ultima}");
}
