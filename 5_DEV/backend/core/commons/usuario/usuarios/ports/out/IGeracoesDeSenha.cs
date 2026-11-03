using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A coleção geracoesSenha (RN-181), com os resumos do código e do comprovante.</summary>
public interface IGeracoesDeSenha
{
    /// <summary>Grava uma geração nova; as anteriores do usuário ainda abertas deixam de valer.</summary>
    Task Inserir(GeracaoDeSenhaPedida geracao, CancellationToken ct = default);

    /// <summary>A geração mais recente do usuário; null se nenhuma.</summary>
    Task<GeracaoDeSenhaPedida?> UltimaDoUsuario(string usuarioId, CancellationToken ct = default);

    /// <summary>Grava a senha mostrada e o resumo do comprovante.</summary>
    Task Atualizar(GeracaoDeSenhaPedida geracao, CancellationToken ct = default);
}
