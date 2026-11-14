using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Commons.Usuario.Autorizacao;

// Permissões guardadas no documento do usuário (permissoes: versao, pv, mods e
// suspensos [{mod, abas}], RN-285 a RN-287)
// e o registro de mudanças (registrosPermissao), conforme autorizacao-documentos.
// A gravação só vale se a versão não mudou desde a leitura (RNF-222, RNF-223).
public sealed class PermissoesNoBanco(Armazenamento armazenamento) : IUsuarios, IPermissoesGuardadas
{
    IMongoCollection<BsonDocument> Usuarios => armazenamento.Colecao(Colecoes.Usuarios);

    IMongoCollection<BsonDocument> Registros => armazenamento.Colecao(Colecoes.RegistrosPermissao);

    // ── IUsuarios ──────────────────────────────────────────────────────────────

    public async Task<UsuarioAutorizado?> Buscar(string id, CancellationToken ct = default)
    {
        var d = await Usuarios.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return d is null ? null : await Autorizado(d, ct);
    }

    public async Task<IReadOnlyList<UsuarioAutorizado>> DaConta(string contaId, CancellationToken ct = default)
    {
        var lidos = await Usuarios.Find(Bson.Filtro.Eq("contaId", contaId)).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync(ct);
        var daConta = new List<UsuarioAutorizado>();
        foreach (var d in lidos) daConta.Add(await Autorizado(d, ct));
        return daConta;
    }

    async Task<UsuarioAutorizado> Autorizado(BsonDocument d, CancellationToken ct)
    {
        var id = d["_id"].AsString;
        var contaId = d.Texto("contaId")!;
        var deAutonomo = false;
        if (d.Texto("tipo") == "MASTER")
        {
            var conta = await armazenamento.Colecao(Colecoes.Contas).Find(Bson.Filtro.Eq("_id", contaId)).FirstOrDefaultAsync(ct);
            deAutonomo = conta?.Texto("tipo") == "AUTONOMO";
        }
        return new UsuarioAutorizado(id, contaId, d.Texto("nome") ?? "", Papeis.DoTipo(d.Texto("tipo")!, deAutonomo), d.Texto("criadoPor"));
    }

    // ── IPermissoesGuardadas ───────────────────────────────────────────────────

    async Task<PermissoesDoUsuario?> IPermissoesGuardadas.Buscar(string usuarioId, CancellationToken ct)
    {
        var d = await Usuarios.Find(Bson.Filtro.Eq("_id", usuarioId)).FirstOrDefaultAsync(ct);
        if (d?.Documento("permissoes") is not { } p) return null;
        var versao = p.Inteiro("versao", 1);
        var mods = p.Documento("mods")?.Elements
            .Where(e => e.Value.IsString && Abas.De(e.Value.AsString) is not null)
            .ToDictionary(e => e.Name, e => Abas.De(e.Value.AsString)!.Value) ?? [];
        return new PermissoesDoUsuario(usuarioId, versao, p.Inteiro("pv", versao), mods, Suspensos(p));
    }

    // as permissões e o registro da mudança numa só operação (RNF-223): numa
    // transação quando o MongoDB aceita (replica set); num standalone, em
    // sequência (Armazenamento.NumaOperacao)
    public Task<bool> Salvar(PermissoesDoUsuario permissoes, MudancaDePermissao mudanca, int versaoLida, CancellationToken ct = default) =>
        armazenamento.NumaOperacao((sessao, c) => Gravar(sessao, permissoes, mudanca, versaoLida, c), ct);

