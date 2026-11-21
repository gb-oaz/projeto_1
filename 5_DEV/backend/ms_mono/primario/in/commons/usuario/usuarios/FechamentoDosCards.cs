using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Sistema;

namespace Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;

// O agendamento do fechamento da 00:00 de Brasília (15 RN-278), em segundo
// plano: confere a cada 30 s, pelo relógio da aplicação, se o dia de Brasília
// virou e, quando vira, roda o caso de uso (que pega a trava do dia: uma
// réplica só fecha). Falha vai para o log e a próxima virada tenta de novo.
public sealed class FechamentoDosCards(IFecharCardsDoDia fechar, TimeProvider relogio, RelogioDaAplicacao aplicacao, ILogger<FechamentoDosCards> log)
    : BackgroundService
{
    static readonly TimeSpan Conferencia = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var vigia = new VigiaDoDia(relogio, () => aplicacao.Ajustes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Conferencia, relogio, stoppingToken);
                if (vigia.Virou() is not { } hoje) continue;
                await fechar.Executar(sobDemanda: false, stoppingToken);
                vigia.Fechado(hoje);
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

// O dia de Brasília já fechado e a conferência de que ele virou. Quando o relógio de teste é trocado (PUT ou DELETE /_teste/relogio, o
// que a suíte do qa faz dezenas de vezes), a referência passa para o dia do novo relógio: o salto não fecha nada, só a virada de
// verdade do dia ou o POST /_teste/fechamento.
public sealed class VigiaDoDia(TimeProvider relogio, Func<long> ajustes)
{
    DateOnly ultimo = CalendarioDoCard.Dia(relogio.GetUtcNow());
    long vistos = ajustes();

    /// <summary>O dia novo, se o dia de Brasília virou desde o último fechado; null se não virou ou se foi só um salto do relógio de teste.</summary>
    public DateOnly? Virou()
    {
        var hoje = CalendarioDoCard.Dia(relogio.GetUtcNow());
        var agora = ajustes();
        if (agora != vistos)
        {
            (vistos, ultimo) = (agora, hoje);
            return null;
        }
        return hoje == ultimo ? null : hoje;
    }

    /// <summary>O fechamento do dia deu certo.</summary>
    public void Fechado(DateOnly dia) => ultimo = dia;
}
