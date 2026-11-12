using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Sistema;

/// <summary>O corpo de PUT /_teste/relogio.</summary>
/// <param name="Agora">Data e hora com o fuso.</param>
public sealed record RelogioDeTesteCorpo(string? Agora);

/// <summary>
/// O relógio de teste (contrato usuarios v15, 15 RN-278): só com AMBIENTE=hml e
/// Teste__Relogio=sim; fora disso, os endereços respondem 404, como se não
/// existissem. Sem login: a proteção é o endereço não existir fora do teste.
/// Fora da documentação da API.
/// </summary>
[ApiController]
[Route("_teste")]
[ApiExplorerSettings(IgnoreApi = true)]
[SoComRelogioDeTeste]
public sealed class TesteController(RelogioDaAplicacao relogio) : ControllerBase
{
    /// <summary>A aplicação passa a contar a partir da hora dada, no ritmo real.</summary>
    [HttpPut("relogio")]
    public IActionResult Definir([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RelogioDeTesteCorpo? corpo)
    {
        relogio.Definir(RelogioDaAplicacao.Ler(corpo?.Agora) ?? throw FalhaDeUsuarios.DadosInvalidos("agora"));
        return NoContent();
    }

    /// <summary>Volta ao relógio real.</summary>
    [HttpDelete("relogio")]
    public IActionResult Real()
    {
        relogio.Real();
        return NoContent();
    }

    /// <summary>Roda agora o fechamento da 00:00 de Brasília para o dia do relógio, mesmo que o dia já tenha sido fechado.</summary>
    [HttpPost("fechamento")]
    public async Task<IActionResult> Fechar([FromServices] IFecharCardsDoDia fechar, CancellationToken ct)
    {
        await fechar.Executar(sobDemanda: true, ct);
        return NoContent();
    }
}

// Desligado, 404 sem corpo antes de tudo (até da leitura do corpo).
[AttributeUsage(AttributeTargets.Class)]
sealed class SoComRelogioDeTesteAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!RelogioDaAplicacao.DeTesteLigado(context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()))
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
