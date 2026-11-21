using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Dtos;

namespace Boaz.Core.Hub.Auditoria.Registros.Ports.In;

/// <summary>GET /auditoria/registros (RF-199, RF-200): auditoria e auditoriaErros juntos, por período.</summary>
public interface IConsultarRegistros
{
    /// <summary>Os registros do período e do filtro, o mais novo primeiro; 503 sem o banco (RNF-252).</summary>
    Task<RegistrosDto> Executar(PedidoDeRegistros pedido, ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>GET /auditoria/registros/ao-vivo (RF-201, RNF-249): cada registro novo em até 5 s, pelos mesmos filtros.</summary>
public interface IAcompanharRegistrosAoVivo
{
    /// <summary>Abre a conexão (até 3 por usuário) e grava a consulta uma vez; os filtros de /registros, sem período nem página.</summary>
    Task<AoVivoAberto> Abrir(PedidoDeRegistros filtros, ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>GET /auditoria/acoes (RN-319): o catálogo das ações; não é auditado.</summary>
public interface IListarAcoes
{
    /// <summary>As 41 ações com nome, módulo e o que quer dizer a falta de quem.</summary>
    IReadOnlyList<AcaoDto> Executar();
}

/// <summary>O registro de toda consulta atendida (RN-325): CONSULTAR_AUDITORIA, INICIADO e CONCLUIDO, uma vez a cada 5 min por consulta.</summary>
public interface IRegistrarConsulta
{
    /// <summary>
    /// Grava a consulta na auditoria. Não grava a repetida em menos de 5 min nem, com o banco fora, qualquer uma; a falha da
    /// gravação não derruba a consulta.
    /// </summary>
    Task<ResultadoDaConsulta> Executar(ConsultaPedida consulta, CancellationToken ct = default);
}

/// <summary>O que aconteceu com a consulta ao ser registrada.</summary>
public enum ResultadoDaConsulta
{
    /// <summary>Gravada: INICIADO e CONCLUIDO.</summary>
    Gravada,
    /// <summary>A mesma consulta da mesma pessoa em menos de 5 min: não grava de novo.</summary>
    Repetida,
    /// <summary>Sem o banco ou com a gravação falhando: a consulta responde e fica sem registro.</summary>
    SemRegistro,
}
