using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Boaz.Qa.Integracao.Suporte;

// Confere a resposta com o JSON do cenário: os campos do exemplo precisam
// estar na resposta com o mesmo valor; listas em qualquer ordem; data e hora
// do exemplo só pedem uma data e hora; "eyJ…" só pede o começo. Com exato,
// campo a mais também é diferença (os mods de um token, por exemplo).
public static partial class Comparacao
{
    public static void Confere(JsonNode? esperado, JsonNode? atual, string caminho = "$", bool exato = false)
    {
        var erro = Diferenca(esperado, atual, caminho, exato);
        if (erro is not null) throw new Xunit.Sdk.XunitException($"{erro}\nresposta: {atual?.ToJsonString()}");
    }

    static string? Diferenca(JsonNode? esperado, JsonNode? atual, string caminho, bool exato)
    {
        switch (esperado)
        {
            case null:
                return atual is null ? null : $"{caminho}: esperava null, veio {atual.ToJsonString()}";
            case JsonObject objeto:
                if (atual is not JsonObject a) return $"{caminho}: esperava um objeto, veio {atual?.ToJsonString()}";
                foreach (var (campo, valor) in objeto)
                {
                    if (!a.ContainsKey(campo)) return $"{caminho}.{campo}: ausente";
                    if (Diferenca(valor, a[campo], $"{caminho}.{campo}", exato) is { } e) return e;
                }
                if (exato && a.FirstOrDefault(c => !objeto.ContainsKey(c.Key)) is { Key: not null } extra) return $"{caminho}.{extra.Key}: a mais";
                return null;
            case JsonArray lista:
                if (atual is not JsonArray l) return $"{caminho}: esperava uma lista, veio {atual?.ToJsonString()}";
                if (lista.Count != l.Count) return $"{caminho}: esperava {lista.Count} itens, vieram {l.Count}";
                var livres = l.ToList();
                for (var i = 0; i < lista.Count; i++)
                {
                    var par = livres.FirstOrDefault(x => Diferenca(lista[i], x, caminho, exato) is null);
                    if (par is null && livres.Count > 0) return $"{caminho}[{i}]: nenhum item igual a {lista[i]!.ToJsonString()}";
                    livres.Remove(par);
                }
                return null;
            default:
                var texto = esperado.ToJsonString();
                if (esperado.GetValueKind() == System.Text.Json.JsonValueKind.String)
                {
                    var s = esperado.GetValue<string>();
                    var veio = atual?.GetValueKind() == System.Text.Json.JsonValueKind.String ? atual.GetValue<string>() : null;
                    if (DataHora().IsMatch(s)) return veio is not null && DateTimeOffset.TryParse(veio, out _) ? null : $"{caminho}: esperava data e hora, veio {atual?.ToJsonString()}";
                    if (s.EndsWith('…')) return veio?.StartsWith(s[..^1], StringComparison.Ordinal) == true ? null : $"{caminho}: esperava começar com {s[..^1]}, veio {atual?.ToJsonString()}";
                }
                return JsonNode.DeepEquals(esperado, atual) ? null : $"{caminho}: esperava {texto}, veio {atual?.ToJsonString()}";
        }
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")]
    private static partial Regex DataHora();
}
