using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /usuarios (RN-165, RN-185).</summary>
public sealed class ListarUsuarios(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio) : IListarUsuarios
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<PaginaDto<UsuarioNaListaDto>> Executar(ListaDeUsuariosDto pedido, CancellationToken ct = default)
    {
        var (tipo, busca, estado, pagina) = pedido.Validar();
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var conta = await apoio.ContaDe(quem, ct);
        if (!Hierarquia.PodeListar(quem, conta.DeAutonomo, tipo)) throw FalhaDeUsuarios.AcessoNegado();
        var lista = await usuarios.Listar(new FiltroDeUsuarios(quem.ContaId, tipo, busca, estado, pagina), ct);
        return new([.. lista.Itens.Select(UsuarioNaListaDto.De)], lista.Pagina, lista.Total);
    }
}
