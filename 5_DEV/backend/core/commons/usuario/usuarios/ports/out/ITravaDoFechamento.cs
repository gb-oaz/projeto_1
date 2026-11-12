namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A trava do fechamento (fechamentoCard:&lt;dia&gt; no Redis): uma réplica só fecha o dia.</summary>
public interface ITravaDoFechamento
{
    /// <summary>Pega a trava do dia; false se outra réplica já pegou.</summary>
    /// <param name="dia">O dia de Brasília.</param>
    /// <param name="renovar">Pega mesmo que já exista, renovando o prazo (fechamento sob demanda).</param>
    /// <param name="ct">Cancelamento.</param>
    Task<bool> Pegar(DateOnly dia, bool renovar = false, CancellationToken ct = default);
}
