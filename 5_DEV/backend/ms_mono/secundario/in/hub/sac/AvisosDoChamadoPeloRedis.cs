using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.In.Hub.Sac;

// O que volta do Redis para esta réplica no SAC: o canal sac:chamado:<id>
// (a versão do chamado que mudou, RNF-230), pela assinatura por padrão
// sac:chamado:*, uma por réplica e feita no primeiro uso. É a entrada do
// adaptador secundário: o Redis chama de volta; a publicação e a espera das
// conexões ao vivo ficam em CanalDoChamadoNoRedis (secundario/out).
public sealed class AvisosDoChamadoPeloRedis(Armazenamento armazenamento)
{
    public const string Prefixo = "sac:chamado:";

    readonly Lock trava = new();
    Task? assinatura;

    // o canal de um chamado, como SAC-000042
    public static RedisChannel Canal(string chamado) => RedisChannel.Literal(Prefixo + chamado);

    // assina sac:chamado:* uma vez só, na primeira espera da réplica; receber
    // ganha o número do chamado avisado. Se a assinatura falhou, a próxima
    // espera tenta de novo.
    public Task Assinar(Action<string> receber)
    {
        lock (trava)
        {
            if (assinatura is null || assinatura.IsFaulted || assinatura.IsCanceled)
            {
                assinatura = armazenamento.Mensagens.SubscribeAsync(RedisChannel.Pattern(Prefixo + "*"),
                    (canal, _) => receber(canal.ToString()[Prefixo.Length..]));
            }
            return assinatura;
        }
    }
}
