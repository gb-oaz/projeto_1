using System.Collections.Concurrent;
using System.Globalization;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using StackExchange.Redis;
using ISessoes = Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out.ISessoes;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Usuarios;

// As conexões ao vivo de cada usuário, entre as réplicas (identidade-acesso-
// documentos v18): o zset aoVivo:<usuarioId> (membro conexaoId, score abertaEm
// em ms) com TTL de 600 s refeito a cada intervalo, e o canal aoVivo:encerrar
// ("<conexaoId> <codigo>"), pelo qual a réplica dona da conexão fica sabendo
// que ela termina. Cada réplica guarda o sinal das conexões que ela atende.
public sealed class ConexoesNoRedis : IConexoesAoVivo
{
    const int Limite = 3;
    static readonly TimeSpan Prazo = TimeSpan.FromSeconds(600);
    static readonly RedisChannel Canal = RedisChannel.Literal("aoVivo:encerrar");

    readonly Armazenamento armazenamento;
    readonly TimeProvider relogio;
    readonly ConcurrentDictionary<string, TaskCompletionSource<MotivoEncerramento>> locais = new();
    readonly Lazy<Task> assinatura;

    public ConexoesNoRedis(Armazenamento armazenamento, TimeProvider relogio)
    {
        this.armazenamento = armazenamento;
        this.relogio = relogio;
        assinatura = new(() => armazenamento.Mensagens.SubscribeAsync(Canal, (_, mensagem) => Receber(mensagem)));
    }

    static RedisKey Chave(string usuarioId) => $"aoVivo:{usuarioId}";

    public async Task<IReadOnlyList<string>> Abrir(string usuarioId, string conexaoId, CancellationToken ct = default)
    {
        await assinatura.Value;
        locais[conexaoId] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = armazenamento.Cache;
        var chave = Chave(usuarioId);
        await cache.SortedSetAddAsync(chave, conexaoId, relogio.GetUtcNow().ToUnixTimeMilliseconds());
        await cache.KeyExpireAsync(chave, Prazo);
        var total = await cache.SortedSetLengthAsync(chave);
        if (total <= Limite) return [];
        var sobra = await cache.SortedSetRangeByRankAsync(chave, 0, total - Limite - 1);
        await cache.SortedSetRemoveAsync(chave, sobra);
        return sobra.Select(s => s.ToString()).ToList();
    }

    // a dona da conexão é esta réplica: o sinal vai direto; senão, pelo canal
    public async Task Encerrar(string conexaoId, MotivoEncerramento motivo, CancellationToken ct = default)
    {
        if (locais.TryGetValue(conexaoId, out var sinal))
        {
            sinal.TrySetResult(motivo);
            return;
        }
        await armazenamento.Mensagens.PublishAsync(Canal, $"{conexaoId} {motivo.Texto()}");
    }

    public Task Renovar(string usuarioId, string conexaoId, CancellationToken ct = default) =>
        armazenamento.Cache.KeyExpireAsync(Chave(usuarioId), Prazo);

    public async Task Fechar(string usuarioId, string conexaoId, CancellationToken ct = default)
    {
        locais.TryRemove(conexaoId, out _);
        await armazenamento.Cache.SortedSetRemoveAsync(Chave(usuarioId), conexaoId);
    }

    public async Task<MotivoEncerramento?> Esperar(string conexaoId, TimeSpan tempo, CancellationToken ct = default)
    {
        if (!locais.TryGetValue(conexaoId, out var sinal))
        {
            await Task.Delay(tempo, relogio, ct);
            return null;
        }
        if (!sinal.Task.IsCompleted)
        {
            using var parar = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var espera = Task.Delay(tempo, relogio, parar.Token);
            await Task.WhenAny(sinal.Task, espera);
            await parar.CancelAsync();
            ct.ThrowIfCancellationRequested();
        }
        return sinal.Task.IsCompleted ? sinal.Task.Result : null;
    }

    void Receber(RedisValue mensagem)
    {
        var partes = mensagem.ToString().Split(' ', 2);
        if (partes.Length == 2 && TextosDoCard.MotivoDe(partes[1]) is { } motivo && locais.TryGetValue(partes[0], out var sinal))
        {
            sinal.TrySetResult(motivo);
        }
    }
}

// A trava do fechamento (fechamentoCard:<dia>, SET NX): uma réplica só fecha
// o dia. Dura 26 h, mais que o dia, para não voltar a fechar no mesmo dia.
public sealed class TravaNoRedis(Armazenamento armazenamento) : ITravaDoFechamento
{
    static readonly TimeSpan Prazo = TimeSpan.FromHours(26);

    public Task<bool> Pegar(DateOnly dia, bool renovar = false, CancellationToken ct = default) =>
        armazenamento.Cache.StringSetAsync($"fechamentoCard:{dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", "1", Prazo,
            renovar ? When.Always : When.NotExists);
}

// O que o ao vivo confere a cada intervalo (RN-281): a sessão do token
// (sessao:<sid>, da Autenticação) e o MOD-03 · U pelas permissões de agora (da
// Autorização), como o [ComAcesso] da rota confere na abertura.
public sealed class AcessoDoAoVivo(ISessoes sessoes, ICalcularPermissoes calcular, IAutorizarPedido autorizar, TimeProvider relogio)
    : IAcessoAoVivo
{
    static readonly ExigenciaDaRota DoCard = new(CalculoDePermissoes.Usuarios, Abas.U);

    public async Task<bool> SessaoAberta(string sid, CancellationToken ct = default) =>
        await sessoes.Buscar(sid, ct) is { } sessao && sessao.EstaAtiva(relogio.GetUtcNow());

    public async Task<bool> PodeVerOCard(string usuarioId, CancellationToken ct = default)
    {
        try
        {
            var permissoes = await calcular.Executar(usuarioId, ct);
            autorizar.Executar(DoCard, new PedidoAutorizavelDto(permissoes.Papel, permissoes.Mods));
            return true;
        }
        catch (FalhaDeAutorizacao)
        {
            return false;
        }
    }
}
