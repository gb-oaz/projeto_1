using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Execucoes.Dtos;

namespace Boaz.Core.Hub.Auditoria.Execucoes.Ports.In;

/// <summary>GET /auditoria/execucoes (RF-203): as execuções do período, com a contagem de cada resultado.</summary>
public interface IListarExecucoes
{
    /// <summary>As execuções do período (até 7 dias) e do filtro, a mais nova primeiro; 503 sem o banco (RNF-252).</summary>
    Task<ExecucoesDto> Executar(PedidoDeExecucoes pedido, ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>GET /auditoria/execucoes/{traceId} (RF-202): uma execução do começo ao fim.</summary>
public interface IVerExecucao
{
    /// <summary>A execução com os passos em ordem de quando; 404 EXECUCAO_INEXISTENTE se nenhum registro tem o traceId.</summary>
    Task<ExecucaoDto> Executar(string traceId, ConsultaPedida consulta, CancellationToken ct = default);
}
