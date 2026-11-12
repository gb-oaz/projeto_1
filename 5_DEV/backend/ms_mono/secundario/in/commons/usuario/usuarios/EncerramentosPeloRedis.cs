using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.In.Commons.Usuario.Usuarios;

// O que volta do Redis para esta réplica: o canal aoVivo:encerrar
// ("<conexaoId> <codigo>"), pelo qual a réplica dona de uma conexão ao vivo
// fica sabendo que ela termina (RN-279, RN-281). É a entrada do adaptador
// secundário: o Redis chama de volta; a publicação fica em ConexoesNoRedis.
public sealed class EncerramentosPeloRedis(Armazenamento armazenamento)
{
    public static readonly RedisChannel Canal = RedisChannel.Literal("aoVivo:encerrar");

    readonly Lock trava = new();
    Task? assinatura;

    public static string Mensagem(string conexaoId, MotivoEncerramento motivo) => $"{conexaoId} {motivo.Texto()}";

    // assina o canal uma vez só, na primeira conexão ao vivo da réplica
    public Task Assinar(Action<string, MotivoEncerramento> receber)
    {
        lock (trava)
        {
            return assinatura ??= armazenamento.Mensagens.SubscribeAsync(Canal, (_, mensagem) =>
            {
                var partes = mensagem.ToString().Split(' ', 2);
                if (partes.Length == 2 && TextosDoCard.MotivoDe(partes[1]) is { } motivo) receber(partes[0], motivo);
            });
        }
    }
}
