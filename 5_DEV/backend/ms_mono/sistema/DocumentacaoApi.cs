using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.In.Commons.Usuario.Autenticacao;
using Boaz.MsMono.Primario.In.Commons.Usuario.Autorizacao;
using Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Primario.In.Hub.Auditoria;
using Boaz.MsMono.Primario.In.Hub.Pagamento;
using Boaz.MsMono.Primario.In.Hub.Sac;
using Microsoft.OpenApi;

namespace Boaz.MsMono.Sistema;

// Catálogo das APIs do backend em /doc/api: documentos OpenAPI gerados pelo
// ASP.NET Core e Swagger UI com a identidade do deboaz (banner, logo, favicon e
// cores em wwwroot/doc/api/design/, cópia gerada por 0_Assets/design; o estilo
// em wwwroot/doc/api/swagger.css). A primeira página é a capa do catálogo; o
// seletor do topo abre cada módulo como uma API própria, cada uma com o guia de
// uso de sistema/guias/<documento>.md e os diagramas de wwwroot/doc/api/diagramas/
// (cópia de 2_Diagramas/ feita por scripts/diagramas-da-doc.sh). Dentro de cada
// API, um grupo (tag) por controller; o resumo, a descrição, os parâmetros e as
// respostas de cada endpoint vêm dos comentários XML das ações. Liga só com
// Swagger:Habilitado, que o Terraform define como true em homologação e false
// em produção.
public static class DocumentacaoApi
{
    public const string Caminho = "doc/api";
    public const string Catalogo = "catalogo";
    public const string DocumentoSistema = "sistema";
    public const string DocumentoAutenticacao = "autenticacao";
    public const string DocumentoUsuarios = "usuarios";
    public const string DocumentoAutorizacao = "autorizacao";
    public const string DocumentoSac = "sac";
    public const string DocumentoPagamento = "pagamento";
    public const string DocumentoAuditoria = "auditoria";
    public const string Versao = "v1";
    public const string Titulo = "deboaz · catálogo de APIs";
    public const string EsquemaBearer = "Bearer";

    // Um grupo de endpoints dentro de uma API: a tag de um controller e a frase dela.
    public sealed record Grupo(string Tag, string Descricao);

    // Uma API do seletor: o documento (/doc/api/<documento>.json), o rótulo no
    // seletor e os grupos (um por controller), na ordem em que aparecem. Com
    // ComTraceId, todo endpoint mostra o X-Trace-Id que pode vir no pedido e o
    // que vem em toda resposta (o Pagamento, 19 RNF-243).
    public sealed record Api(string Documento, string Rotulo, IReadOnlyList<Grupo> Grupos, bool ComTraceId = false)
    {
        public string Titulo => $"deboaz · {Rotulo}";
    }

