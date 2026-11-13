using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.MsMono.Primario.In.Commons;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>Os anexos do SAC (RN-292), com MOD-02 · U: enviar antes e baixar.</summary>
[ApiController]
[Route("sac/anexos")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AnexosController : ControllerBase
{
    // 10 MB do arquivo e a folga do multipart
    const long LimiteDoPedido = 12L * 1024 * 1024;

    /// <summary>Enviar um arquivo (multipart/form-data, campo "arquivo").</summary>
    [HttpPost]
    [RequestSizeLimit(LimiteDoPedido)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteDoPedido)]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    public async Task<IActionResult> Enviar([FromForm(Name = "arquivo")] IFormFile? arquivo, [FromServices] IEnviarAnexo enviar,
        CancellationToken ct)
    {
        if (arquivo is null) throw FalhaDoSac.DadosInvalidos("arquivo");
        await using var conteudo = arquivo.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await enviar.Executar(conteudo, arquivo.FileName, HttpContext.QuemPedeAoSac(), ct));
    }

    /// <summary>Baixar o arquivo, para quem vê o chamado dele (RN-288).</summary>
    [HttpGet("{id}")]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    public async Task<IActionResult> Ler([FromRoute] string id, [FromServices] ILerAnexo ler, CancellationToken ct)
    {
        var (anexo, conteudo) = await ler.Executar(id, HttpContext.QuemPedeAoSac(), ct);
        return File(conteudo, anexo.Tipo, anexo.Nome);
    }
}
