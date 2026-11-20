using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Hub.Pagamento.Faturas.Domain;

/// <summary>Grupo de uma recusa do Pagamento, para o adaptador de entrada escolher o status HTTP.</summary>
public enum GrupoDaFalhaDoPagamento
{
    /// <summary>401: a assinatura do aviso do banco não confere (RN-305).</summary>
    NaoAutenticado,
    /// <summary>403: sem MOD-01 · U, ou não é o ADMINISTRADOR (RN-298).</summary>
    Proibido,
    /// <summary>404: não existe ou é de outra conta.</summary>
    NaoEncontrado,
    /// <summary>409: o estado da fatura ou do pagamento não permite.</summary>
    Conflito,
    /// <summary>412: a Configuração mudou desde a leitura (RF-193).</summary>
    VersaoDesatualizada,
    /// <summary>428: o PUT da Configuração sem If-Match (RF-193-api).</summary>
    VersaoAusente,
    /// <summary>422: a forma do pedido.</summary>
    DadosInvalidos,
    /// <summary>503: o banco não respondeu (RNF-241).</summary>
    Indisponivel,
}

/// <summary>
/// Uma recusa do Pagamento, com o código e a mensagem do contrato
/// (pagamento-contrato-api v03, formato-comum do 19). O adaptador de entrada a transforma na
/// resposta { codigo, mensagem, campo, saldo, traceId } (RNF-243).
/// </summary>
public sealed class FalhaDoPagamento : Exception, IRecusaPrevista
{
    FalhaDoPagamento(GrupoDaFalhaDoPagamento grupo, string codigo, string mensagem) : base(mensagem) => (Grupo, Codigo) = (grupo, codigo);

    /// <summary>O grupo, que decide o status HTTP.</summary>
    public GrupoDaFalhaDoPagamento Grupo { get; }

    /// <summary>O código do contrato, como FATURA_PAGA.</summary>
    public string Codigo { get; }

    /// <summary>O campo errado, em DADOS_INVALIDOS.</summary>
    public string? Campo { get; private init; }

    /// <summary>O saldo que ainda pode ser estornado, em ESTORNO_ACIMA_DO_SALDO (RN-306).</summary>
    public decimal? Saldo { get; private init; }

    /// <summary>Sem MOD-01 · U, ou o recebedor que não é ADMINISTRADOR (RN-298).</summary>
    public static FalhaDoPagamento AcessoNegado() =>
        new(GrupoDaFalhaDoPagamento.Proibido, "ACESSO_NEGADO", "Você não tem permissão para esta ação.");

    /// <summary>A fatura não existe ou é de outra conta.</summary>
    public static FalhaDoPagamento FaturaInexistente() =>
        new(GrupoDaFalhaDoPagamento.NaoEncontrado, "FATURA_INEXISTENTE", "Fatura não encontrada.");

    /// <summary>O pagamento não existe ou é de outra conta.</summary>
    public static FalhaDoPagamento PagamentoInexistente() =>
        new(GrupoDaFalhaDoPagamento.NaoEncontrado, "PAGAMENTO_INEXISTENTE", "Pagamento não encontrado.");

    /// <summary>O PDF do boleto de um pagamento que não é boleto (RF-188).</summary>
    public static FalhaDoPagamento BoletoInexistente() =>
        new(GrupoDaFalhaDoPagamento.NaoEncontrado, "BOLETO_INEXISTENTE", "Este pagamento não tem boleto.");

    /// <summary>A fatura já está paga (RN-301).</summary>
    public static FalhaDoPagamento FaturaPaga(string mensagem = "Esta fatura já está paga.") =>
        new(GrupoDaFalhaDoPagamento.Conflito, "FATURA_PAGA", mensagem);

    /// <summary>A fatura foi cancelada (RN-301).</summary>
    public static FalhaDoPagamento FaturaCancelada() =>
        new(GrupoDaFalhaDoPagamento.Conflito, "FATURA_CANCELADA", "Fatura cancelada não recebe pagamento.");

    /// <summary>Já há um pagamento AGUARDANDO ou PROCESSANDO na fatura (RN-303).</summary>
    public static FalhaDoPagamento PagamentoEmAberto() => new(GrupoDaFalhaDoPagamento.Conflito, "PAGAMENTO_EM_ABERTO",
        "Esta fatura já tem um pagamento em andamento.");

