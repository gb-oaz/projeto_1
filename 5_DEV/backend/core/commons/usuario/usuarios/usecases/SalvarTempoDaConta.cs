using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>PUT /contas/{id}/tempo-sessao (RF-114, RN-183): dentro dos limites.</summary>
public sealed class SalvarTempoDaConta(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio) : ISalvarTempoDaConta
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<TempoDaContaLidoDto> Executar(TempoDaContaDto pedido, CancellationToken ct = default)
    {
        var conta = await TempoDaPropriaConta.Conta(apoio, pedido.QuemPede, pedido.ContaId, ct);
        var tempo = pedido.Validar();
        await contas.SalvarTempoDaConta(conta.Id, tempo, ct);
        await apoio.Auditar(conta.Id, pedido.QuemPede, AcaoAuditada.SalvarTempoSessao, conta.Id, ct);
        return TempoDaContaLidoDto.De(tempo);
    }
}
