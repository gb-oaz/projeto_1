using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using MongoDB.Bson;
using MongoDB.Driver;
using IContas = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IContas;
using IGeracoesDeSenha = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IGeracoesDeSenha;
using IUsuarios = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IUsuarios;

namespace Boaz.MsMono.Secundario.Out.Commons.Usuario.Usuarios;

// Listas de A a Z como se lê em português, sem diferenciar maiúsculas.
static class Ordem
{
    public static readonly Collation Portugues = new("pt", strength: CollationStrength.Secondary);

    public static readonly SortDefinition<BsonDocument> PorNome = Builders<BsonDocument>.Sort.Ascending("nome").Ascending("_id");

    public static async Task<PaginaDe<T>> Pagina<T>(IMongoCollection<BsonDocument> colecao, FilterDefinition<BsonDocument> filtro,
        int pagina, Func<BsonDocument, T> ler, CancellationToken ct)
    {
        var total = await colecao.CountDocumentsAsync(filtro, cancellationToken: ct);
        var itens = await colecao.Find(filtro, new FindOptions { Collation = Portugues })
            .Sort(PorNome).Skip((pagina - 1) * PaginaDe<T>.Tamanho).Limit(PaginaDe<T>.Tamanho).ToListAsync(ct);
        return new PaginaDe<T>(itens.Select(ler).ToList(), pagina, (int)total);
    }
}

// Os usuários do cadastro (usuarios). Grava só os campos do cadastro: o último
// acesso é da Autenticação e as permissões, da Autorização.
public sealed class UsuariosNoBanco(Armazenamento armazenamento) : IUsuarios
{
    IMongoCollection<BsonDocument> Usuarios => armazenamento.Colecao(Colecoes.Usuarios);

    public async Task<UsuarioCadastrado?> Buscar(string id, CancellationToken ct = default) =>
        await Usuarios.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct) is { } d ? Documentos.Usuario(d) : null;

    public async Task<UsuarioCadastrado?> BuscarPorEntrada(string entrada, CancellationToken ct = default) =>
        await Usuarios.Find(Bson.Filtro.Eq("login", entrada)).FirstOrDefaultAsync(ct) is { } d ? Documentos.Usuario(d) : null;

    public async Task<UsuarioCadastrado?> MasterDaConta(string contaId, CancellationToken ct = default) =>
        await Usuarios.Find(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.Eq("tipo", "MASTER")).FirstOrDefaultAsync(ct) is { } d
            ? Documentos.Usuario(d)
            : null;

    public Task<bool> NomeCurtoEmUso(string contaId, string nomeCurto, string? exceto = null, CancellationToken ct = default) =>
        Existe(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.Eq("nomeCurto", nomeCurto), exceto, ct);

    public Task<bool> MatriculaEmUso(string contaId, string matricula, string? exceto = null, CancellationToken ct = default) =>
        Existe(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.Eq("matricula", matricula), exceto, ct);

    public Task<bool> LoginEmUso(string login, CancellationToken ct = default) => Existe(Bson.Filtro.Eq("login", login), null, ct);

    public Task Inserir(UsuarioCadastrado usuario, CancellationToken ct = default)
    {
        var d = Documentos.DoCadastro(usuario);
        d.InsertAt(0, new BsonElement("_id", usuario.Id));
        return Usuarios.InsertOneAsync(d, cancellationToken: ct);
    }

    public Task Atualizar(UsuarioCadastrado usuario, CancellationToken ct = default)
    {
        var mudancas = Documentos.DoCadastro(usuario).Elements.Select(e => Bson.Mudar.Set(e.Name, e.Value));
        return Usuarios.UpdateOneAsync(Bson.Filtro.Eq("_id", usuario.Id), Bson.Mudar.Combine(mudancas), cancellationToken: ct);
    }

    // upsert pelo _id (RN-267): o cadastro só no nascimento; a entrada (ou o
    // código), o resumo da senha e o estado ATIVO a cada partida
    public async Task<bool> Garantir(UsuarioCadastrado usuario, CancellationToken ct = default)
    {
        var sempre = new HashSet<string> { "login", "resumoSenha", "estado" };
        var cadastro = Documentos.DoCadastro(usuario);
        var mudancas = cadastro.Elements.Select(e => sempre.Contains(e.Name) ? Bson.Mudar.Set(e.Name, e.Value) : Bson.Mudar.SetOnInsert(e.Name, e.Value));
        var resultado = await Usuarios.UpdateOneAsync(Bson.Filtro.Eq("_id", usuario.Id), Bson.Mudar.Combine(mudancas),
            new UpdateOptions { IsUpsert = true }, ct);
        return resultado.UpsertedId is not null;
    }

    // as permissões do usuário qa voltam à lista (RN-267, RN-268); se os mods
    // mudaram, a versão e o pv sobem: o token antigo é refeito (RN-230)
    public async Task ReporModulos(string usuarioId, IReadOnlyDictionary<string, string> mods, CancellationToken ct = default)
    {
        var d = await Usuarios.Find(Bson.Filtro.Eq("_id", usuarioId)).FirstOrDefaultAsync(ct);
        if (d is null) return;
        var lista = new BsonDocument(mods.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => new BsonElement(m.Key, m.Value)));
        var p = d.Documento("permissoes");
        if (p?.Documento("mods") is { } atuais && atuais.Equals(lista)) return;
        var versao = p?.Inteiro("versao", 1) ?? 0;
        var pv = p?.Inteiro("pv", versao) ?? 0;
        await Usuarios.UpdateOneAsync(Bson.Filtro.Eq("_id", usuarioId),
            Bson.Mudar.Set("permissoes.versao", versao + 1).Set("permissoes.pv", Math.Max(pv, versao) + 1).Set("permissoes.mods", lista),
            cancellationToken: ct);
    }

    // a busca acha nome, entrada (ou código) e matrícula (RN-185)
    public Task<PaginaDe<UsuarioCadastrado>> Listar(FiltroDeUsuarios filtro, CancellationToken ct = default)
    {
        var f = Bson.Filtro.Eq("contaId", filtro.ContaId) & Bson.Filtro.In("tipo", filtro.Tipos.Select(t => t.Texto()));
        if (filtro.Estado is { } estado) f &= Bson.Filtro.Eq("estado", estado.Texto());
        if (filtro.Busca is { } busca)
        {
            var contem = Bson.Contem(busca);
            f &= Bson.Filtro.Regex("nome", contem) | Bson.Filtro.Regex("login", contem) | Bson.Filtro.Regex("matricula", contem);
        }
        return Ordem.Pagina(Usuarios, f, filtro.Pagina, Documentos.Usuario, ct);
    }

    async Task<bool> Existe(FilterDefinition<BsonDocument> filtro, string? exceto, CancellationToken ct)
    {
        if (exceto is not null) filtro &= Bson.Filtro.Ne("_id", exceto);
        return await Usuarios.Find(filtro).Limit(1).AnyAsync(ct);
    }
}

