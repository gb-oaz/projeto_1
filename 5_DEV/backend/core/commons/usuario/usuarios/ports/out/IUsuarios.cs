using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A coleção usuarios: o cadastro de todos os tipos, inclusive o administrador do deboaz.</summary>
public interface IUsuarios
{
    /// <summary>O usuário pelo id; null se não existir.</summary>
    Task<UsuarioCadastrado?> Buscar(string id, CancellationToken ct = default);

    /// <summary>O usuário dono da entrada, já em minúsculas; null se não existir.</summary>
    Task<UsuarioCadastrado?> BuscarPorEntrada(string entrada, CancellationToken ct = default);

    /// <summary>A conta master da conta; null se a conta não existir.</summary>
    Task<UsuarioCadastrado?> MasterDaConta(string contaId, CancellationToken ct = default);

    /// <summary>O nome curto já é de outro usuário da conta (fora o de id <paramref name="exceto"/>).</summary>
    Task<bool> NomeCurtoEmUso(string contaId, string nomeCurto, string? exceto = null, CancellationToken ct = default);

    /// <summary>A matrícula já é de outro usuário da conta (fora o de id <paramref name="exceto"/>).</summary>
    Task<bool> MatriculaEmUso(string contaId, string matricula, string? exceto = null, CancellationToken ct = default);

    /// <summary>A entrada ou o código de acesso já é de alguém, em qualquer conta.</summary>
    Task<bool> LoginEmUso(string login, CancellationToken ct = default);

    /// <summary>Grava um usuário novo.</summary>
    Task Inserir(UsuarioCadastrado usuario, CancellationToken ct = default);

    /// <summary>Grava as mudanças de um usuário.</summary>
    Task Atualizar(UsuarioCadastrado usuario, CancellationToken ct = default);

    /// <summary>
    /// Garante um usuário inicial pelo id (RN-267): grava o cadastro só se ele
    /// não existir e, sempre, a entrada (ou o código), o resumo da senha e o
    /// estado. Devolve true se ele nasceu agora.
    /// </summary>
    Task<bool> Garantir(UsuarioCadastrado usuario, CancellationToken ct = default);

    /// <summary>Uma página da lista, de A a Z pelo nome, com o total (RN-185).</summary>
    Task<PaginaDe<UsuarioCadastrado>> Listar(FiltroDeUsuarios filtro, CancellationToken ct = default);
}
