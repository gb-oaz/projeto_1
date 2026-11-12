using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using MongoDB.Bson;
using MongoDB.Driver;
using N = Boaz.Core.Commons.Usuario.Usuarios.Domain.NomesDoCard;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Usuarios;

// O card guardado (cardUsuarios, identidade-acesso-documentos v18): _id
// card-<sigla>-<PERFIL>[-<nomeCurto>], gravado inteiro com $set pelo _id. O
// índice único contaId + perfil + usuarioId (RNF-227) pede o usuarioId null
// fora do STAFF.
public sealed class CardsNoBanco(Armazenamento armazenamento) : IRepositorioCardUsuarios
{
    // chave duplicada: dois pedidos criando o mesmo card ao mesmo tempo
    const int ChaveDuplicada = 11000;

    IMongoCollection<BsonDocument> Cards => armazenamento.Colecao(Colecoes.CardUsuarios);

    public async Task<CardUsuarios?> Obter(string id, CancellationToken ct = default) =>
        await Cards.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct) is { } d ? Card(d) : null;

    public async Task Salvar(CardUsuarios card, CancellationToken ct = default)
    {
        var mudancas = Bson.Mudar.Combine(Documento(card).Elements.Select(e => Bson.Mudar.Set(e.Name, e.Value)));
        try
        {
            await Gravar(card.Id, mudancas, ct);
        }
        catch (MongoWriteException erro) when (erro.WriteError?.Code == ChaveDuplicada)
        {
            // o outro pedido criou o card antes: agora ele existe, e o $set vale
            await Gravar(card.Id, mudancas, ct);
        }
    }

    Task Gravar(string id, UpdateDefinition<BsonDocument> mudancas, CancellationToken ct) =>
        Cards.UpdateOneAsync(Bson.Filtro.Eq("_id", id), mudancas, new UpdateOptions { IsUpsert = true }, ct);

    public async Task<int?> Versao(string id, CancellationToken ct = default) =>
        await Cards.Find(Bson.Filtro.Eq("_id", id)).Project(Builders<BsonDocument>.Projection.Include("versao")).FirstOrDefaultAsync(ct) is { } d
            ? d.Inteiro("versao")
            : null;

    public async Task<IReadOnlyList<CardUsuarios>> Todos(CancellationToken ct = default) =>
        (await Cards.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct)).Select(Card).Where(c => c is not null).Select(c => c!).ToList();

    static BsonDocument Documento(CardUsuarios c) => new()
    {
        ["contaId"] = c.ContaId,
        ["perfil"] = c.Perfil.Texto(),
        ["usuarioId"] = Bson.Valor(c.UsuarioId),
        ["versao"] = c.Versao,
        ["atualizadoEm"] = Bson.Valor(c.AtualizadoEm),
        ["atual"] = new BsonDocument
        {
            ["selo"] = c.Atual.Selo,
            ["valor"] = c.Atual.Valor,
            ["apoio"] = Linhas(c.Atual.Apoio),
            ["lista"] = Linhas(c.Atual.Lista),
        },
        ["dias"] = Serie(c.Dias, "dia"),
        ["semanas"] = Serie(c.Semanas, "semana"),
        ["meses"] = Serie(c.Meses, "mes"),
    };

    static BsonDocument Linhas(IEnumerable<LinhaDoCard> linhas) => new(linhas.Select(l => new BsonElement(l.Chave, l.Valor)));

    static BsonArray Serie(IEnumerable<PontoDaSerie> pontos, string campo) =>
        new(pontos.Select(p => new BsonDocument { [campo] = p.Chave, ["valor"] = p.Valor }));

    // card de perfil desconhecido fica de fora
    static CardUsuarios? Card(BsonDocument d)
    {
        if (TextosDoCard.PerfilDe(d.Texto("perfil")) is not { } perfil) return null;
        var atual = d.Documento("atual");
        return new CardUsuarios(
            d["_id"].AsString,
            d.Texto("contaId") ?? "",
            perfil,
            d.Texto("usuarioId"),
            d.Inteiro("versao"),
            d.Data("atualizadoEm") ?? DateTimeOffset.UnixEpoch,
            new NumerosDoCard(atual?.Inteiro("selo") ?? 0, atual?.Inteiro("valor") ?? 0, Linhas(atual?.Documento("apoio")), Linhas(atual?.Documento("lista"))),
            Serie(d, "dias", "dia"),
            Serie(d, "semanas", "semana"),
            Serie(d, "meses", "mes"));
    }

    static List<LinhaDoCard> Linhas(BsonDocument? d) =>
        d?.Elements.Where(e => e.Value.IsNumeric).Select(e => new LinhaDoCard(e.Name, e.Value.ToInt32())).ToList() ?? [];

    static IEnumerable<PontoDaSerie> Serie(BsonDocument d, string lista, string campo) =>
        d.TryGetValue(lista, out var v) && v.IsBsonArray
            ? v.AsBsonArray.OfType<BsonDocument>().Where(p => p.Texto(campo) is not null).Select(p => new PontoDaSerie(p.Texto(campo)!, p.Inteiro("valor")))
            : [];
}

