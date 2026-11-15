using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.In.Hub.Pagamento;

// O que volta do Redis para esta réplica no Pagamento: o canal
// pagamento:pagamento:<id> (o pagamento que mudou de status, RNF-238), pela
// assinatura por padrão pagamento:pagamento:*, uma por réplica e feita no
// primeiro uso. A publicação e a espera ficam em CanalDoPagamentoNoRedis.
public sealed class AvisosDoPagamentoPeloRedis(Armazenamento armazenamento)
{
    public const string Prefixo = "pagamento:pagamento:";

    readonly Lock trava = new();
    Task? assinatura;

    // o canal de um pagamento, como PAG-000781
    public static RedisChannel Canal(string pagamento) => RedisChannel.Literal(Prefixo + pagamento);

    // assina pagamento:pagamento:* uma vez só; se falhou, a próxima espera tenta de novo
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
