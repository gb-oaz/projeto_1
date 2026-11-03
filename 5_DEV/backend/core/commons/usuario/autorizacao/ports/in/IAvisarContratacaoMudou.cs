namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>
/// Para o Catálogo: a conta contratou ou descontratou um módulo. O pv de todos
/// da conta sobe e o próximo token já vem com ou sem o módulo (RN-238, RN-239).
/// </summary>
public interface IAvisarContratacaoMudou
{
    /// <summary>Sobe o pv da conta e avisa as sessões.</summary>
    Task Executar(string contaId, CancellationToken ct = default);
}
