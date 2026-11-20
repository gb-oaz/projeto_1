using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>A coleção pagamentos (pagamento-documentos v02).</summary>
public interface IPagamentos
{
    /// <summary>O pagamento; null se não existe.</summary>
    Task<Pagamento?> Obter(string id, CancellationToken ct = default);

    /// <summary>O pagamento da cobrança no intermediador que a gerou; null se nenhum (RN-305, RNF-242).</summary>
    Task<Pagamento?> PorIdExterno(string intermediador, string idExterno, CancellationToken ct = default);

    /// <summary>O endereço do último boleto da conta, para lembrar na próxima vez (RF-184-api); null se nenhum.</summary>
    Task<EnderecoDoPagador?> UltimoEnderecoDoBoleto(string contaId, CancellationToken ct = default);

    /// <summary>As tentativas das faturas dadas (RN-304, RN-308).</summary>
    Task<IReadOnlyList<Pagamento>> DasFaturas(IEnumerable<string> faturaIds, CancellationToken ct = default);

    /// <summary>Quantos pagamentos confirmados em duplicidade há nas faturas da competência (RN-303, RN-308), contados no banco.</summary>
    Task<int> Duplicidades(Competencia competencia, CancellationToken ct = default);

    /// <summary>Os "AGUARDANDO" e "PROCESSANDO" (agendamento: expirar e consultar o banco, RN-302).</summary>
    Task<IReadOnlyList<Pagamento>> EmAberto(CancellationToken ct = default);

    /// <summary>Grava o pagamento novo.</summary>
    Task Incluir(Pagamento pagamento, CancellationToken ct = default);

    /// <summary>Grava a mudança se a versão guardada ainda é a lida; false se outro mudou antes.</summary>
    Task<bool> Salvar(Pagamento pagamento, CancellationToken ct = default);

    /// <summary>Apaga o pagamento que o banco não chegou a criar (RNF-241: nada fica "AGUARDANDO").</summary>
    Task Remover(string id, CancellationToken ct = default);
}

/// <summary>A coleção estornos (pagamento-documentos v02).</summary>
public interface IEstornos
{
    /// <summary>O estorno no intermediador que o recebeu; null se nenhum (RN-305, RNF-242).</summary>
    Task<Estorno?> PorIdExterno(string intermediador, string idExterno, CancellationToken ct = default);

    /// <summary>Os estornos do pagamento, o mais antigo primeiro (RF-192-api).</summary>
    Task<IReadOnlyList<Estorno>> DoPagamento(string pagamentoId, CancellationToken ct = default);

    /// <summary>Grava o estorno novo.</summary>
    Task Incluir(Estorno estorno, CancellationToken ct = default);

    /// <summary>Grava a mudança se a versão guardada ainda é a lida.</summary>
    Task<bool> Salvar(Estorno estorno, CancellationToken ct = default);

    /// <summary>Apaga o estorno que o banco não chegou a receber.</summary>
    Task Remover(string id, CancellationToken ct = default);
}

/// <summary>O aviso de mudança de um pagamento para o ao vivo (RNF-238): canal pagamento:pagamento:&lt;id&gt;.</summary>
public interface ICanalDoPagamento
{
    /// <summary>Avisa as conexões ao vivo do pagamento.</summary>
    Task Publicar(string pagamentoId, CancellationToken ct = default);

    /// <summary>Espera o aviso do pagamento até o tempo dado; true se chegou.</summary>
    Task<bool> Esperar(string pagamentoId, TimeSpan tempo, CancellationToken ct = default);
}

