using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Hub.Sac.Anexos.Dtos;
using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>Os anexos do SAC (RN-292, sac-contrato-api v02), com MOD-02 · U: enviar antes e baixar.</summary>
[ApiController]
[Route("sac/anexos")]
[Tags(Tag)]
[ApiExplorerSettings(GroupName = DocumentacaoApi.DocumentoSac)]
public sealed class AnexosController : ControllerBase
{
    public const string Tag = "Anexos";
    public const string Descricao =
        "Com MOD-02 · U: enviar o arquivo antes de citá-lo no chamado ou na mensagem (PNG ou JPG, até 10 MB, " +
        "verificado) e baixar o anexo de um chamado que quem pede vê";

    // 10 MB do arquivo e a folga do multipart
    const long LimiteDoPedido = 12L * 1024 * 1024;

    /// <summary>Enviar um arquivo</summary>
    /// <remarks>
    /// multipart/form-data, campo "arquivo" (RN-292): até 10 MB; só PNG ou JPG pelo conteúdo, nunca pela extensão, guardado
    /// sem conversão; passa pela verificação de segurança, e sem ela nada é guardado. O arquivo vai para o destino da
    /// configuração, o armazenamento de objetos (BLOB) ou o GridFS (RNF-233). O anexo não citado em 24 h é descartado
    /// (RNF-234). Não auditado: o anexo entra na auditoria de quem o vincula.
    /// </remarks>
    /// <param name="arquivo">O arquivo, no campo "arquivo" do formulário.</param>
    /// <param name="enviar">Caso de uso EnviarAnexo.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="201">O anexo guardado, com o id para citar.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    /// <response code="413">ANEXO_GRANDE: mais de 10 MB.</response>
    /// <response code="415">TIPO_NAO_ACEITO: o conteúdo não é PNG nem JPG ("Envie PNG ou JPG.").</response>
    /// <response code="422">ANEXO_RECUSADO pela verificação, ou sem o campo "arquivo".</response>
    /// <response code="503">VERIFICACAO_INDISPONIVEL: a verificação está fora do ar.</response>
    [HttpPost("", Name = "enviarAnexo")]
    [RequestSizeLimit(LimiteDoPedido)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteDoPedido)]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    [ProducesResponseType<AnexoDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status413PayloadTooLarge, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status415UnsupportedMediaType, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status422UnprocessableEntity, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status503ServiceUnavailable, "application/json")]
    public async Task<IActionResult> Enviar([FromForm(Name = "arquivo")] IFormFile? arquivo, [FromServices] IEnviarAnexo enviar,
        CancellationToken ct)
    {
        if (arquivo is null) throw FalhaDoSac.DadosInvalidos("arquivo");
        await using var conteudo = arquivo.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await enviar.Executar(conteudo, arquivo.FileName, HttpContext.QuemPedeAoSac(), ct));
    }

    /// <summary>Baixar o arquivo de um anexo</summary>
    /// <remarks>
    /// Para quem vê o chamado do anexo; o de nota interna, só o time (RN-288, RN-292). Lido de onde foi guardado, BLOB ou
    /// GRIDFS, sem migração (RNF-233). Sai com o tipo do anexo e como download, com o nome enviado.
    /// </remarks>
    /// <param name="id">O id do anexo, como anx-7f3a21.</param>
    /// <param name="ler">Caso de uso LerAnexo.</param>
    /// <param name="ct">Cancelamento do pedido.</param>
    /// <response code="200">O arquivo.</response>
    /// <response code="401">Token de acesso ausente ou inválido.</response>
    /// <response code="403">Sem MOD-02 · U.</response>
    /// <response code="404">ANEXO_INEXISTENTE: não existe, é de outra conta, de nota interna (para a conta), venceu pelo expiraEm ou o arquivo já saiu pelo descarte (RNF-234).</response>
    [HttpGet("{id}", Name = "baixarAnexo")]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<Erro>(StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status403Forbidden, "application/json")]
    [ProducesResponseType<Erro>(StatusCodes.Status404NotFound, "application/json")]
    public async Task<IActionResult> Ler([FromRoute] string id, [FromServices] ILerAnexo ler, CancellationToken ct)
    {
        var (anexo, conteudo) = await ler.Executar(id, HttpContext.QuemPedeAoSac(), ct);
        return File(conteudo, anexo.Tipo, anexo.Nome);
    }
}
