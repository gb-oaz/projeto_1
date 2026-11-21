using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Boaz.MsMono.Secundario.Out.Hub.Sac;

namespace Boaz.MsMono.Tests;

// A sonda do armazenamento de anexos (20 RN-315): contêiner que ninguém usou ainda não é serviço com erro.
public class SondaDoArmazenamentoTests
{
    sealed class ConteinerFalso(bool existe) : BlobContainerClient
    {
        public bool Existe { get; private set; } = existe;

        public int Criacoes { get; private set; }

        public override Task<Response<bool>> ExistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Response.FromValue(Existe, null!));

        public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string>? metadata = null, BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null,
            CancellationToken cancellationToken = default)
        {
            Criacoes++;
            Existe = true;
            return Task.FromResult<Response<BlobContainerInfo>>(null!);
        }
    }

    [Fact]
    public async Task RN_315_no_azurite_o_conteiner_que_ninguem_usou_e_criado_pela_sonda()
    {
        var conteiner = new ConteinerFalso(existe: false);

        await new ArquivosNoBlob(conteiner, criarConteiner: true, TimeProvider.System).Verificar();

        Assert.Equal(1, conteiner.Criacoes);
        Assert.True(conteiner.Existe);
    }

    [Fact]
    public async Task RN_315_na_azure_o_conteiner_que_falta_continua_sendo_falha()
    {
        var conteiner = new ConteinerFalso(existe: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ArquivosNoBlob(conteiner, criarConteiner: false, TimeProvider.System).Verificar());

        Assert.Equal(0, conteiner.Criacoes);
    }
}
