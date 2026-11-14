using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// PUT /contas/{id}/tempo-sessao (RF-114, RN-183): dentro dos limites. Auditado
/// com o traceId recebido: "INICIADO" com quem e a conta pedida (contaId e
/// alvo), "CONCLUIDO" antes de responder e, na recusa (como FORA_DOS_LIMITES)
/// ou na falha, o registro de erro antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class SalvarTempoDaConta(IUsuarios usuarios, IContas contas, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : ISalvarTempoDaConta
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<TempoDaContaLidoDto> Executar(TempoDaContaDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.SalvarTempoSessao, pedido.ContaId, pedido.QuemPede, pedido.ContaId);
        return auditada.Auditar(execucao, () => Salvar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do tempo da conta, de quem pede ao "CONCLUIDO".</summary>
    async Task<TempoDaContaLidoDto> Salvar(TempoDaContaDto pedido, Execucao execucao, CancellationToken ct)
    {
        var conta = await TempoDaPropriaConta.Conta(apoio, pedido.QuemPede, pedido.ContaId, ct);
        var tempo = pedido.Validar();
        await contas.SalvarTempoDaConta(conta.Id, tempo, ct);
        await auditada.Concluir(execucao, ct);
        return TempoDaContaLidoDto.De(tempo);
    }
}
