using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>POST /usuarios/{id}/novo-codigo-acesso (RF-154, RN-245): o código anterior e as sessões dele terminam.</summary>
public sealed class GerarNovoCodigoDeAcesso(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, TimeProvider relogio) : IGerarNovoCodigoDeAcesso
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<CodigoDeAcessoDto> Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        if (alvo.Tipo != TipoUsuario.Participante) throw FalhaDeUsuarios.SoParticipante();
        var agora = apoio.Agora;
        alvo.TrocarCodigo(await apoio.NovoCodigo(alvo.Sigla, alvo.NomeCurto, ct), agora);
        await usuarios.Atualizar(alvo, ct);
        await sessoes.EncerrarDoUsuario(alvo.Id, MotivoFim.NovoCodigo, ct);
        await apoio.Auditar(alvo.ContaId, quem.Id, AcaoAuditada.NovoCodigoAcesso, alvo.Id, ct);
        return new(alvo.Login, agora);
    }
}
