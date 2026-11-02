using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas/{id}/estado (RF-113, RN-173 a RN-175): fora de ATIVA, as sessões
/// da conta terminam; ENCERRADA apaga os dados e não volta.
/// </summary>
public sealed class MudarEstadoDaConta(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, TimeProvider relogio) : IMudarEstadoDaConta
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<ContaComEstadoDto> Executar(EstadoDaContaDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.DoTime(pedido.QuemPede, ct);
        var novo = pedido.Validar();
        var conta = await apoio.Assinante(pedido.ContaId, ct);
        if (conta.MudarEstado(novo))
        {
            await contas.Atualizar(conta, ct);
            if (novo != EstadoConta.Ativa) await sessoes.EncerrarDaConta(conta.Id, MotivoFim.ContaIndisponivel, ct);
            if (novo == EstadoConta.Encerrada) await contas.Encerrar(conta.Id, apoio.Agora, ct);
            await apoio.Auditar(conta.Id, quem.Id, AcaoAuditada.MudarEstadoConta, conta.Id, ct);
        }
        return new(conta.Id, conta.Estado.Texto());
    }
}
