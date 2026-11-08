using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/{id}/novo-codigo-acesso (RF-154, RN-245): o código anterior e
/// as sessões dele terminam. Auditado com o traceId recebido: "INICIADO" com
/// quem, a conta e o alvo, "CONCLUIDO" antes de responder e, na recusa ou na
/// falha, o registro de erro antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class GerarNovoCodigoDeAcesso(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IGerarNovoCodigoDeAcesso
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task<CodigoDeAcessoDto> Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.NovoCodigoAcesso, pedido.QuemPede, pedido.UsuarioId, ct);
        return await auditada.Auditar(execucao, () => Gerar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do novo código, de quem pede ao "CONCLUIDO".</summary>
    async Task<CodigoDeAcessoDto> Gerar(PedidoDeUsuarioDto pedido, Execucao execucao, CancellationToken ct)
    {
        var alvo = await ParticipanteAdministravel(pedido, ct);
        var agora = apoio.Agora;
        await TrocarCodigo(alvo, agora, ct);
        await auditada.Concluir(execucao, ct);
        return new(alvo.Login, agora);
    }

    /// <summary>Um código livre no lugar do anterior, gravado, e as sessões do código antigo encerradas (RN-245).</summary>
    async Task TrocarCodigo(UsuarioCadastrado alvo, DateTimeOffset agora, CancellationToken ct)
    {
        alvo.TrocarCodigo(await apoio.NovoCodigo(alvo.Sigla, alvo.NomeCurto, ct), agora);
        await usuarios.Atualizar(alvo, ct);
        await sessoes.EncerrarDoUsuario(alvo.Id, MotivoFim.NovoCodigo, ct);
    }

    /// <summary>O alvo, se quem pede está acima dele e ele é participante (RN-169, RN-245).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, USUARIO_INEXISTENTE ou SO_PARTICIPANTE.</exception>
    async Task<UsuarioCadastrado> ParticipanteAdministravel(PedidoDeUsuarioDto pedido, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        return alvo.Tipo == TipoUsuario.Participante ? alvo : throw FalhaDeUsuarios.SoParticipante();
    }
}
