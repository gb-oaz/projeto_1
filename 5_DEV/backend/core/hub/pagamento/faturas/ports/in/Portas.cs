using Boaz.Core.Hub.Pagamento.Faturas.Dtos;

namespace Boaz.Core.Hub.Pagamento.Faturas.Ports.In;

/// <summary>GET /pagamento/faturas (RF-182).</summary>
public interface IListarFaturas
{
    /// <summary>As faturas da conta de quem pede, na situação pedida, com o resumo.</summary>
    Task<ListaDeFaturasDto> Executar(string? situacao, int? pagina, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /pagamento/faturas/{id} (RF-183).</summary>
public interface ILerFatura
{
    /// <summary>A fatura da conta de quem pede, com os juros de hoje e as tentativas.</summary>
    Task<FaturaDto> Executar(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /pagamento/avisos (RN-311).</summary>
public interface ILerAvisos
{
    /// <summary>Os avisos de atraso da conta de quem pede.</summary>
    Task<ListaDeAvisosDto> Executar(QuemPede quem, CancellationToken ct = default);
}

/// <summary>
/// O que o AgendamentoDoPagamento faz a cada 1 min, numa execução com traceId
/// próprio (RNF-244): fecha a competência anterior (RN-300), vira as vencidas e
/// abre os avisos de atraso (RN-301, RN-311), expira e consulta os pagamentos
/// em aberto (RN-302).
/// </summary>
public interface IRodarAgendamentoDoPagamento
{
    /// <summary>Roda tudo, agora, pelo relógio da aplicação.</summary>
    Task Executar(CancellationToken ct = default);
}
