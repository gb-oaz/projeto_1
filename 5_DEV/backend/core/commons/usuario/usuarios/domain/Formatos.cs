using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// Sigla da conta (RN-179): 5 letras de A a Z, única em qualquer caixa, gravada
/// em maiúsculas e sem edição depois de criada. DBOAZ é do time deboaz.
/// </summary>
public static partial class Sigla
{
    /// <summary>A sigla do time deboaz.</summary>
    public const string DoTime = "DBOAZ";

    /// <summary>Palavras que não dão letra à sigla nem à senha.</summary>
    internal static readonly IReadOnlySet<string> Conectores =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de", "da", "do", "das", "dos", "e" };

    /// <summary>Confere e devolve a sigla em maiúsculas.</summary>
    /// <exception cref="FalhaDeUsuarios">SIGLA_INVALIDA: sem 5 letras, ou DBOAZ.</exception>
    public static string Validar(string? sigla)
    {
        var texto = (sigla ?? "").Trim().ToUpperInvariant();
        if (!CincoLetras().IsMatch(texto)) throw FalhaDeUsuarios.SiglaComCincoLetras();
        if (texto == DoTime) throw FalhaDeUsuarios.SiglaReservada();
        return texto;
    }

    /// <summary>
    /// A sigla sugerida pelo nome: as iniciais das palavras, sem conectores nem
    /// acentos ("Escola Bento Freitas Gonçalves Araújo" → EBFGA). Com mais de 5
    /// palavras ficam as 5 primeiras; com menos, completa com as letras seguintes
    /// da última palavra. Null se o nome não tiver 5 letras.
    /// </summary>
    public static string? Sugerir(string? nome)
    {
        var palavras = Palavras(nome);
        if (palavras.Count == 0) return null;
        var sigla = new StringBuilder(string.Concat(palavras.Take(5).Select(p => p[0])));
        sigla.Append(palavras[^1].AsSpan(1, Math.Min(palavras[^1].Length - 1, Math.Max(0, 5 - sigla.Length))));
        return sigla.Length == 5 ? sigla.ToString().ToUpperInvariant() : null;
    }

    /// <summary>As palavras do nome, sem acentos, sem conectores e só com letras.</summary>
    internal static List<string> Palavras(string? nome) =>
        SemAcentos(nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => new string([.. p.Where(char.IsAsciiLetter)]))
            .Where(p => p.Length > 0 && !Conectores.Contains(p))
            .ToList();

    static string SemAcentos(string texto) =>
        new([.. texto.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);

    [GeneratedRegex("^[A-Z]{5}$")]
    private static partial Regex CincoLetras();
}

/// <summary>Documento da conta (RN-242): CNPJ na instituição, CPF no autônomo.</summary>
public static class Documento
{
    /// <summary>Só os números do documento.</summary>
    public static string Numeros(string? documento) => new([.. (documento ?? "").Where(char.IsAsciiDigit)]);

    /// <summary>O documento tem os números do tipo da conta: 14 no CNPJ, 11 no CPF.</summary>
    public static bool ServePara(TipoConta tipo, string? documento)
    {
        var numeros = Numeros(documento);
        var soNumerosEPontuacao = (documento ?? "").Trim().All(c => char.IsAsciiDigit(c) || c is '.' or '/' or '-');
        return soNumerosEPontuacao && numeros.Length == (tipo == TipoConta.Instituicao ? 14 : 11);
    }
}

/// <summary>
/// Entradas do hub e códigos de acesso (RN-180, RN-184). A entrada só
/// identifica a pessoa: nenhuma caixa de e-mail a recebe.
/// </summary>
public static partial class Logins
{
    /// <summary>O domínio das entradas.</summary>
    public const string Dominio = "deboaz.com";

    /// <summary>Confere o nome curto: letras minúsculas, números e sublinhado.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public static string NomeCurto(string? nomeCurto, string campo = "nomeCurto")
    {
        var texto = (nomeCurto ?? "").Trim().ToLowerInvariant();
        return Simples().IsMatch(texto) ? texto : throw FalhaDeUsuarios.DadosInvalidos(campo);
    }

    /// <summary>Entrada da conta master: o domínio escolhido (objetivo_unidade1@deboaz.com).</summary>
    public static string DaMaster(string dominio) => $"{dominio}@{Dominio}";

    /// <summary>Entrada do staff e do membro do time: nome curto e sigla (maria.ebfga@deboaz.com).</summary>
    public static string DoNomeCurto(string nomeCurto, string sigla) => $"{nomeCurto}.{sigla.ToLowerInvariant()}@{Dominio}";

    /// <summary>Código de acesso do participante, em maiúsculas: SIGLA-NOMECURTO-0412.</summary>
    public static string CodigoDeAcesso(string sigla, string nomeCurto) =>
        $"{sigla.ToUpperInvariant()}-{nomeCurto.ToUpperInvariant()}-{RandomNumberGenerator.GetInt32(10_000):D4}";

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex Simples();
}

/// <summary>Matrícula (RN-188): a da escola, ou 8 números gerados, única na conta.</summary>
public static class Matricula
{
    /// <summary>Uma matrícula de 8 números.</summary>
    public static string Gerar() => RandomNumberGenerator.GetInt32(100_000_000).ToString("D8", CultureInfo.InvariantCulture);
}

/// <summary>Código de liberação (RN-181, RN-182): 6 números, mostrado uma vez, vale 72 h.</summary>
public static class CodigoDeLiberacao
{
    /// <summary>Validade do código, contada do pedido.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(72);

    /// <summary>Um código de 6 números.</summary>
    public static string Gerar() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>Um comprovante aleatório, que confirma a senha anotada (RF-143).</summary>
    public static string Comprovante() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}

/// <summary>
/// Senha gerada (RN-171): as 2 primeiras letras do primeiro nome, um caractere
/// especial sorteado, o segundo nome (sem conectores) e 6 números sorteados —
/// Gustavo Boaz de Moura Souza → Gu#Boaz507318. Com um nome só, a segunda parte
/// são as 2 últimas letras dele (Xuxa → Xu@Xa182482).
/// </summary>
public static class Senha
{
    /// <summary>Os caracteres especiais sorteados.</summary>
    public const string Especiais = "!@#$%&*";

    /// <summary>Gera uma senha nova para o nome.</summary>
    public static string Gerar(string nome)
    {
        var palavras = Sigla.Palavras(nome);
        var primeira = palavras.Count > 0 ? palavras[0] : "Deboaz";
        var segunda = palavras.Count > 1 ? palavras[1] : primeira[Math.Max(0, primeira.Length - 2)..];
        var numeros = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        return Capital(primeira[..Math.Min(2, primeira.Length)]) + Especiais[RandomNumberGenerator.GetInt32(Especiais.Length)]
            + Capital(segunda) + numeros;
    }

    /// <summary>
    /// Força exigida pelo servidor (RNF-202): 8 caracteres ou mais, com
    /// maiúscula, caractere especial e número.
    /// </summary>
    public static bool Forte(string? senha) =>
        senha is { Length: >= 8 } && senha.Any(char.IsUpper) && senha.Any(char.IsDigit) && senha.Any(c => !char.IsLetterOrDigit(c));

    static string Capital(string palavra) =>
        palavra.Length == 0 ? palavra : char.ToUpperInvariant(palavra[0]) + palavra[1..].ToLowerInvariant();
}
