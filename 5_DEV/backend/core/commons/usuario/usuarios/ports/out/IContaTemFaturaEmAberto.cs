namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>
/// O Pagamento (19 RN-313) diz se a conta tem fatura em aberto (A_PAGAR ou
/// VENCIDA): a conta assim não se encerra (RN-173, 409 FATURA_EM_ABERTO).
/// </summary>
public interface IContaTemFaturaEmAberto
{
    /// <summary>true se a conta tem fatura A_PAGAR ou VENCIDA.</summary>
    Task<bool> Executar(string contaId, CancellationToken ct = default);
}
