using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>PUT /time/tempo-sessao (RF-152, RN-243): a conta master não muda o próprio tempo.</summary>
public sealed class SalvarTempoDoTime(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio) : ISalvarTempoDoTime
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<TempoDoTimeLidoDto> Executar(TempoDoTimeDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.DoTime(pedido.QuemPede, ct);
        var tempo = pedido.Validar();
        await contas.SalvarTempoDoTime(tempo, ct);
        await apoio.Auditar(quem.ContaId, quem.Id, AcaoAuditada.SalvarTempoSessao, "time", ct);
        return TempoDoTimeLidoDto.De(tempo);
    }
}