/// <summary>Um aviso recebido do banco, guardado em avisosDoBanco (RN-305, RNF-244).</summary>
/// <param name="Id">O id do aviso no banco: garante a idempotência.</param>
/// <param name="Intermediador">De quem é o aviso, pelo cabeçalho (RNF-242); null se nenhum confere.</param>
/// <param name="IdExterno">A cobrança ou o estorno no banco.</param>
/// <param name="Tipo">PAGAMENTO_CONFIRMADO, PAGAMENTO_RECUSADO, ESTORNO_CONCLUIDO ou ESTORNO_RECUSADO.</param>
/// <param name="RecebidoEm">Quando chegou.</param>
/// <param name="AssinaturaValida">A assinatura conferiu.</param>
/// <param name="Resultado">APLICADO, REPETIDO, DESCONHECIDO, IGNORADO ou RECUSADO.</param>
/// <param name="PagamentoId">O pagamento ligado, se achado.</param>
/// <param name="TraceId">A execução do aviso, nova.</param>
/// <param name="TraceIdOrigem">O traceId da criação, se o banco o devolveu.</param>
public sealed record RegistroDoAviso(string Id, string? Intermediador, string? IdExterno, string? Tipo, DateTimeOffset RecebidoEm,
    bool AssinaturaValida, string Resultado, string? PagamentoId, string TraceId, string? TraceIdOrigem);

/// <summary>A coleção avisosDoBanco: o _id é o id do aviso (repetido não soma em dobro, RN-305).</summary>
public interface IAvisosDoBanco
{
    /// <summary>Registra o aviso; false se o mesmo id já foi registrado.</summary>
    Task<bool> Registrar(RegistroDoAviso aviso, CancellationToken ct = default);

    /// <summary>O resultado e o pagamento ligado, depois de aplicar.</summary>
    Task Concluir(string id, string resultado, string? pagamentoId, CancellationToken ct = default);

    /// <summary>Tira o registro do aviso que falhou ao aplicar: o banco reenvia e ele vale de novo.</summary>
    Task Remover(string id, CancellationToken ct = default);
}

// ── o intermediador (RNF-242: o domínio não sabe qual é) ──────────────────────

/// <summary>A cobrança pedida ao banco (RF-185 a RF-188, RNF-244).</summary>
/// <param name="PagamentoId">O pagamento, nos metadados e na referência externa.</param>
/// <param name="FaturaId">A fatura, na descrição.</param>
/// <param name="Forma">A forma.</param>
/// <param name="Valor">O valor, com os juros.</param>
/// <param name="Cartao">O token, o método e o emissor gerados pela tela direto no banco (RNF-239).</param>
/// <param name="ExpiraEm">A validade do PIX ou do débito.</param>
/// <param name="VencimentoDoBoleto">O vencimento do boleto.</param>
/// <param name="TraceId">A execução: cabeçalho X-Trace-Id e metadados (RNF-244).</param>
/// <param name="Pagador">O nome e o documento da conta e o e-mail da conta master ou do autônomo.</param>
/// <param name="Endereco">O endereço de quem paga o boleto (RF-188).</param>
public sealed record PedidoDeCobranca(string PagamentoId, string FaturaId, FormaDePagamento Forma, Dinheiro Valor, CartaoTokenizado? Cartao,
    DateTimeOffset? ExpiraEm, DateOnly? VencimentoDoBoleto, string TraceId, Pagador Pagador, EnderecoDoPagador? Endereco = null);

/// <summary>O que o banco disse da cobrança.</summary>
public enum SituacaoDaCobranca
{
    /// <summary>Aprovada na hora (crédito): PAGO.</summary>
    Aprovada,
    /// <summary>Recusada pelo emissor: RECUSADO.</summary>
    Recusada,
    /// <summary>Em análise (cartão): PROCESSANDO.</summary>
    EmAnalise,
    /// <summary>À espera de quem paga (PIX, débito, boleto): AGUARDANDO.</summary>
    Aguardando,
    /// <summary>O token do cartão não vale (vencido ou já usado): 422 em "cartao.token".</summary>
    TokenInvalido,
    /// <summary>O banco não respondeu no prazo (15 s): 503 (RNF-241).</summary>
    Indisponivel,
}

/// <summary>A resposta do banco à cobrança.</summary>
/// <param name="Situacao">O que o banco disse.</param>
/// <param name="IdExterno">O id da cobrança no banco.</param>
/// <param name="PixCopiaECola">O código PIX copia e cola; o QR Code é desenhado pelo deboaz (IDesenhoDoQrCode).</param>
/// <param name="LinhaDigitavel">A linha do boleto.</param>
/// <param name="CodigoBarras">O código de barras do boleto.</param>
/// <param name="ConfirmacaoUrl">Onde quem paga confirma o débito (https).</param>
/// <param name="Cartao">A bandeira e o final do cartão.</param>
/// <param name="PagoEm">Quando o banco aprovou.</param>
public sealed record ResultadoDaCobranca(SituacaoDaCobranca Situacao, string? IdExterno = null, string? PixCopiaECola = null, string? LinhaDigitavel = null,
    string? CodigoBarras = null, string? ConfirmacaoUrl = null, CartaoMascarado? Cartao = null, DateTimeOffset? PagoEm = null);

