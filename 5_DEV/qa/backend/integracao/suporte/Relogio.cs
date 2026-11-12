using System.Text.Json.Nodes;
using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

// Relógio do backend, só para teste (15 RN-278: o fechamento da 00:00 de
// Brasília). O backend expõe, só com AMBIENTE=hml e Teste__Relogio=sim (o
// backend do qa no ci.sh); fora disso os endereços não existem (404):
//
//   PUT /_teste/relogio      {"agora": "2027-02-28T23:50:00-03:00"}   → 204
//       o TimeProvider da aplicação passa a contar a partir dessa hora, no
//       ritmo real (tokens, sessões, card e fechamento usam esse relógio)
//   DELETE /_teste/relogio                                            → 204
//       volta ao relógio real
//   POST /_teste/fechamento                                           → 204
//       roda agora o fechamento da 00:00 de Brasília para a data do relógio
//       (o mesmo caso de uso do agendamento) e só responde ao terminar
//
// Sem login: a proteção é o endereço não existir fora do ambiente de teste.
// A suíte continua caixa-preta: só fala HTTP com esses endereços.
public sealed class Relogio(Api api)
{
    public bool Mudou { get; private set; }

    public async Task Definir(DateTimeOffset agora)
    {
        Mudou = true;
        var r = await api.Enviar("PUT", "/_teste/relogio", new JsonObject { ["agora"] = agora.ToString("yyyy-MM-ddTHH:mm:sszzz") }.ToJsonString());
        Mundo.Exigir(r, 204, "PUT /_teste/relogio (relógio de teste do backend, Teste__Relogio=sim)");
    }

    public async Task Fechamento() =>
        Mundo.Exigir(await api.Enviar("POST", "/_teste/fechamento"), 204, "POST /_teste/fechamento (fechamento da 00:00 sob demanda)");

    public async Task Real()
    {
        if (!Mudou) return;
        Mudou = false;
        try { await api.Enviar("DELETE", "/_teste/relogio"); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // o próximo cenário que mudar o relógio o define de novo
        }
    }
}

// o relógio volta ao real no fim do cenário que o mudou
[Binding]
public sealed class RelogioNoFim(Relogio relogio)
{
    [AfterScenario]
    public Task VoltarAoReal() => relogio.Real();
}
