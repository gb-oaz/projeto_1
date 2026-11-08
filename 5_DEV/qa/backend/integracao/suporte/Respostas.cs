using System.Text.Json.Nodes;
using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

// Passos de resposta comuns a todas as features: status, código de erro,
// mensagem e o JSON do exemplo.
[Binding]
public sealed class Respostas(Mundo mundo)
{
    [Then(@"^a resposta é (\d{3})$")]
    public void Status(int status) => Confere(mundo.Ultima, status);

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+)$")]
    public void StatusECodigo(int status, string codigo) => Confere(mundo.Ultima, status, codigo);

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com (?:a mensagem )?""(.*)""$")]
    public void StatusCodigoEMensagem(int status, string codigo, string mensagem)
    {
        Confere(mundo.Ultima, status, codigo);
        Assert.Equal(mensagem, mundo.Ultima.Corpo?["mensagem"]?.GetValue<string>());
    }

    [Then(@"^a mensagem é ""(.*)""$")]
    public void Mensagem(string mensagem) =>
        Assert.True(mundo.Ultima.Corpo?["mensagem"]?.GetValue<string>() == mensagem, $"esperava a mensagem \"{mensagem}\", veio {mundo.Ultima}");

    [Then(@"^a resposta é (\d{3}) com:$")]
    public async Task StatusEJson(int status, string json)
    {
        Confere(mundo.Ultima, status);
        var esperado = JsonNode.Parse(await mundo.Traduzir(json));
        if (esperado is JsonObject campos) foreach (var campo in mundo.CamposSemConferir) campos.Remove(campo);
        Comparacao.Confere(esperado, mundo.Ultima.Corpo);
    }

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com:$")]
    public async Task StatusCodigoEJson(int status, string codigo, string json)
    {
        Confere(mundo.Ultima, status, codigo);
        var esperado = JsonNode.Parse(await mundo.Traduzir(json));
        Comparacao.Confere(esperado, mundo.Ultima.Corpo);
        // "chega às 09:00" e "tentarEm 09:15": a resposta tenta de novo 15 min depois do pedido
        if (esperado?["tentarEm"] is { } exemplo && mundo.HoraDoExemplo is { } hora)
        {
            var espera = DateTimeOffset.Parse(exemplo.GetValue<string>()).TimeOfDay - hora;
            TentarEm(mundo, espera);
        }
    }

    public static void TentarEm(Mundo mundo, TimeSpan espera)
    {
        var tentarEm = DateTimeOffset.Parse(mundo.Ultima.Corpo!["tentarEm"]!.GetValue<string>());
        var veio = tentarEm - mundo.HoraDoPedido;
        Assert.True((veio - espera).Duration() < TimeSpan.FromMinutes(1),
            $"esperava tentarEm {espera} depois do pedido, veio {veio} ({mundo.Ultima})");
    }

    public static void Confere(Resposta r, int status, string? codigo = null)
    {
        Assert.True(r.Status == status, $"esperava {status}, veio {r}");
        if (codigo is not null) Assert.True(r.Corpo?["codigo"]?.GetValue<string>() == codigo, $"esperava o código {codigo}, veio {r}");
    }
}
