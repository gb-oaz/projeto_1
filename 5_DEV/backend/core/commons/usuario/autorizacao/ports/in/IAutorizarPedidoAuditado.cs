using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>
/// A conferência da rota de um endereço que muda estado (RN-270): como a de
/// <see cref="IAutorizarPedido"/>, mas a recusa por faltar o módulo grava o
/// "INICIADO" da ação do endereço e o ALERTA ACESSO_NEGADO em "auditoriaErros",
/// com o mesmo traceId, antes de sair. Atendida, não grava nada: a auditoria é
/// a do caso de uso do endereço.
/// </summary>
public interface IAutorizarPedidoAuditado
{
    /// <summary>Recusa o pedido, auditado, se o token não atende à exigência.</summary>
    /// <param name="exigencia">O que a rota declarou; null se não declarou nada.</param>
    /// <param name="pedido">Papel e módulos do token.</param>
    /// <param name="execucao">A execução do endereço, para os registros da recusa.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <exception cref="FalhaDeAutorizacao">ACESSO_NEGADO.</exception>
    Task Executar(ExigenciaDaRota? exigencia, PedidoAutorizavelDto pedido, ExecucaoDaRotaDto execucao, CancellationToken ct = default);
}
