using System.Text.Json;
using System.Text.RegularExpressions;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// A lista de módulos e abas iniciais das contas qa, lida do Key Vault de
/// homologação (USUARIO_QA_MODULOS, RN-268), em duas partes:
/// {"staff": {"mods": {"MOD-03": "CU"}}, "time": {"mods": {"MOD-14": "U"}}}.
/// A parte "staff" vira as contratações da "QAINS" e da "QAAUT" e as abas do
/// "qastaff"; a parte "time", só as abas do "qamembro", sem contratação. Trocar
/// a lista não muda o requisito: vale na próxima partida.
/// </summary>
public sealed partial class ModulosIniciais
{
    ModulosIniciais(IReadOnlyDictionary<string, string> staff, IReadOnlyDictionary<string, string> time)
    {
        Staff = staff;
        Time = time;
    }

    /// <summary>Parte "staff": módulo → abas ("C", "U" ou "CU"), em ordem de módulo.</summary>
    public IReadOnlyDictionary<string, string> Staff { get; }

    /// <summary>Parte "time": módulo → abas do membro do time qa, sem contratação.</summary>
    public IReadOnlyDictionary<string, string> Time { get; }

    /// <summary>Os módulos da parte "staff": o que a QAINS e a QAAUT contratam.</summary>
    public IReadOnlySet<string> Modulos => Staff.Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>Lê e confere a lista (RNF-226): as duas partes são obrigatórias.</summary>
    /// <param name="json">O valor de USUARIO_QA_MODULOS.</param>
    /// <exception cref="FalhaNaInicializacao">Ausente ou fora do formato: só o nome da variável, nunca o valor.</exception>
    public static ModulosIniciais Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw FalhaNaInicializacao.SegredoAusente([ContasIniciais.VariavelDosModulos]);
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object) throw Invalida();
            return new ModulosIniciais(Parte(documento.RootElement, "staff"), Parte(documento.RootElement, "time"));
        }
        catch (JsonException)
        {
            throw Invalida();
        }
    }

    static SortedDictionary<string, string> Parte(JsonElement raiz, string nome)
    {
        if (!raiz.TryGetProperty(nome, out var parte) || parte.ValueKind != JsonValueKind.Object
            || !parte.TryGetProperty("mods", out var mods) || mods.ValueKind != JsonValueKind.Object)
        {
            throw Invalida();
        }
        var lidos = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var mod in mods.EnumerateObject())
        {
            if (!Modulo().IsMatch(mod.Name) || mod.Value.ValueKind != JsonValueKind.String
                || mod.Value.GetString() is not ("C" or "U" or "CU") || !lidos.TryAdd(mod.Name, mod.Value.GetString()!))
            {
                throw Invalida();
            }
        }
        return lidos;
    }

    static FalhaNaInicializacao Invalida() => FalhaNaInicializacao.ListaInvalida(ContasIniciais.VariavelDosModulos);

    [GeneratedRegex("^MOD-[0-9]{2}$")]
    private static partial Regex Modulo();
}