// As contas assinantes (contas), com o tempo de sessão de cada uma; o tempo do
// time deboaz fica na conta do time (tempoDoTime).
public sealed class ContasNoBanco(Armazenamento armazenamento) : IContas
{
    IMongoCollection<BsonDocument> Contas => armazenamento.Colecao(Colecoes.Contas);

    public async Task<ContaAssinante?> Buscar(string id, CancellationToken ct = default) =>
        await Contas.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct) is { } d ? Documentos.Conta(d) : null;

    // a sigla é gravada sempre em maiúsculas (RN-179): a busca exata usa o
    // índice sigla_unica (identidade-acesso-documentos v11)
    public Task<bool> SiglaEmUso(string sigla, CancellationToken ct = default) =>
        Contas.Find(Bson.Filtro.Eq("sigla", sigla.ToUpperInvariant())).AnyAsync(ct);

    public Task<bool> DominioEmUso(string dominio, CancellationToken ct = default) =>
        Contas.Find(Bson.Filtro.Eq("dominio", dominio)).AnyAsync(ct);

    // a conta, a conta master e a senha mostrada nascem juntas
    public async Task Criar(ContaAssinante conta, UsuarioCadastrado master, GeracaoDeSenhaPedida geracao, TempoDaConta tempo,
        CancellationToken ct = default)
    {
        var d = Documentos.DaConta(conta);
        d["tempoSessao"] = Documentos.DoTempo(tempo);
        d["conferencias"] = new BsonArray();
        await InserirConta(d, ct);
        var usuario = Documentos.DoCadastro(master);
        usuario.InsertAt(0, new BsonElement("_id", master.Id));
        await armazenamento.Colecao(Colecoes.Usuarios).InsertOneAsync(usuario, cancellationToken: ct);
        await armazenamento.Colecao(Colecoes.GeracoesSenha).InsertOneAsync(Documentos.DaGeracao(geracao), cancellationToken: ct);
    }

    // Dois pedidos com a mesma sigla ou o mesmo domínio ao mesmo tempo passam
    // pela conferência do caso de uso; o índice único recusa o segundo, que
    // responde como a conferência (RN-179): SIGLA_EM_USO ou DOMINIO_EM_USO. O
    // Cosmos DB não diz qual índice recusou ("Failed _id or unique index
    // constraint"), por isso o banco é relido: o _id sai da sigla
    // (conta-<sigla>), então _id ou sigla existentes são sigla em uso.
    async Task InserirConta(BsonDocument conta, CancellationToken ct)
    {
        try
        {
            await Contas.InsertOneAsync(conta, cancellationToken: ct);
        }
        catch (MongoWriteException erro) when (erro.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var sigla = conta.Texto("sigla") ?? "";
            if (await Contas.Find(Bson.Filtro.Eq("_id", conta["_id"]) | Bson.Filtro.Eq("sigla", sigla)).AnyAsync(ct))
            {
                throw FalhaDeUsuarios.SiglaEmUso();
            }
            if (await DominioEmUso(conta.Texto("dominio") ?? "", ct)) throw FalhaDeUsuarios.DominioEmUso();
            throw;
        }
    }

    // upsert pelo _id (RN-267): a conta inicial só é gravada se não existir
    public async Task<bool> Garantir(ContaAssinante conta, TempoDaConta? tempo, CancellationToken ct = default)
    {
        var d = Documentos.DaConta(conta);
        if (tempo is not null)
        {
            d["tempoSessao"] = Documentos.DoTempo(tempo);
            d["conferencias"] = new BsonArray();
        }
        var resultado = await Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", conta.Id),
            Bson.Mudar.Combine(d.Elements.Where(e => e.Name != "_id").Select(e => Bson.Mudar.SetOnInsert(e.Name, e.Value))),
            new UpdateOptions { IsUpsert = true }, ct);
        return resultado.UpsertedId is not null;
    }

    // só os módulos da lista em vigor na conta qa (RN-268): as de fora
    // terminam agora, as que faltam começam agora
    public async Task ReporContratacoes(ContaAssinante conta, IReadOnlySet<string> modulos, DateTimeOffset agora, CancellationToken ct = default)
    {
        var colecao = armazenamento.Colecao(Colecoes.Contratacoes);
        var emVigor = Bson.Filtro.Eq("contaId", conta.Id)
            & Bson.Filtro.Lte("desde", Bson.Valor(agora))
            & (Bson.Filtro.Eq("ate", BsonNull.Value) | Bson.Filtro.Gt("ate", Bson.Valor(agora)));
        var vigentes = await colecao.Find(emVigor).ToListAsync(ct);
        await colecao.UpdateManyAsync(emVigor & Bson.Filtro.Nin("moduloId", modulos), Bson.Mudar.Set("ate", Bson.Valor(agora)),
            cancellationToken: ct);
        var faltam = modulos.Except(vigentes.Select(c => c.Texto("moduloId")).OfType<string>()).Order(StringComparer.Ordinal).ToList();
        if (faltam.Count == 0) return;
        await colecao.InsertManyAsync(faltam.Select(m => new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString("N"),
            ["contaId"] = conta.Id,
            ["sigla"] = conta.Sigla,
            ["moduloId"] = m,
            ["desde"] = Bson.Valor(agora),
            ["ate"] = BsonNull.Value,
        }), cancellationToken: ct);
    }

    public Task Atualizar(ContaAssinante conta, CancellationToken ct = default) =>
        Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", conta.Id), Bson.Mudar.Set("estado", conta.Estado.Texto()), cancellationToken: ct);

    // encerrada: fica marcada; os usuários não entram mais (RN-175). Apagar em
    // cascata o conteúdo da conta fica para quando os módulos de conteúdo existirem.
    public Task Encerrar(string contaId, DateTimeOffset quando, CancellationToken ct = default) =>
        Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", contaId), Bson.Mudar.Set("encerradaEm", Bson.Valor(quando)), cancellationToken: ct);

    // a conta do time não aparece; a busca acha nome, sigla e CNPJ (RN-185)
    public Task<PaginaDe<ContaAssinante>> Listar(FiltroDeContas filtro, CancellationToken ct = default)
    {
        var f = Bson.Filtro.Ne("_id", TimeDeboaz.ContaId);
        if (filtro.Estado is { } estado) f &= Bson.Filtro.Eq("estado", estado.Texto());
        if (filtro.Busca is { } busca)
        {
            var contem = Bson.Contem(busca);
            var numeros = Documento.Numeros(busca);
            var acha = Bson.Filtro.Regex("nome", contem) | Bson.Filtro.Regex("sigla", contem) | Bson.Filtro.Regex("documento", contem);
            if (numeros.Length > 0) acha |= Bson.Filtro.Regex("documentoNumeros", Bson.Contem(numeros));
            f &= acha;
        }
        return Ordem.Pagina(Contas, f, filtro.Pagina, Documentos.Conta, ct);
    }

    public async Task<TempoDaConta?> TempoDaConta(string contaId, CancellationToken ct = default) =>
        await Contas.Find(Bson.Filtro.Eq("_id", contaId)).FirstOrDefaultAsync(ct) is { } d ? Documentos.TempoDaConta(d) : null;

    public Task SalvarTempoDaConta(string contaId, TempoDaConta tempo, CancellationToken ct = default) =>
        Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", contaId), Bson.Mudar.Set("tempoSessao", Documentos.DoTempo(tempo)), cancellationToken: ct);

    public async Task<TempoDoTime?> TempoDoTime(CancellationToken ct = default) =>
        Documentos.TempoDoTime(await Contas.Find(Bson.Filtro.Eq("_id", TimeDeboaz.ContaId)).FirstOrDefaultAsync(ct));

    public Task SalvarTempoDoTime(TempoDoTime tempo, CancellationToken ct = default) =>
        Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", TimeDeboaz.ContaId), Bson.Mudar.Set("tempoDoTime", Documentos.DoTempo(tempo)),
            cancellationToken: ct);

    // a conferência do SAC fica na conta (contas.conferencias, RN-172)
    public Task RegistrarConferencia(string contaId, string quem, bool conferiu, DateTimeOffset quando, CancellationToken ct = default) =>
        Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", contaId), Bson.Mudar.Push("conferencias", new BsonDocument
        {
            ["quem"] = quem,
            ["conferiu"] = conferiu,
            ["quando"] = Bson.Valor(quando),
        }), cancellationToken: ct);
}

