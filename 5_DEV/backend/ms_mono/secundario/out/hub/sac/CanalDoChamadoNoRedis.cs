using System.Collections.Concurrent;
using System.Globalization;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// O aviso de mudança de um chamado (RNF-230): PUBLISH sac:chamado:<id> com a
// versão; a assinatura por padrão (sac:chamado:*), uma por réplica e feita no
// primeiro uso, acorda as conexões ao vivo daquele chamado. Se o aviso se
// perder, a conferência seguinte (a cada 4 s) lê o banco de qualquer jeito.
public sealed class CanalDoChamadoNoRedis(Armazenamento armazenamento) : ICanalDoChamado
{
    const string Prefixo = "sac:chamado:";

    readonly ConcurrentDictionary<string, TaskCompletionSource> esperas = new();
    readonly SemaphoreSlim assinando = new(1, 1);
    bool assinado;

    public Task Publicar(NumeroDoChamado id, long versao, CancellationToken ct = default) =>
        armazenamento.Mensagens.PublishAsync(RedisChannel.Literal(Prefixo + id.Valor), versao.ToString(CultureInfo.InvariantCulture));

    public async Task<bool> Esperar(NumeroDoChamado id, TimeSpan tempo, CancellationToken ct = default)
    {
        await Assinar();
        var espera = esperas.GetOrAdd(id.Valor, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        var fim = await Task.WhenAny(espera.Task, Task.Delay(tempo, ct));
        ct.ThrowIfCancellationRequested();
        return fim == espera.Task;
    }

    async Task Assinar()
    {
        if (assinado) return;
        await assinando.WaitAsync();
        try
        {
            if (assinado) return;
            await armazenamento.Mensagens.SubscribeAsync(RedisChannel.Pattern(Prefixo + "*"), (canal, _) =>
            {
                var id = canal.ToString()[Prefixo.Length..];
                if (esperas.TryRemove(id, out var espera)) espera.TrySetResult();
            });
            assinado = true;
        }
        finally
        {
            assinando.Release();
        }
    }
}
