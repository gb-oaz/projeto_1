using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// Confere a exigência da rota com o papel e os módulos do token, sem ler as
/// permissões (RF-147, RNF-208). Negar por padrão: rota sem exigência, papel
/// desconhecido ou abas fora do contrato são recusados (RNF-220).
/// </summary>
public sealed class AutorizarPedido : IAutorizarPedido
{
    /// <inheritdoc />
    public void Executar(ExigenciaDaRota? exigencia, PedidoAutorizavelDto pedido)
    {
        var papel = Papeis.De(pedido.Papel);
        var mods = ModsDoToken(pedido);
        if (exigencia is null || papel is null || !exigencia.Atende(papel.Value, mods)) throw FalhaDeAutorizacao.AcessoNegado();
    }

    /// <summary>Os módulos do token com abas do contrato; o resto não conta (RNF-220).</summary>
    static Dictionary<string, Abas> ModsDoToken(PedidoAutorizavelDto pedido) =>
        pedido.Mods
            .Select(m => (m.Key, Abas: Abas.De(m.Value)))
            .Where(m => m.Abas is not null)
            .ToDictionary(m => m.Key, m => m.Abas!.Value);
}
