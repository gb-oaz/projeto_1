using System.Globalization;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using StackExchange.Redis;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Autenticacao;

// Sessões no Redis (autenticacao-documentos): o hash sessao:<sid> expira junto
// com o tempo sem uso (RN-226), e o conjunto sessoes:<usuario> guarda os sids
// de cada pessoa, para listar e encerrar todas. A chave que expirou é a sessão
// que terminou: o conjunto é limpo na próxima leitura.
public sealed class SessoesNoRedis(Armazenamento armazenamento) : ISessoes
{
    static RedisKey Chave(string sid) => $"sessao:{sid}";

    static RedisKey DoUsuarioChave(string usuarioId) => $"sessoes:{usuarioId}";

    public async Task<Sessao?> Buscar(string sid, CancellationToken ct = default)
    {
        var campos = await armazenamento.Cache.HashGetAllAsync(Chave(sid));
        return campos.Length == 0 ? null : Ler(sid, campos.ToDictionary(c => c.Name.ToString(), c => c.Value.ToString()));
    }

    public async Task Gravar(Sessao sessao, CancellationToken ct = default)
    {
        var cache = armazenamento.Cache;
        var chave = Chave(sessao.Sid);
        await cache.HashSetAsync(chave,
        [
            new("usuarioId", sessao.UsuarioId),
            new("contaId", sessao.ContaId),
            new("sigla", sessao.Sigla),
            new("frente", sessao.Frente.Texto()),
            new("aparelho", sessao.Aparelho.Texto()),
            new("quemEntrou", sessao.QuemEntrou?.Tipo.Texto() ?? ""),
            new("parentesco", sessao.QuemEntrou?.Parentesco ?? ""),
            new("sala", sessao.Sala ?? ""),
            new("pv", sessao.Pv),
            new("resumoRenovacao", sessao.ResumoRenovacao),
            new("inicio", sessao.Inicio.ToUnixTimeMilliseconds()),
            new("ultimaAcao", sessao.UltimaAcao.ToUnixTimeMilliseconds()),
            new("tempoSemUso", sessao.TempoSemUso is { } t ? (long)t.TotalSeconds : -1),
        ]);
        if (sessao.ExpiraEm is { } fim)
        {
            await cache.KeyExpireAsync(chave, fim - DateTimeOffset.UtcNow is var resta && resta > TimeSpan.Zero ? resta : TimeSpan.FromMilliseconds(1));
        }
        await cache.SetAddAsync(DoUsuarioChave(sessao.UsuarioId), sessao.Sid);
    }

    public async Task Apagar(Sessao sessao, MotivoFim motivo, CancellationToken ct = default)
    {
        await armazenamento.Cache.KeyDeleteAsync(Chave(sessao.Sid));
        await armazenamento.Cache.SetRemoveAsync(DoUsuarioChave(sessao.UsuarioId), sessao.Sid);
    }

    public async Task<IReadOnlyList<Sessao>> DoUsuario(string usuarioId, CancellationToken ct = default)
    {
        var sessoes = new List<Sessao>();
        foreach (var sid in await armazenamento.Cache.SetMembersAsync(DoUsuarioChave(usuarioId)))
        {
            if (await Buscar(sid.ToString(), ct) is { } sessao) sessoes.Add(sessao);
            else await armazenamento.Cache.SetRemoveAsync(DoUsuarioChave(usuarioId), sid);
        }
        return sessoes;
    }

    static Sessao Ler(string sid, Dictionary<string, string> c)
    {
        var quem = Nomes.QuemEntrouDe(c.GetValueOrDefault("quemEntrou")) is { } tipo
            ? new QuemEntrou(tipo, c.GetValueOrDefault("parentesco") is { Length: > 0 } p ? p : null)
            : null;
        var segundos = Numero(c, "tempoSemUso", -1);
        return new Sessao(
            sid,
            c.GetValueOrDefault("usuarioId", ""),
            c.GetValueOrDefault("contaId", ""),
            c.GetValueOrDefault("sigla", ""),
            c.GetValueOrDefault("frente") switch { "QUEST" => Frente.Quest, "SALA" => Frente.Sala, _ => Frente.Hub },
            Nomes.AparelhoDe(c.GetValueOrDefault("aparelho")) ?? Aparelho.Navegador,
            quem,
            c.GetValueOrDefault("sala") is { Length: > 0 } sala ? sala : null,
            (int)Numero(c, "pv", 0),
            c.GetValueOrDefault("resumoRenovacao", ""),
            DateTimeOffset.FromUnixTimeMilliseconds(Numero(c, "inicio", 0)),
            DateTimeOffset.FromUnixTimeMilliseconds(Numero(c, "ultimaAcao", 0)),
            segundos < 0 ? null : TimeSpan.FromSeconds(segundos));
    }

    static long Numero(Dictionary<string, string> c, string campo, long padrao) =>
        long.TryParse(c.GetValueOrDefault(campo), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : padrao;
}
