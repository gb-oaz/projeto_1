using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// Os arquivos dos anexos (IArquivos) no GridFS do MongoDB do backend, balde
// "sacAnexos", com o caminho "sac/anexos/<id>" como nome. É o armazenamento
// enquanto o contêiner privado sac-anexos do armazenamento de objetos não
// existe no Terraform (sac-documentos v01): trocar é só outro adaptador da
// mesma porta, ligado no Program.cs.
public sealed class ArquivosNoGridFs(Armazenamento armazenamento) : IArquivos
{
    GridFSBucket Balde => new(armazenamento.Banco, new GridFSBucketOptions { BucketName = "sacAnexos" });

    public Task Guardar(string objeto, byte[] conteudo, CancellationToken ct = default) =>
        Balde.UploadFromBytesAsync(objeto, conteudo, cancellationToken: ct);

    public async Task<Stream?> Abrir(string objeto, CancellationToken ct = default)
    {
        try
        {
            return new MemoryStream(await Balde.DownloadAsBytesByNameAsync(objeto, cancellationToken: ct), writable: false);
        }
        catch (GridFSFileNotFoundException)
        {
            return null;
        }
    }

    public async Task Apagar(string objeto, CancellationToken ct = default)
    {
        var balde = Balde;
        var arquivos = await (await balde.FindAsync(Builders<GridFSFileInfo<ObjectId>>.Filter.Eq(a => a.Filename, objeto), cancellationToken: ct))
            .ToListAsync(ct);
        foreach (var arquivo in arquivos) await balde.DeleteAsync(arquivo.Id, ct);
    }
}
