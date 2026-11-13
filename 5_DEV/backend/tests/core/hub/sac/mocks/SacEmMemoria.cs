using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.Core.Hub.Sac.Card.Domain;
using Boaz.Core.Hub.Sac.Card.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.Core.Hub.Sac.Configuracao.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Ports.Out;

namespace Boaz.Core.Hub.Tests.Sac.Mocks;

/// <summary>Relógio parado que o teste avança à mão; começa em 2026-11-12 13:00 UTC (10:00 de Brasília).</summary>
public sealed class Relogio : TimeProvider
{
    /// <summary>A hora atual do teste.</summary>
    public DateTimeOffset Agora { get; set; } = new(2026, 11, 12, 13, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Agora;
}

/// <summary>
/// Todas as portas de saída do SAC em memória: chamados e itens, numeração,
/// canal, pessoas, anexos, arquivos, verificação, prazos, contagem e séries,
/// auditoria e erros.
/// </summary>
public sealed class SacEmMemoria : IChamados, INumeracao, ICanalDoChamado, IPessoasDoSac, IAnexosDoChamado, IPrazosDoSac, IAnexos, IArquivos,
    IVerificacaoDeArquivos, IConfiguracaoDoSac, IContagemDoSac, ISeriesDoSac, IAuditoria, IAuditoriaDeErros
{
    /// <summary>Os chamados guardados.</summary>
    public Dictionary<string, DadosDoChamado> Chamados { get; } = [];

    /// <summary>Os itens guardados.</summary>
    public List<(string Chamado, ItemDaConversa Item)> ItensGuardados { get; } = [];

    /// <summary>Os anexos guardados.</summary>
    public Dictionary<string, Anexo> Anexos { get; } = [];

    /// <summary>Os arquivos guardados.</summary>
    public Dictionary<string, byte[]> Arquivos { get; } = [];

    /// <summary>A auditoria.</summary>
    public List<RegistroDeAuditoria> Auditoria { get; } = [];

    /// <summary>Os erros.</summary>
    public List<RegistroDeErro> Erros { get; } = [];

    /// <summary>Os avisos publicados no canal.</summary>
    public List<(string Id, long Versao)> Avisos { get; } = [];

    /// <summary>Os dias fechados do card.</summary>
    public Dictionary<(string Escopo, DateOnly Dia), ContagemDoSac> DiasFechados { get; } = [];

    /// <summary>Os nomes dos usuários.</summary>
    public Dictionary<string, string> Nomes { get; } = new()
    {
        ["usr-ebfga-master"] = "Escola Bento Freitas",
        ["usr-preis-master"] = "Pedro Reis",
        ["usr-dboaz-caio"] = "Caio Lima",
        ["usr-dboaz-ana"] = "Ana Lima",
    };

    /// <summary>As contas.</summary>
    public Dictionary<string, ContaDoChamado> Contas { get; } = new()
    {
        ["conta-ebfga"] = new("conta-ebfga", "EBFGA", "Escola Bento Freitas", "INSTITUICAO"),
        ["conta-preis"] = new("conta-preis", "PREIS", "Pedro Reis", "AUTONOMO"),
    };

    /// <summary>O resultado da próxima verificação.</summary>
    public ResultadoDaVerificacao Verificacao { get; set; } = ResultadoDaVerificacao.Limpo;

    /// <summary>A próxima gravação perde para outra (versão mudou).</summary>
    public int ConflitosAntesDeGravar { get; set; }

    /// <summary>O canal avisa na espera.</summary>
    public bool AvisoNaEspera { get; set; }

    /// <summary>Os prazos guardados.</summary>
    public PrazosDoSac Prazos { get; set; } = PrazosDoSac.Padrao;

    readonly Dictionary<string, long> contadores = [];

    // ── IChamados ─────────────────────────────────────────────────────────
    public Task<Chamado?> Obter(NumeroDoChamado id, CancellationToken ct = default) =>
        Task.FromResult(Chamados.TryGetValue(id.Valor, out var d) ? new Chamado(d) : null);

    public Task Incluir(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct = default)
    {
        Chamados.Add(chamado.Dados.Id.Valor, chamado.Dados);
        ItensGuardados.AddRange(itens.Select(i => (chamado.Dados.Id.Valor, i)));
        return Task.CompletedTask;
    }

    public Task<bool> Salvar(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct = default)
    {
        if (ConflitosAntesDeGravar > 0)
        {
            ConflitosAntesDeGravar--;
            return Task.FromResult(false);
        }
        if (Chamados[chamado.Dados.Id.Valor].Versao != chamado.VersaoLida) return Task.FromResult(false);
        Chamados[chamado.Dados.Id.Valor] = chamado.Dados;
        ItensGuardados.AddRange(itens.Select(i => (chamado.Dados.Id.Valor, i)));
        return Task.FromResult(true);
    }

    public Task MarcarLido(NumeroDoChamado id, Visao visao, CancellationToken ct = default)
    {
        var d = Chamados[id.Valor];
        Chamados[id.Valor] = visao == Visao.Conta ? d with { NaoLidoPelaConta = false } : d with { NaoLidoPeloTime = false };
        return Task.CompletedTask;
    }

    public Task<PaginaDeChamados> Listar(FiltroDeChamados filtro, CancellationToken ct = default)
    {
        var todos = Chamados.Values.Where(d =>
            (filtro.ContaId is null || d.Conta?.Id == filtro.ContaId)
            && (filtro.Status is null || filtro.Status.Contains(d.Status))
            && (filtro.Categoria is null || d.Categoria == filtro.Categoria)
            && (filtro.Busca is null || $"{d.Id.Valor} {d.Conta?.Sigla} {d.Assunto}".Contains(filtro.Busca, StringComparison.OrdinalIgnoreCase))
            && (!filtro.SemResponsavel || d.Responsavel is null)
            && (filtro.ResponsavelId is null || d.Responsavel?.UsuarioId == filtro.ResponsavelId)
            && (!filtro.Atrasados || Chamado.EstaAtrasado(d, filtro.Agora))
            && (filtro.NotaMaxima is null || d.Avaliacao?.Nota <= filtro.NotaMaxima));
        var ordem = filtro.OrdemDoTime
            ? todos.OrderBy(d => d.EsperandoTimeDesde ?? DateTimeOffset.MaxValue).ThenBy(d => d.Id.Numero)
            : todos.OrderByDescending(d => d.AtualizadoEm).ThenByDescending(d => d.Id.Numero);
        var lista = ordem.ToList();
        return Task.FromResult(new PaginaDeChamados([.. lista.Skip((filtro.Pagina - 1) * filtro.PorPagina).Take(filtro.PorPagina)], lista.Count));
    }

    public Task<int> EmAndamentoDaConta(string contaId, CancellationToken ct = default) =>
        Task.FromResult(Chamados.Values.Count(d => d.Conta?.Id == contaId && Chamado.EstaEmAndamento(d.Status)));

    public Task<IReadOnlyList<ItemDaConversa>> Itens(NumeroDoChamado id, long? depoisDe = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ItemDaConversa>>([.. ItensGuardados
            .Where(i => i.Chamado == id.Valor && (depoisDe is null || ItemDaConversa.NumeroDe(i.Item.Id) > depoisDe))
            .Select(i => i.Item)]);

    public Task<long> UltimoItem(NumeroDoChamado id, CancellationToken ct = default) =>
        Task.FromResult(ItensGuardados.Where(i => i.Chamado == id.Valor).Select(i => ItemDaConversa.NumeroDe(i.Item.Id) ?? 0).DefaultIfEmpty(0).Max());

    public Task<IReadOnlyList<Chamado>> VencidosEm(DateTimeOffset agora, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Chamado>>([.. Chamados.Values
            .Where(d => (d.Status == StatusDoChamado.AguardandoCliente && d.AguardandoDesde <= agora - LimitesDoChamado.SeteDias)
                || (d.Status == StatusDoChamado.Resolvido && d.ResolvidoEm <= agora - LimitesDoChamado.SeteDias))
            .Select(d => new Chamado(d))]);

    // ── INumeracao ────────────────────────────────────────────────────────
    public Task<long> Proximo(string serie, CancellationToken ct = default)
    {
        contadores[serie] = contadores.GetValueOrDefault(serie, serie == INumeracao.Chamados ? 41 : 317) + 1;
        return Task.FromResult(contadores[serie]);
    }

    // ── ICanalDoChamado ───────────────────────────────────────────────────
    public Task Publicar(NumeroDoChamado id, long versao, CancellationToken ct = default)
    {
        Avisos.Add((id.Valor, versao));
        return Task.CompletedTask;
    }

    public Task<bool> Esperar(NumeroDoChamado id, TimeSpan tempo, CancellationToken ct = default) => Task.FromResult(AvisoNaEspera);

    // ── IPessoasDoSac ─────────────────────────────────────────────────────
    public Task<string> Nome(string usuarioId, CancellationToken ct = default) => Task.FromResult(Nomes.GetValueOrDefault(usuarioId, usuarioId));

    public Task<ContaDoChamado?> Conta(string contaId, CancellationToken ct = default) => Task.FromResult(Contas.GetValueOrDefault(contaId));

    // ── IAnexosDoChamado, IAnexos, IArquivos, IVerificacaoDeArquivos ──────
    public Task<bool> Disponiveis(IReadOnlyList<string> ids, string usuarioId, DateTimeOffset agora, CancellationToken ct = default) =>
        Task.FromResult(ids.All(id => Anexos.TryGetValue(id, out var a) && a.EnviadoPor == usuarioId && a.ChamadoId is null && a.ExpiraEm > agora));

    public Task Vincular(IReadOnlyList<string> ids, NumeroDoChamado chamado, string? mensagemId, bool interna, CancellationToken ct = default)
    {
        foreach (var id in ids) Anexos[id] = Anexos[id] with { ChamadoId = chamado.Valor, MensagemId = mensagemId, Interna = interna, ExpiraEm = null };
        return Task.CompletedTask;
    }

    public Task Incluir(Anexo anexo, CancellationToken ct = default)
    {
        Anexos.Add(anexo.Id, anexo);
        return Task.CompletedTask;
    }

    Task<Anexo?> IAnexos.Obter(string id, CancellationToken ct) => Task.FromResult(Anexos.GetValueOrDefault(id));

    public Task<IReadOnlyList<Anexo>> NaoUsadosAte(DateTimeOffset agora, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Anexo>>([.. Anexos.Values.Where(a => a.Vencido(agora))]);

    Task IAnexos.Apagar(string id, CancellationToken ct)
    {
        Anexos.Remove(id);
        return Task.CompletedTask;
    }

    public Task Guardar(string objeto, byte[] conteudo, CancellationToken ct = default)
    {
        Arquivos[objeto] = conteudo;
        return Task.CompletedTask;
    }

    public Task<Stream?> Abrir(string objeto, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Arquivos.TryGetValue(objeto, out var b) ? new MemoryStream(b) : null);

    Task IArquivos.Apagar(string objeto, CancellationToken ct)
    {
        Arquivos.Remove(objeto);
        return Task.CompletedTask;
    }

    public Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default) => Task.FromResult(Verificacao);

    // ── IPrazosDoSac, IConfiguracaoDoSac ──────────────────────────────────
    public Task<int> HorasUteis(Categoria categoria, CancellationToken ct = default) => Task.FromResult(Prazos.HorasDe(categoria));

    public Task<PrazosDoSac> Ler(CancellationToken ct = default) => Task.FromResult(Prazos);

    public Task Salvar(PrazosDoSac prazos, CancellationToken ct = default)
    {
        Prazos = prazos;
        return Task.CompletedTask;
    }

    // ── IContagemDoSac, ISeriesDoSac ──────────────────────────────────────
    public Task<ContagemDoSac> Contar(string? contaId, DateTimeOffset agora, CancellationToken ct = default)
    {
        var d = Chamados.Values.Where(c => contaId is null || c.Conta?.Id == contaId).ToList();
        var notas = d.Where(c => c.Avaliacao is not null).Select(c => (decimal)c.Avaliacao!.Nota).ToList();
        return Task.FromResult(new ContagemDoSac(d.Count, d.Count(c => Chamado.EstaEmAndamento(c.Status)),
            d.Count(c => c.Status == StatusDoChamado.AguardandoCliente), d.Count(c => c.Status == StatusDoChamado.EmAtendimento),
            d.Count(c => c.ResolvidoEm is not null), d.Count(c => c.NaoLidoPelaConta),
            d.Count(c => c.Status is StatusDoChamado.Aberto or StatusDoChamado.EmAtendimento && c.EsperandoTimeDesde is not null),
            d.Count(c => Chamado.EstaAtrasado(c, agora)), notas.Count == 0 ? null : Math.Round(notas.Average(), 1), d.Sum(c => c.Versao),
            d.Count == 0 ? null : d.Max(c => c.AtualizadoEm)));
    }

    public Task<IReadOnlyList<string>> ContasComChamado(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Chamados.Values.Select(c => c.Conta?.Id).OfType<string>().Distinct()]);

    public Task<IReadOnlyDictionary<DateOnly, int>> Fechados(string escopo, DateOnly desde, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<DateOnly, int>>(DiasFechados
            .Where(f => f.Key.Escopo == escopo && f.Key.Dia >= desde).ToDictionary(f => f.Key.Dia, f => f.Value.EmAndamento));

    public Task Fechar(string escopo, DateOnly dia, ContagemDoSac contagem, CancellationToken ct = default)
    {
        DiasFechados[(escopo, dia)] = contagem;
        return Task.CompletedTask;
    }

    // ── IAuditoria, IAuditoriaDeErros ─────────────────────────────────────
    public Task Registrar(RegistroDeAuditoria registro, CancellationToken ct = default)
    {
        Auditoria.Add(registro);
        return Task.CompletedTask;
    }

    public Task Registrar(RegistroDeErro registro, CancellationToken ct = default)
    {
        Erros.Add(registro);
        return Task.CompletedTask;
    }
}
