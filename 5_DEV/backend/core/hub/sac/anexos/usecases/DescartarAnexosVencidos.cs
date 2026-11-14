using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;

namespace Boaz.Core.Hub.Sac.Anexos.UseCases;

/// <summary>
/// O descarte (RN-292, RNF-234), pelo AgendamentoDoSac: o anexo de expiraEm
/// vencido perde o arquivo de onde foi guardado (armazenamento de objetos ou
/// GridFS). O não usado sai também do banco; o usado fica, com removidoEm, e o
/// chamado continua citando-o.
/// </summary>
public sealed class DescartarAnexosVencidos(IAnexos anexos, IArquivos arquivos, TimeProvider relogio) : IDescartarAnexosVencidos
{
    /// <inheritdoc />
    public async Task<int> Executar(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var vencidos = await anexos.VencidosAte(agora, ct);
        foreach (var anexo in vencidos)
        {
            await arquivos.Apagar(anexo.Armazenamento, anexo.Objeto, ct);
            if (anexo.ChamadoId is null) await anexos.Apagar(anexo.Id, ct);
            else await anexos.MarcarRemovido(anexo.Id, agora, ct);
        }
        return vencidos.Count;
    }
}
