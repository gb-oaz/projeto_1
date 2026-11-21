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
        (Colecoes.PedidosAjuda, Comum(Chave.Ascending("chamado"), "chamado")),
        // SAC (sac-documentos v01): Meus chamados, o limite de 10, a fila do
        // time, o atrasado, os 7 dias do agendamento e a guarda de 5 anos
        (Colecoes.Chamados, Comum(Chave.Ascending("contaId").Descending("atualizadoEm"), "contaId_atualizadoEm")),
        (Colecoes.Chamados, Comum(Chave.Ascending("contaId").Ascending("status"), "contaId_status")),
        (Colecoes.Chamados, Comum(Chave.Ascending("status").Ascending("ordemDaFila"), "status_ordemDaFila")),
        (Colecoes.Chamados, Comum(Chave.Ascending("responsavel.usuarioId").Ascending("status"), "responsavel_status")),
        (Colecoes.Chamados, Comum(Chave.Ascending("status").Ascending("prazoResposta"), "status_prazoResposta")),
        (Colecoes.Chamados, Comum(Chave.Ascending("status").Ascending("aguardandoDesde"), "status_aguardandoDesde")),
        (Colecoes.Chamados, Comum(Chave.Ascending("status").Ascending("resolvidoEm"), "status_resolvidoEm")),
        (Colecoes.Chamados, Comum(Chave.Ascending("avaliacao.nota"), "avaliacao_nota")),
        (Colecoes.Chamados, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.MensagensChamado, Comum(Chave.Ascending("chamadoId").Ascending("numero"), "chamadoId_numero")),
        (Colecoes.AnexosSac, Comum(Chave.Ascending("chamadoId"), "chamadoId")),
        (Colecoes.AnexosSac, Comum(Chave.Ascending("expiraEm"), "expiraEm")),
        (Colecoes.AnexosSac, Comum(Chave.Ascending("enviadoPor").Ascending("enviadoEm"), "enviadoPor_enviadoEm")),
        (Colecoes.CardSac, Unico(Chave.Ascending("escopo").Ascending("dia"), "escopo_dia_unico")),
        // Pagamento (pagamento-documentos v03): uma fatura por conta e mês (RN-300),
        // as listas do pagador e do recebedor, o agendamento e o traceId (RNF-244)
        (Colecoes.Faturas, Unico(Chave.Ascending("conta.id").Ascending("competencia"), "conta_competencia_unica")),
        (Colecoes.Faturas, Comum(Chave.Ascending("conta.id").Ascending("status").Descending("competencia"), "conta_status_competencia")),
        (Colecoes.Faturas, Comum(Chave.Ascending("competencia").Ascending("status").Ascending("vencimento"), "competencia_status_vencimento")),
        (Colecoes.Faturas, Comum(Chave.Ascending("status").Ascending("vencimento"), "status_vencimento")),
        (Colecoes.Faturas, Comum(Chave.Ascending("conta.sigla"), "conta_sigla")),
        (Colecoes.Faturas, Comum(Chave.Ascending("conta.nome"), "conta_nome")),
        (Colecoes.Faturas, Comum(Chave.Ascending("historico.traceId"), "historico_traceId")),
        (Colecoes.Pagamentos, Comum(Chave.Ascending("faturaId").Descending("criadoEm"), "faturaId_criadoEm")),
        // o aviso acha o pagamento pelo intermediador e pelo id externo (RNF-242); o
        // índice é único só entre os que já têm o id externo do banco
        (Colecoes.Pagamentos, UnicoParcial(Chave.Ascending("intermediador").Ascending("idExterno"), "intermediador_idExterno_unico",
            new BsonDocument("idExterno", new BsonDocument("$type", "string")))),
        // o último endereço do boleto da conta (RF-184-api)
        (Colecoes.Pagamentos, Comum(Chave.Ascending("contaId").Ascending("forma").Descending("criadoEm"), "contaId_forma_criadoEm")),
        (Colecoes.Pagamentos, Comum(Chave.Ascending("status").Ascending("expiraEm"), "status_expiraEm")),
        (Colecoes.Pagamentos, Comum(Chave.Ascending("traceId"), "traceId")),
        (Colecoes.Pagamentos, Comum(Chave.Ascending("historico.traceId"), "historico_traceId")),
        (Colecoes.Estornos, Comum(Chave.Ascending("pagamentoId").Descending("pedidoEm"), "pagamentoId_pedidoEm")),
        (Colecoes.Estornos, Comum(Chave.Ascending("intermediador").Ascending("idExterno"), "intermediador_idExterno")),
        (Colecoes.ConfiguracaoPagamento, Unico(Chave.Ascending("versao"), "versao_unica")),
        (Colecoes.ConfiguracaoPagamento, Comum(Chave.Descending("vigenteDesde"), "vigenteDesde")),
        (Colecoes.AvisosDoBanco, Comum(Chave.Ascending("resultado").Descending("recebidoEm"), "resultado_recebidoEm")),
        (Colecoes.AvisosDoBanco, Comum(Chave.Ascending("idExterno"), "idExterno")),
        (Colecoes.AvisosDoBanco, Comum(Chave.Ascending("traceId"), "traceId")),
        (Colecoes.AvisosPagamento, Comum(Chave.Ascending("contaId").Ascending("encerradoEm"), "contaId_encerradoEm")),
        (Colecoes.Cobrancas, Comum(Chave.Ascending("competencia"), "competencia")),
        // Auditoria (auditoria-documentos v02, RNF-251): nenhum é único, então nenhum para a partida. As leituras vão do mais novo
        // ao mais antigo; auditoria e auditoriaErros ganham os índices por hora, por ação, por conta, por quem, por nível e por código
        (Colecoes.VerificacoesSaude, Comum(Chave.Ascending("servico").Descending("quando"), "servico_quando")),
        (Colecoes.VerificacoesSaude, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.QuedasServico, Comum(Chave.Ascending("servico").Descending("inicio"), "servico_inicio")),
        (Colecoes.QuedasServico, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.MedicoesMinuto, Comum(Chave.Ascending("minuto"), "minuto")),
        (Colecoes.MedicoesMinuto, Comum(Chave.Ascending("modulo").Ascending("minuto"), "modulo_minuto")),
        (Colecoes.MedicoesMinuto, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.MedicoesHora, Comum(Chave.Ascending("hora"), "hora")),
        (Colecoes.MedicoesHora, Comum(Chave.Ascending("modulo").Ascending("hora"), "modulo_hora")),
        (Colecoes.MedicoesHora, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.CardAuditoria, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.Ocorrencias, Comum(Chave.Descending("quando"), "quando_desc")),
        (Colecoes.Ocorrencias, Comum(Chave.Ascending("alvo").Descending("quando"), "alvo_quando")),
        (Colecoes.Ocorrencias, Comum(Chave.Ascending("para").Descending("quando"), "para_quando")),
        (Colecoes.Ocorrencias, Comum(Chave.Ascending("guardarAte"), "guardarAte")),
        (Colecoes.Auditoria, Comum(Chave.Descending("quando"), "quando_desc")),
        (Colecoes.Auditoria, Comum(Chave.Ascending("acao").Descending("quando"), "acao_quando")),
        (Colecoes.Auditoria, Comum(Chave.Ascending("contaId").Descending("quando"), "contaId_quando_desc")),
        (Colecoes.Auditoria, Comum(Chave.Ascending("quem").Descending("quando"), "quem_quando")),
        (Colecoes.AuditoriaErros, Comum(Chave.Descending("quando"), "quando_desc")),
        (Colecoes.AuditoriaErros, Comum(Chave.Ascending("nivel").Descending("quando"), "nivel_quando")),
        (Colecoes.AuditoriaErros, Comum(Chave.Ascending("codigo").Descending("quando"), "codigo_quando")),
        (Colecoes.AuditoriaErros, Comum(Chave.Ascending("acao").Descending("quando"), "acao_quando")),
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

    static CreateIndexModel<BsonDocument> UnicoParcial(IndexKeysDefinition<BsonDocument> chave, string nome, BsonDocument quando) =>
        new(chave, new CreateIndexOptions<BsonDocument> { Name = nome, Unique = true, PartialFilterExpression = quando });

    static CreateIndexModel<BsonDocument> Comum(IndexKeysDefinition<BsonDocument> chave, string nome) =>
        new(chave, new CreateIndexOptions { Name = nome });
}
