using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>POST /usuarios/{id}/reativar (RF-112): volta com as mesmas permissões.</summary>
public sealed class ReativarUsuario(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio) : IReativarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        alvo.Reativar();
        await usuarios.Atualizar(alvo, ct);
        await apoio.Auditar(alvo.ContaId, quem.Id, AcaoAuditada.ReativarUsuario, alvo.Id, ct);
    }
}
