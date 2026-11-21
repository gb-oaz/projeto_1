using System.Globalization;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Dtos;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.In;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Metricas.UseCases;

/// <summary>
/// GET /auditoria/metricas (RF-197, RN-317): o resumo e a série de pedidos da janela, de todos os módulos ou de um. A janela de
/// 1 h vem de medicoesMinuto e as outras de medicoesHora, somadas no banco. Sem o banco, 503 (RNF-252).
/// </summary>
public sealed class VerMetricas(IMedicoes medicoes, IGravarMedicoes gravar, IUltimaVerificacao ultima, IRegistrarConsulta registrar, TimeProvider relogio)
    : IVerMetricas
{
    /// <inheritdoc />
    public async Task<MetricasDto> Executar(string? janela, string? modulo, ConsultaPedida consulta, CancellationToken ct = default)
    {
        var j = Janelas.De(janela ?? "1h") ?? throw FalhaDaAuditoria.DadosInvalidos("janela");
        if (modulo is not null && !ModuloDoEndereco.Valido(modulo)) throw FalhaDaAuditoria.DadosInvalidos("modulo");
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        await gravar.GravarOQueEsperaAntesDeLer(ct);
        var (de, ate) = j.Limites(relogio.GetUtcNow());
        var resumo = await medicoes.Resumo(j, de, ate, modulo, ct);
        var pontos = (await medicoes.Serie(j, de, ate, modulo, ct)).ToDictionary(p => p.Inicio, p => p.Resumo);
        var serie = Enumerable.Range(0, j.Pontos()).Select(i =>
        {
            var inicio = de + (j.Passo() * i);
            var r = pontos.GetValueOrDefault(inicio, ResumoDeMedicoes.Vazio);
            return new PontoDto(inicio, r.Pedidos, r.P95Ms, r.Erros5xx);
        }).ToList();
        var minutos = (decimal)(ate - de).TotalMinutes;
        var porMinuto = minutos <= 0 ? 0 : Math.Round(resumo.Pedidos / minutos, 1, MidpointRounding.AwayFromZero);

        await registrar.Executar(consulta, ct);
        return new MetricasDto(j.Texto(), modulo, de, ate,
            new ResumoDto(resumo.Pedidos, porMinuto, resumo.Tempos.Percentil(50), resumo.P95Ms, resumo.Tempos.Percentil(99), resumo.Erros4xx, resumo.Erros5xx,
                resumo.TaxaErro5xx),
            serie);
    }
}

/// <summary>
/// GET /auditoria/metricas/enderecos (RF-198): o tempo de cada endereço, sempre pelo modelo da rota, ordenado por p95 (o
/// acima de 10 s primeiro), por pedidos ou por erros 5xx e depois 4xx. Sem o banco, 503 (RNF-252).
/// </summary>
public sealed class VerTempoDosEnderecos(IMedicoes medicoes, IGravarMedicoes gravar, IUltimaVerificacao ultima, IRegistrarConsulta registrar, TimeProvider relogio)
    : IVerTempoDosEnderecos
{
    /// <summary>O máximo de endereços por pedido.</summary>
    public const int Maximo = 100;

    /// <summary>O padrão.</summary>
    public const int Padrao = 20;

    /// <inheritdoc />
    public async Task<EnderecosDto> Executar(string? janela, string? modulo, string? ordem, string? limite, ConsultaPedida consulta, CancellationToken ct = default)
    {
        var j = Janelas.De(janela ?? "1h") ?? throw FalhaDaAuditoria.DadosInvalidos("janela");
        if (modulo is not null && !ModuloDoEndereco.Valido(modulo)) throw FalhaDaAuditoria.DadosInvalidos("modulo");
        var por = ordem ?? "p95";
        if (por is not ("p95" or "pedidos" or "erros")) throw FalhaDaAuditoria.DadosInvalidos("ordem");
        var quantos = Padrao;
        if (limite is not null)
        {
            quantos = int.TryParse(limite, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= Maximo
                ? n
                : throw FalhaDaAuditoria.DadosInvalidos("limite");
        }
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        await gravar.GravarOQueEsperaAntesDeLer(ct);
        var (de, ate) = j.Limites(relogio.GetUtcNow());
        var todos = await medicoes.PorEndereco(j, de, ate, modulo, ct);
        IEnumerable<MedicaoDoEndereco> ordenados = por switch
        {
            "pedidos" => todos.OrderByDescending(m => m.Resumo.Pedidos),
            "erros" => todos.OrderByDescending(m => m.Resumo.Erros5xx).ThenByDescending(m => m.Resumo.Erros4xx),
            _ => todos.OrderByDescending(m => m.Resumo.P95Ms ?? int.MaxValue).ThenByDescending(m => m.Resumo.Pedidos),
        };
        var itens = ordenados.Take(quantos).Select(m => new EnderecoDto(
            m.Metodo, m.Endereco, m.Modulo, m.Resumo.Pedidos, m.Resumo.Tempos.Percentil(50), m.Resumo.P95Ms, m.Resumo.Tempos.Percentil(99),
            m.Resumo.Erros4xx, m.Resumo.Erros5xx, m.Resumo.Lento)).ToList();

        await registrar.Executar(consulta, ct);
        return new EnderecosDto(j.Texto(), modulo, por, itens);
    }
}

/// <summary>
/// A gravação das medições a cada 1 min (RNF-247): retira da memória os minutos fechados e soma no banco, no minuto e na hora
/// ($inc com upsert), onde a verificação dos módulos e as métricas os leem.
/// </summary>
public sealed class GravarMedicoes(AcumuladorDeMedicoes acumulador, IMedicoes medicoes, TimeProvider relogio) : IGravarMedicoes
{
    /// <inheritdoc />
    public async Task Executar(bool tudo = false, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var ate = tudo ? Janelas.Minuto(agora).AddMinutes(1) : Janelas.Minuto(agora);
        var retiradas = acumulador.Retirar(ate);
        if (retiradas.Count > 0) await medicoes.Somar(retiradas, ct);
    }
}

static class LeituraDasMedicoes
{
    // O que esta instância juntou até agora entra no banco antes de a leitura somar: quem acabou de pedir vê o pedido nas métricas.
    // Sem o banco, a leitura segue e responde o 503 dela.
    public static async Task GravarOQueEsperaAntesDeLer(this IGravarMedicoes gravar, CancellationToken ct)
    {
        try
        {
            await gravar.Executar(true, ct);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            // a leitura que vem a seguir responde pelo banco
        }
    }
}
