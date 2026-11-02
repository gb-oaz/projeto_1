using System.Security.Cryptography;
using System.Text;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Resumo irreversível da senha (RNF-201): PBKDF2 com SHA-256, sal aleatório de
/// 128 bits e o número de iterações gravado junto, no formato
/// <c>pbkdf2-sha256$&lt;iterações&gt;$&lt;sal&gt;$&lt;resumo&gt;</c> (base64). Guardar as
/// iterações deixa subir o custo depois sem invalidar os resumos antigos.
/// </summary>
public static class ResumoDeSenha
{
    const string Prefixo = "pbkdf2-sha256";

    /// <summary>Iterações padrão, a recomendação da OWASP para PBKDF2-SHA256.</summary>
    public const int IteracoesPadrao = 600_000;

    const int BytesDoSal = 16, BytesDoResumo = 32;

    /// <summary>Gera o resumo de uma senha, com um sal novo.</summary>
    /// <param name="senha">A senha em claro, que não é guardada.</param>
    /// <param name="iteracoes">Custo do PBKDF2; menos que o padrão só em testes.</param>
    public static string Gerar(string senha, int iteracoes = IteracoesPadrao)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesDoSal);
        var resumo = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, iteracoes, HashAlgorithmName.SHA256, BytesDoResumo);
        return $"{Prefixo}${iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(resumo)}";
    }

    /// <summary>
    /// Confere a senha com o resumo guardado, em tempo constante. Resumo ausente
    /// ou fora do formato nunca confere.
    /// </summary>
    public static bool Confere(string senha, string? guardado)
    {
        var partes = guardado?.Split('$');
        if (partes is not [Prefixo, var texto, var salB64, var resumoB64] || !int.TryParse(texto, out var iteracoes) || iteracoes < 1)
        {
            return false;
        }
        try
        {
            var esperado = Convert.FromBase64String(resumoB64);
            var resumo = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), Convert.FromBase64String(salB64),
                iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(resumo, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
