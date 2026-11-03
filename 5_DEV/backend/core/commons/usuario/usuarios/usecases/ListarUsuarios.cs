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
public sealed class ListarUsuarios(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio) : IListarUsuarios
{
    static readonly TipoUsuario[] Listaveis = [TipoUsuario.Staff, TipoUsuario.Participante, TipoUsuario.MembroTime];

    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<PaginaDto<UsuarioNaListaDto>> Executar(ListaDeUsuariosDto pedido, CancellationToken ct = default)
    {
        var (tipo, busca, estado, pagina) = pedido.Validar();
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var conta = await apoio.ContaDe(quem, ct);
        var tipos = (tipo is { } escolhido ? new[] { escolhido } : Listaveis).Where(t => Hierarquia.PodeListar(quem, conta.DeAutonomo, t)).ToHashSet();
        if (tipos.Count == 0) throw FalhaDeUsuarios.AcessoNegado();
        var lista = await usuarios.Listar(new FiltroDeUsuarios(quem.ContaId, tipos, busca, estado, pagina), ct);
        return new([.. lista.Itens.Select(UsuarioNaListaDto.De)], lista.Pagina, lista.Total);
    }
}
