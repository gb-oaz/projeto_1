using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Boaz.MsMono.Primario.Commons;

// Os controllers da API (primario/<contexto>/<feature>/*Controller.cs): finos,
// cada ação só traduz o HTTP para a porta de entrada do caso de uso. O MVC é
// ajustado para responder como o contrato de API manda, sem os automatismos do
// [ApiController] que mudariam as respostas:
// - sem o 400 automático do ModelState nem o ProblemDetails dos erros 4xx: a
//   ligação recusada vira o 422 DADOS_INVALIDOS (CorpoInvalido) e as falhas dos
//   casos de uso viram o Erro do contrato (Http.UseRespostasDeErro);
// - sem validação de modelo (DataAnnotations, [Required] implícito): quem valida
//   é o caso de uso, com o código de erro do contrato;
// - sem inferência de origem: cada parâmetro diz de onde vem;
// - JSON só application/json, com as datas em UTC (Http.DataHoraUtc);
// - texto da rota e da query como chega: vazio continua vazio, não vira null.
public static class Controladores
{
    public static IServiceCollection AddControladoresDasApis(this IServiceCollection servicos)
    {
        servicos.AddControllers(opcoes =>
            {
                opcoes.Filters.Add(new CorpoInvalido());
                opcoes.ModelBinderProviders.Insert(0, new TextoComoChega());
            })
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new Http.DataHoraUtc()))
            .ConfigureApiBehaviorOptions(o =>
            {
                o.SuppressModelStateInvalidFilter = true;
                o.SuppressMapClientErrors = true;
                o.SuppressInferBindingSourcesForParameters = true;
            });
        // os formatadores JSON do MVC já criados: text/json fora, como no corpo
        // que o ASP.NET Core aceita como JSON (application/json e *+json)
        servicos.PostConfigure<MvcOptions>(opcoes =>
        {
            foreach (var entrada in opcoes.InputFormatters.OfType<SystemTextJsonInputFormatter>()) entrada.SupportedMediaTypes.Remove("text/json");
            foreach (var saida in opcoes.OutputFormatters.OfType<SystemTextJsonOutputFormatter>()) saida.SupportedMediaTypes.Remove("text/json");
        });
        servicos.Replace(ServiceDescriptor.Singleton<IObjectModelValidator, SemValidacaoDeModelo>());
        return servicos;
    }

    // A ligação recusada (corpo que não é JSON, com outro content-type ou com o
    // tipo errado num campo): antes de qualquer outro filtro — inclusive o de
    // acesso —, como a ligação do corpo que lança antes dos filtros. O
    // UseRespostasDeErro a responde com 422 DADOS_INVALIDOS em "corpo" (RN-271).
    sealed class CorpoInvalido : IActionFilter, IOrderedFilter
    {
        public int Order => int.MinValue;

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.ErrorCount > 0) throw new BadHttpRequestException("O corpo do pedido não pôde ser lido.");
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }

    // Sem validação de modelo no MVC: o caso de uso valida (dtos/Validacao.cs).
    sealed class SemValidacaoDeModelo : IObjectModelValidator
    {
        public void Validate(ActionContext actionContext, ValidationStateDictionary? validationState, string prefix, object? model)
        {
        }
    }

    // Texto da rota e da query como chega: "?busca=" é "", e o parâmetro repetido
    // junta os valores com vírgula (StringValues), em vez do primeiro valor e do
    // vazio virando null do MVC.
    sealed class TextoComoChega : IModelBinderProvider, IModelBinder
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
            context.Metadata.ModelType == typeof(string)
            && (context.BindingInfo.BindingSource == BindingSource.Query || context.BindingInfo.BindingSource == BindingSource.Path)
                ? this
                : null;

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valor = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valor == ValueProviderResult.None) return Task.CompletedTask;
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valor);
            bindingContext.Result = ModelBindingResult.Success(valor.Values.ToString());
            return Task.CompletedTask;
        }
    }
}

// A marca das ações com login: a documentação põe o cadeado do Bearer nelas
// (DocumentacaoApi). Quem confere o token é o filtro ([ComAcesso]) ou o caso de uso.
public interface IMarcaDeLogin;

// Ação com login cujo token o próprio caso de uso confere (sair, aparelhos).
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComLoginAttribute : Attribute, IMarcaDeLogin;

