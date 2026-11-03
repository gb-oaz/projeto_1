namespace Boaz.MsMono.Tests;

// wwwroot/doc/api/diagramas/ guarda a versão mais recente de cada diagrama que
// os guias mostram (cópia de 2_Diagramas/ por scripts/diagramas-da-doc.sh): uma
// versão nova sem rodar o script deixa a documentação para trás, e este teste falha.
public class DiagramasDaDocumentacaoTests
{
    static readonly string Backend = AcharBackend();
    static readonly string Copias = Path.Combine(Backend, "ms_mono", "wwwroot", "doc", "api", "diagramas");
    static readonly string Origem = Path.GetFullPath(Path.Combine(Backend, "..", "..", "2_Diagramas"));

    public static TheoryData<string> Legendas()
    {
        var dados = new TheoryData<string>();
        foreach (var copia in Directory.GetFiles(Copias, "*.svg").Order()) dados.Add(Path.GetFileNameWithoutExtension(copia));
        return dados;
    }

    [Theory]
    [MemberData(nameof(Legendas))]
    public void Copia_e_a_versao_mais_recente(string legenda)
    {
        var ultima = Directory.GetFiles(Origem, $"{legenda}_v*.svg")
            .OrderBy(f => int.Parse(Path.GetFileName(f)[(legenda.Length + 2)..].Split('_')[0], System.Globalization.CultureInfo.InvariantCulture))
            .LastOrDefault();

        Assert.True(ultima is not null, $"{legenda} não existe mais em 2_Diagramas/");
        Assert.True(File.ReadAllBytes(ultima).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Copias, $"{legenda}.svg"))),
            $"doc/api/diagramas/{legenda}.svg está atrás de {Path.GetFileName(ultima)}: rode 5_DEV/backend/scripts/diagramas-da-doc.sh");
    }

    [Fact]
    public void Todo_diagrama_citado_nos_guias_existe()
    {
        var guias = Directory.GetFiles(Path.Combine(Backend, "ms_mono", "sistema", "guias"), "*.md");
        var citados = guias.SelectMany(g => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(g), @"/doc/api/diagramas/([a-z0-9-]+)\.svg"))
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.NotEmpty(citados);
        Assert.All(citados, legenda => Assert.True(File.Exists(Path.Combine(Copias, $"{legenda}.svg")), $"falta a cópia de {legenda}"));
    }

    // 5_DEV/backend, subindo a partir da pasta do teste
    static string AcharBackend()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Boaz.slnx"))) pasta = pasta.Parent;
        return pasta?.FullName ?? throw new InvalidOperationException("Boaz.slnx não encontrado acima da pasta do teste.");
    }
}
