using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A coleção cardUsuarios (RN-280, 15 RN-278).</summary>
public interface IRepositorioCardUsuarios
{
    /// <summary>O card pelo _id; null se ainda não foi contado.</summary>
    Task<CardUsuarios?> Obter(string id, CancellationToken ct = default);

    /// <summary>Grava o card inteiro pelo _id ($set), criando-o se faltar.</summary>
    Task Salvar(CardUsuarios card, CancellationToken ct = default);

    /// <summary>Só a versao do card, a cada intervalo do ao vivo; null se não existe.</summary>
    Task<int?> Versao(string id, CancellationToken ct = default);

    /// <summary>Todos os cards, para o fechamento.</summary>
    Task<IReadOnlyList<CardUsuarios>> Todos(CancellationToken ct = default);
}
