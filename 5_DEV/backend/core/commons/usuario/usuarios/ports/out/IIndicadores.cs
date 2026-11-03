using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>Os números do card Usuários (RN-189), contados no banco.</summary>
public interface IIndicadores
{
    /// <summary>Usuários ativos dos tipos, no fim de cada um dos últimos meses; o último valor é agora.</summary>
    Task<IReadOnlyList<int>> UsuariosAtivosPorMes(string contaId, IReadOnlySet<TipoUsuario> tipos, DateTimeOffset agora, int meses,
        CancellationToken ct = default);

    /// <summary>Usuários ativos do tipo, agora.</summary>
    Task<int> UsuariosAtivos(string contaId, TipoUsuario tipo, CancellationToken ct = default);

    /// <summary>Contas ativas no fim de cada um dos últimos meses; o último valor é agora.</summary>
    Task<IReadOnlyList<int>> ContasAtivasPorMes(DateTimeOffset agora, int meses, CancellationToken ct = default);

    /// <summary>Códigos de liberação da conta ainda abertos que vencem entre as horas dadas.</summary>
    Task<int> CodigosQueVencem(string contaId, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default);
}
