using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// A verificação de segurança dos anexos (RN-292), falha fechada. O adaptador
// de verdade é o clamd (VerificacaoNoClamAv), em Sac:ClamAv ("host:porta").
// Sem ele: em homologação (e nos testes do backend, com Sac:VerificacaoLocal=sim)
// a VerificacaoLocal, que acha a assinatura de teste EICAR como o ClamAV;
// fora disso, nenhum arquivo entra (Indisponivel). O interruptor deixa o
// relógio de teste derrubar a verificação (PUT /_teste/sac/verificacao).
public sealed class InterruptorDaVerificacao(IVerificacaoDeArquivos verificacao) : IVerificacaoDeArquivos
{
    volatile bool disponivel = true;

    public bool Disponivel
    {
        get => disponivel;
        set => disponivel = value;
    }

    public Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default) =>
        disponivel ? verificacao.Verificar(conteudo, ct) : Task.FromResult(ResultadoDaVerificacao.Indisponivel);

    // o adaptador pela configuração
    public static IVerificacaoDeArquivos Escolher(IConfiguration configuracao, bool emHomologacao) =>
        configuracao["Sac:ClamAv"] is { Length: > 0 } endereco ? new VerificacaoNoClamAv(endereco)
        : emHomologacao || string.Equals(configuracao["Sac:VerificacaoLocal"], "sim", StringComparison.OrdinalIgnoreCase)
            ? new VerificacaoLocal()
            : new SemVerificacao();
}

// Sem verificação configurada fora de homologação: falha fechada.
public sealed class SemVerificacao : IVerificacaoDeArquivos
{
    public Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default) =>
        Task.FromResult(ResultadoDaVerificacao.Indisponivel);
}

// O dublê de homologação: ameaça é o arquivo com a assinatura de teste EICAR.
public sealed class VerificacaoLocal : IVerificacaoDeArquivos
{
    // montada em partes, para o código-fonte não conter a assinatura inteira
    static readonly byte[] Eicar = Encoding.ASCII.GetBytes(string.Concat(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$", "EICAR-STANDARD-",
        "ANTIVIRUS-TEST-FILE!$H+H*"));

    public Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default) =>
        Task.FromResult(conteudo.AsSpan().IndexOf(Eicar) >= 0 ? ResultadoDaVerificacao.Ameaca : ResultadoDaVerificacao.Limpo);
}

// O clamd pelo INSTREAM (blocos com o tamanho em 4 bytes big-endian e um bloco
// vazio no fim): "stream: OK" é limpo, "... FOUND" é ameaça; qualquer outra
// resposta, erro ou demora de mais de 10 s é indisponível.
public sealed class VerificacaoNoClamAv(string endereco) : IVerificacaoDeArquivos
{
    static readonly TimeSpan Limite = TimeSpan.FromSeconds(10);
    const int Bloco = 64 * 1024;

    public async Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default)
    {
        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(Limite);
        try
        {
            var (host, porta) = Endereco();
            using var cliente = new TcpClient();
            await cliente.ConnectAsync(host, porta, tempo.Token);
            await using var canal = cliente.GetStream();
            await canal.WriteAsync("zINSTREAM\0"u8.ToArray(), tempo.Token);
            var tamanho = new byte[4];
            for (var i = 0; i < conteudo.Length; i += Bloco)
            {
                var parte = conteudo.AsMemory(i, Math.Min(Bloco, conteudo.Length - i));
                BinaryPrimitives.WriteInt32BigEndian(tamanho, parte.Length);
                await canal.WriteAsync(tamanho, tempo.Token);
                await canal.WriteAsync(parte, tempo.Token);
            }
            BinaryPrimitives.WriteInt32BigEndian(tamanho, 0);
            await canal.WriteAsync(tamanho, tempo.Token);
            using var leitor = new StreamReader(canal, Encoding.ASCII);
            var resposta = (await leitor.ReadToEndAsync(tempo.Token)).TrimEnd('\0', '\n');
            return resposta.EndsWith("OK", StringComparison.Ordinal) ? ResultadoDaVerificacao.Limpo
                : resposta.EndsWith("FOUND", StringComparison.Ordinal) ? ResultadoDaVerificacao.Ameaca
                : ResultadoDaVerificacao.Indisponivel;
        }
        catch (Exception erro) when (erro is SocketException or IOException or OperationCanceledException or FormatException)
        {
            if (ct.IsCancellationRequested) throw;
            return ResultadoDaVerificacao.Indisponivel;
        }
    }

    (string Host, int Porta) Endereco()
    {
        var partes = endereco.Split(':');
        return (partes[0], partes.Length > 1 ? int.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture) : 3310);
    }
}
