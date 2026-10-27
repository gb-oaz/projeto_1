using Microsoft.OpenApi;

namespace Boaz.MsMono.Sistema;

// Catálogo das APIs do backend em /doc/api: documentos OpenAPI gerados pelo
// ASP.NET Core e Swagger UI com a identidade do deboaz (banner, logo, favicon e
// cores em wwwroot/doc/api/design/, cópia gerada por 0_Assets/design; o estilo
// em wwwroot/doc/api/swagger.css). A primeira página é a capa do catálogo; o
// seletor do topo abre cada API. Liga só com Swagger:Habilitado, que o
// Terraform define como true em homologação e false em produção.
public static class DocumentacaoApi
{
    public const string Caminho = "doc/api";
    public const string Catalogo = "catalogo";
    public const string Documento = "v1";
    public const string Titulo = "deboaz · catálogo de APIs";
    public const string TituloMsMono = "deboaz · API ms_mono";
    public const string TagSistema = "Sistema";

    private const string DescricaoCatalogo = """
        ![deboaz](/doc/api/design/banner.svg)

        Catálogo das APIs do **deboaz**. Escolha a API no seletor do topo, à direita, para ver os endpoints e testá-los daqui mesmo.

        ### APIs do catálogo

        | API | O que documenta |
        |---|---|
        | `ms_mono · v1` | O backend único do deboaz: os contextos `landing_page`, `hub`, `quest` e `commons` num só serviço. Hoje traz os endpoints de sistema |

        ### O que vem por aí

        Cada módulo publica os próprios endpoints na API do `ms_mono` quando for construído, agrupados pelo contexto dele. O catálogo cresce junto, sem mudar de endereço.

        ### Onde este catálogo existe

        Em homologação, para acompanhar o sistema, e na máquina de quem desenvolve. Em produção, `/doc/api` não existe.
        """;

    private const string DescricaoMsMono = """
        O **ms_mono** é o backend único do deboaz: os contextos `landing_page`, `hub`, `quest` e `commons` rodam num só serviço.

        Por enquanto só existem os **endpoints de sistema**, do próprio runtime, sem módulo:

        | Endpoint | Para quê |
        |---|---|
        | `GET /health` | Prontidão: responde 200 quando o backend está pronto para atender |
        | `GET /` | Leva a este catálogo |
        | `GET /doc/api` | Este catálogo (Swagger UI) |
        | `GET /doc/api/catalogo.json` | O documento OpenAPI da capa do catálogo |
        | `GET /doc/api/v1.json` | O documento OpenAPI desta API |
        """;

    public static bool Habilitada(IConfiguration configuracao) => configuracao.GetValue<bool>("Swagger:Habilitado");

    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection servicos) => servicos
        // capa: só a apresentação, sem endpoints
        .AddOpenApi(Catalogo, opcoes =>
        {
            opcoes.ShouldInclude = _ => false;
            opcoes.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info = new OpenApiInfo { Title = Titulo, Version = Documento, Description = DescricaoCatalogo };
                documento.Servers = [];
                return Task.CompletedTask;
            });
        })
        .AddOpenApi(Documento, opcoes => opcoes.AddDocumentTransformer((documento, _, _) =>
        {
            documento.Info = new OpenApiInfo { Title = TituloMsMono, Version = Documento, Description = DescricaoMsMono };
            // sem servers: o Swagger UI chama a origem da própria página (atrás do
            // HTTPS da Azure o backend vê http e o endereço gerado sairia errado)
            documento.Servers = [];
            documento.Tags = new HashSet<OpenApiTag>
            {
                new() { Name = TagSistema, Description = "Endpoints do próprio runtime, sem módulo: prontidão e documentação" },
            };
            return Task.CompletedTask;
        }));

    public static WebApplication UseDocumentacaoApi(this WebApplication app)
    {
        // wwwroot/doc/api/: identidade (design/) e estilo (swagger.css)
        app.UseStaticFiles();
        // a raiz abre o catálogo; 302 (e não 301) para o navegador não guardar o
        // desvio, porque onde a documentação não existe a raiz responde 404
        app.MapGet("/", () => Results.Redirect($"/{Caminho}")).ExcludeFromDescription();
        app.MapOpenApi($"/{Caminho}/{{documentName}}.json");
        app.UseSwaggerUI(opcoes =>
        {
            opcoes.RoutePrefix = Caminho;
            // o primeiro do seletor abre primeiro: a capa
            opcoes.SwaggerEndpoint($"/{Caminho}/{Catalogo}.json", "Catálogo de APIs");
            opcoes.SwaggerEndpoint($"/{Caminho}/{Documento}.json", $"ms_mono · {Documento}");
            opcoes.DocumentTitle = Titulo;
            opcoes.HeadContent = $"""<link rel="icon" type="image/svg+xml" href="/{Caminho}/design/favicon.svg">""";
            opcoes.InjectStylesheet($"/{Caminho}/design/cores.css");
            opcoes.InjectStylesheet($"/{Caminho}/swagger.css");
            opcoes.DefaultModelsExpandDepth(-1);
            opcoes.DisplayRequestDuration();
        });
        return app;
    }
}
