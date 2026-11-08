using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// GET /usuarios (RN-165, RN-185). Sem tipo, a busca percorre todos os tipos
/// que quem pede pode listar; nenhum, ACESSO_NEGADO.
/// </summary>
public sealed class ListarUsuarios(IUsuarios usuarios, IContas contas, TimeProvider relogio) : IListarUsuarios
{
    static readonly TipoUsuario[] Listaveis = [TipoUsuario.Staff, TipoUsuario.Participante, TipoUsuario.MembroTime];

    readonly Apoio apoio = new(usuarios, contas, relogio);

    /// <inheritdoc />
    public async Task<PaginaDto<UsuarioNaListaDto>> Executar(ListaDeUsuariosDto pedido, CancellationToken ct = default)
    {
        var (tipo, busca, estado, pagina) = pedido.Validar();
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var tipos = await TiposQuePodeListar(quem, tipo, ct);
        var lista = await usuarios.Listar(new FiltroDeUsuarios(quem.ContaId, tipos, busca, estado, pagina), ct);
        return new([.. lista.Itens.Select(UsuarioNaListaDto.De)], lista.Pagina, lista.Total);
    }

    /// <summary>O tipo pedido, ou todos os listáveis, que quem pede pode listar (RN-165).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, se não sobra nenhum.</exception>
    async Task<HashSet<TipoUsuario>> TiposQuePodeListar(UsuarioCadastrado quem, TipoUsuario? tipo, CancellationToken ct)
    {
        var conta = await apoio.ContaDe(quem, ct);
        var tipos = (tipo is { } escolhido ? new[] { escolhido } : Listaveis).Where(t => Hierarquia.PodeListar(quem, conta.DeAutonomo, t)).ToHashSet();
        return tipos.Count == 0 ? throw FalhaDeUsuarios.AcessoNegado() : tipos;
    }
}
