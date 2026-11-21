using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Ports.In;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.In;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Dtos;
using Boaz.Core.Hub.Auditoria.Saude.Ports.In;
using Boaz.MsMono.Secundario.Out.Hub.Auditoria;
using Boaz.MsMono.Sistema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

/// <summary>
/// Os ganchos de teste da Auditoria (contrato auditoria v04, 20 RN-315 a RN-318, RN-322, RN-324, RNF-247): só com AMBIENTE=hml e
/// Teste__Relogio=sim, como /_teste/relogio; fora disso, 404 sem corpo, antes de tudo. Sem login: a proteção é o endereço não existir
/// fora do teste. Trocam as sondas, verificam agora, injetam medições e registros e rodam o card do minuto e o descarte. Fora da
/// documentação da API.
/// </summary>
[ApiController]
[Route("_teste/auditoria")]
[ApiExplorerSettings(IgnoreApi = true)]
[SoComRelogioDeTeste]
public sealed partial class GanchosDaAuditoria(TimeProvider relogio) : ControllerBase
{
    const int TempoMaximoDaSondaMs = 3000;

    /// <summary>O que o serviço "responde" na próxima verificação (NORMAL, LENTA, NENHUMA, RECUSADA ou ERRO), até o DELETE.</summary>
    [HttpPut("sondas/{servico}")]
    public IActionResult DefinirSonda(string servico, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonNode? corpo, [FromServices] SondasDeTeste sondas)
    {
        var s = Servico.DeId(servico);
        if (corpo is not JsonObject o || Texto(o, "resposta") is not { } resposta || !RespostaDaSondaDeTeste.Respostas.Contains(resposta))
        {
            throw FalhaDaAuditoria.DadosInvalidos("resposta");
        }
        var tempo = Inteiro(o, "tempoMs", 0, TempoMaximoDaSondaMs);
        var ultima = o["ultimaExecucao"] is null ? null : Periodo.Ler(Texto(o, "ultimaExecucao") ?? "", "ultimaExecucao");
        sondas.Definir(s, new RespostaDaSondaDeTeste(resposta, tempo, ultima));
        return NoContent();
    }

    /// <summary>A sonda do serviço volta à verificação real.</summary>
    [HttpDelete("sondas/{servico}")]
    public IActionResult RemoverSonda(string servico, [FromServices] SondasDeTeste sondas)
    {
        sondas.Remover(Servico.DeId(servico));
        return NoContent();
    }

    /// <summary>Verifica agora, como a verificação de 30 s, e guarda o histórico, as quedas e as ocorrências.</summary>
    [HttpPost("verificacao")]
    public async Task<IActionResult> Verificar([FromServices] IVerificarServicos verificar, CancellationToken ct) =>
        Ok(SaudeDto.De(await verificar.Executar(true, ct)));

