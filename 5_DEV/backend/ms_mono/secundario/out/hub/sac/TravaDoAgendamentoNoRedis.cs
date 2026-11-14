using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// A trava do AgendamentoDoSac (sac-documentos v01): SET sac:prazos NX PX 55 s,
// para uma réplica só rodar as rotinas em cada minuto.
public sealed class TravaDoAgendamentoNoRedis(Armazenamento armazenamento)
{
    static readonly RedisKey Chave = "sac:prazos";
    static readonly TimeSpan Validade = TimeSpan.FromSeconds(55);

    public Task<bool> Pegar(CancellationToken ct = default) =>
        armazenamento.Cache.StringSetAsync(Chave, Environment.MachineName, Validade, When.NotExists);
}