// Gerações de senha (geracoesSenha): o código de liberação e o comprovante
// guardados só como resumo.
public sealed class GeracoesNoBanco(Armazenamento armazenamento) : IGeracoesDeSenha
{
    IMongoCollection<BsonDocument> Geracoes => armazenamento.Colecao(Colecoes.GeracoesSenha);

    public Task Inserir(GeracaoDeSenhaPedida geracao, CancellationToken ct = default) =>
        Geracoes.InsertOneAsync(Documentos.DaGeracao(geracao), cancellationToken: ct);

    public async Task<GeracaoDeSenhaPedida?> UltimaDoUsuario(string usuarioId, CancellationToken ct = default) =>
        await Geracoes.Find(Bson.Filtro.Eq("usuarioId", usuarioId)).Sort(Builders<BsonDocument>.Sort.Descending("pedidaEm"))
            .FirstOrDefaultAsync(ct) is { } d
            ? Documentos.Geracao(d)
            : null;

    public Task Atualizar(GeracaoDeSenhaPedida geracao, CancellationToken ct = default) =>
        Geracoes.UpdateOneAsync(Bson.Filtro.Eq("_id", geracao.Id), Bson.Mudar
            .Set("senhaMostradaEm", Bson.Valor(geracao.SenhaMostradaEm))
            .Set("resumoComprovante", Bson.Valor(geracao.ResumoComprovante)), cancellationToken: ct);
}