    /// <summary>Soma pedidos medidos direto em medicoesMinuto e medicoesHora: a verificação seguinte já os usa.</summary>
    [HttpPost("medicoes")]
    public async Task<IActionResult> Medir([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonNode? corpo, [FromServices] IMedicoes medicoes,
        CancellationToken ct)
    {
        if (corpo is not JsonArray { Count: > 0 } itens) throw FalhaDaAuditoria.DadosInvalidos("medicoes");
        var medidas = new List<Medicao>();
        foreach (var item in itens)
        {
            if (item is not JsonObject o) throw FalhaDaAuditoria.DadosInvalidos("medicoes");
            var metodo = Texto(o, "metodo")?.ToUpperInvariant();
            if (metodo is not { Length: > 0 }) throw FalhaDaAuditoria.DadosInvalidos("metodo");
            var endereco = Texto(o, "endereco");
            if (endereco is null || !endereco.StartsWith('/')) throw FalhaDaAuditoria.DadosInvalidos("endereco");
            var status = Inteiro(o, "status", 100, 599) ?? throw FalhaDaAuditoria.DadosInvalidos("status");
            var duracao = Inteiro(o, "duracaoMs", 0, int.MaxValue) ?? throw FalhaDaAuditoria.DadosInvalidos("duracaoMs");
            var quantidade = Inteiro(o, "quantidade", 1, 1_000_000) ?? 1;
            var minuto = Janelas.Minuto(o["minuto"] is null ? relogio.GetUtcNow() : Periodo.Ler(Texto(o, "minuto") ?? "", "minuto")!.Value);
            var medicao = new Medicao(minuto, metodo, endereco, ModuloDoEndereco.De(endereco));
            medicao.Somar(status, duracao, quantidade);
            medidas.Add(medicao);
        }
        await medicoes.Somar(medidas, ct);
        return NoContent();
    }

    /// <summary>Grava agora o que os pedidos de verdade juntaram na memória, minuto de agora inclusive.</summary>
    [HttpPost("medicoes/gravar")]
    public async Task<IActionResult> GravarMedicoes([FromServices] IGravarMedicoes gravar, CancellationToken ct)
    {
        await gravar.Executar(true, ct);
        return NoContent();
    }

    /// <summary>Grava registros de auditoria ou de erro, com qualquer ação, e avisa o ao vivo (201).</summary>
    [HttpPost("registros")]
    public async Task<IActionResult> Registrar([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonNode? corpo, [FromServices] INomesDaAuditoria nomes,
        [FromServices] RegistrosDeTeste registros, CancellationToken ct)
    {
        if (corpo is not JsonArray { Count: > 0 } itens) throw FalhaDaAuditoria.DadosInvalidos("registros");
        var lidos = new List<RegistroBruto>();
        foreach (var item in itens)
        {
            if (item is not JsonObject o) throw FalhaDaAuditoria.DadosInvalidos("registros");
            lidos.Add(await Registro(o, nomes, ct));
        }
        foreach (var registro in lidos) await registros.Gravar(registro, ct);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Roda agora o fechamento do minuto do card e o descarte do que passou da guarda.</summary>
    [HttpPost("agendamento")]
    public async Task<IActionResult> Agendamento([FromServices] IFecharCardEDescartar fechar, CancellationToken ct)
    {
        await fechar.Executar(true, ct);
        return NoContent();
    }

    async Task<RegistroBruto> Registro(JsonObject o, INomesDaAuditoria nomes, CancellationToken ct)
    {
        var traceId = Texto(o, "traceId");
        if (traceId is null || !TraceIdValido().IsMatch(traceId)) throw FalhaDaAuditoria.DadosInvalidos("traceId");
        var acao = Texto(o, "acao");
        if (acao is null || !AcaoValida().IsMatch(acao)) throw FalhaDaAuditoria.DadosInvalidos("acao");
        var tipo = Texto(o, "tipo") ?? (o["nivel"] is not null ? "ERRO" : "AUDITORIA");
        if (tipo is not ("AUDITORIA" or "ERRO")) throw FalhaDaAuditoria.DadosInvalidos("tipo");
        var erro = tipo == "ERRO";
        var etapa = erro ? null : Texto(o, "etapa");
        var nivel = erro ? Texto(o, "nivel") : null;
        if (!erro && etapa is not ("INICIADO" or "CONCLUIDO")) throw FalhaDaAuditoria.DadosInvalidos("etapa");
        if (erro && nivel is not ("ALERTA" or "ERRO")) throw FalhaDaAuditoria.DadosInvalidos("nivel");
        var quem = Texto(o, "quem");
        if (quem is not null && !(await nomes.Usuarios([quem], ct)).ContainsKey(quem)) throw FalhaDaAuditoria.DadosInvalidos("quem");
        var conta = Texto(o, "conta");
        if (conta is not null && !(await nomes.Contas([conta], ct)).ContainsKey(conta)) throw FalhaDaAuditoria.DadosInvalidos("conta");
        var quando = o["quando"] is null ? relogio.GetUtcNow() : Periodo.Ler(Texto(o, "quando") ?? "", "quando")!.Value;
        return new RegistroBruto(Guid.NewGuid().ToString("N"), erro ? TipoDoRegistro.Erro : TipoDoRegistro.Auditoria, traceId, etapa, nivel,
            erro ? Texto(o, "codigo") : null, acao, quem, conta, Texto(o, "alvo"), quando);
    }

    // o campo como texto; null se não veio ou veio null; outro tipo de valor é DADOS_INVALIDOS no campo
    static string? Texto(JsonObject o, string campo)
    {
        if (o[campo] is not { } valor) return null;
        return valor is JsonValue v && v.TryGetValue<string>(out var texto) ? texto : throw FalhaDaAuditoria.DadosInvalidos(campo);
    }

    // o campo como inteiro entre o mínimo e o máximo; null se não veio; fora disso, DADOS_INVALIDOS no campo
    static int? Inteiro(JsonObject o, string campo, int minimo, int maximo)
    {
        if (o[campo] is not { } valor) return null;
        if (valor is JsonValue v && v.TryGetValue<int>(out var n) && n >= minimo && n <= maximo) return n;
        throw FalhaDaAuditoria.DadosInvalidos(campo);
    }

    [GeneratedRegex("^trc-[0-9a-f]{8}$")]
    private static partial Regex TraceIdValido();

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$")]
    private static partial Regex AcaoValida();
}
