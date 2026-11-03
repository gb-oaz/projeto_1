using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>Leitura das contas (coleção contas): estado e tempo sem uso.</summary>
public interface IContas
{
    /// <summary>A conta pelo id; null se não existir.</summary>
    Task<ContaDaEntrada?> Buscar(string id, CancellationToken ct = default);

    /// <summary>A conta do time deboaz, dona dos tempos dos membros e das contas master (RN-243).</summary>
    Task<ContaDaEntrada> BuscarTimeDeboaz(CancellationToken ct = default);
}
