using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/{id}/nova-geracao-senha (RF-110, RN-190, RNF-203): a senha
/// antiga deixa de valer e as sessões abertas terminam.
/// </summary>
public sealed class PedirNovaGeracaoDeSenha(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, ISessoesDosUsuarios sessoes, IAuditoria auditoria,
    TimeProvider relogio) : IPedirNovaGeracaoDeSenha
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<CodigoDeLiberacaoDto> Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        if (!alvo.TemSenha) throw FalhaDeUsuarios.SemSenha();
        var (geracao, codigo) = GeracaoDeSenhaPedida.Pedir(alvo, quem.Id, apoio.Agora);
        alvo.EsquecerSenha();
        await usuarios.Atualizar(alvo, ct);
        await geracoes.Inserir(geracao, ct);
        await sessoes.EncerrarDoUsuario(alvo.Id, MotivoFim.NovaSenha, ct);
        await apoio.Auditar(alvo.ContaId, quem.Id, AcaoAuditada.NovaGeracaoSenha, alvo.Id, ct);
        return new(codigo, geracao.VenceEm);
    }
}
