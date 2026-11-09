using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Sistema;

/// <summary>Endpoints de sistema: do runtime, não de um módulo.</summary>
[ApiController]
[Route("health")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoSistema)]
public sealed class SaudeController : ControllerBase
{
    public const string Tag = "Sistema";
    public const string Descricao = "Endpoints do próprio runtime, sem módulo: a prontidão";

    /// <summary>Prontidão do backend</summary>
    /// <remarks>
    /// Responde 200, sem corpo, quando o ms_mono está no ar e pronto para atender. É a verificação de prontidão de
    /// todos os ambientes: o CI local espera o 200 antes de rodar o qa, e no Azure Container Apps uma revisão nova
    /// só recebe tráfego depois dele (05_cd-implantacao RNF-54).
    /// </remarks>
    /// <response code="200">No ar e pronto para atender.</response>
    [HttpGet("", Name = "prontidao")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Prontidao() => Ok();
}
