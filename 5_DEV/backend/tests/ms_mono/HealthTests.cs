using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Boaz.MsMono.Tests;

public class HealthTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_responde_200()
    {
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