/// <summary>O que o banco diz de uma cobrança já feita, na consulta do agendamento.</summary>
public enum SituacaoNoBanco
{
    /// <summary>Ainda à espera, ou o banco não respondeu: nada muda agora.</summary>
    Aguardando,
    /// <summary>Paga.</summary>
    Paga,
    /// <summary>Recusada ou cancelada no banco.</summary>
    Recusada,
}

/// <summary>A consulta de uma cobrança.</summary>
/// <param name="Situacao">O que o banco diz.</param>
/// <param name="Em">Quando pagou ou recusou.</param>
public sealed record ConsultaNoBanco(SituacaoNoBanco Situacao, DateTimeOffset? Em = null);

/// <summary>O estorno pedido ao banco (RF-192, RNF-244).</summary>
/// <param name="EstornoId">O estorno, nos metadados.</param>
/// <param name="PagamentoId">O pagamento, nos metadados.</param>
/// <param name="IdExternoDoPagamento">A cobrança no banco.</param>
/// <param name="Valor">Quanto devolver.</param>
/// <param name="TraceId">A execução.</param>
public sealed record PedidoDeEstorno(string EstornoId, string PagamentoId, string IdExternoDoPagamento, Dinheiro Valor, string TraceId);

/// <summary>O que o banco disse do estorno.</summary>
public enum SituacaoDoEstorno
{
    /// <summary>Recebido; o aviso dirá se concluiu.</summary>
    Pendente,
    /// <summary>Devolvido na hora.</summary>
    Concluido,
    /// <summary>Recusado na hora.</summary>
    Recusado,
    /// <summary>O banco não respondeu: nada foi pedido.</summary>
    Indisponivel,
}

/// <summary>A resposta do banco ao estorno.</summary>
/// <param name="Situacao">O que o banco disse.</param>
/// <param name="IdExterno">O id do estorno no banco.</param>
public sealed record ResultadoDoEstorno(SituacaoDoEstorno Situacao, string? IdExterno = null);

/// <summary>O aviso como chegou em POST /pagamento/notificacoes: o corpo cru e os cabeçalhos, para conferir a assinatura.</summary>
/// <param name="Corpo">O corpo, como veio.</param>
/// <param name="Cabecalhos">Os cabeçalhos, com o nome em minúsculas.</param>
/// <param name="Query">A query do endereço.</param>
public sealed record AvisoRecebido(string Corpo, IReadOnlyDictionary<string, string> Cabecalhos, IReadOnlyDictionary<string, string> Query);

/// <summary>O que um aviso do banco diz, já no vocabulário do Pagamento.</summary>
public enum TipoDoAviso
{
    /// <summary>PAGAMENTO_CONFIRMADO.</summary>
    PagamentoConfirmado,
    /// <summary>PAGAMENTO_RECUSADO.</summary>
    PagamentoRecusado,
    /// <summary>ESTORNO_CONCLUIDO.</summary>
    EstornoConcluido,
    /// <summary>ESTORNO_RECUSADO.</summary>
    EstornoRecusado,
}

/// <summary>Um aviso lido.</summary>
/// <param name="Id">O id do aviso (idempotência).</param>
/// <param name="Tipo">O que aconteceu.</param>
/// <param name="IdExterno">A cobrança ou o estorno no banco.</param>
/// <param name="Em">Quando o banco pagou, recusou ou devolveu.</param>
/// <param name="TraceIdOrigem">O traceId da criação, se o banco o devolveu (RNF-244).</param>
public sealed record AvisoDoBanco(string Id, TipoDoAviso Tipo, string IdExterno, DateTimeOffset Em, string? TraceIdOrigem);

