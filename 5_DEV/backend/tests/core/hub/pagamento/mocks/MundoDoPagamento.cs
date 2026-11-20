using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Pagamento.Card.UseCases;
using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.UseCases;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.UseCases;
using Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;
using Boaz.Core.Hub.Pagamento.Recebimentos.UseCases;

namespace Boaz.Core.Hub.Tests.Pagamento.Mocks;

/// <summary>
/// O mundo dos Cenários de API do 19: a EBFGA (master e a staff Rita), o
/// Pedro Reis autônomo e o administrador do deboaz; os casos de uso do
/// Pagamento ligados às portas em memória e ao relógio do teste.
/// </summary>
public sealed class MundoDoPagamento
{
    /// <summary>A staff Rita, com MOD-01 · U.</summary>
    public static readonly QuemPede Rita = new("usr-ebfga-rita", "STAFF", "conta-ebfga");

    /// <summary>O Pedro Reis, autônomo.</summary>
    public static readonly QuemPede Pedro = new("usr-preis-pedro", "AUTONOMO", "conta-preis");

    /// <summary>O administrador do deboaz, com MOD-01 · U no token só para o card (17-b RN-233-api).</summary>
    public static readonly QuemPede Admin = new("usr-deboaz-admin", QuemPede.Administrador, "conta-dboaz");

    /// <summary>A staff Maria, sem MOD-01.</summary>
    public static readonly QuemPede Maria = new("usr-ebfga-maria", "STAFF", "conta-ebfga", false);

    /// <summary>Um traceId novo.</summary>
    public static TraceId Trace => TraceId.Novo();

    /// <summary>As portas.</summary>
    public PagamentoEmMemoria Mock { get; } = new();

    /// <summary>O relógio.</summary>
    public RelogioDoPagamento Relogio { get; } = new();

    /// <summary>Monta os casos de uso.</summary>
    public MundoDoPagamento()
    {
        var m = Mock;
        Expirar = new(m, m, m, m, Relogio);
        Agendamento = new(m, m, m, m, m, m, Expirar, m, m, Relogio);
        Listar = new(m, m, m, Relogio);
        LerFatura = new(m, m, m, m, Relogio);
        LerAvisos = new(m, m, m, Relogio);
        Criar = new(m, m, m, m, m, m, m, m, m, m, Relogio);
        LerPagamento = new(m, m, m, m);
        Cancelar = new(m, m, m, m, m, m, Relogio);
        Documento = new(m, m, m, m);
        AoVivo = new(m, m, m);
        Aviso = new(m, m, m, m, m, m, m, m, Relogio);
        Recebimentos = new(m, m, Relogio);
        ListarDoRecebedor = new(m, Relogio);
        FaturaDoRecebedor = new(m, m, m, m, Relogio);
        PagamentoDoRecebedor = new(m, m, m, m);
        CancelarFatura = new(m, m, m, m, m, m, m, m, m, Relogio);
        Estornar = new(m, m, m, m, m, m, m, m, Relogio);
        LerConfiguracao = new(m);
        SalvarConfiguracao = new(m, m, m, Relogio);
        Formas = new(m, m, m);
        Card = new(m, Relogio);
        CardAoVivo = new(Card);
    }

    /// <summary>ExpirarPagamentos.</summary>
    public ExpirarPagamentos Expirar { get; }

    /// <summary>RodarAgendamentoDoPagamento.</summary>
    public RodarAgendamentoDoPagamento Agendamento { get; }

    /// <summary>ListarFaturas.</summary>
    public ListarFaturas Listar { get; }

    /// <summary>LerFatura.</summary>
    public LerFatura LerFatura { get; }

    /// <summary>LerAvisos.</summary>
    public LerAvisos LerAvisos { get; }

    /// <summary>CriarPagamento.</summary>
    public CriarPagamento Criar { get; }

    /// <summary>LerPagamento.</summary>
    public LerPagamento LerPagamento { get; }

    /// <summary>CancelarPagamento.</summary>
    public CancelarPagamento Cancelar { get; }

    /// <summary>LerDocumentoDoPagamento.</summary>
    public LerDocumentoDoPagamento Documento { get; }

    /// <summary>AcompanharPagamento.</summary>
    public AcompanharPagamento AoVivo { get; }

    /// <summary>ReceberAvisoDoBanco.</summary>
    public ReceberAvisoDoBanco Aviso { get; }

    /// <summary>LerRecebimentos.</summary>
    public LerRecebimentos Recebimentos { get; }

    /// <summary>ListarFaturasDoRecebedor.</summary>
    public ListarFaturasDoRecebedor ListarDoRecebedor { get; }

    /// <summary>LerFaturaDoRecebedor.</summary>
    public LerFaturaDoRecebedor FaturaDoRecebedor { get; }

    /// <summary>LerPagamentoDoRecebedor.</summary>
    public LerPagamentoDoRecebedor PagamentoDoRecebedor { get; }

    /// <summary>CancelarFatura.</summary>
    public CancelarFatura CancelarFatura { get; }

    /// <summary>PedirEstorno.</summary>
    public PedirEstorno Estornar { get; }

    /// <summary>LerConfiguracao.</summary>
    public LerConfiguracao LerConfiguracao { get; }

    /// <summary>SalvarConfiguracao.</summary>
    public SalvarConfiguracao SalvarConfiguracao { get; }

    /// <summary>LerFormas.</summary>
    public LerFormas Formas { get; }

    /// <summary>LerCardDoPagamento.</summary>
    public LerCardDoPagamento Card { get; }

    /// <summary>AcompanharCardDoPagamento.</summary>
    public AcompanharCardDoPagamento CardAoVivo { get; }

    /// <summary>A cobrança fechada de um módulo na competência (RN-300).</summary>
    public void Cobranca(string contaId, string modulo, string competencia, decimal total) =>
        Mock.Cobrancas.Add((Competencia.Ler(competencia)!.Value,
            new CobrancaFechada($"cob-{contaId}-{modulo}-{competencia}", contaId, modulo, Dinheiro.De(total))));

    /// <summary>A fatura emitida no fechamento da competência, pelo agendamento à 00:00 do dia 1.</summary>
    public async Task<Fatura> Emitir(string contaId, string competencia, params (string Modulo, decimal Valor)[] itens)
    {
        foreach (var (modulo, valor) in itens) Cobranca(contaId, modulo, competencia, valor);
        var mes = Competencia.Ler(competencia)!.Value;
        Relogio.Agora = mes.EmitidaEm.AddMinutes(1);
        await Agendamento.Executar();
        return Mock.Faturas.Values.Single(f => f.Conta.Id == contaId && f.Competencia == mes);
    }

    /// <summary>A configuração salva agora, como a versão seguinte.</summary>
    public void Configurar(decimal juros = 0m, int tolerancia = 0, bool boleto = true)
    {
        var versao = (Mock.Configuracoes.Count == 0 ? 1 : Mock.Configuracoes.Max(c => c.Versao)) + 1;
        var formas = TextosDoPagamento.Formas.ToDictionary(f => f, f => f != FormaDePagamento.Boleto || boleto);
        Mock.Configuracoes.Add(new ConfiguracaoDoPagamento(versao, Relogio.Agora, formas, juros, tolerancia, ConfiguracaoDoPagamento.Dia,
            new PrazosDoPagamento(30, 10, 3), "usr-deboaz-admin"));
    }
}