// As consultas dos números do card (RN-280, contrato usuarios v14): o DEBOAZ lê
// as contas assinantes (sem a do time); os outros perfis leem o staff e os
// participantes da conta (no STAFF, só os criados por ela) e a última geração
// de senha de cada pendente.
public sealed class ContagemNoBanco(Armazenamento armazenamento) : IContagemDoCard
{
    static readonly string[] TiposDoCard = ["STAFF", "PARTICIPANTE"];
    static readonly string[] TiposDeConta = ["INSTITUICAO", "AUTONOMO"];

    IMongoCollection<BsonDocument> Usuarios => armazenamento.Colecao(Colecoes.Usuarios);
    IMongoCollection<BsonDocument> Contas => armazenamento.Colecao(Colecoes.Contas);
    IMongoCollection<BsonDocument> Geracoes => armazenamento.Colecao(Colecoes.GeracoesSenha);

    public async Task<NumerosDoCard> Contar(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora,
        CancellationToken ct = default)
    {
        if (perfil == PerfilCard.Deboaz) return await DoTime(agora, ct);
        var escopo = Escopo(contaId, perfil, usuarioId);
        var tempo = await DoUsuarioNoTempo(perfil, contaId, usuarioId, agora, ct);
        var participantes = await Contar(Usuarios, escopo & Tipo("PARTICIPANTE") & Estado("ATIVO"), ct);
        var pendentes = await Contar(Usuarios, escopo & Estado("PENDENTE"), ct);
        var lista = new List<LinhaDoCard>
        {
            new(N.SemAcesso30Dias, tempo.Lista[N.SemAcesso30Dias]),
            new(N.Desativados, await Contar(Usuarios, escopo & Estado("DESATIVADO"), ct)),
            new(N.CodigosVencidos, tempo.Lista[N.CodigosVencidos]),
        };
        if (perfil == PerfilCard.Autonomo)
        {
            var inicioDoMes = InicioDoMes(agora);
            var criados = await Contar(Usuarios, escopo & Bson.Filtro.Gte("criadoEm", Bson.Valor(inicioDoMes)), ct);
            return new NumerosDoCard(tempo.Selo, participantes, [new(N.CriadosNoMes, criados), new(N.Pendentes, pendentes)], lista);
        }
        var staff = await Contar(Usuarios, escopo & Tipo("STAFF") & Estado("ATIVO"), ct);
        return new NumerosDoCard(tempo.Selo, staff + participantes,
            [new(N.Staff, staff), new(N.Participantes, participantes), new(N.Pendentes, pendentes)], lista);
    }

    public async Task<NumerosNoTempo> NoTempo(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora,
        CancellationToken ct = default)
    {
        if (perfil != PerfilCard.Deboaz) return await DoUsuarioNoTempo(perfil, contaId, usuarioId, agora, ct);
        var semAcesso = await MastersSemAcesso(agora, ct);
        return new NumerosNoTempo(semAcesso, new Dictionary<string, int> { [N.MasterSemAcesso30Dias] = semAcesso });
    }

