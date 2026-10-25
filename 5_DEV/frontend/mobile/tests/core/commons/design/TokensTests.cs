using System.Reflection;
using Microsoft.Maui.Controls.Xaml;

namespace Boaz.Mobile.Core.Commons.Tests.Design;

// Os arquivos gerados do design system ficam no Commons (RNF-184) e são
// compilados com ele: os tokens e cada paleta carregam com as cores do tema.
public class TokensTests
{
    private static readonly XamlResourceIdAttribute[] Recursos =
        [.. Assembly.Load("Boaz.Mobile.Core.Commons").GetCustomAttributes<XamlResourceIdAttribute>()];

    public static TheoryData<string> Cores()
    {
        var dados = new TheoryData<string> { "Resources/Styles/Design/Tokens.xaml" };
        foreach (var recurso in Recursos)
        {
            if (recurso.Path.StartsWith("Resources/Styles/Design/paletas/", StringComparison.Ordinal))
            {
                dados.Add(recurso.Path);
            }
        }
        return dados;
    }

    [Theory]
    [MemberData(nameof(Cores))]
    public void Dicionario_carrega_com_as_cores_primarias(string caminho)
    {
        var recurso = Assert.Single(Recursos, r => r.Path == caminho);

        var dicionario = Assert.IsAssignableFrom<ResourceDictionary>(Activator.CreateInstance(recurso.Type));

        Assert.True(dicionario.ContainsKey("PrimaryLight"), $"{caminho} sem PrimaryLight");
        Assert.True(dicionario.ContainsKey("PrimaryDark"), $"{caminho} sem PrimaryDark");
    }
}
