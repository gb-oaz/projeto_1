using System.Collections.Concurrent;
using System.Globalization;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.In.Hub.Sac;
using Boaz.MsMono.Secundario.Out.Commons;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// O aviso de mudança de um chamado (RNF-230): PUBLISH sac:chamado:<id> com a
// versão, e a espera das conexões ao vivo daquele chamado nesta réplica. O que
// chega pelo canal entra por AvisosDoChamadoPeloRedis (secundario/in), que
// acorda a espera. Se o aviso se perder, a conferência seguinte (a cada 4 s)
// lê o banco de qualquer jeito.
public sealed class CanalDoChamadoNoRedis(Armazenamento armazenamento, AvisosDoChamadoPeloRedis avisos) : ICanalDoChamado
{
    readonly ConcurrentDictionary<string, TaskCompletionSource> esperas = new();

    public Task Publicar(NumeroDoChamado id, long versao, CancellationToken ct = default) =>
        armazenamento.Mensagens.PublishAsync(AvisosDoChamadoPeloRedis.Canal(id.Valor), versao.ToString(CultureInfo.InvariantCulture));

    public async Task<bool> Esperar(NumeroDoChamado id, TimeSpan tempo, CancellationToken ct = default)
    {
        await avisos.Assinar(Acordar);
        var espera = esperas.GetOrAdd(id.Valor, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        var fim = await Task.WhenAny(espera.Task, Task.Delay(tempo, ct));
        ct.ThrowIfCancellationRequested();
        return fim == espera.Task;
    }

    // o aviso de um chamado: acorda quem espera por ele nesta réplica
    void Acordar(string chamado)
    {
        if (esperas.TryRemove(chamado, out var espera)) espera.TrySetResult();
    }
}
