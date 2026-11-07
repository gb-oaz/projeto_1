using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Infra.Out.Commons;

// Os índices do MongoDB como o diagrama identidade-acesso-documentos (v05)
// especifica, criados na partida, antes do primeiro pedido. Criar um índice
// que já existe igual não faz nada; um que não pode ser criado (dados que
// ferem um único, ou um recurso que o servidor não tem) vai para o log como
// aviso e a partida segue: o índice é desempenho e proteção extra, a regra de
// negócio continua conferida pela aplicação.
public sealed class Indices(Armazenamento armazenamento, ILogger<Indices> log)
{
    static IndexKeysDefinitionBuilder<BsonDocument> Chave => Builders<BsonDocument>.IndexKeys;

    // coleção → índices
    public static IReadOnlyList<(string Colecao, CreateIndexModel<BsonDocument> Indice)> Todos { get; } =
    [
        // contas: sigla e domínio únicos (a sigla é gravada em maiúsculas, RN-179); busca por CNPJ/CPF (RN-185)
        (Colecoes.Contas, Unico(Chave.Ascending("sigla"), "sigla_unica")),
        (Colecoes.Contas, Unico(Chave.Ascending("dominio"), "dominio_unico")),
        (Colecoes.Contas, Comum(Chave.Ascending("documentoNumeros"), "documentoNumeros")),
        // usuarios: login único; nome curto e matrícula únicos na conta; listas de A a Z
        (Colecoes.Usuarios, Unico(Chave.Ascending("login"), "login_unico")),
        (Colecoes.Usuarios, Unico(Chave.Ascending("contaId").Ascending("nomeCurto"), "contaId_nomeCurto_unico")),
        (Colecoes.Usuarios, new CreateIndexModel<BsonDocument>(Chave.Ascending("contaId").Ascending("matricula"), new CreateIndexOptions<BsonDocument>
        {
            Name = "contaId_matricula_unica",
            Unique = true,
            // só quando há matrícula: o autônomo e o time não têm (RN-188)
            PartialFilterExpression = Builders<BsonDocument>.Filter.Type("matricula", BsonType.String),
        })),
        (Colecoes.Usuarios, Comum(Chave.Ascending("contaId").Ascending("tipo").Ascending("estado").Ascending("nome"), "contaId_tipo_estado_nome")),
        // geracoesSenha
        (Colecoes.GeracoesSenha, Comum(Chave.Ascending("usuarioId").Descending("pedidaEm"), "usuarioId_pedidaEm")),
        (Colecoes.GeracoesSenha, Comum(Chave.Ascending("resumoComprovante"), "resumoComprovante")),
        (Colecoes.GeracoesSenha, Comum(Chave.Ascending("contaId").Ascending("pedidaEm"), "contaId_pedidaEm")),
        // registrosPermissao: o histórico, do mais novo ao mais velho (RN-256)
        (Colecoes.RegistrosPermissao, Comum(Chave.Ascending("usuarioId").Descending("em").Descending("versao"), "usuarioId_em_versao")),
        // auditoria (RN-178) e pedidos de ajuda (limite por aparelho, RNF-205)
        (Colecoes.Auditoria, Comum(Chave.Ascending("contaId").Ascending("quando"), "contaId_quando")),
        (Colecoes.PedidosAjuda, Comum(Chave.Ascending("aparelho").Ascending("quando"), "aparelho_quando")),
    ];

    // Cria os índices; devolve quantos não puderam ser criados.
    public async Task<int> Criar(CancellationToken ct = default)
    {
        var falhas = 0;
        foreach (var (colecao, indice) in Todos)
        {
            try
            {
                await armazenamento.Colecao(colecao).Indexes.CreateOneAsync(indice, cancellationToken: ct);
            }
            catch (MongoCommandException erro)
            {
                falhas++;
                log.LogWarning("Índice {Indice} de {Colecao} não criado: {Motivo}", indice.Options.Name, colecao, erro.CodeName);
            }
        }
        return falhas;
    }

    static CreateIndexModel<BsonDocument> Unico(IndexKeysDefinition<BsonDocument> chave, string nome) =>
        new(chave, new CreateIndexOptions { Name = nome, Unique = true });

    static CreateIndexModel<BsonDocument> Comum(IndexKeysDefinition<BsonDocument> chave, string nome) =>
        new(chave, new CreateIndexOptions { Name = nome });
}
