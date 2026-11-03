using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /contas: as contas assinantes, para o time deboaz (RF-113, RN-185, RN-186).</summary>
public interface IListarContas
{
    /// <summary>Lista as contas.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO ou DADOS_INVALIDOS.</exception>
    Task<PaginaDto<ContaNaListaDto>> Executar(ListaDeContasDto pedido, CancellationToken ct = default);
}
