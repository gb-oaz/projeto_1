using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/{id}/nova-geracao-senha (RF-110, RN-190, RNF-203): a senha
/// antiga deixa de valer e as sessões abertas terminam. Auditado com o traceId
/// recebido: "INICIADO" com quem, a conta e o alvo ao chegar, "CONCLUIDO"
/// antes de responder e, na recusa ou na falha, o registro de erro antes de a
/// exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class PedirNovaGeracaoDeSenha(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, ISessoesDosUsuarios sessoes, IAuditoria auditoria,
    IAuditoriaDeErros erros, TimeProvider relogio) : IPedirNovaGeracaoDeSenha
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task<CodigoDeLiberacaoDto> Executar(PedidoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.NovaGeracaoSenha, pedido.QuemPede, pedido.UsuarioId, ct);
        return await auditada.Auditar(execucao, () => Pedir(pedido, execucao, ct), ct);
    }

    /// <summary>A regra da nova geração, de quem pede ao "CONCLUIDO".</summary>
    async Task<CodigoDeLiberacaoDto> Pedir(PedidoDeUsuarioDto pedido, Execucao execucao, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await AlvoComSenha(quem, pedido.UsuarioId, ct);
        var (geracao, codigo) = GeracaoDeSenhaPedida.Pedir(alvo, quem.Id, apoio.Agora);
        await Gravar(alvo, geracao, ct);
        await auditada.Concluir(execucao, ct);
        return new(codigo, geracao.VenceEm);
    }

    /// <summary>O alvo, se quem pede está acima dele e ele já tem senha (RN-169, RN-190).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, USUARIO_INEXISTENTE ou SEM_SENHA.</exception>
    async Task<UsuarioCadastrado> AlvoComSenha(UsuarioCadastrado quem, string alvoId, CancellationToken ct)
    {
        var alvo = await apoio.Administravel(quem, alvoId, ct);
        return alvo.TemSenha ? alvo : throw FalhaDeUsuarios.SemSenha();
    }

    /// <summary>Esquece a senha antiga, grava a geração nova e encerra as sessões do alvo (RNF-203).</summary>
    async Task Gravar(UsuarioCadastrado alvo, GeracaoDeSenhaPedida geracao, CancellationToken ct)
    {
        alvo.EsquecerSenha();
        await usuarios.Atualizar(alvo, ct);
        await geracoes.Inserir(geracao, ct);
        await sessoes.EncerrarDoUsuario(alvo.Id, MotivoFim.NovaSenha, ct);
    }
}
