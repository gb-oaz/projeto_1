using Boaz.Core.Hub.Auditoria.Card.Ports.In;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.UseCases;
using Boaz.MsMono.Secundario.Out.Hub.Auditoria;
using Boaz.MsMono.Sistema;

namespace Boaz.MsMono.Primario.In.Hub.Auditoria;

// O AgendamentoDaAuditoria, em segundo plano, pelo relógio da aplicação (auditoria-classes v03):
//  - a cada 30 s, cada instância verifica os serviços e os módulos e renova o sinal dela (RNF-246); só a que pega a trava guarda o
//    histórico, as quedas e as ocorrências;
//  - a cada 1 min, com a trava auditoria:agendamento (uma instância só por vez), grava as medições dos minutos fechados, fecha o card
//    do dia e, a cada hora, descarta o que passou da guarda (RNF-247, RN-322, RNF-248).
// Falha vai para o log e a rodada seguinte tenta de novo. Com o relógio de teste ligado, os ganchos de /_teste/auditoria fazem tudo na
// hora; o agendamento só verifica enquanto alguém acompanha a saúde ao vivo, para uma verificação a mais não desfazer um cenário.
public sealed class AgendamentoDaAuditoria(IServiceProvider servicos, ITravaDaAuditoria trava, TimeProvider relogio, IConfiguration configuracao,
    ObservadoresDaSaude observadores, ILogger<AgendamentoDaAuditoria> log) : BackgroundService
{
    static readonly TimeSpan Passo = TimeSpan.FromSeconds(30);
    static readonly TimeSpan PrazoDaTrava = TimeSpan.FromSeconds(55);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var deTeste = RelogioDaAplicacao.DeTesteLigado(configuracao);
        var rodada = 0;
        var horaDoDescarte = relogio.GetUtcNow().Hour;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!deTeste || observadores.Quantos > 0) await servicos.GetRequiredService<IVerificarServicos>().Executar(false, stoppingToken);
                if (!deTeste && rodada++ % 2 == 0 && await trava.Tentar(TravaDaAuditoriaNoRedis.DoMinuto, PrazoDaTrava, stoppingToken))
                {
                    await servicos.GetRequiredService<IGravarMedicoes>().Executar(false, stoppingToken);
                    var hora = relogio.GetUtcNow().Hour;
                    await servicos.GetRequiredService<IFecharCardEDescartar>().Executar(hora != horaDoDescarte, stoppingToken);
                    horaDoDescarte = hora;
                }
                await Task.Delay(Passo, relogio, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception erro)
            {
                log.LogWarning("Agendamento da Auditoria não concluído ({Erro}); tenta de novo em 30 s", erro.GetType().Name);
                await Espera(stoppingToken);
            }
        }
    }

    // o que as medições ainda na memória viram na parada: gravadas, minuto de agora inclusive
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await servicos.GetRequiredService<IGravarMedicoes>().Executar(true, CancellationToken.None);
        }
        catch (Exception)
        {
            // sem o banco, o que estava na memória se perde: são só medições
        }
    }

    async Task Espera(CancellationToken parar)
    {
        try
        {
            await Task.Delay(Passo, relogio, parar);
        }
        catch (OperationCanceledException)
        {
            // a aplicação parou
        }
    }
}
