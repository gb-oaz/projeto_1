using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas/{id}/estado (RF-113, RN-173 a RN-175): fora de ATIVA, as sessões
/// da conta terminam; ENCERRADA apaga os dados e não volta. Auditado com o
/// traceId recebido: "INICIADO" com quem e a conta pedida (contaId e alvo),
/// "CONCLUIDO" antes de responder e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class MudarEstadoDaConta(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IMudarEstadoDaConta
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<ContaComEstadoDto> Executar(EstadoDaContaDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.MudarEstadoConta, pedido.ContaId, pedido.QuemPede, pedido.ContaId);
        return auditada.Auditar(execucao, () => Mudar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do estado da conta, de quem pede ao "CONCLUIDO".</summary>
    async Task<ContaComEstadoDto> Mudar(EstadoDaContaDto pedido, Execucao execucao, CancellationToken ct)
    {
        await apoio.DoTime(pedido.QuemPede, ct);
        var novo = pedido.Validar();
        var conta = await apoio.Assinante(pedido.ContaId, ct);
        if (conta.MudarEstado(novo)) await Gravar(conta, novo, ct);
        await auditada.Concluir(execucao, ct);
        return new(conta.Id, conta.Estado.Texto());
    }

    /// <summary>Grava o estado novo; fora de ATIVA as sessões terminam e ENCERRADA apaga os dados (RN-173 a RN-175).</summary>
    async Task Gravar(ContaAssinante conta, EstadoConta novo, CancellationToken ct)
    {
        await contas.Atualizar(conta, ct);
        if (novo != EstadoConta.Ativa) await sessoes.EncerrarDaConta(conta.Id, MotivoFim.ContaIndisponivel, ct);
        if (novo == EstadoConta.Encerrada) await contas.Encerrar(conta.Id, apoio.Agora, ct);
    }
}
