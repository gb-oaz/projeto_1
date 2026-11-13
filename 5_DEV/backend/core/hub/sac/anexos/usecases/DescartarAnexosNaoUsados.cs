using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;

namespace Boaz.Core.Hub.Sac.Anexos.UseCases;

/// <summary>
/// O descarte (RN-292), pelo AgendamentoDoSac: o anexo não usado num chamado
/// ou mensagem em 24 h sai do armazenamento e do banco.
/// </summary>
public sealed class DescartarAnexosNaoUsados(IAnexos anexos, IArquivos arquivos, TimeProvider relogio) : IDescartarAnexosNaoUsados
{
    /// <inheritdoc />
    public async Task<int> Executar(CancellationToken ct = default)
    {
        var vencidos = await anexos.NaoUsadosAte(relogio.GetUtcNow(), ct);
        foreach (var anexo in vencidos)
        {
            await arquivos.Apagar(anexo.Objeto, ct);
            await anexos.Apagar(anexo.Id, ct);
        }
        return vencidos.Count;
    }
}
