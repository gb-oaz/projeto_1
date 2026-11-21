using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Ports.Out;
using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Tests.Auditoria.Mocks;

/// <summary>O relógio dos testes: anda só quando o teste manda.</summary>
public sealed class RelogioFalso(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = agora;

    public override DateTimeOffset GetUtcNow() => Agora;
}

/// <summary>A última verificação na memória, como a do ms_mono.</summary>
public sealed class UltimaVerificacaoFalsa : IUltimaVerificacao
{
    public DateTimeOffset? VerificadoEm { get; private set; }

    public IReadOnlyList<Verificacao> Servicos { get; private set; } = [];

    public IReadOnlyList<SaudeDoModulo> Modulos { get; private set; } = [];

    public bool BancoForaDoAr => De(Servico.Banco)?.Estado == EstadoDoServico.ForaDoAr;

    public Verificacao? De(Servico servico) => Servicos.FirstOrDefault(v => v.Servico == servico);

    public SaudeDoModulo? DoModulo(string modulo) => Modulos.FirstOrDefault(m => m.Modulo == modulo);

    public void Guardar(DateTimeOffset em, IReadOnlyList<Verificacao> servicos, IReadOnlyList<SaudeDoModulo> modulos) =>
        (VerificadoEm, Servicos, Modulos) = (em, servicos, modulos);
}

/// <summary>Uma sonda que responde o que o teste definiu.</summary>
public sealed class SondaFalsa(Servico servico) : ISondaDeServico
{
    public Servico Servico { get; } = servico;

    public ResultadoDaSonda Resposta { get; set; } = ResultadoDaSonda.Respondeu(10);

    public TimeSpan? Demora { get; set; }

    public async Task<ResultadoDaSonda> Verificar(CancellationToken ct = default)
    {
        if (Demora is { } demora) await Task.Delay(demora, ct);
        return Resposta;
    }
}

public sealed class InstanciasFalsas : IInstancias
{
    public List<Instancia> Anunciadas { get; } = [];

    public bool Falha { get; set; }

    public Task Anunciar(Instancia instancia, CancellationToken ct = default)
    {
        if (Falha) throw new InvalidOperationException("cache fora");
        Anunciadas.RemoveAll(i => i.Id == instancia.Id);
        Anunciadas.Add(instancia);
        return Task.CompletedTask;
    }

    // assíncrona, como a do Redis: a falha chega na espera, não na chamada
    public async Task<IReadOnlyList<Instancia>> Listar(CancellationToken ct = default)
    {
        await Task.Yield();
        return Falha ? throw new InvalidOperationException("cache fora") : [.. Anunciadas];
    }
}

public sealed class TravaFalsa : ITravaDaAuditoria
{
    public bool Livre { get; set; } = true;

    public Task<bool> Tentar(string tarefa, TimeSpan prazo, CancellationToken ct = default) => Task.FromResult(Livre);
}

public sealed class HistoricoFalso : IHistoricoDaSaude
{
    public List<Verificacao> Verificacoes { get; } = [];

    public List<Queda> Quedas_ { get; } = [];

    public Task Gravar(IReadOnlyList<Verificacao> verificacoes, CancellationToken ct = default)
    {
        Verificacoes.AddRange(verificacoes);
        return Task.CompletedTask;
    }

    public Task AbrirQueda(Queda queda, CancellationToken ct = default)
    {
        Quedas_.Add(queda);
        return Task.CompletedTask;
    }

