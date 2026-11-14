using System.Globalization;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Boaz.Core.Hub.Sac.Anexos.Domain;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// Os arquivos dos anexos no armazenamento de objetos (18 RNF-233, RNF-234; 05
// RNF-235, RNF-236): contêiner privado (anexos-sac), blob "sac/anexos/<id>"
// com o Content-Type do anexo, guardado como veio. A tag de índice "uso"
// (pendente, chamado, fechado) e o metadado "fechadoem" são o que a regra de
// ciclo de vida do Terraform lê; o descarte do AgendamentoDoSac apaga daqui
// também. A conta vem de ConnectionStrings:Blob (o Azurite local e do CI, e
// aí o backend cria o contêiner) ou de Sac:Arquivos:Blob, o endereço da conta
// na Azure, com a identidade da aplicação (quem cria o contêiner é o Terraform).
public sealed class ArquivosNoBlob(BlobContainerClient conteiner, bool criarConteiner, TimeProvider relogio)
{
    public const string ConteinerPadrao = "anexos-sac";
    public const string TagDeUso = "uso";
    public const string MetadadoDoFechamento = "fechadoem";

    readonly Lazy<Task> conteinerPronto = new(() => criarConteiner ? conteiner.CreateIfNotExistsAsync() : Task.CompletedTask);

    // null sem conta configurada: o armazenamento de objetos está desligado e
    // nenhum anexo foi guardado nele
    public static ArquivosNoBlob? Da(IConfiguration configuracao, TimeProvider relogio)
    {
        var nome = configuracao["Sac:Arquivos:Conteiner"] is { Length: > 0 } c ? c : ConteinerPadrao;
        if (configuracao.GetConnectionString("Blob") is { Length: > 0 } conexao)
            return new ArquivosNoBlob(new BlobContainerClient(conexao, nome), true, relogio);
        if (configuracao["Sac:Arquivos:Blob"] is { Length: > 0 } endereco)
        {
            var conta = new BlobServiceClient(new Uri(endereco), new DefaultAzureCredential());
            return new ArquivosNoBlob(conta.GetBlobContainerClient(nome), false, relogio);
        }
        return null;
    }

    // o contêiner, na partida (só quando a conexão é do Azurite)
    public Task Preparar() => conteinerPronto.Value;

    BlobClient Blob(string objeto) => conteiner.GetBlobClient(objeto);

    public async Task Guardar(string objeto, byte[] conteudo, string tipo, CancellationToken ct = default)
    {
        await conteinerPronto.Value;
        await Blob(objeto).UploadAsync(new BinaryData(conteudo), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = tipo },
            Tags = new Dictionary<string, string> { [TagDeUso] = UsoDoArquivo.Pendente.Texto() },
        }, ct);
    }

    public async Task<Stream?> Abrir(string objeto, CancellationToken ct = default)
    {
        try
        {
            var lido = await Blob(objeto).DownloadContentAsync(ct);
            return new MemoryStream(lido.Value.Content.ToArray(), writable: false);
        }
        catch (RequestFailedException falha) when (falha.Status == 404)
        {
            return null;
        }
    }

    public Task Apagar(string objeto, CancellationToken ct = default) =>
        Blob(objeto).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

    // no fechado, o metadado fechadoem vem antes (e renova o Last-Modified,
    // que a regra de produção conta); depois, a tag
    public async Task MarcarUso(string objeto, UsoDoArquivo uso, CancellationToken ct = default)
    {
        var blob = Blob(objeto);
        try
        {
            if (uso == UsoDoArquivo.Fechado)
            {
                var quando = relogio.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                await blob.SetMetadataAsync(new Dictionary<string, string> { [MetadadoDoFechamento] = quando }, cancellationToken: ct);
            }
            await blob.SetTagsAsync(new Dictionary<string, string> { [TagDeUso] = uso.Texto() }, cancellationToken: ct);
        }
        catch (RequestFailedException falha) when (falha.Status == 404)
        {
            // o arquivo já saiu (ciclo de vida ou descarte): nada a marcar
        }
    }
}
