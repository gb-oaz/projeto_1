using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A cobrança (catálogo de preços): a data do último pagamento da conta (RN-172, RN-187).</summary>
public interface IPagamentos
{
    /// <summary>A data do último pagamento; null se nunca pagou.</summary>
    Task<DateOnly?> UltimoPagamento(string contaId, CancellationToken ct = default);
}
