using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;

namespace Boaz.Core.Hub.Pagamento.Recebimentos.Ports.In;

/// <summary>GET /pagamento/recebimentos/resumo e …/competencias (RF-190, RN-308): só o ADMINISTRADOR.</summary>
public interface ILerRecebimentos
{
    /// <summary>Os números da competência; sem competência, a anterior à de hoje.</summary>
    Task<ResumoDosRecebimentosDto> Resumo(string? competencia, QuemPede quem, CancellationToken ct = default);

    /// <summary>As 12 competências até a dada, a mais nova primeiro.</summary>
    Task<IReadOnlyList<CompetenciaDto>> Competencias(string? ate, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /pagamento/recebimentos/faturas (RF-191): as faturas de todas as contas.</summary>
public interface IListarFaturasDoRecebedor
{
    /// <summary>As faturas do filtro, do vencimento mais antigo e, no mesmo vencimento, do menor número.</summary>
    Task<FaturasDoRecebedorDto> Executar(string? competencia, string? situacao, string? busca, int? pagina, QuemPede quem,
        CancellationToken ct = default);
}

/// <summary>GET /pagamento/recebimentos/faturas/{id} (RF-191-api): a fatura de qualquer conta, só para o ADMINISTRADOR.</summary>
public interface ILerFaturaDoRecebedor
{
    /// <summary>O mesmo corpo do GET da conta: itens, juros de hoje e tentativas.</summary>
    Task<FaturaDto> Executar(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /pagamento/recebimentos/pagamentos/{id} (RF-192-api): o pagamento de qualquer conta com os estornos, só para o ADMINISTRADOR.</summary>
public interface ILerPagamentoDoRecebedor
{
    /// <summary>O pagamento, a conta, quem pagou, o saldo e os estornos.</summary>
    Task<PagamentoDoRecebedorDto> Executar(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>POST /pagamento/recebimentos/faturas/{id}/cancelar (RF-191).</summary>
public interface ICancelarFatura
{
    /// <summary>Cancela a fatura com o motivo; auditado como FATURA_CANCELADA.</summary>
    Task<FaturaDto> Executar(string id, PedidoDeCancelamento pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>POST /pagamento/recebimentos/pagamentos/{id}/estornos (RF-192, RN-306).</summary>
public interface IPedirEstorno
{
    /// <summary>Pede o estorno ao banco; auditado como ESTORNO_PEDIDO.</summary>
    Task<EstornoDto> Executar(string pagamentoId, PedidoDeEstornoDto pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}
