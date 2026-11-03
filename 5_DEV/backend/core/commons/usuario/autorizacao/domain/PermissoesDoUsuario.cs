namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>Uma mudança salva nas permissões de alguém: só inclusão, nunca muda nem sai (RN-256).</summary>
/// <param name="UsuarioId">De quem são as permissões.</param>
/// <param name="Por">Quem salvou.</param>
/// <param name="Em">Quando salvou.</param>
/// <param name="Versao">A versão que o salvamento criou.</param>
/// <param name="Antes">Só os módulos que mudaram, com as abas de antes (null = não tinha).</param>
/// <param name="Depois">Os mesmos módulos, com as abas de depois (null = saiu).</param>
public sealed record MudancaDePermissao(
    string UsuarioId, string Por, DateTimeOffset Em, int Versao,
    IReadOnlyDictionary<string, Abas?> Antes, IReadOnlyDictionary<string, Abas?> Depois);

/// <summary>
/// As permissões guardadas de um usuário (permissoes, embutido em usuarios):
/// o mapa de módulo para abas, a versão do If-Match (RNF-222) e o pv que vai no
/// token (RN-239). O mapa guarda também os módulos de contratação suspensa, que
/// voltam quando a conta contrata de novo (RN-238).
/// </summary>
public sealed class PermissoesDoUsuario
{
    /// <summary>Remonta as permissões gravadas.</summary>
    public PermissoesDoUsuario(string usuarioId, int versao, int pv, IReadOnlyDictionary<string, Abas> mods) =>
        (UsuarioId, Versao, Pv, Mods) = (usuarioId, versao, pv, mods);

    /// <summary>As de quem ainda não tem nada salvo: versão 1, pv 1, sem módulos.</summary>
    public static PermissoesDoUsuario Vazias(string usuarioId) => new(usuarioId, 1, 1, new Dictionary<string, Abas>());

    /// <summary>De quem são.</summary>
    public string UsuarioId { get; }

    /// <summary>Versão: sobe uma vez a cada salvamento com mudança (RN-255).</summary>
    public int Versao { get; private set; }

    /// <summary>Versão das permissões no token: sobe com o salvamento e com a contratação (RN-239).</summary>
    public int Pv { get; private set; }

    /// <summary>Módulo → abas, inclusive os de contratação suspensa.</summary>
    public IReadOnlyDictionary<string, Abas> Mods { get; private set; }

    /// <summary>Só os módulos que a conta tem agora (RN-238).</summary>
    public IReadOnlyDictionary<string, Abas> Visiveis(IReadOnlySet<string> disponiveis) =>
        Mods.Where(m => disponiveis.Contains(m.Key)).ToDictionary();

    /// <summary>
    /// Salva o conjunto inteiro dos módulos visíveis (RN-254): o que não vem sai;
    /// os de contratação suspensa ficam. Com mudança, sobe a versão e o pv uma
    /// vez e devolve o registro (RN-255, RN-256); sem mudança, devolve null.
    /// </summary>
    /// <param name="novos">O mapa pedido, já conferido.</param>
    /// <param name="disponiveis">Os módulos que a conta tem agora.</param>
    /// <param name="por">Quem salva.</param>
    /// <param name="agora">Hora do salvamento.</param>
    public MudancaDePermissao? Salvar(IReadOnlyDictionary<string, Abas> novos, IReadOnlySet<string> disponiveis, string por, DateTimeOffset agora)
    {
        var antes = Visiveis(disponiveis);
        var mudaram = antes.Keys.Union(novos.Keys)
            .Where(m => !(antes.TryGetValue(m, out var a) && novos.TryGetValue(m, out var d) && a == d))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (mudaram.Count == 0) return null;

        Mods = Mods.Where(m => !disponiveis.Contains(m.Key)).Concat(novos).ToDictionary();
        Versao++;
        Pv++;
        return new MudancaDePermissao(UsuarioId, por, agora, Versao,
            mudaram.ToDictionary(m => m, m => antes.TryGetValue(m, out var a) ? a : (Abas?)null),
            mudaram.ToDictionary(m => m, m => novos.TryGetValue(m, out var d) ? d : (Abas?)null));
    }
}
