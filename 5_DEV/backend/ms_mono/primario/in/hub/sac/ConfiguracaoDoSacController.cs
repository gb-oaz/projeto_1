using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

/// <summary>Os prazos do SAC por categoria (RF-181): o time lê (MOD-02 · U); o administrador salva (MOD-02 · CU).</summary>
[ApiController]
[Route("sac/configuracao")]
[ApiExplorerSettings(IgnoreApi = true)]
[Produces("application/json")]
public sealed class ConfiguracaoDoSacController : ControllerBase
{
    /// <summary>Ler os prazos.</summary>
    [HttpGet]
    [ComAcesso(Sac.Modulo, nameof(Abas.U))]
    public async Task<IActionResult> Ler([FromServices] ILerPrazos ler, CancellationToken ct) =>
        Ok(await ler.Executar(HttpContext.QuemPedeAoSac(), ct));

    /// <summary>Salvar os prazos que mudam.</summary>
    [HttpPut]
    [ComAcessoAuditado(Sac.Modulo, nameof(Abas.CU), AcaoAuditada.SalvarPrazosSac)]
    public async Task<IActionResult> Salvar([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PedidoPrazos? corpo,
        [FromServices] ISalvarPrazos salvar, CancellationToken ct) =>
        Ok(await salvar.Executar(corpo ?? new(null), HttpContext.QuemPedeAoSac(), HttpContext.NovoTraceId(), ct));
}