// Toda rota com login declara o que exige (RNF-220): o token é conferido
// (assinatura, sessão viva e pv atual) e depois o módulo e a aba pedidos, com as
// permissões que o token carrega (RN-241). A aba vem como texto, nameof(Abas.U),
// porque atributo não recebe struct.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComAcessoAttribute(string modulo, string aba) : Attribute, IAsyncActionFilter, IMarcaDeLogin
{
    public Frente Frente { get; init; } = Frente.Hub;

    // a frente do próprio token (hub ou quest): o participante, que entra pelo
    // quest, recebe o 403 do módulo, não o 401 da frente (card do Usuários, RN-189)
    public bool FrenteDoToken { get; init; }

    public ExigenciaDaRota Exigencia { get; } = Acesso.Exigencia(modulo, aba);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var acesso = await Acesso.Conferir(http, FrenteDoToken ? Http.FrenteDoToken(Http.Token(http)) : Frente);
        http.RequestServices.GetRequiredService<IAutorizarPedido>().Executar(Exigencia, new PedidoAutorizavelDto(acesso.Papel, acesso.Mods));
        http.Items[typeof(AcessoConferido)] = acesso;
        await next();
    }
}

// Rota com login que não exige módulo (preferências, RN-283): o token é
// conferido (assinatura, sessão viva e pv atual), na frente dele, hub ou quest.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComSessaoAttribute : Attribute, IAsyncActionFilter, IMarcaDeLogin
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        http.Items[typeof(AcessoConferido)] = await Acesso.Conferir(http, Http.FrenteDoToken(Http.Token(http)));
        await next();
    }
}

// O alvo do endereço que muda estado, na auditoria (RN-270).
public enum AlvoAuditado
{
    // sem alvo no endereço (o que é criado ainda não tem id)
    Nenhum,

    // o {id} da rota
    IdDaRota,

    // os tempos de sessão do time, não uma conta (RN-178)
    Time,
}

// O endereço que muda estado (RN-270): o token é conferido como em [ComAcesso],
// e o 401 sai sem registro; o 403 por faltar o módulo grava o "INICIADO" da ação
// e o ALERTA ACESSO_NEGADO, com quem e a conta do token, o alvo do endereço e o
// traceId do X-Trace-Id, que o caso de uso reaproveita quando a rota atende (RN-271).
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComAcessoAuditadoAttribute(string modulo, string aba, AcaoAuditada acao) : Attribute, IAsyncActionFilter, IMarcaDeLogin
{
    public const string AlvoDoTime = "time";

    public Frente Frente { get; init; } = Frente.Hub;

    public AlvoAuditado Alvo { get; init; } = AlvoAuditado.Nenhum;

    public ExigenciaDaRota Exigencia { get; } = Acesso.Exigencia(modulo, aba);

    public AcaoAuditada Acao { get; } = acao;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var acesso = await Acesso.Conferir(http, Frente);
        var execucao = new ExecucaoDaRotaDto(http.NovoTraceId(), Acao, acesso.ContaId, acesso.Sub, AlvoDe(http));
        await http.RequestServices.GetRequiredService<IAutorizarPedidoAuditado>()
            .Executar(Exigencia, new PedidoAutorizavelDto(acesso.Papel, acesso.Mods), execucao, http.RequestAborted);
        http.Items[typeof(AcessoConferido)] = acesso;
        await next();
    }

    string? AlvoDe(HttpContext http) => Alvo switch
    {
        AlvoAuditado.IdDaRota => http.Request.RouteValues["id"]?.ToString(),
        AlvoAuditado.Time => AlvoDoTime,
        _ => null,
    };
}

static class Acesso
{
    // A exigência do atributo: o módulo e a aba ("C", "U" ou "CU").
    public static ExigenciaDaRota Exigencia(string modulo, string aba) =>
        new(modulo, Abas.De(aba) ?? throw new ArgumentException($"Aba desconhecida: {aba}.", nameof(aba)));

    // O token do pedido, conferido pela Autenticação (assinatura, sessão viva e pv atual).
    public static Task<AcessoConferido> Conferir(HttpContext http, Frente frente) =>
        http.RequestServices.GetRequiredService<IConferirAcesso>()
            .Executar(new ConferenciaDeAcessoDto(Http.Token(http), frente), http.RequestAborted);
}
