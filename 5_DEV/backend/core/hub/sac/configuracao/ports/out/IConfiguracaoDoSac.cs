using Boaz.Core.Hub.Sac.Configuracao.Domain;

namespace Boaz.Core.Hub.Sac.Configuracao.Ports.Out;

/// <summary>A coleção configuracaoSac: um documento, "prazos".</summary>
public interface IConfiguracaoDoSac
{
    /// <summary>Os prazos guardados; o padrão da RN-289 se nunca foram salvos.</summary>
    Task<PrazosDoSac> Ler(CancellationToken ct = default);

    /// <summary>Guarda os prazos.</summary>
    Task Salvar(PrazosDoSac prazos, CancellationToken ct = default);
}
