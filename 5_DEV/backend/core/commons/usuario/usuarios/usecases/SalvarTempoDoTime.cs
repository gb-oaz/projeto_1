using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// PUT /time/tempo-sessao (RF-152, RN-243): a conta master não muda o próprio
/// tempo. Auditado com o traceId recebido: "INICIADO" com quem, a conta dele e o
/// alvo "time", "CONCLUIDO" antes de responder e, na recusa ou na falha, o
/// registro de erro antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class SalvarTempoDoTime(IUsuarios usuarios, IContas contas, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : ISalvarTempoDoTime
{
    /// <summary>O alvo gravado na auditoria: os tempos do time, não uma conta.</summary>
    public const string Alvo = "time";

    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task<TempoDoTimeLidoDto> Executar(TempoDoTimeDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.SalvarTempoSessao, pedido.QuemPede, Alvo, ct);
        return await auditada.Auditar(execucao, () => Salvar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do tempo do time, de quem pede ao "CONCLUIDO".</summary>
    async Task<TempoDoTimeLidoDto> Salvar(TempoDoTimeDto pedido, Execucao execucao, CancellationToken ct)
    {
        await apoio.DoTime(pedido.QuemPede, ct);
        var tempo = pedido.Validar();
        await contas.SalvarTempoDoTime(tempo, ct);
        await auditada.Concluir(execucao, ct);
        return TempoDoTimeLidoDto.De(tempo);
    }
}