    public Task FecharQueda(Servico servico, DateTimeOffset fim, CancellationToken ct = default)
    {
        var i = Quedas_.FindIndex(q => q.Servico == servico && q.Fim is null);
        if (i >= 0) Quedas_[i] = Quedas_[i] with { Fim = fim };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Queda>> Quedas(Servico servico, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Queda>>([.. Quedas_.Where(q => q.Servico == servico).OrderByDescending(q => q.Inicio)]);

    public Task<IReadOnlyList<PontoDoHistorico>> Serie(Servico servico, DateTimeOffset de, TimeSpan passo, int pontos, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PontoDoHistorico>>([.. Enumerable.Range(0, pontos).Select(i => new PontoDoHistorico(de + (passo * i), null, 0))]);
}

public sealed class MedicoesFalsas : IMedicoes
{
    public Dictionary<string, ResumoDeMedicoes> DosModulos { get; } = [];

    public ResumoDeMedicoes Total { get; set; } = ResumoDeMedicoes.Vazio;

    public List<PontoDaMedicao> Pontos { get; } = [];

    public List<MedicaoDoEndereco> Enderecos { get; } = [];

    public bool Falha { get; set; }

    public List<Medicao> Somadas { get; } = [];

    public Task Somar(IReadOnlyList<Medicao> medicoes, CancellationToken ct = default)
    {
        Somadas.AddRange(medicoes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, ResumoDeMedicoes>> PorModulo(DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default) =>
        Falha ? throw FalhaDaAuditoria.BancoDeDadosFora() : Task.FromResult<IReadOnlyDictionary<string, ResumoDeMedicoes>>(DosModulos);

    public Task<ResumoDeMedicoes> Resumo(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default) => Task.FromResult(Total);

    public Task<IReadOnlyList<PontoDaMedicao>> Serie(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PontoDaMedicao>>(Pontos);

    public Task<IReadOnlyList<MedicaoDoEndereco>> PorEndereco(Janela janela, DateTimeOffset de, DateTimeOffset ate, string? modulo, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MedicaoDoEndereco>>(Enderecos);
}

public sealed class OcorrenciasFalsas : IOcorrencias, IOcorrenciasAbertas, IAvisosDeOcorrencias
{
    readonly Channel<Ocorrencia> canal = Channel.CreateUnbounded<Ocorrencia>();

    public List<Ocorrencia> Gravadas { get; } = [];

    public Dictionary<string, Ocorrencia> EmAberto { get; } = [];

    public List<Ocorrencia> Voltas { get; } = [];

    public List<Ocorrencia> Publicadas { get; } = [];

    public Task Gravar(Ocorrencia ocorrencia, CancellationToken ct = default)
    {
        Gravadas.Add(ocorrencia);
        return Task.CompletedTask;
    }

    public Task<PaginaDeOcorrencias> Listar(Periodo periodo, TipoDoAlvo? tipo, string? alvo, EstadoDeOcorrencia? para, Cursor? depois, int limite, CancellationToken ct = default)
    {
        var itens = Gravadas.Where(o => (tipo is null || o.Tipo == tipo) && (alvo is null || o.Alvo == alvo) && (para is null || o.Para == para))
            .OrderByDescending(o => o.Quando).ToList();
        var pagina = itens.Take(limite).ToList();
        return Task.FromResult(new PaginaDeOcorrencias(pagina, itens.Count > limite ? new Cursor(pagina[^1].Quando, pagina[^1].Id) : null));
    }

    public Task Mudar(Ocorrencia ocorrencia, CancellationToken ct = default)
    {
        if (ocorrencia.Para == EstadoDeOcorrencia.NoAr)
        {
            EmAberto.Remove(ocorrencia.Alvo);
            Voltas.Add(ocorrencia);
        }
        else EmAberto[ocorrencia.Alvo] = ocorrencia;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Ocorrencia>> Abertas(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Ocorrencia>>([.. EmAberto.Values]);

    public Task<IReadOnlyList<Ocorrencia>> Voltaram(DateTimeOffset agora, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Ocorrencia>>([.. Voltas.Where(o => agora - o.Quando <= TimeSpan.FromMinutes(10))]);

    public Task Publicar(Ocorrencia ocorrencia, CancellationToken ct = default)
    {
        Publicadas.Add(ocorrencia);
        canal.Writer.TryWrite(ocorrencia);
        return Task.CompletedTask;
    }

    public Task<IAssinatura<Ocorrencia>> Assinar(CancellationToken ct = default) => Task.FromResult<IAssinatura<Ocorrencia>>(new Assinatura(canal));

    sealed class Assinatura(Channel<Ocorrencia> canal) : IAssinatura<Ocorrencia>
    {
        public bool Descartada { get; private set; }

        public async IAsyncEnumerable<Ocorrencia> Avisos([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var o in canal.Reader.ReadAllAsync(ct)) yield return o;
        }

        public ValueTask DisposeAsync()
        {
            Descartada = true;
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class AuditoriaFalsa : IAuditoria
{
    public List<RegistroDeAuditoria> Registros { get; } = [];

    public bool Falha { get; set; }

    public Task Registrar(RegistroDeAuditoria registro, CancellationToken ct = default)
    {
        if (Falha) throw new InvalidOperationException("banco fora");
        Registros.Add(registro);
        return Task.CompletedTask;
    }
}

public sealed class ConsultasRecentesFalsas : IConsultasRecentes
{
    readonly Dictionary<(string, string), DateTimeOffset> vistas = [];

    public Task<bool> Primeira(string quem, string alvo, DateTimeOffset agora, TimeSpan janela, CancellationToken ct = default)
    {
        if (vistas.TryGetValue((quem, alvo), out var antes) && agora - antes < janela) return Task.FromResult(false);
        vistas[(quem, alvo)] = agora;
        return Task.FromResult(true);
    }
}

public sealed class LeituraFalsa : ILeituraDaAuditoria
{
    public List<RegistroBruto> Todos { get; } = [];

    public List<PontasDaExecucao> Pontas { get; } = [];

    public ConsultaDeRegistros? UltimaConsulta { get; private set; }

    public (int Erros, int Alertas) Erros { get; set; }

    public Task<IReadOnlyList<RegistroBruto>> Registros(ConsultaDeRegistros consulta, CancellationToken ct = default)
    {
        UltimaConsulta = consulta;
        return Task.FromResult<IReadOnlyList<RegistroBruto>>([.. Todos.OrderByDescending(r => r.Quando).ThenByDescending(r => r.Id, StringComparer.Ordinal).Take(consulta.Limite + 1)]);
    }

    public Task<IReadOnlyList<RegistroBruto>> DoTrace(string traceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RegistroBruto>>([.. Todos.Where(r => r.TraceId == traceId).OrderBy(r => r.Quando)]);

    public Task<IReadOnlyList<PontasDaExecucao>> Execucoes(Periodo periodo, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PontasDaExecucao>>(Pontas);

    public Task<(int Erros, int Alertas)> ContagemDeErros(DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default) => Task.FromResult(Erros);
}

public sealed class NomesFalsos : INomesDaAuditoria
{
    public Dictionary<string, string> Pessoas { get; } = new() { ["usr-ebfga-maria"] = "Maria Souza", ["usr-dboaz-caio"] = "Caio Lima" };

    public Dictionary<string, string> SiglasDasContas { get; } = new() { ["conta-ebfga"] = "EBFGA" };

    public Task<IReadOnlyDictionary<string, string>> Usuarios(IEnumerable<string> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(Pessoas.Where(p => ids.Contains(p.Key)).ToDictionary());

    public Task<IReadOnlyDictionary<string, string>> Contas(IEnumerable<string> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(SiglasDasContas.Where(p => ids.Contains(p.Key)).ToDictionary());

    public Task<string?> IdDaConta(string sigla, CancellationToken ct = default) =>
        Task.FromResult<string?>(SiglasDasContas.FirstOrDefault(c => string.Equals(c.Value, sigla, StringComparison.OrdinalIgnoreCase)).Key);
}

public sealed class CardFalso : ICardDaAuditoria, IDescarteDaAuditoria
{
    public List<DiaDoCard> Fechados { get; } = [];

    public List<DiaDoCard> Guardados { get; } = [];

    public int Descartes { get; private set; }

    public bool Falha { get; set; }

    public Task<DiaDoCard> Fechar(DiaDoCard dia, CancellationToken ct = default)
    {
        Fechados.Add(dia);
        return Task.FromResult(dia);
    }

    public Task<IReadOnlyList<DiaDoCard>> Dias(DateOnly desde, DateOnly ate, CancellationToken ct = default) =>
        Falha ? throw FalhaDaAuditoria.BancoDeDadosFora() : Task.FromResult<IReadOnlyList<DiaDoCard>>(Guardados);

    public Task ApagarVencidos(DateTimeOffset agora, CancellationToken ct = default)
    {
        Descartes++;
        return Task.CompletedTask;
    }
}

/// <summary>As conexões ao vivo: o que o teste manda encerrar volta no Esperar.</summary>
public sealed class ConexoesFalsas : IConexoesAoVivo
{
    readonly Dictionary<string, TaskCompletionSource<MotivoEncerramento>> sinais = [];

    public List<string> Abertas { get; } = [];

    public List<string> Fechadas { get; } = [];

    public Task<IReadOnlyList<string>> Abrir(string usuarioId, string conexaoId, CancellationToken ct = default)
    {
        sinais[conexaoId] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Abertas.Add(conexaoId);
        var passam = Abertas.Count > 3 ? Abertas.Take(Abertas.Count - 3).ToList() : [];
        return Task.FromResult<IReadOnlyList<string>>(passam);
    }

    public Task Encerrar(string conexaoId, MotivoEncerramento motivo, CancellationToken ct = default)
    {
        sinais[conexaoId].TrySetResult(motivo);
        return Task.CompletedTask;
    }

    public Task Renovar(string usuarioId, string conexaoId, CancellationToken ct = default) => Task.CompletedTask;

    public Task Fechar(string usuarioId, string conexaoId, CancellationToken ct = default)
    {
        Fechadas.Add(conexaoId);
        return Task.CompletedTask;
    }

    public async Task<MotivoEncerramento?> Esperar(string conexaoId, TimeSpan tempo, CancellationToken ct = default)
    {
        var fim = await Task.WhenAny(sinais[conexaoId].Task, Task.Delay(tempo, ct));
        return fim == sinais[conexaoId].Task ? sinais[conexaoId].Task.Result : null;
    }
}

public sealed class AcessoFalso : IAcessoDoAoVivo
{
    public bool Sessao { get; set; } = true;

    public bool Pode { get; set; } = true;

    public Task<bool> SessaoAberta(string sid, CancellationToken ct = default) => Task.FromResult(Sessao);

    public Task<bool> PodeUsar(string usuarioId, CancellationToken ct = default) => Task.FromResult(Pode);
}
