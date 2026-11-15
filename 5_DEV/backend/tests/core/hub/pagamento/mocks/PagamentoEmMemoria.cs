using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.Core.Hub.Tests.Pagamento.Mocks;

// dentro do namespace: "Pagamento" sozinho seria o namespace dos testes
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>O relógio dos testes do Pagamento, parado onde o teste pôs.</summary>
public sealed class RelogioDoPagamento : TimeProvider
{
    /// <summary>Agora.</summary>
    public DateTimeOffset Agora { get; set; } = new(2026, 12, 3, 13, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Agora;
}

/// <summary>
/// Todas as portas de saída do Pagamento em memória, com o intermediador
/// controlado pelo teste. As entidades ficam por referência; a versão guardada
/// é conferida em Salvar, como no banco.
/// </summary>
public sealed class PagamentoEmMemoria : IFaturas, INumeracaoDoPagamento, ICobrancasDoMes, ICadastroDoPagamento, IAvisosDeAtraso,
    IConfiguracoesDoPagamento, IPagamentos, IEstornos, ICanalDoPagamento, IAvisosDoBanco, IIntermediador, IAuditoria, IAuditoriaDeErros
{
    readonly Dictionary<string, long> versoes = [];
    readonly Dictionary<string, long> contadores = [];

    /// <summary>As faturas.</summary>
    public Dictionary<string, Fatura> Faturas { get; } = [];

    /// <summary>A ocupação de cada fatura (RN-303).</summary>
    public Dictionary<string, string> Ocupadas { get; } = [];

    /// <summary>Os pagamentos.</summary>
    public Dictionary<string, Pagamento> Pagamentos { get; } = [];

    /// <summary>Os estornos.</summary>
    public Dictionary<string, Estorno> Estornos { get; } = [];

    /// <summary>As cobranças fechadas, por competência.</summary>
    public List<(Competencia Competencia, CobrancaFechada Cobranca)> Cobrancas { get; } = [];

    /// <summary>As contas.</summary>
    public Dictionary<string, ContaDaFatura> Contas { get; } = new()
    {
        ["conta-ebfga"] = new("conta-ebfga", "EBFGA", "Escola Bento Freitas", "INSTITUICAO"),
        ["conta-preis"] = new("conta-preis", "PREIS", "Pedro Reis", "AUTONOMO"),
        ["conta-dboaz"] = new("conta-dboaz", "DBOAZ", "Time deboaz", "INSTITUICAO"),
    };

    /// <summary>Os avisos de atraso, abertos e encerrados.</summary>
    public Dictionary<string, (AvisoDeAtraso Aviso, DateTimeOffset? EncerradoEm)> Avisos { get; } = [];

    /// <summary>As versões da configuração.</summary>
    public List<ConfiguracaoDoPagamento> Configuracoes { get; } = [];

    /// <summary>Os avisos do banco registrados.</summary>
    public Dictionary<string, RegistroDoAviso> AvisosDoBanco { get; } = [];

    /// <summary>Os pagamentos publicados no canal.</summary>
    public List<string> Publicados { get; } = [];

    /// <summary>A auditoria.</summary>
    public List<RegistroDeAuditoria> Auditoria { get; } = [];

    /// <summary>Os erros auditados.</summary>
    public List<RegistroDeErro> Erros { get; } = [];

    /// <summary>O que o intermediador responde à cobrança.</summary>
    public Func<PedidoDeCobranca, ResultadoDaCobranca> Cobranca { get; set; } = p => p.Forma switch
    {
        FormaDePagamento.CartaoCredito => new(SituacaoDaCobranca.Aprovada, "card_1", Cartao: new CartaoMascarado("VISA", "4242"),
            PagoEm: p.ExpiraEm),
        FormaDePagamento.CartaoDebito => new(SituacaoDaCobranca.Aguardando, "deb_1", ConfirmacaoUrl: "https://banco.exemplo/confirmar/1",
            Cartao: new CartaoMascarado("MASTERCARD", "5100")),
        FormaDePagamento.Boleto => new(SituacaoDaCobranca.Aguardando, "bol_1", LinhaDigitavel: "23793.38128", CodigoBarras: "2379112960"),
        _ => new(SituacaoDaCobranca.Aguardando, "pix_1", new DadosDoPix("000201", "<svg></svg>")),
    };

    /// <summary>As cobranças pedidas.</summary>
    public List<PedidoDeCobranca> Cobrancas_ { get; } = [];

    /// <summary>O que o intermediador responde ao estorno.</summary>
    public ResultadoDoEstorno Estorno { get; set; } = new(SituacaoDoEstorno.Pendente, "ref_1");

    /// <summary>O que o banco diz na consulta.</summary>
    public ConsultaNoBanco Consulta { get; set; } = new(SituacaoNoBanco.Aguardando);

    /// <summary>A leitura do próximo aviso.</summary>
    public LeituraDoAviso Leitura { get; set; } = new(true, null);

    /// <summary>Os ids externos cancelados no banco.</summary>
    public List<string> CanceladosNoBanco { get; } = [];

    // ── IFaturas ─────────────────────────────────────────────────────────────
    Task<Fatura?> IFaturas.Obter(string id, CancellationToken ct) => Task.FromResult(Faturas.GetValueOrDefault(id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Fatura>> Listar(FiltroDeFaturas filtro, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fatura>>([.. Faturas.Values.Where(f =>
            (filtro.ContaId is null || f.Conta.Id == filtro.ContaId)
            && (filtro.Status.Count == 0 || filtro.Status.Contains(f.Status))
            && (filtro.Competencia is null || f.Competencia == filtro.Competencia)
            && (filtro.Busca is null || new[] { f.Conta.Sigla, f.Conta.Nome, f.Id }.Any(t => t.Contains(filtro.Busca, StringComparison.OrdinalIgnoreCase))))]);

    /// <inheritdoc />
    public Task<IReadOnlyList<Fatura>> APagarVencidasAntesDe(DateOnly dia, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fatura>>([.. Faturas.Values.Where(f => f.Status == StatusDaFatura.APagar && f.Vencimento < dia)]);

    /// <inheritdoc />
    public Task<bool> Existe(string contaId, Competencia competencia, CancellationToken ct = default) =>
        Task.FromResult(Faturas.Values.Any(f => f.Conta.Id == contaId && f.Competencia == competencia));

    /// <inheritdoc />
    public Task<bool> Incluir(Fatura fatura, CancellationToken ct = default)
    {
        if (!Faturas.TryAdd(fatura.Id, fatura)) return Task.FromResult(false);
        versoes[fatura.Id] = fatura.Versao;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> Salvar(Fatura fatura, CancellationToken ct = default) => Task.FromResult(Versionar(fatura.Id, fatura.Versao, fatura.Versionar));

    /// <inheritdoc />
    public Task<string?> Ocupar(string faturaId, string pagamentoId, CancellationToken ct = default)
    {
        if (Ocupadas.TryGetValue(faturaId, out var atual)) return Task.FromResult<string?>(atual);
        Ocupadas[faturaId] = pagamentoId;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task Liberar(string faturaId, string pagamentoId, CancellationToken ct = default)
    {
        if (Ocupadas.GetValueOrDefault(faturaId) == pagamentoId) Ocupadas.Remove(faturaId);
        return Task.CompletedTask;
    }

    // ── numeração, cobranças, cadastro ───────────────────────────────────────
    /// <inheritdoc />
    public Task<long> Proximo(string serie, CancellationToken ct = default)
    {
        contadores[serie] = contadores.GetValueOrDefault(serie) + 1;
        return Task.FromResult(contadores[serie]);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CobrancaFechada>> Fechadas(Competencia competencia, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CobrancaFechada>>([.. Cobrancas.Where(c => c.Competencia == competencia).Select(c => c.Cobranca)]);

    /// <inheritdoc />
    public Task<ContaDaFatura?> Conta(string contaId, CancellationToken ct = default) => Task.FromResult(Contas.GetValueOrDefault(contaId));

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> Nomes(IEnumerable<string> usuarioIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(usuarioIds.Distinct().ToDictionary(id => id, id => $"Nome de {id}"));

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> Modulos(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { ["MOD-03"] = "Usuários", ["MOD-05"] = "Quiz" });

    // ── avisos de atraso ─────────────────────────────────────────────────────
    /// <inheritdoc />
    public Task<bool> Abrir(AvisoDeAtraso aviso, CancellationToken ct = default)
    {
        if (Avisos.TryGetValue(aviso.FaturaId, out var atual) && atual.EncerradoEm is null) return Task.FromResult(false);
        Avisos[aviso.FaturaId] = (aviso, null);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task Encerrar(string faturaId, DateTimeOffset em, CancellationToken ct = default)
    {
        if (Avisos.TryGetValue(faturaId, out var atual) && atual.EncerradoEm is null) Avisos[faturaId] = (atual.Aviso, em);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AvisoDeAtraso>> Abertos(string contaId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AvisoDeAtraso>>([.. Avisos.Values.Where(a => a.EncerradoEm is null && a.Aviso.ContaId == contaId).Select(a => a.Aviso)]);

    /// <inheritdoc />
    public Task<IReadOnlyList<AvisoDeAtraso>> TodosAbertos(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AvisoDeAtraso>>([.. Avisos.Values.Where(a => a.EncerradoEm is null).Select(a => a.Aviso)]);

    // ── configuração ─────────────────────────────────────────────────────────
    /// <inheritdoc />
    public Task<ConfiguracaoDoPagamento?> Atual(CancellationToken ct = default) =>
        Task.FromResult(Configuracoes.OrderByDescending(c => c.Versao).FirstOrDefault());

    /// <inheritdoc />
    public Task<ConfiguracaoDoPagamento?> VigenteEm(DateTimeOffset quando, CancellationToken ct = default) =>
        Task.FromResult(Configuracoes.Where(c => c.VigenteDesde <= quando).OrderByDescending(c => c.VigenteDesde).FirstOrDefault());

    /// <inheritdoc />
    public Task<bool> Incluir(ConfiguracaoDoPagamento configuracao, CancellationToken ct = default)
    {
        if (Configuracoes.Any(c => c.Versao == configuracao.Versao)) return Task.FromResult(false);
        Configuracoes.Add(configuracao);
        return Task.FromResult(true);
    }

    // ── pagamentos e estornos ────────────────────────────────────────────────
    Task<Pagamento?> IPagamentos.Obter(string id, CancellationToken ct) => Task.FromResult(Pagamentos.GetValueOrDefault(id));

    Task<Pagamento?> IPagamentos.PorIdExterno(string idExterno, CancellationToken ct) =>
        Task.FromResult(Pagamentos.Values.FirstOrDefault(p => p.IdExterno == idExterno));

    /// <inheritdoc />
    public Task<IReadOnlyList<Pagamento>> DasFaturas(IEnumerable<string> faturaIds, CancellationToken ct = default)
    {
        var ids = faturaIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<Pagamento>>([.. Pagamentos.Values.Where(p => ids.Contains(p.FaturaId))]);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Pagamento>> EmAberto(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Pagamento>>([.. Pagamentos.Values.Where(p => p.EmAberto)]);

    /// <inheritdoc />
    public Task Incluir(Pagamento pagamento, CancellationToken ct = default)
    {
        Pagamentos[pagamento.Id] = pagamento;
        versoes[pagamento.Id] = pagamento.Versao;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> Salvar(Pagamento pagamento, CancellationToken ct = default) =>
        Task.FromResult(Versionar(pagamento.Id, pagamento.Versao, pagamento.Versionar));

    Task IPagamentos.Remover(string id, CancellationToken ct)
    {
        Pagamentos.Remove(id);
        return Task.CompletedTask;
    }

    Task<Estorno?> IEstornos.PorIdExterno(string idExterno, CancellationToken ct) =>
        Task.FromResult(Estornos.Values.FirstOrDefault(e => e.IdExterno == idExterno));

    /// <inheritdoc />
    public Task Incluir(Estorno estorno, CancellationToken ct = default)
    {
        Estornos[estorno.Id] = estorno;
        versoes[estorno.Id] = estorno.Versao;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> Salvar(Estorno estorno, CancellationToken ct = default) => Task.FromResult(Versionar(estorno.Id, estorno.Versao, estorno.Versionar));

    Task IEstornos.Remover(string id, CancellationToken ct)
    {
        Estornos.Remove(id);
        return Task.CompletedTask;
    }

    // ── canal ────────────────────────────────────────────────────────────────
    /// <inheritdoc />
    public Task Publicar(string pagamentoId, CancellationToken ct = default)
    {
        Publicados.Add(pagamentoId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> Esperar(string pagamentoId, TimeSpan tempo, CancellationToken ct = default) => Task.FromResult(false);

    // ── avisos do banco ──────────────────────────────────────────────────────
    /// <inheritdoc />
    public Task<bool> Registrar(RegistroDoAviso aviso, CancellationToken ct = default) => Task.FromResult(AvisosDoBanco.TryAdd(aviso.Id, aviso));

    /// <inheritdoc />
    public Task Concluir(string id, string resultado, string? pagamentoId, CancellationToken ct = default)
    {
        AvisosDoBanco[id] = AvisosDoBanco[id] with { Resultado = resultado, PagamentoId = pagamentoId };
        return Task.CompletedTask;
    }

    Task IAvisosDoBanco.Remover(string id, CancellationToken ct)
    {
        AvisosDoBanco.Remove(id);
        return Task.CompletedTask;
    }

    // ── intermediador ────────────────────────────────────────────────────────
    /// <inheritdoc />
    public Task<ResultadoDaCobranca> Cobrar(PedidoDeCobranca pedido, CancellationToken ct = default)
    {
        Cobrancas_.Add(pedido);
        return Task.FromResult(Cobranca(pedido));
    }

    /// <inheritdoc />
    public Task<ConsultaNoBanco> Consultar(string idExterno, CancellationToken ct = default) => Task.FromResult(Consulta);

    /// <inheritdoc />
    public Task Cancelar(string idExterno, CancellationToken ct = default)
    {
        CanceladosNoBanco.Add(idExterno);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ResultadoDoEstorno> Estornar(PedidoDeEstorno pedido, CancellationToken ct = default) => Task.FromResult(Estorno);

    /// <inheritdoc />
    public Task<LeituraDoAviso> LerAviso(AvisoRecebido recebido, CancellationToken ct = default) => Task.FromResult(Leitura);

    // ── auditoria ────────────────────────────────────────────────────────────
    /// <inheritdoc />
    public Task Registrar(RegistroDeAuditoria registro, CancellationToken ct = default)
    {
        Auditoria.Add(registro);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Registrar(RegistroDeErro registro, CancellationToken ct = default)
    {
        Erros.Add(registro);
        return Task.CompletedTask;
    }

    bool Versionar(string id, long lida, Action versionar)
    {
        if (versoes.GetValueOrDefault(id) != lida) return false;
        versoes[id] = lida + 1;
        versionar();
        return true;
    }
}
