using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /time/tempo-sessao (RF-152, RN-243): só o time deboaz.</summary>
public sealed class LerTempoDoTime(IUsuarios usuarios, IContas contas, TimeProvider relogio) : ILerTempoDoTime
{
    readonly Apoio apoio = new(usuarios, contas, relogio);

    /// <inheritdoc />
    public async Task<TempoDoTimeLidoDto> Executar(QuemPedeDto pedido, CancellationToken ct = default)
    {
        await apoio.DoTime(pedido.QuemPede, ct);
        return TempoDoTimeLidoDto.De(await contas.TempoDoTime(ct) ?? TempoDoTime.Padrao);
    }
}
