using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>
/// A conferência de cada rota pelo token, sem ler as permissões (RF-147,
/// RNF-208): a rota sem exigência declarada é recusada (RNF-220).
/// </summary>
public interface IAutorizarPedido
{
    /// <summary>Recusa o pedido se o token não atende à exigência.</summary>
    /// <param name="exigencia">O que a rota declarou; null se não declarou nada.</param>
    /// <param name="pedido">Papel e módulos do token.</param>
    /// <exception cref="FalhaDeAutorizacao">ACESSO_NEGADO.</exception>
    void Executar(ExigenciaDaRota? exigencia, PedidoAutorizavelDto pedido);
}
