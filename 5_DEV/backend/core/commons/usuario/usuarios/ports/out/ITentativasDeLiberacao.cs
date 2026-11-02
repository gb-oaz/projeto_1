using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>
/// Erros seguidos do código de liberação por entrada (tentativas:CodigoLiberacao:&lt;entrada&gt;,
/// RNF-204), no mesmo lugar das tentativas da autenticação.
/// </summary>
public interface ITentativasDeLiberacao
{
    /// <summary>As tentativas da entrada; null se não houver.</summary>
    Task<TentativasDeEntrada?> Buscar(string entrada, CancellationToken ct = default);

    /// <summary>Grava as tentativas, expirando em <see cref="TentativasDeEntrada.ExpiraEm"/>.</summary>
    Task Gravar(TentativasDeEntrada tentativas, CancellationToken ct = default);

    /// <summary>Esquece as tentativas: o código certo zera a contagem.</summary>
    Task Apagar(string entrada, CancellationToken ct = default);
}
