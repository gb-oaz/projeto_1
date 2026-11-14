using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Ports.In;
using Boaz.Core.Hub.Sac.Configuracao.Ports.Out;

namespace Boaz.Core.Hub.Sac.Configuracao.UseCases;

/// <summary>GET /sac/configuracao (RF-181): só o time; a conta recebe 403.</summary>
public sealed class LerPrazos(IConfiguracaoDoSac configuracao) : ILerPrazos
{
    /// <inheritdoc />
    public async Task<PrazosDto> Executar(QuemPede quem, CancellationToken ct = default)
    {
        quem.SoTime();
        return PrazosDto.De(await configuracao.Ler(ct));
    }
}
