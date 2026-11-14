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
    [InlineData("/doc/api/sistema.json", "application/json")]
    [InlineData("/doc/api/autenticacao.json", "application/json")]
    [InlineData("/doc/api/usuarios.json", "application/json")]
    [InlineData("/doc/api/autorizacao.json", "application/json")]
    [InlineData("/doc/api/diagramas/usuarios-contrato-api.svg", "image/svg+xml")]
    // SAC (18_modulo-sac v05, sac-contrato-api v02): API própria, como o Usuários
    [InlineData("/doc/api/sac.json", "application/json")]
    [InlineData("/doc/api/diagramas/sac-contrato-api.svg", "image/svg+xml")]
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
        Assert.Contains("Autenticação", documento);
        Assert.DoesNotContain("\"/health\"", documento);
    }

    [Fact]
    public async Task Seletor_abre_a_capa_e_depois_as_apis()
    {
        using var cliente = Cliente(true);

        var configuracao = await cliente.GetStringAsync("/doc/api/index.js");

        var ordem = new[] { "catalogo", "sistema", "autenticacao", "usuarios", "autorizacao" }
            .Select(d => configuracao.IndexOf($"/doc/api/{d}.json", StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, ordem);
        Assert.Equal(ordem.Order(), ordem);
    }

    // SAC (MOD-02): entra no seletor depois da Autorização, como os módulos já construídos
    [Fact]
    public async Task Seletor_traz_o_sac_depois_da_autorizacao()
    {
        using var cliente = Cliente(true);

        var configuracao = await cliente.GetStringAsync("/doc/api/index.js");

        var autorizacao = configuracao.IndexOf("/doc/api/autorizacao.json", StringComparison.Ordinal);
        var sac = configuracao.IndexOf("/doc/api/sac.json", StringComparison.Ordinal);
        Assert.True(sac > autorizacao && autorizacao >= 0, "o seletor não traz /doc/api/sac.json depois de /doc/api/autorizacao.json");
    }

    // a capa lista o SAC na tabela das APIs do catálogo, como lista o Usuários
    [Fact]
    public async Task Capa_lista_o_sac()
    {
        using var cliente = Cliente(true);

        var documento = await cliente.GetStringAsync("/doc/api/catalogo.json");

        Assert.Contains("**SAC**", documento);
        Assert.Contains("`/sac/", documento);
    }

    [Fact]
    public async Task Sistema_documenta_so_o_health()
    {
        using var cliente = Cliente(true);

        var documento = await cliente.GetStringAsync("/doc/api/sistema.json");

        Assert.Contains("\"/health\"", documento);
        Assert.Contains("\"Sistema\"", documento);
        Assert.Contains("deboaz · Sistema", documento);
        Assert.DoesNotContain("\"servers\"", documento);
        Assert.DoesNotContain("\"/auth/", documento);
    }

    // cada módulo é uma API própria: só os endereços dele, com o guia e os diagramas
    [Theory]
    [InlineData("autenticacao", "/auth/hub/entrar", "/usuarios/{id}/permissoes", "autenticacao-sequencia.svg")]
    [InlineData("usuarios", "/contas", "/auth/hub/entrar", "usuarios-contrato-api.svg")]
    [InlineData("autorizacao", "/usuarios/{id}/permissoes", "/contas", "autorizacao-casos-de-uso.svg")]
    [InlineData("sac", "/sac/chamados", "/contas", "sac-contrato-api.svg")]
    public async Task Cada_modulo_e_uma_api_com_guia_e_diagramas(string api, string tem, string naoTem, string diagrama)
    {
        using var cliente = Cliente(true);

        var documento = System.Text.Json.Nodes.JsonNode.Parse(await cliente.GetStringAsync($"/doc/api/{api}.json"))!;
        var caminhos = documento["paths"]!.AsObject().Select(p => p.Key).ToList();
        var guia = documento["info"]!["description"]!.GetValue<string>();

        Assert.Contains(tem, caminhos);
        Assert.DoesNotContain(naoTem, caminhos);
        Assert.Contains("## Como usar", guia);
        Assert.Contains($"/doc/api/diagramas/{diagrama}", guia);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/doc/api/diagramas/{diagrama}")).StatusCode);
    }

    // cada guia mostra todos os diagramas do módulo em 2_Diagramas/ (menos as vistas),
    // agrupados por tipo e na ordem: casos de uso, sequência, contrato, classes, documentos
    [Theory]
    [InlineData("autenticacao", "autenticacao-casos-de-uso", "autenticacao-sequencia", "autenticacao-contrato-api", "autenticacao-classes", "autenticacao-documentos")]
    [InlineData("usuarios", "identidade-acesso", "usuarios-sequencia", "usuario-inicializacao-sequencia", "usuarios-contrato-api", "identidade-acesso-classes", "usuario-hexagonal", "identidade-acesso-documentos")]
    [InlineData("autorizacao", "autorizacao-casos-de-uso", "autorizacao-sequencia", "autorizacao-contrato-api", "autorizacao-classes", "autorizacao-documentos")]
    // os 7 diagramas da seção Diagramas de 18_modulo-sac v05, na ordem do usuarios
    [InlineData("sac", "sac-casos-de-uso", "sac-sequencia", "sac-inicializacao-sequencia", "sac-contrato-api", "sac-classes", "sac-hexagonal", "sac-documentos")]
    public async Task Guia_mostra_todos_os_diagramas_do_modulo_por_tipo(string api, params string[] legendas)
    {
        using var cliente = Cliente(true);

        var documento = System.Text.Json.Nodes.JsonNode.Parse(await cliente.GetStringAsync($"/doc/api/{api}.json"))!;
        var guia = documento["info"]!["description"]!.GetValue<string>();
        var citados = System.Text.RegularExpressions.Regex.Matches(guia, @"\]\(/doc/api/diagramas/([a-z0-9-]+)\.svg\)")
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.Equal(legendas, citados);
        Assert.Contains("### Casos de uso", guia);
        Assert.Contains("### Sequência", guia);
        Assert.Contains("### Contrato de API", guia);
        Assert.Contains("### Modelo de documentos", guia);
        foreach (var legenda in legendas)
        {
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/doc/api/diagramas/{legenda}.svg")).StatusCode);
        }
    }

    // Authorize: o cadeado do Bearer só nos endereços com login
    [Theory]
    [InlineData("autenticacao", "/auth/sessoes", "get", true)]
    [InlineData("autenticacao", "/auth/hub/entrar", "post", false)]
    [InlineData("usuarios", "/usuarios", "get", true)]
    [InlineData("usuarios", "/contas", "post", false)]
    [InlineData("autorizacao", "/usuarios/{id}/permissoes", "put", true)]
    [InlineData("sac", "/sac/chamados", "post", true)]
    [InlineData("sac", "/sac/configuracao", "put", true)]
    public async Task Cadeado_so_nos_enderecos_com_login(string api, string caminho, string metodo, bool comLogin)
    {
        using var cliente = Cliente(true);

        var documento = System.Text.Json.Nodes.JsonNode.Parse(await cliente.GetStringAsync($"/doc/api/{api}.json"))!;

        Assert.Equal("bearer", documento["components"]!["securitySchemes"]!["Bearer"]!["scheme"]!.GetValue<string>());
        Assert.Equal(comLogin, documento["paths"]![caminho]![metodo]!["security"] is not null);
    }

    // os exemplos do contrato (sistema/exemplos/, de scripts/exemplos-da-doc.py)
    // ficam dentro de cada endpoint: todo endpoint tem o seu, e nenhum sobra
    [Theory]
    [InlineData("autenticacao")]
    [InlineData("usuarios")]
    [InlineData("autorizacao")]
    [InlineData("sac")]
    public async Task Todo_endpoint_traz_os_exemplos_do_contrato(string api)
    {
        using var cliente = Cliente(true);

        var documento = System.Text.Json.Nodes.JsonNode.Parse(await cliente.GetStringAsync($"/doc/api/{api}.json"))!;
        var operacoes = documento["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(m => $"{m.Key.ToUpperInvariant()} {p.Key.TrimStart('/')}"))
            .Order().ToList();
        var doContrato = Sistema.ExemplosDaApi.Do(api);
        var exemplos = doContrato.Keys.Order().ToList();

        Assert.True(operacoes.SequenceEqual(exemplos),
            "Endpoints e exemplos divergem; rode scripts/exemplos-da-doc.py depois de mudar o contrato. " +
            $"Sem exemplo: {string.Join(", ", operacoes.Except(exemplos))}. Sobrando: {string.Join(", ", exemplos.Except(operacoes))}.");
        // e chegam ao endpoint: todo endpoint com pedido ou resposta com corpo no contrato mostra os exemplos
        foreach (var (chave, exemplo) in doContrato)
        {
            var temCorpo = exemplo["pedido"] is not null
                || exemplo["respostas"]!.AsObject().Any(r => r.Value!.AsArray().Count > 0);
            if (!temCorpo) continue;
            var (metodo, caminho) = (chave.Split(' ')[0].ToLowerInvariant(), "/" + chave.Split(' ')[1]);
            var operacao = documento["paths"]![caminho]![metodo]!.ToJsonString();
            Assert.True(operacao.Contains("\"example\"") || operacao.Contains("\"examples\""), $"{chave} sem os exemplos no endpoint.");
        }
    }

    [Fact]
    public async Task Exemplos_de_pedido_e_de_erro_aparecem_no_endpoint()
    {
        using var cliente = Cliente(true);

        var documento = System.Text.Json.Nodes.JsonNode.Parse(await cliente.GetStringAsync("/doc/api/autenticacao.json"))!;
        var entrar = documento["paths"]!["/auth/hub/entrar"]!["post"]!;

        Assert.Equal("NAVEGADOR", entrar["requestBody"]!["content"]!["application/json"]!["example"]!["aparelho"]!.GetValue<string>());
        Assert.Equal("Bearer", entrar["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["200"]!["value"]!["tipo"]!.GetValue<string>());
        var bloqueada = entrar["responses"]!["429"]!["content"]!["application/json"]!["examples"]!["ENTRADA_BLOQUEADA"]!["value"]!;
        Assert.Equal("ENTRADA_BLOQUEADA", bloqueada["codigo"]!.GetValue<string>());
        Assert.NotNull(bloqueada["tentarEm"]);
    }

    [Theory]
    [InlineData("/doc/api/index.html")]
    [InlineData("/doc/api/usuarios.json")]
    [InlineData("/doc/api/design/banner.svg")]
    [InlineData("/doc/api/diagramas/usuarios-contrato-api.svg")]
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
