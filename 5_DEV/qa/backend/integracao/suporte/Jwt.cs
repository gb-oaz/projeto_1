using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Boaz.Qa.Integracao.Suporte;

// Lê o token de acesso e, para os cenários de token recusado (RNF-216),
// assina um token com a chave do ambiente.
public static class Jwt
{
    public static JsonObject Cabecalho(string token) => Parte(token, 0);
    public static JsonObject Carga(string token) => Parte(token, 1);

    public static string Assinar(JsonObject cabecalho, JsonObject carga)
    {
        if (Ambiente.ChavePrivada.Length == 0) throw new InvalidOperationException("CHAVE_PRIVADA ausente: sem ela não há como assinar o token do cenário");
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(Ambiente.ChavePrivada), out _);
        var dados = $"{B64(Encoding.UTF8.GetBytes(cabecalho.ToJsonString()))}.{B64(Encoding.UTF8.GetBytes(carga.ToJsonString()))}";
        var assinatura = rsa.SignData(Encoding.ASCII.GetBytes(dados), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{dados}.{B64(assinatura)}";
    }

    public static bool Confere(string token, string n, string e)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = DeB64(n), Exponent = DeB64(e) });
        var partes = token.Split('.');
        return rsa.VerifyData(Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}"), DeB64(partes[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public static byte[] DeB64(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    static JsonObject Parte(string token, int i) =>
        JsonNode.Parse(Encoding.UTF8.GetString(DeB64(token.Split('.')[i])))!.AsObject();

    static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
