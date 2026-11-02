using System.Security.Cryptography;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Autenticacao;

// Assinatura RS256 do token de acesso (RNF-207) com a chave privada da
// configuração: Autenticacao:ChaveId (o kid) e Autenticacao:ChavePrivada (PKCS#8
// em base64; no Azure vem do Key Vault). Sem chave configurada, uma chave nova
// é gerada a cada partida: serve à máquina de quem desenvolve, e os tokens
// deixam de valer quando o backend reinicia.
public sealed class AssinadorRsa : IAssinador, IDisposable
{
    public const string KidLocal = "deboaz-local";

    readonly string kid;
    readonly RSA rsa = RSA.Create(2048);

    public AssinadorRsa(IConfiguration configuracao)
    {
        kid = configuracao["Autenticacao:ChaveId"] is { Length: > 0 } k ? k : KidLocal;
        if (configuracao["Autenticacao:ChavePrivada"] is { Length: > 0 } chave)
        {
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(chave), out _);
        }
    }

    public Task<string> KidAtual(CancellationToken ct = default) => Task.FromResult(kid);

    public Task<byte[]> Assinar(string kid, byte[] dados, CancellationToken ct = default) =>
        kid == this.kid
            ? Task.FromResult(rsa.SignData(dados, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            : throw new InvalidOperationException($"Chave {kid} desconhecida.");

    public Task<IReadOnlyList<ChavePublica>> ChavesAceitas(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChavePublica>>([ChavePublica.De(kid, rsa)]);

    public void Dispose() => rsa.Dispose();
}
