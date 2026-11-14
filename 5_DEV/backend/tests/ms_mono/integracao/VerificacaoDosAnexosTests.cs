using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using Microsoft.Extensions.Configuration;

namespace Boaz.MsMono.Tests.Integracao;

// A verificação dos anexos (RN-292, falha fechada; sac-inicializacao-sequencia
// v01, fase 1): o InterruptorDaVerificacao escolhe o adaptador pela
// configuração, e o VerificacaoNoClamAv fala o INSTREAM do clamd por TCP. Um
// clamd de mentira, num TcpListener local, responde como o de verdade.
public class VerificacaoDosAnexosTests
{
    // só PNG e JPG entram (18 RN-292 v06): o arquivo verificado é um JPG
    static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0xFF, 0xD9];

    static IConfiguration Configuracao(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.Select(v => KeyValuePair.Create(v.Chave, (string?)v.Valor))).Build();

    [Theory]
    [InlineData("127.0.0.1:3310", false, nameof(VerificacaoNoClamAv))]
    [InlineData("127.0.0.1:3310", true, nameof(VerificacaoNoClamAv))]
    [InlineData("", true, nameof(VerificacaoLocal))]
    [InlineData("", false, nameof(SemVerificacao))]
    public void RN_292_o_interruptor_escolhe_pela_configuracao(string clamAv, bool emHomologacao, string adaptador)
    {
        var escolhido = InterruptorDaVerificacao.Escolher(Configuracao(("Sac:ClamAv", clamAv)), emHomologacao);

        Assert.Equal(adaptador, escolhido.GetType().Name);
    }

    // o dublê também vale fora de homologação com Sac:VerificacaoLocal=sim (os testes do backend)
    [Fact]
    public void RN_292_verificacao_local_ligada_pela_configuracao()
    {
        var escolhido = InterruptorDaVerificacao.Escolher(Configuracao(("Sac:VerificacaoLocal", "sim")), false);

        Assert.IsType<VerificacaoLocal>(escolhido);
    }

    // sem verificação configurada fora de homologação, nenhum arquivo entra
    [Fact]
    public async Task RN_292_sem_verificacao_a_falha_e_fechada()
    {
        Assert.Equal(ResultadoDaVerificacao.Indisponivel, await new SemVerificacao().Verificar(Jpg));
    }

    // o interruptor do qa (PUT /_teste/sac/verificacao) derruba e devolve a verificação
    [Fact]
    public async Task RN_292_interruptor_derruba_e_devolve_a_verificacao()
    {
        var interruptor = new InterruptorDaVerificacao(new VerificacaoLocal());

        interruptor.Disponivel = false;
        var fora = await interruptor.Verificar(Jpg);
        interruptor.Disponivel = true;
        var deVolta = await interruptor.Verificar(Jpg);

        Assert.Equal((ResultadoDaVerificacao.Indisponivel, ResultadoDaVerificacao.Limpo), (fora, deVolta));
    }

    // "stream: OK" é limpo, "... FOUND" é ameaça, qualquer outra resposta é indisponível
    [Theory]
    [InlineData("stream: OK", ResultadoDaVerificacao.Limpo)]
    [InlineData("stream: Win.Test.EICAR_HDB-1 FOUND", ResultadoDaVerificacao.Ameaca)]
    [InlineData("INSTREAM size limit exceeded. ERROR", ResultadoDaVerificacao.Indisponivel)]
    public async Task RN_292_clamd_pelo_instream(string resposta, ResultadoDaVerificacao esperado)
    {
        using var clamd = new ClamdDeMentira(resposta);

        var resultado = await new VerificacaoNoClamAv(clamd.Endereco).Verificar(Jpg);

        Assert.Equal(esperado, resultado);
        Assert.Equal(Jpg, await clamd.Recebido);
    }

    // clamd fora do ar: indisponível, sem esperar
    [Fact]
    public async Task RN_292_clamd_fora_do_ar_e_indisponivel()
    {
        var livre = new TcpListener(IPAddress.Loopback, 0);
        livre.Start();
        var porta = ((IPEndPoint)livre.LocalEndpoint).Port;
        livre.Stop();

        var resultado = await new VerificacaoNoClamAv($"127.0.0.1:{porta}").Verificar(Jpg);

        Assert.Equal(ResultadoDaVerificacao.Indisponivel, resultado);
    }

    // clamd que não responde: indisponível em até 10 s (sac-inicializacao-sequencia, passo 7)
    [Fact]
    public async Task RN_292_clamd_mudo_e_indisponivel_em_ate_10_s()
    {
        using var clamd = new ClamdDeMentira(null);
        var relogio = Stopwatch.StartNew();

        var resultado = await new VerificacaoNoClamAv(clamd.Endereco).Verificar(Jpg);

        Assert.Equal(ResultadoDaVerificacao.Indisponivel, resultado);
        Assert.InRange(relogio.Elapsed, TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(12));
    }

    // Um clamd de uma conexão só: lê "zINSTREAM\0" e os blocos (4 bytes
    // big-endian com o tamanho, o último vazio), responde e fecha. Sem
    // resposta, segura a conexão aberta até o fim do teste.
    sealed class ClamdDeMentira : IDisposable
    {
        readonly TcpListener ouvinte = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource fim = new();

        public ClamdDeMentira(string? resposta)
        {
            ouvinte.Start();
            Recebido = Atender(resposta);
        }

        public string Endereco => $"127.0.0.1:{((IPEndPoint)ouvinte.LocalEndpoint).Port}";

        public Task<byte[]> Recebido { get; }

        async Task<byte[]> Atender(string? resposta)
        {
            using var cliente = await ouvinte.AcceptTcpClientAsync(fim.Token);
            await using var canal = cliente.GetStream();
            var comando = new byte[10];
            await canal.ReadExactlyAsync(comando, fim.Token);
            Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(comando));
            using var conteudo = new MemoryStream();
            var tamanho = new byte[4];
            while (true)
            {
                await canal.ReadExactlyAsync(tamanho, fim.Token);
                var bloco = BinaryPrimitives.ReadInt32BigEndian(tamanho);
                if (bloco == 0) break;
                var parte = new byte[bloco];
                await canal.ReadExactlyAsync(parte, fim.Token);
                conteudo.Write(parte);
            }
            if (resposta is null)
            {
                await Task.Delay(Timeout.Infinite, fim.Token).ContinueWith(_ => { }, TaskScheduler.Default);
                return conteudo.ToArray();
            }
            await canal.WriteAsync(Encoding.ASCII.GetBytes(resposta + "\0"), fim.Token);
            return conteudo.ToArray();
        }

        public void Dispose()
        {
            fim.Cancel();
            ouvinte.Stop();
            fim.Dispose();
        }
    }
}
