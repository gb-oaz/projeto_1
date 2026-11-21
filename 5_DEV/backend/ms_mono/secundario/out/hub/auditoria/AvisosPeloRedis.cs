using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// Uma assinatura de um canal do Redis (pub/sub): cada conexão ao vivo da Auditoria assina o canal na abertura, antes de
// responder, e recebe só o que chega depois. O que não se lê (mensagem malformada) fica de fora.
public sealed class AssinaturaNoRedis<T> : IAssinatura<T>
{
    readonly Channel<T> canal = Channel.CreateUnbounded<T>();
    readonly ISubscriber assinante;
    readonly RedisChannel nome;
    readonly Action<RedisChannel, RedisValue> tratar;
    int descartada;

    AssinaturaNoRedis(ISubscriber assinante, RedisChannel nome, Func<string, T?> ler)
    {
        (this.assinante, this.nome) = (assinante, nome);
        tratar = (_, mensagem) =>
        {
            if (mensagem.HasValue && ler(mensagem.ToString()) is { } lida) canal.Writer.TryWrite(lida);
        };
    }

    public static async Task<AssinaturaNoRedis<T>> Assinar(ISubscriber assinante, RedisChannel nome, Func<string, T?> ler)
    {
        var assinatura = new AssinaturaNoRedis<T>(assinante, nome, ler);
        await assinante.SubscribeAsync(nome, assinatura.tratar);
        return assinatura;
    }

    public async IAsyncEnumerable<T> Avisos([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var aviso in canal.Reader.ReadAllAsync(ct)) yield return aviso;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref descartada, 1) != 0) return;
        canal.Writer.TryComplete();
        try
        {
            await assinante.UnsubscribeAsync(nome, tratar);
        }
        catch (Exception)
        {
            // sem o cache, a assinatura já se foi com a conexão
        }
    }
}

// O aviso de cada registro gravado (auditoria-documentos v02): PUBLISH auditoria:registros depois de gravar auditoria e
// auditoriaErros. A falha do PUBLISH é só do aviso: o registro já está gravado e a ação segue (RNF-249).
public sealed class AvisosDaAuditoriaPeloRedis(Armazenamento armazenamento) : IAvisosDaAuditoria
{
    public const string Canal = "auditoria:registros";

    public Task Publicar(RegistroBruto registro, CancellationToken ct = default) =>
        armazenamento.Mensagens.PublishAsync(RedisChannel.Literal(Canal), JsonSerializer.Serialize(registro));

    public async Task<IAssinatura<RegistroBruto>> Assinar(CancellationToken ct = default) =>
        await AssinaturaNoRedis<RegistroBruto>.Assinar(armazenamento.Mensagens, RedisChannel.Literal(Canal), Ler);

    static RegistroBruto? Ler(string texto)
    {
        try
        {
            return JsonSerializer.Deserialize<RegistroBruto>(texto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

// O aviso de cada mudança de estado: PUBLISH auditoria:ocorrencias a cada mudança, para todo ao vivo da Auditoria.
public sealed class AvisosDeOcorrenciasPeloRedis(Armazenamento armazenamento) : IAvisosDeOcorrencias
{
    public const string Canal = "auditoria:ocorrencias";

    public Task Publicar(Ocorrencia ocorrencia, CancellationToken ct = default) =>
        armazenamento.Mensagens.PublishAsync(RedisChannel.Literal(Canal), JsonSerializer.Serialize(ocorrencia));

    public async Task<IAssinatura<Ocorrencia>> Assinar(CancellationToken ct = default) =>
        await AssinaturaNoRedis<Ocorrencia>.Assinar(armazenamento.Mensagens, RedisChannel.Literal(Canal), Ler);

    static Ocorrencia? Ler(string texto)
    {
        try
        {
            return JsonSerializer.Deserialize<Ocorrencia>(texto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
