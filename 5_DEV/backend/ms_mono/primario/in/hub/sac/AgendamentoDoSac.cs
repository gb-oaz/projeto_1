using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.MsMono.Secundario.Out.Hub.Sac;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

// O agendamento do SAC, em segundo plano, a cada 1 min pelo relógio da
// aplicação, com a trava sac:prazos (uma réplica só por vez): os 7 dias de
// Aguardando cliente e de Resolvido (RN-290), o descarte dos anexos de
// expiraEm vencido, no Blob ou no GridFS (RN-292, RNF-234) e, quando o dia de
// Brasília vira, o fechamento do card
// (15 RN-278). Falha vai para o log e o minuto seguinte tenta de novo. O
// relógio de teste roda as rotinas na hora (POST /_teste/sac/agendamento).
public sealed class AgendamentoDoSac(IServiceProvider servicos, TravaDoAgendamentoNoRedis trava, TimeProvider relogio,
    ILogger<AgendamentoDoSac> log) : BackgroundService
{
    static readonly TimeSpan Passo = TimeSpan.FromMinutes(1);

    // os prazos e o descarte, agora
    public static async Task Rotinas(IServiceProvider servicos, CancellationToken ct)
    {
        await servicos.GetRequiredService<IAplicarPrazos>().Executar(ct);
        await servicos.GetRequiredService<IDescartarAnexosVencidos>().Executar(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dia = CalendarioDoCard.Dia(relogio.GetUtcNow());
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Passo, relogio, stoppingToken);
                if (!await trava.Pegar(stoppingToken)) continue;
                await Rotinas(servicos, stoppingToken);
                var hoje = CalendarioDoCard.Dia(relogio.GetUtcNow());
                if (hoje == dia) continue;
                await servicos.GetRequiredService<IFecharCardDoSacDoDia>().Executar(dia, stoppingToken);
                dia = hoje;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception erro)
            {
                log.LogWarning("Agendamento do SAC não concluído ({Erro}); tenta de novo em 1 min", erro.GetType().Name);
            }
        }
    }
}
