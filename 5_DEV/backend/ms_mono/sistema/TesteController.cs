using Boaz.Core.Commons.Auditoria.Domain;
using System.Text.Json;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Secundario.Out.Commons.Usuario.Autorizacao;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Sistema;

/// <summary>O corpo de PUT /_teste/relogio.</summary>
/// <param name="Agora">Data e hora com o fuso.</param>
public sealed record RelogioDeTesteCorpo(string? Agora);

/// <summary>O corpo de PUT /_teste/sac/verificacao.</summary>
/// <param name="Disponivel">false derruba a verificação de segurança dos anexos.</param>
public sealed record VerificacaoDeTesteCorpo(bool? Disponivel);

/// <summary>O corpo de POST /_teste/contratacao (contrato autorizacao v11).</summary>
/// <param name="Conta">A conta cuja contratação muda.</param>
/// <param name="Mod">O módulo contratado ou descontratado.</param>
/// <param name="Contratado">true contrata; false descontrata.</param>
/// <param name="Por">Quem contratou ou descontratou (o "por" do registro).</param>
public sealed record ContratacaoDeTesteCorpo(string? Conta, string? Mod, JsonElement Contratado, string? Por);

/// <summary>
/// O relógio de teste (contrato usuarios v15, 15 RN-278): só com AMBIENTE=hml e
/// Teste__Relogio=sim; fora disso, os endereços respondem 404, como se não
/// existissem. Sem login: a proteção é o endereço não existir fora do teste.
/// Também o papel do Catálogo na contratação, um pedido de módulo sem endereço
/// próprio (contrato autorizacao v11, grupo TESTE) e os ganchos do SAC: o
/// agendamento na hora e a verificação dos anexos fora do ar (18_modulo-sac,
/// hub/sac do qa). Fora da documentação da API.
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

    /// <summary>
    /// O papel do Catálogo (contrato autorizacao v11; RN-285 a RN-287): grava a
    /// contratação e roda o mesmo aviso AVISAR_CONTRATACAO_MUDOU de 17 RN-275,
    /// com quem = por e a conta; responde só depois de mover em todos. O estado
    /// já gravado responde 204 sem aviso, sem subir versão nem pv.
    /// </summary>
    [HttpPost("contratacao")]
    public async Task<IActionResult> Contratacao([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ContratacaoDeTesteCorpo? corpo,
        [FromServices] ContratacoesDeTeste contratacoes, [FromServices] IModulos modulos, [FromServices] IAvisarContratacaoMudou avisar,
        CancellationToken ct)
    {
        var traceId = HttpContext.NovoTraceId();
        var conta = Preenchido(corpo?.Conta, "conta");
        var mod = Preenchido(corpo?.Mod, "mod");
        var contratado = corpo?.Contratado.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw FalhaDeUsuarios.DadosInvalidos("contratado"),
        };
        var por = Preenchido(corpo?.Por, "por");
        var sigla = await contratacoes.SiglaDaConta(conta, ct) ?? throw FalhaDeUsuarios.DadosInvalidos("conta");
        if (!(await modulos.Registrados(ct)).Any(m => m.Id == mod)) throw FalhaDeUsuarios.DadosInvalidos("mod");

        if (await contratacoes.Mudar(conta, sigla, mod, contratado, relogio.GetUtcNow(), ct))
        {
            await avisar.Executar(conta, new ExecucaoDeQuemChama(traceId, por, conta), ct);
        }
        return NoContent();
    }

    /// <summary>
    /// Roda agora, com o relógio da aplicação, o que o AgendamentoDoSac faz a
    /// cada 1 min (18 RN-290, RN-292): os 7 dias e o descarte dos anexos; só
    /// responde ao terminar.
    /// </summary>
    [HttpPost("sac/agendamento")]
    public async Task<IActionResult> AgendamentoDoSac(CancellationToken ct)
    {
        await Primario.In.Hub.Sac.AgendamentoDoSac.Rotinas(HttpContext.RequestServices, ct);
        return NoContent();
    }

    /// <summary>A verificação de segurança dos anexos fora do ar (false) ou de volta (true): 503 VERIFICACAO_INDISPONIVEL (RN-292).</summary>
    [HttpPut("sac/verificacao")]
    public IActionResult Verificacao([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VerificacaoDeTesteCorpo? corpo,
        [FromServices] InterruptorDaVerificacao interruptor)
    {
        interruptor.Disponivel = corpo?.Disponivel ?? throw FalhaDeUsuarios.DadosInvalidos("disponivel");
        return NoContent();
    }

    /// <summary>A verificação de segurança dos anexos volta ao normal.</summary>
    [HttpDelete("sac/verificacao")]
    public IActionResult VerificacaoNormal([FromServices] InterruptorDaVerificacao interruptor)
    {
        interruptor.Disponivel = true;
        return NoContent();
    }

    /// <summary>
    /// Um pedido de módulo sem endereço próprio (contrato autorizacao v11, RN-285):
    /// exige {mod} · U pela mesma exigência de rota de um módulo real.
    /// </summary>
    [HttpGet("modulo/{mod}")]
    [ComAcesso("{mod}", nameof(Abas.U))]
    public IActionResult Modulo() => NoContent();

    static string Preenchido(string? valor, string campo) =>
        string.IsNullOrWhiteSpace(valor) ? throw FalhaDeUsuarios.DadosInvalidos(campo) : valor;
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
