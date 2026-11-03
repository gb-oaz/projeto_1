using System.Buffers.Text;
using System.Security.Cryptography;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// A parte pública de uma chave de assinatura RSA (RNF-207), como sai em GET
/// /auth/chaves. A privada nunca sai do Key Vault; aqui só se confere.
/// </summary>
/// <param name="Kid">Id da chave, que vai no cabeçalho do token (deboaz-2026-11).</param>
/// <param name="N">Módulo RSA em base64url.</param>
/// <param name="E">Expoente RSA em base64url (AQAB).</param>
public sealed record ChavePublica(string Kid, string N, string E)
{
    /// <summary>Confere uma assinatura RS256 (RSASSA-PKCS1-v1_5 com SHA-256).</summary>
    public bool Confere(byte[] dados, byte[] assinatura)
    {
        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(N),
            Exponent = Base64Url.DecodeFromChars(E),
        });
        try
        {
            return rsa.VerifyData(dados, assinatura, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>A parte pública de uma chave RSA, para quem guarda a privada.</summary>
    public static ChavePublica De(string kid, RSA rsa)
    {
        var parametros = rsa.ExportParameters(includePrivateParameters: false);
        return new(kid, Base64Url.EncodeToString(parametros.Modulus), Base64Url.EncodeToString(parametros.Exponent));
    }
}