    async Task<bool> Gravar(IClientSessionHandle? sessao, PermissoesDoUsuario permissoes, MudancaDePermissao mudanca, int versaoLida,
        CancellationToken ct)
    {
        var lida = Bson.Filtro.Eq("permissoes.versao", versaoLida);
        if (versaoLida == 1) lida |= Bson.Filtro.Exists("permissoes.versao", false);
        var filtro = Bson.Filtro.Eq("_id", permissoes.UsuarioId) & lida;
        var mudar = Bson.Mudar
            .Set("permissoes.versao", permissoes.Versao)
            .Set("permissoes.pv", permissoes.Pv)
            .Set("permissoes.mods", Mods(permissoes.Mods))
            .Set("permissoes.suspensos", Suspensos(permissoes.Suspensos));
        var resultado = sessao is null
            ? await Usuarios.UpdateOneAsync(filtro, mudar, cancellationToken: ct)
            : await Usuarios.UpdateOneAsync(sessao, filtro, mudar, cancellationToken: ct);
        if (resultado.ModifiedCount == 0) return false;
        var registro = new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString("N"),
            ["usuarioId"] = mudanca.UsuarioId,
            ["sigla"] = permissoes.UsuarioId.Split('-') is [_, var sigla, ..] ? sigla.ToUpperInvariant() : "",
            ["por"] = mudanca.Por,
            ["em"] = Bson.Valor(mudanca.Em),
            ["versao"] = mudanca.Versao,
            ["antes"] = Mods(mudanca.Antes),
            ["depois"] = Mods(mudanca.Depois),
        };
        if (sessao is null) await Registros.InsertOneAsync(registro, cancellationToken: ct);
        else await Registros.InsertOneAsync(sessao, registro, cancellationToken: ct);
        return true;
    }

    // a contratação da conta mudou sem mexer nos módulos dele: só o pv sobe, para o token ser refeito (RN-239)
    public async Task<int> SubirPv(string usuarioId, CancellationToken ct = default)
    {
        var p = (await Usuarios.Find(Bson.Filtro.Eq("_id", usuarioId)).FirstOrDefaultAsync(ct))?.Documento("permissoes");
        var versao = p?.Inteiro("versao", 1) ?? 1;
        var pv = (p?.Inteiro("pv", versao) ?? versao) + 1;
        await Usuarios.UpdateOneAsync(Bson.Filtro.Eq("_id", usuarioId),
            Bson.Mudar.Set("permissoes.versao", versao).Set("permissoes.pv", pv), cancellationToken: ct);
        return pv;
    }

    public async Task<IReadOnlyList<MudancaDePermissao>> Historico(string usuarioId, DateTimeOffset? ate, int limite, CancellationToken ct = default)
    {
        var filtro = Bson.Filtro.Eq("usuarioId", usuarioId);
        if (ate is { } fim) filtro &= Bson.Filtro.Lt("em", Bson.Valor(fim));
        var lidos = await Registros.Find(filtro).Sort(Builders<BsonDocument>.Sort.Descending("em").Descending("versao")).Limit(limite).ToListAsync(ct);
        return lidos.Select(d => new MudancaDePermissao(usuarioId, d.Texto("por") ?? "", d.Data("em") ?? DateTimeOffset.UnixEpoch,
            d.Inteiro("versao"), ModsDe(d.Documento("antes")), ModsDe(d.Documento("depois")))).ToList();
    }

    static BsonDocument Mods(IReadOnlyDictionary<string, Abas> mods) =>
        new(mods.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => new BsonElement(m.Key, m.Value.ToString())));

    static BsonDocument Mods(IReadOnlyDictionary<string, Abas?> mods) =>
        new(mods.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => new BsonElement(m.Key, Bson.Valor(m.Value?.ToString()))));

    // [{mod, abas}], na ordem do id do módulo
    static BsonArray Suspensos(IReadOnlyDictionary<string, Abas> suspensos) =>
        new(suspensos.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => new BsonDocument { ["mod"] = m.Key, ["abas"] = m.Value.ToString() }));

    static Dictionary<string, Abas> Suspensos(BsonDocument permissoes) =>
        permissoes.GetValue("suspensos", BsonNull.Value) is BsonArray lista
            ? lista.OfType<BsonDocument>()
                .Select(s => (Mod: s.Texto("mod"), Abas: s.Texto("abas") is { } a ? Abas.De(a) : null))
                .Where(s => s.Mod is not null && s.Abas is not null)
                .GroupBy(s => s.Mod!).ToDictionary(g => g.Key, g => g.First().Abas!.Value)
            : [];

    static Dictionary<string, Abas?> ModsDe(BsonDocument? d) =>
        d?.Elements.ToDictionary(e => e.Name, e => e.Value.IsString ? Abas.De(e.Value.AsString) : null) ?? [];
}

// Módulos contratados em vigor (contratacoes, do Catálogo): desde já começou e
// ate é null ou ainda não chegou.
public sealed class ContratacoesNoBanco(Armazenamento armazenamento) : IContratacoes
{
    public async Task<IReadOnlySet<string>> EmVigor(string contaId, DateTimeOffset agora, CancellationToken ct = default)
    {
        var filtro = Bson.Filtro.Eq("contaId", contaId)
            & Bson.Filtro.Lte("desde", Bson.Valor(agora))
            & (Bson.Filtro.Eq("ate", BsonNull.Value) | Bson.Filtro.Gt("ate", Bson.Valor(agora)));
        var lidas = await armazenamento.Colecao(Colecoes.Contratacoes).Find(filtro).ToListAsync(ct);
        return lidas.Select(d => d.Texto("moduloId")).OfType<string>().ToHashSet();
    }
}

// As permissões mudaram: o pv novo vai para as sessões abertas da pessoa (RN-230),
// na execução de quem chama (RN-275).
public sealed class AvisoDePermissoes(IAvisarPermissoesNovas avisar) : ISessoesDoUsuario
{
    public Task AvisarPv(string usuarioId, int pv, ExecucaoDeQuemChama chamada, CancellationToken ct = default) =>
        avisar.Executar(usuarioId, pv, chamada, ct);
}
