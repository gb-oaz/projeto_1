using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;

namespace Boaz.Core.Hub.Pagamento.Faturas.UseCases;

/// <summary>
/// A taxa de juros que vale para uma fatura: a da Configuração vigente no fim
/// do dia do vencimento (RN-310). Mudar a taxa depois não muda as que já venceram.
/// Guarda as já lidas, para a lista não reler a mesma versão.
/// </summary>
public sealed class TaxaDoVencimento(IConfiguracoesDoPagamento configuracoes)
{
    readonly Dictionary<DateOnly, decimal> lidas = [];

    /// <summary>A taxa ao dia, em %, da fatura que vence no dia dado.</summary>
    public async Task<decimal> Do(DateOnly vencimento, CancellationToken ct)
    {
        if (lidas.TryGetValue(vencimento, out var taxa)) return taxa;
        var fimDoDia = Calendario.InicioDoDia(vencimento.AddDays(1)).AddTicks(-1);
        taxa = ((await configuracoes.VigenteEm(fimDoDia, ct)) ?? ConfiguracaoDoPagamento.Padrao).JurosAoDia;
        lidas[vencimento] = taxa;
        return taxa;
    }

    /// <summary>Os juros de hoje da fatura; null se ela não está vencida ou a taxa é 0.</summary>
    public async Task<JurosDaFatura?> Juros(Fatura fatura, DateOnly hoje, CancellationToken ct) =>
        fatura.Status == StatusDaFatura.Vencida ? fatura.Juros(hoje, await Do(fatura.Vencimento, ct)) : null;
}

/// <summary>A configuração em vigor agora: a última salva, ou a padrão (RF-193).</summary>
public static class ConfiguracaoAtual
{
    /// <summary>A última versão salva, ou a padrão.</summary>
    public static async Task<ConfiguracaoDoPagamento> Ler(IConfiguracoesDoPagamento configuracoes, CancellationToken ct) =>
        await configuracoes.Atual(ct) ?? ConfiguracaoDoPagamento.Padrao;
}
