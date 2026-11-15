using Boaz.Core.Hub.Pagamento.Faturas.Ports.In;
using Boaz.MsMono.Secundario.Out.Hub.Pagamento;
using Boaz.MsMono.Sistema;

namespace Boaz.MsMono.Primario.In.Hub.Pagamento;

// O AgendamentoDoPagamento, em segundo plano, a cada 1 min pelo relógio da
// aplicação, com a trava pagamento:agendamento (uma réplica só por vez): o
// fechamento da competência no dia 1 (RN-300), a virada para "VENCIDA" e o
// aviso de atraso (RN-301, RN-311) e a expiração e a consulta dos pagamentos
// em aberto (RN-302). Falha vai para o log e o minuto seguinte tenta de novo.
// Com o relógio de teste ligado, não roda sozinho: o qa o chama na hora
// certa (POST /_teste/pagamento/agendamento), para o resultado não depender
// de quando o minuto cai.
public sealed class AgendamentoDoPagamento(IServiceProvider servicos, TravaDoAgendamentoDoPagamentoNoRedis trava, TimeProvider relogio,
    IConfiguration configuracao, ILogger<AgendamentoDoPagamento> log) : BackgroundService
{
    static readonly TimeSpan Passo = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (RelogioDaAplicacao.DeTesteLigado(configuracao)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Passo, relogio, stoppingToken);
                if (!await trava.Pegar(stoppingToken)) continue;
                await servicos.GetRequiredService<IRodarAgendamentoDoPagamento>().Executar(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception erro)
            {
                log.LogWarning("Agendamento do Pagamento não concluído ({Erro}); tenta de novo em 1 min", erro.GetType().Name);
            }
        }
    }
}
