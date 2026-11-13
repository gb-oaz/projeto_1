using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using GrupoAutenticacao = Boaz.Core.Commons.Usuario.Autenticacao.Domain.GrupoDeFalha;
using GrupoAutorizacao = Boaz.Core.Commons.Usuario.Autorizacao.Domain.GrupoDeFalha;
using GrupoUsuarios = Boaz.Core.Commons.Usuario.Usuarios.Domain.GrupoDeFalha;

namespace Boaz.MsMono.Primario.In.Commons;

// O corpo de toda resposta de erro (contratos *-contrato-api): o código, a
// mensagem e, quando a falha tem, o campo, o tentarEm, os limites, o módulo,
// as abas e a versão. Os ausentes não vão no JSON.
public sealed record Erro(
    string Codigo, string Mensagem,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Campo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? TentarEm = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Minimo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Maximo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mod = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Abas = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Versao = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Responsavel = null);

public static class Http
{
    // Data e hora sempre em UTC, sem frações: 2026-11-01T09:15:00Z.
    internal sealed class DataHoraUtc : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    }

    // JSON das APIs: camelCase (padrão da web) e datas em UTC, igual nas respostas
    // de erro (UseRespostasDeErro, HttpJsonOptions) e nos controllers (MVC,
    // Controladores.AddControladoresDasApis).
    public static IServiceCollection AddJsonDasApis(this IServiceCollection servicos) => servicos
        .ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new DataHoraUtc()));

    // Falhas dos casos de uso viram a resposta de erro do contrato; corpo que não
    // é JSON, com outro content-type ou com o tipo errado num campo (a ligação
    // recusada, Controladores.CorpoInvalido) responde 422 DADOS_INVALIDOS; a falha
    // inesperada responde 500 ERRO_INESPERADO, sem detalhe da exceção, com o
    // X-Trace-Id da execução quando o pedido já o tinha (RN-270, RN-271).
    public static WebApplication UseRespostasDeErro(this WebApplication app)
    {
        app.Use(async (contexto, proximo) =>
        {
            try
            {
                await proximo(contexto);
                if (contexto.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType && !contexto.Response.HasStarted)
                {
                    contexto.Response.Clear();
                    contexto.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                    await contexto.Response.WriteAsJsonAsync(CorpoInvalido);
                }
            }
            catch (Exception falha) when (Traduzir(falha) is { } traduzida && !contexto.Response.HasStarted)
            {
                contexto.Response.Clear();
                ManterTraceId(contexto);
                contexto.Response.StatusCode = traduzida.Status;
                await contexto.Response.WriteAsJsonAsync(traduzida.Erro);
            }
        });
        return app;
    }

    // A resposta do corpo que não é JSON (RN-271). A ligação recusada não chega
    // ao filtro de acesso nem ao caso de uso (Controladores.CorpoInvalido lança
    // BadHttpRequestException; um 415 que escape vira o mesmo 422): 422
    // DADOS_INVALIDOS no campo "corpo", sem X-Trace-Id, porque nenhuma execução começou.
    static readonly Erro CorpoInvalido = new("DADOS_INVALIDOS", "O campo \"corpo\" está faltando ou é inválido.", "corpo");

    // O cabeçalho com o traceId de cada execução auditada (RN-271).
    public const string CabecalhoDoTraceId = "X-Trace-Id";

    // Um traceId novo para a execução auditada (RN-271), já no cabeçalho
    // X-Trace-Id da resposta; a recusa o mantém (UseRespostasDeErro). Se a
    // execução já começou no filtro da rota ([ComAcessoAuditado], RN-270), é o
    // mesmo traceId: um por execução.
    public static TraceId NovoTraceId(this HttpContext contexto)
    {
        if (contexto.Items[typeof(TraceId)] is TraceId atual) return atual;
        var traceId = TraceId.Novo();
        contexto.Items[typeof(TraceId)] = traceId;
        contexto.Response.Headers[CabecalhoDoTraceId] = traceId.Valor;
        return traceId;
    }

    // Response.Clear() apaga os cabeçalhos: a recusa repõe o X-Trace-Id da execução.
    static void ManterTraceId(HttpContext contexto)
    {
        if (contexto.Items[typeof(TraceId)] is TraceId traceId) contexto.Response.Headers[CabecalhoDoTraceId] = traceId.Valor;
    }

    // A resposta da falha inesperada (RN-270): nada da exceção sai para fora.
    public static readonly Erro ErroInesperado = new(RegistroDeErro.ErroInesperado, "Algo deu errado. Tente de novo mais tarde.");

    // O status e o corpo de cada falha; null só para o pedido cancelado, que não tem a quem responder.
    public static (int Status, Erro Erro)? Traduzir(Exception falha) => falha switch
    {
        FalhaDeUsuarios f => (Status(f.Grupo), new Erro(f.Codigo, f.Message, f.Campo, f.TentarEm, f.Minimo, f.Maximo)),
        FalhaDeAutenticacao f => (Status(f.Grupo), new Erro(f.Codigo, f.Message, f.Campo, f.TentarEm)),
        FalhaDeAutorizacao f => (Status(f.Grupo), new Erro(f.Codigo, f.Message, f.Campo, Mod: f.Mod, Abas: f.Abas, Versao: f.Versao)),
        FalhaDoSac f => (Status(f.Grupo), new Erro(f.Codigo, f.Message, f.Campo, Minimo: f.Minimo, Maximo: f.Maximo, Responsavel: f.Responsavel)),
        BadHttpRequestException => (StatusCodes.Status422UnprocessableEntity, CorpoInvalido),
        OperationCanceledException => null,
        _ => (StatusCodes.Status500InternalServerError, ErroInesperado),
    };

    static int Status(GrupoUsuarios grupo) => grupo switch
    {
        GrupoUsuarios.NaoAutenticado => StatusCodes.Status401Unauthorized,
        GrupoUsuarios.Proibido => StatusCodes.Status403Forbidden,
        GrupoUsuarios.NaoEncontrado => StatusCodes.Status404NotFound,
        GrupoUsuarios.Conflito => StatusCodes.Status409Conflict,
        GrupoUsuarios.Vencido => StatusCodes.Status410Gone,
        GrupoUsuarios.DadosInvalidos => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status429TooManyRequests,
    };

    static int Status(GrupoDaFalha grupo) => grupo switch
    {
        GrupoDaFalha.Proibido => StatusCodes.Status403Forbidden,
        GrupoDaFalha.NaoEncontrado => StatusCodes.Status404NotFound,
        GrupoDaFalha.Conflito => StatusCodes.Status409Conflict,
        GrupoDaFalha.MuitoGrande => StatusCodes.Status413PayloadTooLarge,
        GrupoDaFalha.TipoNaoAceito => StatusCodes.Status415UnsupportedMediaType,
        GrupoDaFalha.DadosInvalidos => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status503ServiceUnavailable,
    };

    static int Status(GrupoAutenticacao grupo) => grupo switch
    {
        GrupoAutenticacao.NaoAutenticado => StatusCodes.Status401Unauthorized,
        GrupoAutenticacao.Proibido => StatusCodes.Status403Forbidden,
        GrupoAutenticacao.NaoEncontrado => StatusCodes.Status404NotFound,
        GrupoAutenticacao.DadosInvalidos => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status429TooManyRequests,
    };

    static int Status(GrupoAutorizacao grupo) => grupo switch
    {
        GrupoAutorizacao.Proibido => StatusCodes.Status403Forbidden,
        GrupoAutorizacao.NaoEncontrado => StatusCodes.Status404NotFound,
        GrupoAutorizacao.VersaoDesatualizada => StatusCodes.Status412PreconditionFailed,
        GrupoAutorizacao.DadosInvalidos => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status428PreconditionRequired,
    };

    // O token de acesso do cabeçalho Authorization: Bearer <token>.
    public static string? Token(HttpContext contexto) =>
        contexto.Request.Headers.Authorization.ToString() is var cabecalho && cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? cabecalho["Bearer ".Length..].Trim()
            : null;

    // A frente do token (aud), só para escolher contra qual frente conferir: a
    // conferência de verdade, com a assinatura, é a do caso de uso.
    public static Frente FrenteDoToken(string? token)
    {
        try
        {
            return token is not null && TokenDeAcesso.Ler(token).Claims.Aud == ClaimsDeAcesso.AudDe(Frente.Quest) ? Frente.Quest : Frente.Hub;
        }
        catch (FalhaDeAutenticacao)
        {
            return Frente.Hub;
        }
    }

    // O pedido conferido por [ComAcesso] (Controladores.cs).
    public static AcessoConferido Acesso(this HttpContext contexto) => (AcessoConferido)contexto.Items[typeof(AcessoConferido)]!;

    // Quem pede: o sub do token.
    public static string QuemPede(this HttpContext contexto) => contexto.Acesso().Sub;
}
