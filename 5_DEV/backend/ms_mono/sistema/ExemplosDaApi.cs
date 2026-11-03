using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace Boaz.MsMono.Sistema;

// Os exemplos de pedido e de resposta de cada endpoint, dentro dele no Swagger.
// Vêm de sistema/exemplos/<documento>.json, gerado dos contratos de API de
// 2_Diagramas/ (e das mensagens das falhas do core) por scripts/exemplos-da-doc.py:
// a chave é "MÉTODO rota", como em ApiDescription; o valor traz o "pedido" e, por
// status, a lista de respostas com nome e corpo.
public static class ExemplosDaApi
{
    const string Json = "application/json";

    // Os exemplos do documento, ou nenhum se o módulo não tem arquivo (sistema).
    public static IReadOnlyDictionary<string, JsonObject> Do(string documento)
    {
        using var arquivo = typeof(ExemplosDaApi).Assembly.GetManifestResourceStream($"exemplos/{documento}.json");
        if (arquivo is null) return new Dictionary<string, JsonObject>();
        var raiz = JsonNode.Parse(arquivo)!.AsObject();
        return raiz.ToDictionary(par => par.Key, par => par.Value!.AsObject());
    }

    // "PUT usuarios/{id}/permissoes": a rota "" de um grupo sai com a barra no fim
    public static string Chave(OpenApiOperationTransformerContext contexto) =>
        $"{contexto.Description.HttpMethod} {contexto.Description.RelativePath?.TrimEnd('/')}";

    public static void Aplicar(OpenApiOperation operacao, JsonObject exemplo)
    {
        if (exemplo["pedido"] is JsonNode pedido)
        {
            operacao.RequestBody ??= new OpenApiRequestBody { Required = true };
            if (operacao.RequestBody is OpenApiRequestBody corpo)
            {
                corpo.Content ??= new Dictionary<string, OpenApiMediaType>();
                if (!corpo.Content.TryGetValue(Json, out var tipo)) corpo.Content[Json] = tipo = new OpenApiMediaType();
                tipo.Example = pedido.DeepClone();
            }
        }

        if (exemplo["respostas"] is not JsonObject respostas) return;
        operacao.Responses ??= [];
        foreach (var (status, lista) in respostas)
        {
            if (!operacao.Responses.TryGetValue(status, out var existente))
            {
                existente = new OpenApiResponse { Description = ReasonPhrases.GetReasonPhrase(int.Parse(status)) };
                operacao.Responses[status] = existente;
            }
            if (existente is not OpenApiResponse resposta || lista is not JsonArray itens || itens.Count == 0) continue;
            resposta.Content ??= new Dictionary<string, OpenApiMediaType>();
            if (!resposta.Content.TryGetValue(Json, out var tipo)) resposta.Content[Json] = tipo = new OpenApiMediaType();
            tipo.Examples ??= new Dictionary<string, IOpenApiExample>();
            foreach (var item in itens.OfType<JsonObject>())
            {
                var nome = item["nome"]!.GetValue<string>();
                tipo.Examples[nome] = new OpenApiExample { Summary = nome, Value = item["corpo"]?.DeepClone() };
            }
        }
    }
}
