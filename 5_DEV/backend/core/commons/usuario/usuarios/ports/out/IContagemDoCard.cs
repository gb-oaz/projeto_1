using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>As consultas dos números do card (RN-280), em contas, usuarios e geracoesSenha.</summary>
public interface IContagemDoCard
{
    /// <summary>Todos os números do card agora.</summary>
    /// <param name="perfil">O perfil do card.</param>
    /// <param name="contaId">A conta (no DEBOAZ, a do time; as contas contadas são as assinantes).</param>
    /// <param name="usuarioId">A staff dona do card; só no STAFF.</param>
    /// <param name="agora">A hora da contagem.</param>
    /// <param name="ct">Cancelamento.</param>
    Task<NumerosDoCard> Contar(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora, CancellationToken ct = default);

    /// <summary>Só o que muda com o tempo: o selo, "Sem acesso há 30 dias" e "Códigos vencidos".</summary>
    Task<NumerosNoTempo> NoTempo(PerfilCard perfil, string contaId, string? usuarioId, DateTimeOffset agora, CancellationToken ct = default);
}
