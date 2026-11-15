using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Pagamento.Pagamentos.Ports.In;

/// <summary>POST /pagamento/faturas/{id}/pagamentos (RF-184 a RF-188, RN-303).</summary>
public interface ICriarPagamento
{
    /// <summary>Cria o pagamento e pede a cobrança ao banco; auditado como PAGAMENTO_CRIADO.</summary>
    /// <param name="faturaId">A fatura.</param>
    /// <param name="pedido">A forma e o token do cartão.</param>
    /// <param name="quem">Quem pede.</param>
    /// <param name="traceId">A execução (X-Trace-Id).</param>
    /// <param name="traceIdOrigem">O X-Trace-Id recebido no pedido, se no formato (RNF-243).</param>
    /// <param name="ct">Cancelamento.</param>
    Task<PagamentoDto> Executar(string faturaId, PedidoDePagamento pedido, QuemPede quem, TraceId traceId, TraceId? traceIdOrigem,
        CancellationToken ct = default);
}

/// <summary>GET /pagamento/pagamentos/{id}.</summary>
public interface ILerPagamento
{
    /// <summary>O pagamento da conta de quem pede.</summary>
    Task<PagamentoDto> Executar(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>POST /pagamento/pagamentos/{id}/cancelar (RF-184, RN-303).</summary>
public interface ICancelarPagamento
{
    /// <summary>Cancela o pagamento em aberto; auditado como PAGAMENTO_CANCELADO.</summary>
    Task<PagamentoDto> Executar(string id, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>GET /pagamento/pagamentos/{id}/boleto (RF-188) e …/comprovante (RF-189).</summary>
public interface ILerDocumentoDoPagamento
{
    /// <summary>O que vai no PDF do boleto.</summary>
    Task<DocumentoDoPagamentoDto> Boleto(string id, QuemPede quem, CancellationToken ct = default);

    /// <summary>O que vai no PDF do comprovante do pagamento confirmado.</summary>
    Task<DocumentoDoPagamentoDto> Comprovante(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /pagamento/pagamentos/{id}/ao-vivo (RNF-238).</summary>
public interface IAcompanharPagamento
{
    /// <summary>O estado do pagamento ao abrir (não vai como evento).</summary>
    Task<EventoDoPagamentoDto> Abrir(string id, QuemPede quem, CancellationToken ct = default);

    /// <summary>O evento seguinte, se o status mudou desde o dado, esperando o aviso até o tempo dado; null se não mudou.</summary>
    Task<EventoDoPagamentoDto?> Proximo(EventoDoPagamentoDto atual, TimeSpan espera, CancellationToken ct = default);
}

/// <summary>POST /pagamento/notificacoes (RN-305): o aviso do banco, sem login.</summary>
public interface IReceberAvisoDoBanco
{
    /// <summary>Confere a assinatura e aplica o aviso; auditado como AVISO_DO_BANCO.</summary>
    Task Executar(AvisoRecebido recebido, TraceId traceId, CancellationToken ct = default);
}

/// <summary>Parte do agendamento (RN-302): expira e consulta no banco os pagamentos em aberto.</summary>
public interface IExpirarPagamentos
{
    /// <summary>Roda agora, na execução do agendamento.</summary>
    Task Executar(TraceId traceId, CancellationToken ct = default);
}
