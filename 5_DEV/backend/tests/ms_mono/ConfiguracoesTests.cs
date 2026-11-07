using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Tests;

// Roda sozinha, sem outra classe em paralelo: com o Program de nível superior, o
// WebApplicationFactory acha o host por um ouvinte de diagnóstico global, e duas
// partidas ao mesmo tempo podem trocar de host entre si (a configuração inválida
// pegaria o host válido de outra classe e subiria).
[CollectionDefinition(nameof(PartidaSozinha), DisableParallelization = true)]
public sealed class PartidaSozinha;

// Os parâmetros vêm da configuração (appsettings.json ou variável de ambiente),
// sem padrão no código: o que faltar para a partida dizendo qual variável definir.
[Collection(nameof(PartidaSozinha))]
public class ConfiguracoesTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("Mongo:Banco", "", "Mongo__Banco")]
    [InlineData("Time:Nome", "", "Time__Nome")]
    [InlineData("Autenticacao:ChaveId", "", "Autenticacao__ChaveId")]
    public void Sem_o_parametro_o_backend_nao_sobe(string chave, string valor, string variavel)
    {
        using var semParametro = fabrica.WithWebHostBuilder(b => b.UseSetting(chave, valor));

        var falha = Assert.Throws<OptionsValidationException>(() => semParametro.CreateClient());

        Assert.Contains(variavel, falha.Message);
    }

    // o OpenSSL 3 grava "genpkey -outform DER" em PKCS#1: o backend recusa na
    // partida, dizendo a variável, em vez de responder 500 a cada token
    [Theory]
    [InlineData("PKCS1")]
    [InlineData("nao-e-base64")]
    public void Chave_privada_fora_do_PKCS8_nao_sobe(string formato)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var chave = formato == "PKCS1" ? Convert.ToBase64String(rsa.ExportRSAPrivateKey()) : formato;
        using var comChave = fabrica.WithWebHostBuilder(b => b.UseSetting("Autenticacao:ChavePrivada", chave));

        var falha = Assert.Throws<OptionsValidationException>(() => comChave.CreateClient());

        Assert.Contains("Autenticacao__ChavePrivada", falha.Message);
    }

    [Fact]
    public void Chave_privada_em_PKCS8_sobe()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        using var comChave = fabrica.WithWebHostBuilder(b =>
            b.UseSetting("Autenticacao:ChavePrivada", Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())));

        using var cliente = comChave.CreateClient();

        Assert.NotNull(cliente);
    }

    [Fact]
    public void Com_os_padroes_do_appsettings_o_backend_sobe()
    {
        using var cliente = fabrica.CreateClient();

        Assert.NotNull(cliente);
    }
}