// O último pagamento (RN-172). A auditoria e os erros ficam em AuditoriaNoBanco
// (secundario/out/commons/auditoria); os pedidos de ajuda, no SAC (PedidosDeAjudaNoSac).
public sealed class RegistrosNoBanco : IPagamentos
{
    // a cobrança ainda não existe: nenhuma conta tem pagamento registrado
    public Task<DateOnly?> UltimoPagamento(string contaId, CancellationToken ct = default) => Task.FromResult<DateOnly?>(null);
}

// Encerrar sessões a pedido do cadastro (desativar, nova senha, conta fora do
// ar): o caso de uso da Autenticação encerra as de cada usuário, na execução de
// quem chama (RN-275).
public sealed class SessoesDosUsuarios(Armazenamento armazenamento, IEncerrarSessoesDoUsuario encerrar) : ISessoesDosUsuarios
{
    public Task EncerrarDoUsuario(string usuarioId, MotivoFim motivo, ExecucaoDeQuemChama chamada, CancellationToken ct = default) =>
        encerrar.Executar(usuarioId, motivo, chamada, ct);

    public async Task EncerrarDaConta(string contaId, MotivoFim motivo, ExecucaoDeQuemChama chamada, CancellationToken ct = default)
    {
        var ids = await armazenamento.Colecao(Colecoes.Usuarios).Find(Bson.Filtro.Eq("contaId", contaId))
            .Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(ct);
        foreach (var id in ids) await encerrar.Executar(id["_id"].AsString, motivo, chamada, ct);
    }
}