    // ── DEBOAZ: as contas ───────────────────────────────────────────────────────

    async Task<NumerosDoCard> DoTime(DateTimeOffset agora, CancellationToken ct)
    {
        var ativas = Assinantes & Bson.Filtro.Eq("estado", "ATIVA");
        var semAcesso = await MastersSemAcesso(agora, ct);
        return new NumerosDoCard(semAcesso, await Contar(Contas, ativas, ct),
            [
                new(N.Instituicoes, await Contar(Contas, ativas & Bson.Filtro.Eq("tipo", "INSTITUICAO"), ct)),
                new(N.Autonomos, await Contar(Contas, ativas & Bson.Filtro.Eq("tipo", "AUTONOMO"), ct)),
                new(N.Suspensas, await Contar(Contas, Assinantes & Bson.Filtro.Eq("estado", "SUSPENSA"), ct)),
            ],
            [
                new(N.MasterSemAcesso30Dias, semAcesso),
                new(N.Desativadas, await Contar(Contas, Assinantes & Bson.Filtro.Eq("estado", "DESATIVADA"), ct)),
                new(N.Encerradas, await Contar(Contas, Assinantes & Bson.Filtro.Eq("estado", "ENCERRADA"), ct)),
            ]);
    }

    // contas INSTITUICAO ou AUTONOMO, fora a do time
    static FilterDefinition<BsonDocument> Assinantes =>
        Bson.Filtro.Ne("_id", TimeDeboaz.ContaId) & Bson.Filtro.In("tipo", TiposDeConta);

    // ATIVA, criada há mais de 30 dias e com a master sem acesso há mais de 30 dias (ou nunca)
    Task<int> MastersSemAcesso(DateTimeOffset agora, CancellationToken ct)
    {
        var limite = Bson.Valor(agora - CalendarioDoCard.SemAcesso);
        return Contar(Contas, Assinantes & Bson.Filtro.Eq("estado", "ATIVA") & Bson.Filtro.Lt("criadaEm", limite)
            & (Bson.Filtro.Lt("ultimoAcessoMaster", limite) | Bson.Filtro.Eq("ultimoAcessoMaster", BsonNull.Value)), ct);
    }

    // ── INSTITUICAO, STAFF e AUTONOMO: os usuários ──────────────────────────────

    // staff e participantes da conta; no STAFF, só os que ela criou
    static FilterDefinition<BsonDocument> Escopo(string contaId, PerfilCard perfil, string? usuarioId)
    {
        var f = Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.In("tipo", TiposDoCard);
        return perfil == PerfilCard.Staff ? f & Bson.Filtro.Eq("criadoPor", usuarioId) : f;
    }

    static FilterDefinition<BsonDocument> Tipo(string tipo) => Bson.Filtro.Eq("tipo", tipo);

    static FilterDefinition<BsonDocument> Estado(string estado) => Bson.Filtro.Eq("estado", estado);

