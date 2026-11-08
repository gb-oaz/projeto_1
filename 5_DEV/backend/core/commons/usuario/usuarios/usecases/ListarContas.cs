using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /contas (RF-113, RN-185, RN-186): só para o time deboaz.</summary>
public sealed class ListarContas(IUsuarios usuarios, IContas contas, TimeProvider relogio) : IListarContas
{
    readonly Apoio apoio = new(usuarios, contas, relogio);

    /// <inheritdoc />
    public async Task<PaginaDto<ContaNaListaDto>> Executar(ListaDeContasDto pedido, CancellationToken ct = default)
    {
        await apoio.DoTime(pedido.QuemPede, ct);
        var lista = await contas.Listar(pedido.Validar(), ct);
        return new([.. lista.Itens.Select(ContaNaListaDto.De)], lista.Pagina, lista.Total);
    }
}
