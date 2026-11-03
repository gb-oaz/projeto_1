using System.Security.Cryptography;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Autenticacao;

// Assinatura RS256 do token de acesso (RNF-207) com a chave da configuração
// (seção Autenticacao, em Configuracoes.cs): ChaveId é o kid e ChavePrivada a
// chave em PKCS#8 base64 (no Azure, do Key Vault). Sem chave privada, uma chave
// nova é gerada a cada partida: serve à máquina de quem desenvolve, e os tokens
// deixam de valer quando o backend reinicia.
public sealed class AssinadorRsa : IAssinador, IDisposable
{
    readonly string kid;
    readonly RSA rsa = RSA.Create(2048);

    public AssinadorRsa(IOptions<OpcoesDaAutenticacao> opcoes)
    {
        kid = opcoes.Value.ChaveId;
        if (opcoes.Value.ChavePrivada is { Length: > 0 } chave)
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
