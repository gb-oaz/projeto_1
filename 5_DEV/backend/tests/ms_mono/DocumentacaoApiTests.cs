using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Boaz.MsMono.Tests;

// /doc/api existe só com Swagger:Habilitado (homologação), nunca em produção
public class DocumentacaoApiTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Cliente(bool habilitado) =>
        fabrica.WithWebHostBuilder(b => b.UseSetting("Swagger:Habilitado", habilitado.ToString()))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/doc/api/index.html", "text/html")]
    [InlineData("/doc/api/catalogo.json", "application/json")]
    [InlineData("/doc/api/v1.json", "application/json")]
    [InlineData("/doc/api/swagger.css", "text/css")]
    [InlineData("/doc/api/design/banner.svg", "image/svg+xml")]
    [InlineData("/doc/api/design/banner-parado.svg", "image/svg+xml")]
    public async Task Habilitada_publica_o_catalogo(string caminho, string tipo)
    {
        using var cliente = Cliente(true);

        var resposta = await cliente.GetAsync(caminho);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(tipo, resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Capa_apresenta_o_catalogo_sem_endpoints()
    {
        using var cliente = Cliente(true);

        var documento = await cliente.GetStringAsync("/doc/api/catalogo.json");

        Assert.Contains("deboaz · catálogo de APIs", documento);
        Assert.Contains("ms_mono · v1", documento);
        Assert.DoesNotContain("\"/health\"", documento);
    }

    [Fact]
    public async Task Seletor_abre_a_capa_e_depois_as_apis()
    {
        using var cliente = Cliente(true);

        var configuracao = await cliente.GetStringAsync("/doc/api/index.js");

        var capa = configuracao.IndexOf("/doc/api/catalogo.json", StringComparison.Ordinal);
        Assert.InRange(capa, 0, configuracao.IndexOf("/doc/api/v1.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MsMono_documenta_os_endpoints_de_sistema()
    {
        using var cliente = Cliente(true);

        var documento = await cliente.GetStringAsync("/doc/api/v1.json");

        Assert.Contains("\"/health\"", documento);
        Assert.Contains("\"Sistema\"", documento);
        Assert.Contains("deboaz · API ms_mono", documento);
        Assert.DoesNotContain("\"servers\"", documento);
    }

    [Theory]
    [InlineData("/doc/api/index.html")]
    [InlineData("/doc/api/v1.json")]
    [InlineData("/doc/api/design/banner.svg")]
    public async Task Desabilitada_nao_publica_nada(string caminho)
    {
        using var cliente = Cliente(false);

        var resposta = await cliente.GetAsync(caminho);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Habilitada_a_raiz_leva_ao_catalogo()
    {
        using var cliente = Cliente(true);

        var resposta = await cliente.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/doc/api", resposta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Desabilitada_a_raiz_nao_existe()
    {
        using var cliente = Cliente(false);

        var resposta = await cliente.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Health_continua_sem_a_documentacao()
    {
        using var cliente = Cliente(false);

        var resposta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
