using Boaz.MsMono.Infra.In.Commons;
using Boaz.MsMono.Infra.In.Commons.Usuario.Autenticacao;
using Boaz.MsMono.Infra.In.Commons.Usuario.Autorizacao;
using Boaz.MsMono.Infra.In.Commons.Usuario.Usuarios;
using Microsoft.OpenApi;

namespace Boaz.MsMono.Sistema;

// Catálogo das APIs do backend em /doc/api: documentos OpenAPI gerados pelo
// ASP.NET Core e Swagger UI com a identidade do deboaz (banner, logo, favicon e
// cores em wwwroot/doc/api/design/, cópia gerada por 0_Assets/design; o estilo
// em wwwroot/doc/api/swagger.css). A primeira página é a capa do catálogo; o
// seletor do topo abre cada módulo como uma API própria, cada uma com o guia de
// uso de sistema/guias/<documento>.md e os diagramas de wwwroot/doc/api/diagramas/
// (cópia de 2_Diagramas/ feita por scripts/diagramas-da-doc.sh). Liga só com
// Swagger:Habilitado, que o Terraform define como true em homologação e false
// em produção.
public static class DocumentacaoApi
{
    public const string Caminho = "doc/api";
    public const string Catalogo = "catalogo";
    public const string DocumentoSistema = "sistema";
    public const string Versao = "v1";
    public const string Titulo = "deboaz · catálogo de APIs";
    public const string TagSistema = "Sistema";
    public const string EsquemaBearer = "Bearer";

    // Uma API do seletor: o documento (/doc/api/<documento>.json), o rótulo no
    // seletor, o grupo (tag) dos endpoints e a frase do grupo.
    public sealed record Api(string Documento, string Rotulo, string Tag, string DescricaoDoGrupo)
    {
        public string Titulo => $"deboaz · {Rotulo}";
    }

    // Na ordem do seletor, depois da capa. Módulo novo entra aqui, com o guia.
    public static readonly IReadOnlyList<Api> Apis =
    [
        new(DocumentoSistema, "Sistema", TagSistema, "Endpoints do próprio runtime, sem módulo: a prontidão"),
        new(AutenticacaoApi.Documento, AutenticacaoApi.Tag, AutenticacaoApi.Tag, AutenticacaoApi.Descricao),
        new(UsuariosApi.Documento, UsuariosApi.Tag, UsuariosApi.Tag, UsuariosApi.Descricao),
        new(AutorizacaoApi.Documento, AutorizacaoApi.Tag, AutorizacaoApi.Tag, AutorizacaoApi.Descricao),
    ];

    public static bool Habilitada(IConfiguration configuracao) => configuracao.GetValue<bool>("Swagger:Habilitado");

    // O guia em Markdown embutido no assembly (sistema/guias/<nome>.md).
    public static string Guia(string nome)
    {
        using var arquivo = typeof(DocumentacaoApi).Assembly.GetManifestResourceStream($"guias/{nome}.md")
            ?? throw new InvalidOperationException($"Falta o guia sistema/guias/{nome}.md.");
        using var leitor = new StreamReader(arquivo);
        return leitor.ReadToEnd();
    }

    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection servicos)
    {
        // capa: só a apresentação, sem endpoints
        servicos.AddOpenApi(Catalogo, opcoes =>
        {
            opcoes.ShouldInclude = _ => false;
            opcoes.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info = new OpenApiInfo { Title = Titulo, Version = Versao, Description = Guia(Catalogo) };
                documento.Servers = [];
                return Task.CompletedTask;
            });
        });

        // uma API por módulo: só os endpoints do grupo dele (WithGroupName)
        foreach (var api in Apis)
        {
            var exemplos = ExemplosDaApi.Do(api.Documento);
            servicos.AddOpenApi(api.Documento, opcoes =>
            {
                opcoes.AddDocumentTransformer((documento, _, _) =>
                {
                    documento.Info = new OpenApiInfo { Title = api.Titulo, Version = Versao, Description = Guia(api.Documento) };
                    // sem servers: o Swagger UI chama a origem da própria página (atrás do
                    // HTTPS da Azure o backend vê http e o endereço gerado sairia errado)
                    documento.Servers = [];
                    documento.Tags = new HashSet<OpenApiTag> { new() { Name = api.Tag, Description = api.DescricaoDoGrupo } };
                    documento.Components ??= new OpenApiComponents();
                    documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    documento.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "O tokenAcesso de POST /auth/hub/entrar (ou do quest), sem o \"Bearer \" na frente.",
                    };
                    return Task.CompletedTask;
                });
                // cadeado só nos endpoints marcados com ComLogin()
                opcoes.AddOperationTransformer((operacao, contexto, _) =>
                {
                    if (contexto.Description.ActionDescriptor.EndpointMetadata.OfType<MarcaDeLogin>().Any())
                    {
                        operacao.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(EsquemaBearer, contexto.Document)] = [] }];
                    }
                    // o pedido e as respostas do contrato, dentro do endpoint
                    if (exemplos.TryGetValue(ExemplosDaApi.Chave(contexto), out var exemplo)) ExemplosDaApi.Aplicar(operacao, exemplo);
                    return Task.CompletedTask;
                });
            });
        }
        return servicos;
    }

    public static WebApplication UseDocumentacaoApi(this WebApplication app)
    {
        // wwwroot/doc/api/: identidade (design/), diagramas (diagramas/) e estilo (swagger.css)
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
            foreach (var api in Apis) opcoes.SwaggerEndpoint($"/{Caminho}/{api.Documento}.json", api.Rotulo);
            opcoes.DocumentTitle = Titulo;
            opcoes.HeadContent = $"""<link rel="icon" type="image/svg+xml" href="/{Caminho}/design/favicon.svg">""";
            opcoes.InjectStylesheet($"/{Caminho}/design/cores.css");
            opcoes.InjectStylesheet($"/{Caminho}/swagger.css");
            opcoes.DefaultModelsExpandDepth(-1);
            opcoes.DisplayRequestDuration();
            // o token colado em Authorize vale para todas as APIs do seletor
            opcoes.EnablePersistAuthorization();
            // o endereço da página abre o endpoint (#/Tag/operação), para compartilhar
            opcoes.EnableDeepLinking();
        });
        return app;
    }
}