    // o selo, "Sem acesso há 30 dias" e "Códigos vencidos", refeitos na leitura
    async Task<NumerosNoTempo> DoUsuarioNoTempo(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora, CancellationToken ct)
    {
        var escopo = Escopo(contaId, perfil, usuarioId);
        var limite = Bson.Valor(agora - CalendarioDoCard.SemAcesso);
        var semAcesso = await Contar(Usuarios, escopo & Estado("ATIVO") & Bson.Filtro.Lt("criadoEm", limite)
            & (Bson.Filtro.Lt("ultimoAcesso", limite) | Bson.Filtro.Eq("ultimoAcesso", BsonNull.Value)), ct);

        // a última geração de cada pendente: do escopo para os vencidos; da conta
        // inteira para o selo do STAFF, que conta as pedidas por ela
        var pendentes = await Ids(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.In("tipo", TiposDoCard) & Estado("PENDENTE"), ct);
        var doEscopo = perfil == PerfilCard.Staff ? (await Ids(escopo & Estado("PENDENTE"), ct)).ToHashSet() : pendentes.ToHashSet();
        var ultimas = await UltimasGeracoes(pendentes, ct);
        var fimDoDia = CalendarioDoCard.FimDoDia(agora);
        var vencidos = 0;
        var selo = 0;
        foreach (var (usuario, g) in ultimas)
        {
            if (g.Data("confirmadaEm") is not null || g.Data("pedidaEm") is not { } pedida) continue;
            var vence = pedida + CodigoDeLiberacao.Validade;
            if (vence <= agora && doEscopo.Contains(usuario)) vencidos++;
            var dela = perfil != PerfilCard.Staff || g.Texto("pedidaPor") == usuarioId;
            if (vence > agora && vence <= fimDoDia && dela && (perfil == PerfilCard.Staff || doEscopo.Contains(usuario))) selo++;
        }
        return new NumerosNoTempo(selo, new Dictionary<string, int> { [N.SemAcesso30Dias] = semAcesso, [N.CodigosVencidos] = vencidos });
    }

    async Task<List<string>> Ids(FilterDefinition<BsonDocument> filtro, CancellationToken ct) =>
        (await Usuarios.Find(filtro).Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(ct)).Select(d => d["_id"].AsString).ToList();

    async Task<Dictionary<string, BsonDocument>> UltimasGeracoes(IReadOnlyCollection<string> usuarios, CancellationToken ct)
    {
        if (usuarios.Count == 0) return [];
        var geracoes = await Geracoes.Find(Bson.Filtro.In("usuarioId", usuarios))
            .Sort(Builders<BsonDocument>.Sort.Descending("pedidaEm")).ToListAsync(ct);
        return geracoes.Where(g => g.Texto("usuarioId") is not null).GroupBy(g => g.Texto("usuarioId")!).ToDictionary(g => g.Key, g => g.First());
    }

    // a 00:00 do dia 1 do mês de Brasília, em UTC
    static DateTimeOffset InicioDoMes(DateTimeOffset agora)
    {
        var dia = CalendarioDoCard.Dia(agora);
        var inicio = new DateTime(dia.Year, dia.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(inicio, CalendarioDoCard.Fuso.GetUtcOffset(inicio)).ToUniversalTime();
    }

    static async Task<int> Contar(IMongoCollection<BsonDocument> colecao, FilterDefinition<BsonDocument> filtro, CancellationToken ct) =>
        (int)await colecao.CountDocumentsAsync(filtro, cancellationToken: ct);
}

// usuarios.preferencias (RN-283): gravado só pelo Usuários, sem auditoria.
public sealed class PreferenciasNoBanco(Armazenamento armazenamento) : IPreferencias
{
    IMongoCollection<BsonDocument> Usuarios => armazenamento.Colecao(Colecoes.Usuarios);

    public async Task<PreferenciasDeCards?> Ler(string usuarioId, CancellationToken ct = default)
    {
        var d = await Usuarios.Find(Bson.Filtro.Eq("_id", usuarioId)).Project(Builders<BsonDocument>.Projection.Include("preferencias"))
            .FirstOrDefaultAsync(ct);
        return d?.Documento("preferencias") is { } p && PreferenciasDeCards.AtualizacaoDe(p.Texto("atualizacaoCards")) is { } atualizacao
            ? new PreferenciasDeCards(atualizacao, p.Inteiro("intervaloAoVivo", PreferenciasDeCards.Padrao.IntervaloAoVivo))
            : null;
    }

    public Task Salvar(string usuarioId, PreferenciasDeCards preferencias, CancellationToken ct = default) =>
        Usuarios.UpdateOneAsync(Bson.Filtro.Eq("_id", usuarioId), Bson.Mudar.Set("preferencias", new BsonDocument
        {
            ["atualizacaoCards"] = PreferenciasDeCards.Texto(preferencias.Atualizacao),
            ["intervaloAoVivo"] = preferencias.IntervaloAoVivo,
        }), cancellationToken: ct);
}