    // Na ordem do seletor, depois da capa. Módulo novo entra aqui, com o guia;
    // controller novo, no grupo da API dele (ApiExplorerSettings.GroupName).
    public static readonly IReadOnlyList<Api> Apis =
    [
        new(DocumentoSistema, "Sistema", [new(SaudeController.Tag, SaudeController.Descricao)]),
        new(DocumentoAutenticacao, "Autenticação",
        [
            new(EntradaController.Tag, EntradaController.Descricao),
            new(SessaoController.Tag, SessaoController.Descricao),
            new(AparelhosController.Tag, AparelhosController.Descricao),
            new(ChavesController.Tag, ChavesController.Descricao),
        ]),
        new(DocumentoUsuarios, "Usuários",
        [
            new(ContasController.Tag, ContasController.Descricao),
            new(UsuariosController.Tag, UsuariosController.Descricao),
            new(TempoDeSessaoController.Tag, TempoDeSessaoController.Descricao),
        ]),
        new(DocumentoAutorizacao, "Autorização", [new(PermissoesController.Tag, PermissoesController.Descricao)]),
        new(DocumentoSac, "SAC",
        [
            new(ChamadosController.Tag, ChamadosController.Descricao),
            new(AnexosController.Tag, AnexosController.Descricao),
            new(ConfiguracaoDoSacController.Tag, ConfiguracaoDoSacController.Descricao),
            new(CardDoSacController.Tag, CardDoSacController.Descricao),
        ]),
        new(DocumentoPagamento, "Pagamento",
        [
            new(FaturasController.Tag, FaturasController.Descricao),
            new(PagamentosController.Tag, PagamentosController.Descricao),
            new(NotificacoesController.Tag, NotificacoesController.Descricao),
            new(RecebimentosController.Tag, RecebimentosController.Descricao),
            new(CardDoPagamentoController.Tag, CardDoPagamentoController.Descricao),
        ], ComTraceId: true),
        new(DocumentoAuditoria, "Auditoria",
        [
            new(SaudeDaAuditoriaController.Tag, SaudeDaAuditoriaController.Descricao),
            new(MetricasDaAuditoriaController.Tag, MetricasDaAuditoriaController.Descricao),
            new(RegistrosDaAuditoriaController.Tag, RegistrosDaAuditoriaController.Descricao),
            new(ExecucoesDaAuditoriaController.Tag, ExecucoesDaAuditoriaController.Descricao),
            new(OcorrenciasDaAuditoriaController.Tag, OcorrenciasDaAuditoriaController.Descricao),
            new(CardDaAuditoriaController.Tag, CardDaAuditoriaController.Descricao),
        ]),
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
                    documento.Tags = new HashSet<OpenApiTag>(api.Grupos.Select(g => new OpenApiTag { Name = g.Tag, Description = g.Descricao }));
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
                // cadeado só nas ações com login ([ComLogin], [ComAcesso], [ComAcessoAuditado])
                opcoes.AddOperationTransformer((operacao, contexto, _) =>
                {
                    if (contexto.Description.ActionDescriptor.EndpointMetadata.OfType<IMarcaDeLogin>().Any())
                    {
                        operacao.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(EsquemaBearer, contexto.Document)] = [] }];
                    }
                    // o pedido e as respostas do contrato, dentro do endpoint
                    if (exemplos.TryGetValue(ExemplosDaApi.Chave(contexto), out var exemplo)) ExemplosDaApi.Aplicar(operacao, exemplo);
                    // os cabeçalhos que a ação lê sem binding (If-Match, a assinatura do banco)
                    foreach (var cabecalho in contexto.Description.ActionDescriptor.EndpointMetadata.OfType<CabecalhoDoPedidoAttribute>())
                    {
                        Cabecalho(operacao, cabecalho.Nome, cabecalho.Descricao, cabecalho.Obrigatorio);
                    }
                    if (api.ComTraceId) TraceIdEmTodas(operacao);
                    return Task.CompletedTask;
                });
            });
        }
        return servicos;
    }

    // um cabeçalho do pedido, como parâmetro "in: header"
    static void Cabecalho(OpenApiOperation operacao, string nome, string descricao, bool obrigatorio)
    {
        operacao.Parameters ??= [];
        if (operacao.Parameters.Any(p => string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))) return;
        operacao.Parameters.Add(new OpenApiParameter
        {
            Name = nome,
            In = ParameterLocation.Header,
            Required = obrigatorio,
            Description = descricao,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });
    }

    // RNF-243: o X-Trace-Id opcional do pedido (vira traceIdOrigem) e o da execução em toda resposta
    static void TraceIdEmTodas(OpenApiOperation operacao)
    {
        Cabecalho(operacao, "X-Trace-Id", "Opcional: o traceId de uma execução anterior (\"trc-\" e 8 hexadecimais), gravado como " +
            "traceIdOrigem; fora do formato, é ignorado. Nunca substitui o da execução.", false);
        foreach (var resposta in (operacao.Responses ?? []).Values.OfType<OpenApiResponse>())
        {
            resposta.Headers ??= new Dictionary<string, IOpenApiHeader>();
            resposta.Headers["X-Trace-Id"] = new OpenApiHeader
            {
                Description = "O traceId desta execução (\"trc-\" e 8 hexadecimais); no erro, o mesmo valor vem em \"traceId\" no corpo.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^trc-[0-9a-f]{8}$" },
            };
        }
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

// Um cabeçalho que a ação lê de Request.Headers, sem binding, e que a
// documentação mostra no endpoint (o If-Match da configuração do Pagamento,
// a assinatura do aviso do banco).
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CabecalhoDoPedidoAttribute(string nome, string descricao) : Attribute
{
    public string Nome { get; } = nome;
    public string Descricao { get; } = descricao;
    public bool Obrigatorio { get; init; }
}
