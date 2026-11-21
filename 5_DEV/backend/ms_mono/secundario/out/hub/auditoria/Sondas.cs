using System.Collections.Concurrent;
using System.Diagnostics;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using Boaz.MsMono.Secundario.Out.Hub.Pagamento;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// As sondas dos 8 serviços (auditoria-classes v03, RN-315): cada uma mede o tempo da resposta e diz a falha. O limite de 3 s
// (RNF-246) é do caso de uso, que corta a sonda que não responde; aqui só se diz o que se viu.
static class Medida
{
    // o tempo de uma chamada; a exceção vira a falha pelo tipo dela
    public static async Task<ResultadoDaSonda> Sondar(Func<Task> chamada)
    {
        var relogio = Stopwatch.StartNew();
        try
        {
            await chamada();
            return ResultadoDaSonda.Respondeu((int)relogio.ElapsedMilliseconds);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            return ResultadoDaSonda.Falhou(Falha(erro));
        }
    }

    static FalhaDaVerificacao Falha(Exception erro) => erro switch
    {
        MongoConnectionException or System.Net.Sockets.SocketException or StackExchange.Redis.RedisConnectionException => FalhaDaVerificacao.ConexaoRecusada,
        MongoException or TimeoutException or StackExchange.Redis.RedisTimeoutException => FalhaDaVerificacao.SemResposta,
        _ => FalhaDaVerificacao.RespostaComErro,
    };
}

// A própria aplicação: se ela verifica, está no ar.
public sealed class SondaDaAplicacao : ISondaDeServico
{
    public Servico Servico => Servico.Aplicacao;

    public Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) => Task.FromResult(ResultadoDaSonda.Respondeu(0));
}

// O banco de dados: o comando ping.
public sealed class SondaDoBanco(Armazenamento armazenamento) : ISondaDeServico
{
    public Servico Servico => Servico.Banco;

    public Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) =>
        Medida.Sondar(() => armazenamento.Banco.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct));
}

// O cache: o PING do Redis.
public sealed class SondaDoCache(Armazenamento armazenamento) : ISondaDeServico
{
    public Servico Servico => Servico.Cache;

    public Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) => Medida.Sondar(() => armazenamento.Cache.PingAsync());
}

// O armazenamento dos anexos: o Blob ou o GridFS, onde estiver o destino (Sac:Arquivos:Destino).
public sealed class SondaDoArmazenamento(ArquivosDosAnexos arquivos) : ISondaDeServico
{
    public Servico Servico => Servico.ArmazenamentoAnexos;

    public Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) => Medida.Sondar(() => arquivos.Verificar(ct));
}

// A verificação de segurança dos anexos (clamd ou o dublê de homologação): um arquivo vazio e limpo; "indisponível" é a falha.
public sealed class SondaDaVerificacao(IVerificacaoDeArquivos verificacao) : ISondaDeServico
{
    public Servico Servico => Servico.VerificacaoAnexos;

    public async Task<ResultadoDaSonda> Verificar(CancellationToken ct = default)
    {
        var relogio = Stopwatch.StartNew();
        var resultado = await verificacao.Verificar([0], ct);
        return resultado == ResultadoDaVerificacao.Indisponivel
            ? ResultadoDaSonda.Falhou(FalhaDaVerificacao.ConexaoRecusada)
            : ResultadoDaSonda.Respondeu((int)relogio.ElapsedMilliseconds);
    }
}

// O intermediador de pagamento ligado: o Mercado Pago pela API (o status e o tempo); o falso de homologação responde sempre.
public sealed class SondaDoIntermediador(IIntermediadores intermediadores) : ISondaDeServico
{
    public Servico Servico => Servico.IntermediadorPagamento;

    public async Task<ResultadoDaSonda> Verificar(CancellationToken ct = default)
    {
        if (intermediadores.Ligado is not IntermediadorMercadoPago mercadoPago) return ResultadoDaSonda.Respondeu(0);
        var (status, tempoMs) = await mercadoPago.Sondar(ct);
        return status switch
        {
            null => ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta, tempoMs),
            >= 500 => ResultadoDaSonda.Falhou(FalhaDaVerificacao.RespostaComErro, tempoMs),
            _ => ResultadoDaSonda.Respondeu(tempoMs),
        };
    }
}

// Um agendamento (SAC ou Pagamento): a hora da última rodada, que ele grava a cada uma (auditoria:ultimaExecucao:<agendamento>).
public sealed class SondaDoAgendamento(Servico servico, string agendamento, IUltimaExecucaoDosAgendamentos ultima) : ISondaDeServico
{
    public Servico Servico => servico;

    public async Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) => new(UltimaExecucao: await ultima.Ler(agendamento, ct));
}

// O que o gancho PUT /_teste/auditoria/sondas/{servico} diz que a sonda viu (NORMAL, LENTA, NENHUMA, RECUSADA ou ERRO).
public sealed record RespostaDaSondaDeTeste(string Resposta, int? TempoMs, DateTimeOffset? UltimaExecucao)
{
    public static readonly string[] Respostas = ["NORMAL", "LENTA", "NENHUMA", "RECUSADA", "ERRO"];

    // LENTA sem o tempo: 2 s, Lento e ainda dentro dos 3 s
    public const int TempoDaLenta = 2000;

    // NORMAL sem o tempo: 10 ms
    public const int TempoDaNormal = 10;

    // A última execução vai em todas as respostas: os agendamentos usam só ela e ignoram a "resposta", mesmo a de falha (RN-315)
    public ResultadoDaSonda Resultado() => Resposta switch
    {
        "NORMAL" => new ResultadoDaSonda(TempoMs ?? TempoDaNormal, UltimaExecucao: UltimaExecucao),
        "LENTA" => new ResultadoDaSonda(TempoMs ?? TempoDaLenta, UltimaExecucao: UltimaExecucao),
        "NENHUMA" => ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta) with { UltimaExecucao = UltimaExecucao },
        "RECUSADA" => ResultadoDaSonda.Falhou(FalhaDaVerificacao.ConexaoRecusada) with { UltimaExecucao = UltimaExecucao },
        _ => ResultadoDaSonda.Falhou(FalhaDaVerificacao.RespostaComErro) with { UltimaExecucao = UltimaExecucao },
    };
}

// As sondas que os ganchos de teste trocam (só homologação com o relógio de teste): o que o serviço "responde" no lugar da
// verificação real, até o DELETE.
public sealed class SondasDeTeste
{
    readonly ConcurrentDictionary<string, RespostaDaSondaDeTeste> respostas = new();

    public void Definir(Servico servico, RespostaDaSondaDeTeste resposta) => respostas[servico.Id] = resposta;

    public void Remover(Servico servico) => respostas.TryRemove(servico.Id, out _);

    public void Limpar() => respostas.Clear();

    public RespostaDaSondaDeTeste? De(Servico servico) => respostas.GetValueOrDefault(servico.Id);
}

// A sonda real, ou a do gancho de teste enquanto houver uma definida para o serviço (SondaFalsa, auditoria-classes v03).
public sealed class SondaComGancho(ISondaDeServico real, SondasDeTeste testes) : ISondaDeServico
{
    public Servico Servico => real.Servico;

    public Task<ResultadoDaSonda> Verificar(CancellationToken ct = default) =>
        testes.De(Servico) is { } falsa ? Task.FromResult(falsa.Resultado()) : real.Verificar(ct);
}
