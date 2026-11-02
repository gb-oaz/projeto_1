using System.Buffers.Text;
using System.Security.Cryptography;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Token de renovação de uso único (RNF-213): o id da sessão seguido de 256 bits
/// aleatórios, <c>&lt;sid&gt;.&lt;segredo&gt;</c>. A sessão guarda só o resumo
/// dele; cada renovação troca o token, e o anterior deixa de valer.
/// </summary>
public sealed record TokenDeRenovacao
{
    TokenDeRenovacao(string sid, string segredo) => (Sid, Segredo) = (sid, segredo);

    /// <summary>Id da sessão a que o token pertence.</summary>
    public string Sid { get; }

    /// <summary>Os 256 bits aleatórios, em base64url.</summary>
    public string Segredo { get; }

    /// <summary>Um token novo para a sessão.</summary>
    public static TokenDeRenovacao Novo(string sid) => new(sid, Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)));

    /// <summary>Lê o token como chega no cookie ou no corpo; null se não tiver o formato.</summary>
    public static TokenDeRenovacao? Ler(string? texto)
    {
        var partes = texto?.Split('.');
        return partes is [{ Length: > 0 } sid, { Length: > 0 } segredo] ? new(sid, segredo) : null;
    }

    /// <summary>O resumo irreversível que a sessão guarda.</summary>
    public string Resumo() => Credenciais.Resumo(ToString());

    /// <summary>O token como vai ao cliente: <c>&lt;sid&gt;.&lt;segredo&gt;</c>.</summary>
    public override string ToString() => $"{Sid}.{Segredo}";
}
