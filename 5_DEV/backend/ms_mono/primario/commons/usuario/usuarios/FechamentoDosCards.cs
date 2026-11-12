using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

namespace Boaz.MsMono.Primario.Commons.Usuario.Usuarios;

// O agendamento do fechamento da 00:00 de Brasília (15 RN-278), em segundo
// plano: confere a cada 30 s, pelo relógio da aplicação, se o dia de Brasília
// virou e, quando vira, roda o caso de uso (que pega a trava do dia: uma
// réplica só fecha). Falha vai para o log e a próxima virada tenta de novo.
public sealed class FechamentoDosCards(IFecharCardsDoDia fechar, TimeProvider relogio, ILogger<FechamentoDosCards> log) : BackgroundService
{
    static readonly TimeSpan Conferencia = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ultimo = CalendarioDoCard.Dia(relogio.GetUtcNow());
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Conferencia, relogio, stoppingToken);
                var hoje = CalendarioDoCard.Dia(relogio.GetUtcNow());
                if (hoje == ultimo) continue;
                await fechar.Executar(sobDemanda: false, stoppingToken);
                ultimo = hoje;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception erro)
            {
                log.LogWarning("Fechamento dos cards não concluído ({Erro}); tenta de novo em {Segundos} s", erro.GetType().Name,
                    Conferencia.TotalSeconds);
            }
        }
    }
}
