namespace Boaz.MsMono.Sistema;

// Endpoints de sistema: do runtime, não de um módulo.
public static class Saude
{
    // Prontidão: o CI local, a homologação e a produção esperam 200 em /health
    public static IEndpointRouteBuilder MapSaude(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/health", () => Results.Ok())
            .WithTags(DocumentacaoApi.TagSistema)
            .WithSummary("Prontidão do backend")
            .WithDescription(
                "Responde 200, sem corpo, quando o ms_mono está no ar e pronto para atender. " +
                "É a verificação de prontidão de todos os ambientes: o CI local espera o 200 antes de rodar o qa, " +
                "e no Azure Container Apps uma revisão nova só recebe tráfego depois dele (05_cd-implantacao RNF-54).")
            .Produces(StatusCodes.Status200OK);
        return rotas;
    }
}