    /// <summary>O pagamento já foi confirmado pelo banco: não se cancela (RN-303).</summary>
    public static FalhaDoPagamento PagamentoConfirmado() => new(GrupoDaFalhaDoPagamento.Conflito, "PAGAMENTO_CONFIRMADO",
        "Pagamento já confirmado pelo banco: não pode ser cancelado.");

    /// <summary>O pagamento ainda não foi confirmado: sem comprovante nem estorno (RF-189, RF-192).</summary>
    public static FalhaDoPagamento PagamentoNaoConfirmado() => new(GrupoDaFalhaDoPagamento.Conflito, "PAGAMENTO_NAO_CONFIRMADO",
        "Este pagamento ainda não foi confirmado pelo banco.");

    /// <summary>Já há um estorno PENDENTE do pagamento (RN-306).</summary>
    public static FalhaDoPagamento EstornoEmAndamento() => new(GrupoDaFalhaDoPagamento.Conflito, "ESTORNO_EM_ANDAMENTO",
        "Aguarde o estorno em andamento.");

    /// <summary>O boleto não se estorna pelo Pagamento: quem pagou pede no SAC (RN-312).</summary>
    public static FalhaDoPagamento EstornoPeloSac() => new(GrupoDaFalhaDoPagamento.Conflito, "ESTORNO_PELO_SAC",
        "O estorno de boleto é pedido pela própria conta no SAC.");

    /// <summary>O If-Match não é a versão atual da Configuração (RF-193).</summary>
    public static FalhaDoPagamento ConfiguracaoMudou() => new(GrupoDaFalhaDoPagamento.VersaoDesatualizada, "CONFIGURACAO_MUDOU",
        "A configuração mudou. Recarregue e salve de novo.");

    /// <summary>O PUT da Configuração sem If-Match (RF-193-api, RN-299-api).</summary>
    public static FalhaDoPagamento VersaoAusente() => new(GrupoDaFalhaDoPagamento.VersaoAusente, "VERSAO_AUSENTE",
        "Envie a versão lida da configuração no If-Match.");

    /// <summary>Um campo do pedido está faltando ou é inválido.</summary>
    public static FalhaDoPagamento DadosInvalidos(string campo) =>
        new(GrupoDaFalhaDoPagamento.DadosInvalidos, "DADOS_INVALIDOS", $"O campo \"{campo}\" está faltando ou é inválido.") { Campo = campo };

    /// <summary>A forma está desligada na Configuração (RF-184, RF-193).</summary>
    public static FalhaDoPagamento FormaIndisponivel() => new(GrupoDaFalhaDoPagamento.DadosInvalidos, "FORMA_INDISPONIVEL",
        "Esta forma de pagamento não está disponível agora.")
    { Campo = "forma" };

    /// <summary>A Configuração com as quatro formas desligadas (RF-193).</summary>
    public static FalhaDoPagamento FormaObrigatoria() => new(GrupoDaFalhaDoPagamento.DadosInvalidos, "FORMA_OBRIGATORIA",
        "Mantenha ao menos uma forma ligada.")
    { Campo = "formas" };

    /// <summary>O estorno passa do que ainda pode ser devolvido (RN-306).</summary>
    public static FalhaDoPagamento EstornoAcimaDoSaldo(Dinheiro saldo) => new(GrupoDaFalhaDoPagamento.DadosInvalidos, "ESTORNO_ACIMA_DO_SALDO",
        $"Valor acima do saldo de R$ {saldo.EmTexto()}.")
    {
        Campo = "valor",
        Saldo = saldo.Reais,
    };

    /// <summary>O aviso do banco sem a assinatura certa (RN-305): a tentativa fica registrada.</summary>
    public static FalhaDoPagamento AssinaturaInvalida() =>
        new(GrupoDaFalhaDoPagamento.NaoAutenticado, "ASSINATURA_INVALIDA", "Assinatura do aviso inválida.");

    /// <summary>O banco não respondeu no prazo; nada foi cobrado (RNF-241).</summary>
    public static FalhaDoPagamento BancoIndisponivel() => new(GrupoDaFalhaDoPagamento.Indisponivel, "BANCO_INDISPONIVEL",
        "Não conseguimos falar com o banco agora. Nada foi cobrado.");
}
