using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Infra.In.Commons.Usuario.Autenticacao;

/// <summary>A chave pública que confere a assinatura dos tokens (RNF-207, RNF-218).</summary>
[ApiController]
[Route("auth/chaves")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoAutenticacao)]
[Produces("application/json")]
public sealed class ChavesController : ControllerBase
{
    public const string Tag = "Chaves públicas";
    public const string Descricao = "A chave pública (JWKS) que confere a assinatura do token de acesso. Endereço público";

    /// <summary>Publicar a chave pública que confere a assinatura do token</summary>
    /// <remarks>Endereço público (RNF-207, RNF-218), no formato JWKS, para as aplicações e os serviços do deboaz.</remarks>
    /// <param name="ler">Caso de uso LerChavesPublicas.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">As chaves públicas, no formato JWKS.</response>
    [HttpGet("", Name = "lerChavesPublicas")]
    [ProducesResponseType<ChavesPublicasDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChavesPublicasDto>> Ler([FromServices] ILerChavesPublicas ler, CancellationToken ct) =>
        Ok(await ler.Executar(ct));
}
