using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>Erros seguidos e bloqueios (Redis: tentativas:&lt;alvo&gt;:&lt;chave&gt;, RNF-204).</summary>
public interface ITentativas
{
    /// <summary>As tentativas guardadas; null se não houver.</summary>
    Task<TentativasDeEntrada?> Buscar(AlvoTentativa alvo, string chave, CancellationToken ct = default);

    /// <summary>Grava as tentativas, expirando em <see cref="TentativasDeEntrada.ExpiraEm"/>.</summary>
    Task Gravar(TentativasDeEntrada tentativas, CancellationToken ct = default);

    /// <summary>Esquece as tentativas: a credencial certa zera a contagem.</summary>
    Task Apagar(AlvoTentativa alvo, string chave, CancellationToken ct = default);
}
