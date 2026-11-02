using System.Security.Cryptography;
using System.Text;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Forma única das credenciais antes de procurar e de contar tentativas, e o
/// resumo irreversível dos segredos de alta entropia (comprovante, renovação).
/// </summary>
public static class Credenciais
{
    /// <summary>A entrada do hub não diferencia maiúsculas (RN-180): "Maria.EEJSA@deboaz.com" é "maria.eejsa@deboaz.com".</summary>
    public static string Entrada(string entrada) => entrada.Trim().ToLowerInvariant();

    /// <summary>O código de acesso vem em maiúsculas e aceita minúsculas (RN-184).</summary>
    public static string CodigoDeAcesso(string codigo) => codigo.Trim().ToUpperInvariant();

    /// <summary>
    /// SHA-256 em hexadecimal. Serve para segredos aleatórios de 256 bits (o
    /// comprovante e o token de renovação), que não precisam de sal nem de
    /// iterações; a senha usa <see cref="ResumoDeSenha"/>.
    /// </summary>
    public static string Resumo(string segredo) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(segredo)));
}
