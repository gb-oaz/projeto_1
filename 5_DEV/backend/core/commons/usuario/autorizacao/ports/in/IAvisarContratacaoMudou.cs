using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>
/// Para o Catálogo: a conta contratou ou descontratou um módulo. O pv de todos
/// da conta sobe e o próximo token já vem com ou sem o módulo (RN-238, RN-239).
/// </summary>
public interface IAvisarContratacaoMudou
{
    /// <summary>
    /// Sobe o pv da conta e avisa as sessões, auditado na execução do Catálogo,
    /// ação AVISAR_CONTRATACAO_MUDOU e a conta como alvo (RN-275).
    /// </summary>
    /// <param name="contaId">A conta cuja contratação mudou.</param>
    /// <param name="chamada">A execução do Catálogo: traceId, quem e conta.</param>
    /// <param name="ct">Cancelamento.</param>
    Task Executar(string contaId, ExecucaoDeQuemChama chamada, CancellationToken ct = default);
}
