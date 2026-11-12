using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Commons;

// Os índices do MongoDB como o diagrama identidade-acesso-documentos (v13)
// especifica, criados na partida, antes do primeiro pedido (RNF-227). Criar um
// índice que já existe igual não faz nada. Um índice não único que não pode ser
// criado vai para o log como aviso e a partida segue: é só desempenho. Um único
// que não pode ser criado (dados que ferem a unicidade) também vai para o log,
// e a partida não segue (FalhaNaInicializacao, como na RNF-226): a regra de
// negócio continua conferida pela aplicação (RN-179), mas o banco precisa
// garanti-la. O log e a falha trazem o nome do índice, a coleção e o código do
// erro, nunca a mensagem do servidor, que traz o valor duplicado (RNF-225).
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
            // só quando há matrícula: o autônomo e o time não têm, e o campo fica
            // ausente (RN-188); $exists é o filtro parcial que o Cosmos DB aceita
            PartialFilterExpression = Builders<BsonDocument>.Filter.Exists("matricula"),
        })),
        (Colecoes.Usuarios, Comum(Chave.Ascending("contaId").Ascending("tipo").Ascending("estado").Ascending("nome"), "contaId_tipo_estado_nome")),
        // geracoesSenha
        (Colecoes.GeracoesSenha, Comum(Chave.Ascending("usuarioId").Descending("pedidaEm"), "usuarioId_pedidaEm")),
        (Colecoes.GeracoesSenha, Comum(Chave.Ascending("resumoComprovante"), "resumoComprovante")),
        (Colecoes.GeracoesSenha, Comum(Chave.Ascending("contaId").Ascending("pedidaEm"), "contaId_pedidaEm")),
        // registrosPermissao: o histórico, do mais novo ao mais velho (RN-256)
        (Colecoes.RegistrosPermissao, Comum(Chave.Ascending("usuarioId").Descending("em").Descending("versao"), "usuarioId_em_versao")),
        // auditoria (RN-178) e auditoriaErros (RN-270): não únicos, nascem com
        // dados no Cosmos DB RU 7.0; o traceId junta a execução (RN-271, 2 por execução)
        (Colecoes.Auditoria, Comum(Chave.Ascending("contaId").Ascending("quando"), "contaId_quando")),
        (Colecoes.Auditoria, Comum(Chave.Ascending("traceId"), "traceId")),
        (Colecoes.AuditoriaErros, Comum(Chave.Ascending("traceId"), "traceId")),
        (Colecoes.AuditoriaErros, Comum(Chave.Ascending("contaId").Ascending("quando"), "contaId_quando")),
        // cardUsuarios: um card por conta, perfil e staff (RN-280); usuarioId null fora do STAFF
        (Colecoes.CardUsuarios, Unico(Chave.Ascending("contaId").Ascending("perfil").Ascending("usuarioId"), "contaId_perfil_usuarioId_unico")),
        // pedidos de ajuda (limite por aparelho, RNF-205)
        (Colecoes.PedidosAjuda, Comum(Chave.Ascending("aparelho").Ascending("quando"), "aparelho_quando")),
    ];

    // Cria os índices; devolve quantos não únicos não puderam ser criados.
    // <exception cref="FalhaNaInicializacao">Algum índice único não foi criado.</exception>
    public async Task<int> Criar(CancellationToken ct = default)
    {
        var falhas = 0;
        var unicos = new List<string>();
        foreach (var (colecao, indice) in Todos)
        {
            try
            {
                await armazenamento.Colecao(colecao).Indexes.CreateOneAsync(indice, cancellationToken: ct);
            }
            catch (MongoCommandException erro)
            {
                log.LogWarning("Índice {Indice} de {Colecao} não criado: {Motivo}", indice.Options.Name, colecao, erro.CodeName);
                if (indice.Options.Unique == true) unicos.Add($"{indice.Options.Name} em {colecao}");
                else falhas++;
            }
        }
        if (unicos.Count > 0) throw FalhaNaInicializacao.IndiceUnico(unicos);
        return falhas;
    }

    static CreateIndexModel<BsonDocument> Unico(IndexKeysDefinition<BsonDocument> chave, string nome) =>
        new(chave, new CreateIndexOptions { Name = nome, Unique = true });

    static CreateIndexModel<BsonDocument> Comum(IndexKeysDefinition<BsonDocument> chave, string nome) =>
        new(chave, new CreateIndexOptions { Name = nome });
}