/// <summary>A leitura do aviso: a assinatura e, se ela confere e o aviso importa, o aviso.</summary>
/// <param name="AssinaturaValida">A assinatura do intermediador confere.</param>
/// <param name="Aviso">O aviso; null se ele não muda nada no Pagamento.</param>
public sealed record LeituraDoAviso(bool AssinaturaValida, AvisoDoBanco? Aviso);

/// <summary>
/// A conferência do aviso, sem ir ao banco (RN-305, RNF-242): a assinatura e o
/// id externo do pagamento de que o aviso trata, quando o intermediador o diz
/// no próprio aviso (Mercado Pago: data.id). O pagamento desconhecido fica
/// DESCONHECIDO sem consultar o serviço do intermediador.
/// </summary>
/// <param name="AssinaturaValida">A assinatura do intermediador confere.</param>
/// <param name="IdDoPagamento">O id externo do pagamento; null quando só a leitura do aviso o diz.</param>
public sealed record ConferenciaDoAviso(bool AssinaturaValida, string? IdDoPagamento);

/// <summary>
/// O intermediador de pagamento (RNF-242): a única porta para o banco. O
/// adaptador traduz os status dele para os do Pagamento; trocar de
/// intermediador não muda fatura, status, códigos de erro nem endereços.
/// </summary>
public interface IIntermediador
{
    /// <summary>O nome guardado no pagamento: MERCADO_PAGO ou FALSO (NomeDoIntermediador).</summary>
    string Nome { get; }

    /// <summary>A chave pública com que a tela gera o token do cartão (RF-184-api).</summary>
    string ChavePublica { get; }

    /// <summary>Pede a cobrança, com a chave de idempotência = o id do pagamento.</summary>
    Task<ResultadoDaCobranca> Cobrar(PedidoDeCobranca pedido, CancellationToken ct = default);

    /// <summary>Consulta a cobrança (o agendamento, antes de expirar, RN-302).</summary>
    Task<ConsultaNoBanco> Consultar(string idExterno, CancellationToken ct = default);

    /// <summary>Cancela a cobrança em aberto no banco, se ele aceitar (o pagamento cancelado, RF-184).</summary>
    Task Cancelar(string idExterno, CancellationToken ct = default);

    /// <summary>Pede o estorno (RF-192).</summary>
    Task<ResultadoDoEstorno> Estornar(PedidoDeEstorno pedido, CancellationToken ct = default);

    /// <summary>Confere a assinatura e acha o id do pagamento no aviso, sem ir ao banco (RN-305).</summary>
    ConferenciaDoAviso Conferir(AvisoRecebido recebido);

    /// <summary>Confere a assinatura e lê o aviso (RN-305).</summary>
    Task<LeituraDoAviso> LerAviso(AvisoRecebido recebido, CancellationToken ct = default);
}

/// <summary>
/// Os intermediadores do ambiente (RNF-242): o ligado, que cobra os
/// pagamentos novos, e o de cada pagamento, que recebe os avisos, as
/// consultas, os cancelamentos e os estornos dele até o fim.
/// </summary>
public interface IIntermediadores
{
    /// <summary>O ligado agora: cobra os pagamentos novos e dá a chave pública da tela.</summary>
    IIntermediador Ligado { get; }

    /// <summary>O intermediador pelo nome guardado no pagamento; o ligado se o nome falta (pagamento de antes do campo).</summary>
    IIntermediador Do(string? nome);

    /// <summary>
    /// O dono do aviso, pelo cabeçalho de assinatura (x-signature ou X-Assinatura);
    /// sem cabeçalho conhecido, ou o de um intermediador que o ambiente não tem,
    /// o ligado, que recusa a assinatura (401); null só se não houver nenhum.
    /// </summary>
    IIntermediador? DoAviso(AvisoRecebido recebido);
}

/// <summary>O QR Code do PIX desenhado pelo deboaz, em SVG vetorial, a partir do copia e cola (RF-185-api).</summary>
public interface IDesenhoDoQrCode
{
    /// <summary>O SVG do QR Code do texto, com um quadrado por módulo escuro.</summary>
    string Svg(string texto);
}
