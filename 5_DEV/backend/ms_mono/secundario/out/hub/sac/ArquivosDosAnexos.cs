using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// Os arquivos dos anexos (IArquivos, sac-classes v04): grava no destino da
// configuração (Sac:Arquivos:Destino, BLOB ou GRIDFS; sem ela, GRIDFS) e lê,
// apaga e marca no armazenamento de cada anexo, sem migração (18 RNF-233). O
// anexo do Blob precisa da conta configurada para ser lido, mesmo com o
// destino de volta a GRIDFS; o GridFS existe sempre.
public sealed class ArquivosDosAnexos : IArquivos
{
    readonly ArquivosNoGridFs gridFs;
    readonly ArquivosNoBlob? blob;

    public ArquivosDosAnexos(ArquivosNoGridFs gridFs, ArquivosNoBlob? blob, ArmazenamentoDoAnexo destino)
    {
        if (destino == ArmazenamentoDoAnexo.Blob && blob is null)
            throw new InvalidOperationException("Sac:Arquivos:Destino BLOB pede Sac:Arquivos:Blob ou ConnectionStrings:Blob.");
        (this.gridFs, this.blob, Destino) = (gridFs, blob, destino);
    }

    public ArmazenamentoDoAnexo Destino { get; }

    public static ArmazenamentoDoAnexo DestinoDa(IConfiguration configuracao) =>
        TextosDoAnexo.ArmazenamentoDe(configuracao["Sac:Arquivos:Destino"]);

    public async Task<ArmazenamentoDoAnexo> Guardar(string objeto, byte[] conteudo, string tipo, CancellationToken ct = default)
    {
        if (Destino == ArmazenamentoDoAnexo.Blob) await blob!.Guardar(objeto, conteudo, tipo, ct);
        else await gridFs.Guardar(objeto, conteudo, ct);
        return Destino;
    }

    public Task<Stream?> Abrir(ArmazenamentoDoAnexo armazenamento, string objeto, CancellationToken ct = default) =>
        armazenamento == ArmazenamentoDoAnexo.Blob
            ? blob?.Abrir(objeto, ct) ?? Task.FromResult<Stream?>(null)
            : gridFs.Abrir(objeto, ct);

    public Task Apagar(ArmazenamentoDoAnexo armazenamento, string objeto, CancellationToken ct = default) =>
        armazenamento == ArmazenamentoDoAnexo.Blob
            ? blob?.Apagar(objeto, ct) ?? throw SemConta()
            : gridFs.Apagar(objeto, ct);

    // a tag só existe no armazenamento de objetos; no GridFS, só o expiraEm conta
    public Task MarcarUso(ArmazenamentoDoAnexo armazenamento, string objeto, UsoDoArquivo uso, CancellationToken ct = default) =>
        armazenamento == ArmazenamentoDoAnexo.Blob && blob is not null ? blob.MarcarUso(objeto, uso, ct) : Task.CompletedTask;

    // sem a conta, o arquivo do Blob não sai: o descarte tenta de novo no minuto seguinte
    static InvalidOperationException SemConta() =>
        new("O anexo está no armazenamento de objetos, mas Sac:Arquivos:Blob e ConnectionStrings:Blob não estão configurados.");
}
