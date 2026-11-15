using System.Collections.Concurrent;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.MsMono.Secundario.In.Hub.Pagamento;
using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// O aviso de mudança de um pagamento (RNF-238): PUBLISH pagamento:pagamento:<id>
// e a espera das conexões ao vivo daquele pagamento nesta réplica. O que chega
// pelo canal entra por AvisosDoPagamentoPeloRedis (secundario/in), que acorda a
// espera. Se o aviso se perder, a conferência seguinte lê o banco de qualquer jeito.
public sealed class CanalDoPagamentoNoRedis(Armazenamento armazenamento, AvisosDoPagamentoPeloRedis avisos) : ICanalDoPagamento
{
    readonly ConcurrentDictionary<string, TaskCompletionSource> esperas = new();

    public Task Publicar(string pagamentoId, CancellationToken ct = default) =>
        armazenamento.Mensagens.PublishAsync(AvisosDoPagamentoPeloRedis.Canal(pagamentoId), "mudou");

    public async Task<bool> Esperar(string pagamentoId, TimeSpan tempo, CancellationToken ct = default)
    {
        await avisos.Assinar(Acordar);
        var espera = esperas.GetOrAdd(pagamentoId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        var fim = await Task.WhenAny(espera.Task, Task.Delay(tempo, ct));
        ct.ThrowIfCancellationRequested();
        return fim == espera.Task;
    }

    void Acordar(string pagamento)
    {
        if (esperas.TryRemove(pagamento, out var espera)) espera.TrySetResult();
    }
}

// A trava do AgendamentoDoPagamento (pagamento-documentos v02): SET
// pagamento:agendamento NX PX 55 s, para uma réplica só rodar em cada minuto.
public sealed class TravaDoAgendamentoDoPagamentoNoRedis(Armazenamento armazenamento)
{
    static readonly RedisKey Chave = "pagamento:agendamento";
    static readonly TimeSpan Validade = TimeSpan.FromSeconds(55);

    public Task<bool> Pegar(CancellationToken ct = default) =>
        armazenamento.Cache.StringSetAsync(Chave, Environment.MachineName, Validade, When.NotExists);
}
