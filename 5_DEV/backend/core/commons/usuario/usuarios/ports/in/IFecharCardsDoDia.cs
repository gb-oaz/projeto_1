namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>O fechamento da 00:00 de Brasília (15 RN-278): reconta todos os cards e abre o dia, a semana e o mês.</summary>
public interface IFecharCardsDoDia
{
    /// <summary>Fecha o dia de hoje, uma réplica só (trava fechamentoCard:&lt;dia&gt;).</summary>
    /// <param name="sobDemanda">Fecha mesmo que o dia já tenha sido fechado (POST /_teste/fechamento), renovando a trava.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>True se fechou; false se outra réplica já fechou o dia.</returns>
    Task<bool> Executar(bool sobDemanda = false, CancellationToken ct = default);
}
