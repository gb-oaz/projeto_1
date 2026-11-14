using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Boaz.MsMono.Tests.Integracao;

// Os adaptadores do SAC no Redis de verdade (sac-documentos v02,
// sac-inicializacao-sequencia v01): o canal sac:chamado:<id> da conversa ao
// vivo (RNF-230: PUBLISH com a versão e a espera acordada pela assinatura
// sac:chamado:*) e a trava sac:prazos do agendamento (SET NX PX 55 s, uma
// réplica por minuto). Os números dos chamados ficam entre 900000 e 999999,
// longe dos que os outros testes usam, porque o canal do Redis é de todos.
public class SacNoRedisTests(Backend backend) : IClassFixture<Backend>
{
    ICanalDoChamado Canal => backend.Services.GetRequiredService<ICanalDoChamado>();

    Armazenamento Armazenamento => backend.Services.GetRequiredService<Armazenamento>();

    static NumeroDoChamado Numero() => new(Random.Shared.NextInt64(900_000, 1_000_000));

    // a primeira espera faz a assinatura da réplica; daí em diante a espera
    // registrada antes do PUBLISH é acordada por ele
    async Task Assinado()
    {
        _ = backend.Server;
        await Canal.Esperar(Numero(), TimeSpan.FromMilliseconds(10));
    }

    // RNF-230: a mudança de um chamado acorda quem espera por ele, com a versão no canal
    [Fact]
    public async Task RNF_230_publicar_acorda_a_espera_do_chamado()
    {
        await Assinado();
        var id = Numero();
        var recebido = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var outro = Armazenamento.Mensagens;
        var canal = RedisChannel.Literal($"sac:chamado:{id.Valor}");
        await outro.SubscribeAsync(canal, (_, versao) => recebido.TrySetResult(versao.ToString()));
        try
        {
            var espera = Canal.Esperar(id, TimeSpan.FromSeconds(5));
            await Canal.Publicar(id, 7);

            Assert.True(await espera, "a espera não acordou com o PUBLISH do chamado");
            Assert.Equal("7", await recebido.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await outro.UnsubscribeAsync(canal);
        }
    }

    // RNF-230: sem aviso, a espera termina no tempo pedido e diz que nada mudou;
    // o aviso de outro chamado não a acorda
    [Fact]
    public async Task RNF_230_sem_aviso_a_espera_termina_no_tempo()
    {
        await Assinado();
        var id = Numero();
        var outroChamado = new NumeroDoChamado(id.Numero == 999_999 ? 900_000 : id.Numero + 1);

        var espera = Canal.Esperar(id, TimeSpan.FromMilliseconds(500));
        await Canal.Publicar(outroChamado, 3);

        Assert.False(await espera);
    }

    // a trava do agendamento: com ela pega, a próxima tentativa no mesmo minuto
    // não passa, e ela some sozinha em até 55 s
    [Fact]
    public async Task Trava_do_agendamento_uma_replica_por_minuto()
    {
        _ = backend.Server;
        var trava = backend.Services.GetRequiredService<TravaDoAgendamentoNoRedis>();

        await trava.Pegar();
        var segunda = await trava.Pegar();

        Assert.False(segunda);
        var validade = await Armazenamento.Cache.KeyTimeToLiveAsync("sac:prazos");
        Assert.NotNull(validade);
        Assert.InRange(validade.Value, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(55));
    }
}
