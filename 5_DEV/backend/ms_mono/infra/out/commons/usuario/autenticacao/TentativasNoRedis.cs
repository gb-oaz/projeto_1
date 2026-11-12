using System.Globalization;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using StackExchange.Redis;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Autenticacao;

// Tentativas erradas no Redis (RNF-204): o hash tentativas:<ALVO>:<chave>
// guarda os erros seguidos, os bloqueios seguidos e até quando está bloqueado,
// e expira quando a memória das tentativas acaba. Serve à entrada e ao código
// de acesso (Autenticação) e ao código de liberação (Usuários).
public sealed class TentativasNoRedis(Armazenamento armazenamento, TimeProvider relogio) : ITentativas, ITentativasDeLiberacao
{
    static RedisKey Chave(AlvoTentativa alvo, string chave) => $"tentativas:{alvo.Texto()}:{chave}";

    public async Task<TentativasDeEntrada?> Buscar(AlvoTentativa alvo, string chave, CancellationToken ct = default)
    {
        var campos = await armazenamento.Cache.HashGetAllAsync(Chave(alvo, chave));
        if (campos.Length == 0) return null;
        var c = campos.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        return new TentativasDeEntrada(alvo, chave,
            (int)Numero(c, "errosSeguidos", 0),
            (int)Numero(c, "bloqueiosSeguidos", 0),
            Data(c, "bloqueadoAte"),
            Data(c, "ultimoErro"));
    }

    public async Task Gravar(TentativasDeEntrada tentativas, CancellationToken ct = default)
    {
        var chave = Chave(tentativas.Alvo, tentativas.Chave);
        await armazenamento.Cache.HashSetAsync(chave,
        [
            new("errosSeguidos", tentativas.ErrosSeguidos),
            new("bloqueiosSeguidos", tentativas.BloqueiosSeguidos),
            new("bloqueadoAte", tentativas.BloqueadoAte?.ToUnixTimeMilliseconds() ?? -1),
            new("ultimoErro", tentativas.UltimoErro?.ToUnixTimeMilliseconds() ?? -1),
        ]);
        if (tentativas.ExpiraEm is { } fim && fim - relogio.GetUtcNow() is var resta && resta > TimeSpan.Zero)
        {
            await armazenamento.Cache.KeyExpireAsync(chave, resta);
        }
    }

    public Task Apagar(AlvoTentativa alvo, string chave, CancellationToken ct = default) =>
        armazenamento.Cache.KeyDeleteAsync(Chave(alvo, chave));

    // ── código de liberação (Usuários), pela entrada ──────────────────────────

    public Task<TentativasDeEntrada?> Buscar(string entrada, CancellationToken ct = default) =>
        Buscar(AlvoTentativa.CodigoLiberacao, entrada, ct);

    public Task Apagar(string entrada, CancellationToken ct = default) => Apagar(AlvoTentativa.CodigoLiberacao, entrada, ct);

    static long Numero(Dictionary<string, string> c, string campo, long padrao) =>
        long.TryParse(c.GetValueOrDefault(campo), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : padrao;

    static DateTimeOffset? Data(Dictionary<string, string> c, string campo) =>
        Numero(c, campo, -1) is var ms and >= 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;
}
