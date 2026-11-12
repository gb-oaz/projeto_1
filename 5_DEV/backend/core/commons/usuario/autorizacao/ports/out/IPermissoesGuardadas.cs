using Boaz.Core.Commons.Usuario.Autorizacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

/// <summary>
/// As permissões guardadas (permissoes, em usuarios) e o registro de mudanças
/// (registrosPermissao), conforme autorizacao-documentos.
/// </summary>
public interface IPermissoesGuardadas
{
    /// <summary>As permissões do usuário; null se ainda não há nada guardado.</summary>
    Task<PermissoesDoUsuario?> Buscar(string usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Grava as permissões (mods, suspensos, versão e pv) e o registro numa só operação (RNF-223), só se a
    /// versão guardada ainda for <paramref name="versaoLida"/>. Devolve false se
    /// outra pessoa salvou antes.
    /// </summary>
    Task<bool> Salvar(PermissoesDoUsuario permissoes, MudancaDePermissao mudanca, int versaoLida, CancellationToken ct = default);

    /// <summary>Sobe o pv do usuário sem mudar os módulos e devolve o novo (RN-239).</summary>
    Task<int> SubirPv(string usuarioId, CancellationToken ct = default);

    /// <summary>As mudanças do usuário antes de <paramref name="ate"/>, da mais nova para a mais antiga.</summary>
    Task<IReadOnlyList<MudancaDePermissao>> Historico(string usuarioId, DateTimeOffset? ate, int limite, CancellationToken ct = default);
}
