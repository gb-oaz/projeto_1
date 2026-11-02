using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>POST /usuarios/{id}/desativar (RF-112, RN-174): continua na lista, sem entrar.</summary>
public sealed class DesativarUsuario(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, TimeProvider relogio) : IDesativarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        alvo.Desativar();
        await usuarios.Atualizar(alvo, ct);
        await sessoes.EncerrarDoUsuario(alvo.Id, MotivoFim.UsuarioDesativado, ct);
        await apoio.Auditar(alvo.ContaId, quem.Id, AcaoAuditada.DesativarUsuario, alvo.Id, ct);
    }
}
