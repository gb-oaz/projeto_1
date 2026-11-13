using System.Globalization;
using System.Text;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// As coleções chamados e mensagensChamado e a numeração em contadores
// (sac-documentos v01). O chamado guarda a versão (+1 a cada mudança): a
// gravação só vale se a versão guardada ainda é a lida. Mensagens e eventos só
// são incluídos (RN-293, RN-297). "ordemDaFila" é o esperandoTimeDesde, ou uma
// data no fim dos tempos na vez da conta: a fila do time ordena por ele, com
// os nulos por último. "busca" é número, sigla, nome, instituição e assunto,
// em minúsculas e sem acento.
public sealed class ChamadosNoBanco(Armazenamento armazenamento) : IChamados, INumeracao
{
    static readonly DateTime FimDosTempos = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    static readonly BsonArray EmAndamento =
        [StatusDoChamado.Aberto.Texto(), StatusDoChamado.EmAtendimento.Texto(), StatusDoChamado.AguardandoCliente.Texto()];

    IMongoCollection<BsonDocument> Chamados => armazenamento.Colecao(Colecoes.Chamados);

    IMongoCollection<BsonDocument> Itens_ => armazenamento.Colecao(Colecoes.MensagensChamado);

    public async Task<long> Proximo(string serie, CancellationToken ct = default)
    {
        var contador = await armazenamento.Colecao(Colecoes.Contadores).FindOneAndUpdateAsync(
            Bson.Filtro.Eq("_id", serie), Bson.Mudar.Inc("valor", 1L),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
        return contador.Longo("valor");
    }

    public async Task<Chamado?> Obter(NumeroDoChamado id, CancellationToken ct = default)
    {
        var documento = await Chamados.Find(Bson.Filtro.Eq("_id", id.Valor)).FirstOrDefaultAsync(ct);
        return documento is null ? null : new Chamado(Ler(documento));
    }

    public Task Incluir(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct = default) =>
        armazenamento.NumaOperacao(async (sessao, c) =>
        {
            if (sessao is null) await Chamados.InsertOneAsync(Documento(chamado.Dados), cancellationToken: c);
            else await Chamados.InsertOneAsync(sessao, Documento(chamado.Dados), cancellationToken: c);
            await IncluirItens(sessao, chamado.Dados.Id, itens, c);
            return true;
        }, ct);

    public Task<bool> Salvar(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct = default) =>
        armazenamento.NumaOperacao(async (sessao, c) =>
        {
            var filtro = Bson.Filtro.Eq("_id", chamado.Dados.Id.Valor) & Bson.Filtro.Eq("versao", chamado.VersaoLida);
            var trocado = sessao is null
                ? await Chamados.ReplaceOneAsync(filtro, Documento(chamado.Dados), cancellationToken: c)
                : await Chamados.ReplaceOneAsync(sessao, filtro, Documento(chamado.Dados), cancellationToken: c);
            if (trocado.MatchedCount == 0) return false;
            await IncluirItens(sessao, chamado.Dados.Id, itens, c);
            return true;
        }, ct);

    public Task MarcarLido(NumeroDoChamado id, Visao visao, CancellationToken ct = default) =>
        Chamados.UpdateOneAsync(Bson.Filtro.Eq("_id", id.Valor),
            Bson.Mudar.Set(visao == Visao.Conta ? "naoLidoPelaConta" : "naoLidoPeloTime", false), cancellationToken: ct);

    public async Task<PaginaDeChamados> Listar(FiltroDeChamados filtro, CancellationToken ct = default)
    {
        var f = Bson.Filtro;
        var condicoes = new List<FilterDefinition<BsonDocument>>();
        if (filtro.ContaId is { } conta) condicoes.Add(f.Eq("contaId", conta));
        if (filtro.Status is { } status) condicoes.Add(f.In("status", status.Select(s => s.Texto())));
        if (filtro.Categoria is { } categoria) condicoes.Add(f.Eq("categoria", categoria.Texto()));
        if (filtro.Busca is { } busca) condicoes.Add(f.Regex("busca", Bson.Contem(Normalizar(busca))));
        if (filtro.SemResponsavel) condicoes.Add(f.Eq("responsavel", BsonNull.Value));
        if (filtro.ResponsavelId is { } responsavel) condicoes.Add(f.Eq("responsavel.usuarioId", responsavel));
        if (filtro.Atrasados)
        {
            condicoes.Add(f.Eq("primeiraRespostaEm", BsonNull.Value) & f.In("status", EmAndamento)
                & f.Lt("prazoResposta", Bson.Valor(filtro.Agora)));
        }
        if (filtro.NotaMaxima is { } nota) condicoes.Add(f.Lte("avaliacao.nota", nota));
        var onde = condicoes.Count == 0 ? f.Empty : f.And(condicoes);
        var ordem = filtro.OrdemDoTime
            ? Builders<BsonDocument>.Sort.Ascending("ordemDaFila").Ascending("abertoEm").Ascending("numero")
            : Builders<BsonDocument>.Sort.Descending("atualizadoEm").Descending("numero");
        var total = await Chamados.CountDocumentsAsync(onde, cancellationToken: ct);
        var lidos = await Chamados.Find(onde).Sort(ordem).Skip((filtro.Pagina - 1) * filtro.PorPagina).Limit(filtro.PorPagina).ToListAsync(ct);
        return new PaginaDeChamados([.. lidos.Select(Ler)], total);
    }

    public async Task<int> EmAndamentoDaConta(string contaId, CancellationToken ct = default) =>
        (int)await Chamados.CountDocumentsAsync(Bson.Filtro.Eq("contaId", contaId) & Bson.Filtro.In("status", EmAndamento), cancellationToken: ct);

    public async Task<IReadOnlyList<ItemDaConversa>> Itens(NumeroDoChamado id, long? depoisDe = null, CancellationToken ct = default)
    {
        var filtro = Bson.Filtro.Eq("chamadoId", id.Valor);
        if (depoisDe is { } numero) filtro &= Bson.Filtro.Gt("numero", numero);
        var lidos = await Itens_.Find(filtro).Sort(Builders<BsonDocument>.Sort.Ascending("numero")).ToListAsync(ct);
        return [.. lidos.Select(LerItem)];
    }

    public async Task<long> UltimoItem(NumeroDoChamado id, CancellationToken ct = default)
    {
        var ultimo = await Itens_.Find(Bson.Filtro.Eq("chamadoId", id.Valor)).Sort(Builders<BsonDocument>.Sort.Descending("numero"))
            .Limit(1).FirstOrDefaultAsync(ct);
        return ultimo?.Longo("numero") ?? 0;
    }

    public async Task<IReadOnlyList<Chamado>> VencidosEm(DateTimeOffset agora, CancellationToken ct = default)
    {
        var limite = Bson.Valor(agora - LimitesDoChamado.SeteDias);
        var f = Bson.Filtro;
        var vencidos = (f.Eq("status", StatusDoChamado.AguardandoCliente.Texto()) & f.Lte("aguardandoDesde", limite))
            | (f.Eq("status", StatusDoChamado.Resolvido.Texto()) & f.Lte("resolvidoEm", limite));
        var lidos = await Chamados.Find(vencidos).Sort(Builders<BsonDocument>.Sort.Ascending("numero")).ToListAsync(ct);
        return [.. lidos.Select(d => new Chamado(Ler(d)))];
    }

    // ── documentos ─────────────────────────────────────────────────────────

    async Task IncluirItens(IClientSessionHandle? sessao, NumeroDoChamado id, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct)
    {
        if (itens.Count == 0) return;
        var documentos = itens.Select(i => DocumentoDoItem(id, i)).ToList();
        if (sessao is null) await Itens_.InsertManyAsync(documentos, cancellationToken: ct);
        else await Itens_.InsertManyAsync(sessao, documentos, cancellationToken: ct);
    }

    static BsonDocument Documento(DadosDoChamado d) => new()
    {
        ["_id"] = d.Id.Valor,
        ["numero"] = d.Id.Numero,
        ["versao"] = d.Versao,
        ["contaId"] = Bson.Valor(d.Conta?.Id),
        ["conta"] = d.Conta is { } c
            ? new BsonDocument { ["sigla"] = c.Sigla, ["nome"] = c.Nome, ["tipo"] = c.Tipo }
            : BsonNull.Value,
        ["abertoPor"] = Bson.Valor(d.AbertoPor),
        ["origem"] = new BsonDocument
        {
            ["tipo"] = d.Origem.Tipo.Texto(),
            ["pedido"] = Bson.Valor(d.Origem.Pedido),
            ["instituicao"] = Bson.Valor(d.Origem.Instituicao),
            ["contato"] = Bson.Valor(d.Origem.Contato),
            ["aparelho"] = Bson.Valor(d.Origem.Aparelho),
        },
        ["categoria"] = d.Categoria.Texto(),
        ["assunto"] = d.Assunto,
        ["descricao"] = d.Descricao,
        ["relacionadoA"] = Bson.Valor(d.RelacionadoA?.Valor),
        ["anexos"] = new BsonArray(d.Anexos),
        ["status"] = d.Status.Texto(),
        ["responsavel"] = d.Responsavel is { } r ? new BsonDocument { ["usuarioId"] = Bson.Valor(r.UsuarioId), ["nome"] = r.Nome } : BsonNull.Value,
        ["abertoEm"] = Bson.Valor(d.AbertoEm),
        ["atualizadoEm"] = Bson.Valor(d.AtualizadoEm),
        ["prazoResposta"] = Bson.Valor(d.PrazoResposta),
        ["primeiraRespostaEm"] = Bson.Valor(d.PrimeiraRespostaEm),
        ["esperandoTimeDesde"] = Bson.Valor(d.EsperandoTimeDesde),
        ["ordemDaFila"] = d.EsperandoTimeDesde is { } desde ? Bson.Valor(desde) : new BsonDateTime(FimDosTempos),
        ["aguardandoDesde"] = Bson.Valor(d.AguardandoDesde),
        ["resolvidoEm"] = Bson.Valor(d.ResolvidoEm),
        ["fechadoEm"] = Bson.Valor(d.FechadoEm),
        ["fechamento"] = Bson.Valor(d.Fechamento?.Texto()),
        ["avaliacao"] = d.Avaliacao is { } a
            ? new BsonDocument { ["nota"] = a.Nota, ["comentario"] = Bson.Valor(a.Comentario), ["em"] = Bson.Valor(a.Em) }
            : BsonNull.Value,
        ["naoLidoPelaConta"] = d.NaoLidoPelaConta,
        ["naoLidoPeloTime"] = d.NaoLidoPeloTime,
        ["busca"] = Normalizar(string.Join(' ', new[] { d.Id.Valor, d.Conta?.Sigla, d.Conta?.Nome, d.Origem.Instituicao, d.Assunto }
            .Where(t => t is not null))),
        ["guardarAte"] = Bson.Valor(d.GuardarAte),
    };

    static DadosDoChamado Ler(BsonDocument d)
    {
        var conta = d.Documento("conta") is { } c && d.Texto("contaId") is { } contaId
            ? new ContaDoChamado(contaId, c.Texto("sigla") ?? "", c.Texto("nome") ?? "", c.Texto("tipo") ?? "")
            : null;
        var o = d.Documento("origem") ?? [];
        var origem = new Origem(Textos.OrigemDe(o.Texto("tipo")) ?? TipoDeOrigem.Hub, o.Texto("pedido"), o.Texto("instituicao"),
            o.Texto("contato"), o.Texto("aparelho"));
        var responsavel = d.Documento("responsavel") is { } r ? new Autor(TipoDeAutor.Time, r.Texto("usuarioId"), r.Texto("nome") ?? "") : null;
        var avaliacao = d.Documento("avaliacao") is { } a ? new Avaliacao(a.Inteiro("nota"), a.Texto("comentario"), a.Data("em")!.Value) : null;
        var anexos = d.TryGetValue("anexos", out var lista) && lista.IsBsonArray ? lista.AsBsonArray.Select(x => x.AsString).ToList() : [];
        return new DadosDoChamado(
            new NumeroDoChamado(d.Longo("numero")), d.Longo("versao"), conta, d.Texto("abertoPor"), origem,
            Textos.CategoriaDe(d.Texto("categoria")) ?? Categoria.DuvidaDeUso, d.Texto("assunto") ?? "", d.Texto("descricao") ?? "",
            NumeroDoChamado.Ler(d.Texto("relacionadoA")), anexos, Textos.StatusDe(d.Texto("status")) ?? StatusDoChamado.Aberto, responsavel,
            d.Data("abertoEm")!.Value, d.Data("atualizadoEm")!.Value, d.Data("prazoResposta")!.Value, d.Data("primeiraRespostaEm"),
            d.Data("esperandoTimeDesde"), d.Data("aguardandoDesde"), d.Data("resolvidoEm"), d.Data("fechadoEm"),
            Textos.FechamentoDe(d.Texto("fechamento")), avaliacao, d.Logico("naoLidoPelaConta"), d.Logico("naoLidoPeloTime"));
    }

    static BsonDocument DocumentoDoItem(NumeroDoChamado chamado, ItemDaConversa i) => new()
    {
        ["_id"] = i.Id,
        ["numero"] = ItemDaConversa.NumeroDe(i.Id) ?? 0,
        ["chamadoId"] = chamado.Valor,
        ["versao"] = i.Versao,
        ["tipo"] = i.Tipo.Texto(),
        ["autor"] = new BsonDocument { ["tipo"] = i.Autor.Tipo.Texto(), ["usuarioId"] = Bson.Valor(i.Autor.UsuarioId), ["nome"] = i.Autor.Nome },
        ["texto"] = Bson.Valor(i.Texto),
        ["anexos"] = new BsonArray(i.Anexos),
        ["evento"] = Bson.Valor(i.Evento?.Texto()),
        ["de"] = Bson.Valor(i.De),
        ["para"] = Bson.Valor(i.Para),
        ["em"] = Bson.Valor(i.Em),
    };

    static ItemDaConversa LerItem(BsonDocument d)
    {
        var a = d.Documento("autor") ?? [];
        var anexos = d.TryGetValue("anexos", out var lista) && lista.IsBsonArray ? lista.AsBsonArray.Select(x => x.AsString).ToList() : [];
        return new ItemDaConversa(d.Texto("_id") ?? "", d.Longo("versao"), Textos.ItemDe(d.Texto("tipo")) ?? TipoDeItem.Mensagem,
            new Autor(Textos.AutorDe(a.Texto("tipo")) ?? TipoDeAutor.Sistema, a.Texto("usuarioId"), a.Texto("nome") ?? ""),
            d.Texto("texto"), anexos, Textos.EventoDe(d.Texto("evento")), d.Texto("de"), d.Texto("para"), d.Data("em")!.Value);
    }

    // minúsculas e sem acento, para a busca
    public static string Normalizar(string texto)
    {
        var decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpo = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) limpo.Append(c);
        }
        return limpo.ToString().Normalize(NormalizationForm.FormC);
    }
}
