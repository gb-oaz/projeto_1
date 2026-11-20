using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Boaz.Qa.Integracao.Suporte;

// O conteúdo de um PDF da API (19 RF-188: o boleto), lido de fora, sem
// biblioteca: os fluxos de conteúdo (crus ou FlateDecode) e, neles, o texto
// das strings literais e hexadecimais e os traços desenhados (retângulos "re"
// e segmentos "l"). Vale para o texto em fonte padrão (WinAnsi); texto em
// fonte embutida com códigos de glifo não se lê assim.
public sealed partial class PdfLido
{
    public string Texto { get; }
    public int Retangulos { get; }
    public int Segmentos { get; }

    PdfLido(string texto, int retangulos, int segmentos) => (Texto, Retangulos, Segmentos) = (texto, retangulos, segmentos);

    public static PdfLido Ler(byte[] bytes)
    {
        Assert.True(bytes.Length > 4 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-", "esperava o conteúdo de um PDF");
        var bruto = Encoding.Latin1.GetString(bytes);
        var texto = new StringBuilder();
        var (retangulos, segmentos) = (0, 0);
        foreach (Match m in Fluxo().Matches(bruto))
        {
            var dicionario = m.Groups[1].Value;
            var inicio = m.Index + m.Length;
            var fim = bruto.IndexOf("endstream", inicio, StringComparison.Ordinal);
            if (fim < 0) break;
            var dados = bytes.AsSpan(inicio, fim - inicio).ToArray();
            var conteudo = dicionario.Contains("/FlateDecode", StringComparison.Ordinal) ? Descomprimir(dados) : Encoding.Latin1.GetString(dados);
            if (conteudo is null) continue;
            Strings(conteudo, texto);
            retangulos += Retangulo().Matches(conteudo).Count;
            segmentos += Segmento().Matches(conteudo).Count;
        }
        return new PdfLido(texto.ToString(), retangulos, segmentos);
    }

    // o texto tem o trecho, sem diferença de espaços (o PDF quebra e espaça o texto como quer)
    public bool Traz(string trecho) => SemEspacos(Texto).Contains(SemEspacos(trecho), StringComparison.Ordinal);

    public override string ToString() => $"PDF com o texto \"{Texto}\", {Retangulos} retângulos e {Segmentos} segmentos";

    static string SemEspacos(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));

    static string? Descomprimir(byte[] dados)
    {
        try
        {
            using var entrada = new ZLibStream(new MemoryStream(dados), CompressionMode.Decompress);
            using var saida = new MemoryStream();
            entrada.CopyTo(saida);
            return Encoding.Latin1.GetString(saida.ToArray());
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // as strings literais "(…)", com escapes e parênteses aninhados, e as hexadecimais "<…>"
    static void Strings(string conteudo, StringBuilder texto)
    {
        for (var i = 0; i < conteudo.Length; i++)
        {
            var c = conteudo[i];
            if (c == '(')
            {
                var nivel = 1;
                for (i++; i < conteudo.Length && nivel > 0; i++)
                {
                    var d = conteudo[i];
                    if (d == '\\' && i + 1 < conteudo.Length)
                    {
                        var e = conteudo[++i];
                        if (e is >= '0' and <= '7')
                        {
                            var octal = e.ToString();
                            while (octal.Length < 3 && i + 1 < conteudo.Length && conteudo[i + 1] is >= '0' and <= '7') octal += conteudo[++i];
                            texto.Append((char)Convert.ToInt32(octal, 8));
                        }
                        else texto.Append(e switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => e });
                    }
                    else if (d == '(') { nivel++; texto.Append(d); }
                    else if (d == ')') { if (--nivel > 0) texto.Append(d); }
                    else texto.Append(d);
                }
                i--;
                texto.Append(' ');
            }
            else if (c == '<' && i + 1 < conteudo.Length && conteudo[i + 1] != '<' && (i == 0 || conteudo[i - 1] != '<'))
            {
                var fim = conteudo.IndexOf('>', i);
                if (fim < 0) break;
                var hex = string.Concat(conteudo[(i + 1)..fim].Where(Uri.IsHexDigit));
                if (hex.Length % 2 == 1) hex += "0";
                for (var k = 0; k < hex.Length; k += 2) texto.Append((char)Convert.ToByte(hex.Substring(k, 2), 16));
                texto.Append(' ');
                i = fim;
            }
        }
    }

    [GeneratedRegex(@"<<((?:(?!>>).)*?)>>\s*stream\r?\n", RegexOptions.Singleline)]
    private static partial Regex Fluxo();

    [GeneratedRegex(@"(?:-?[\d.]+\s+){4}re\b")]
    private static partial Regex Retangulo();

    [GeneratedRegex(@"(?:-?[\d.]+\s+){2}l\b")]
    private static partial Regex Segmento();
}
