using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>
/// A chave RSA de 2048 bits que assina os tokens, guardada no Key Vault (RNF-207):
/// a privada nunca sai de lá. Troca a cada 90 dias, e as anteriores continuam
/// aceitas até os tokens delas vencerem; numa troca por vazamento, a antiga sai
/// das aceitas na hora (RNF-218).
/// </summary>
public interface IAssinador
{
    /// <summary>Id da chave que assina agora (deboaz-2026-11).</summary>
    Task<string> KidAtual(CancellationToken ct = default);

    /// <summary>Assina os dados com RS256 usando a chave <paramref name="kid"/>.</summary>
    Task<byte[]> Assinar(string kid, byte[] dados, CancellationToken ct = default);

    /// <summary>As chaves públicas aceitas: a atual e as que ainda valem.</summary>
    Task<IReadOnlyList<ChavePublica>> ChavesAceitas(CancellationToken ct = default);
}
