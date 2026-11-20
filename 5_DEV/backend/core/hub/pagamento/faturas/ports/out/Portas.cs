using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

namespace Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;

/// <summary>O filtro das listas de faturas, do pagador (RF-182) e do recebedor (RF-191).</summary>
/// <param name="ContaId">Só desta conta; null: todas (o recebedor).</param>
/// <param name="Status">Só estes status; vazio: todos.</param>
/// <param name="Competencia">Só desta competência.</param>
/// <param name="Busca">Texto na sigla, no nome da conta ou no número, sem diferenciar maiúsculas.</param>
public sealed record FiltroDeFaturas(string? ContaId, IReadOnlyList<StatusDaFatura> Status, Competencia? Competencia = null, string? Busca = null);

/// <summary>A versão do card: a soma das versões (cada mudança de fatura sobe uma), quantas há, as vencidas e a última mudança (RN-307).</summary>
/// <param name="Versao">A soma de versao + 1 das faturas.</param>
/// <param name="Quantas">Quantas faturas.</param>
/// <param name="Vencidas">Quantas "VENCIDA".</param>
/// <param name="AtualizadoEm">A última mudança; null sem faturas.</param>
public sealed record VersaoDasFaturas(long Versao, int Quantas, int Vencidas, DateTimeOffset? AtualizadoEm);

/// <summary>Uma soma das faturas, feita no banco (RNF-245): por competência, status, tipo de conta e forma do pagamento que quitou.</summary>
/// <param name="Competencia">2026-10.</param>
/// <param name="Status">O status.</param>
/// <param name="TipoDaConta">INSTITUICAO ou AUTONOMO.</param>
/// <param name="FormaPaga">A forma do pagamento que quitou; null se não paga.</param>
/// <param name="Quantas">Quantas faturas.</param>
/// <param name="Valor">A soma do valor das faturas.</param>
/// <param name="Recebido">A soma do pago com juros menos o estornado (RN-308).</param>
public sealed record SomaDeFaturas(Competencia Competencia, StatusDaFatura Status, string TipoDaConta, FormaDePagamento? FormaPaga, int Quantas,
    Dinheiro Valor, Dinheiro Recebido);

/// <summary>A coleção faturas (pagamento-documentos v03).</summary>
public interface IFaturas
{
    /// <summary>A fatura; null se não existe.</summary>
    Task<Fatura?> Obter(string id, CancellationToken ct = default);

    /// <summary>As faturas do filtro, sem ordem.</summary>
    Task<IReadOnlyList<Fatura>> Listar(FiltroDeFaturas filtro, CancellationToken ct = default);

    /// <summary>As "A_PAGAR" vencidas antes do dia (agendamento, RN-301).</summary>
    Task<IReadOnlyList<Fatura>> APagarVencidasAntesDe(DateOnly dia, CancellationToken ct = default);

    /// <summary>A conta já tem a fatura da competência (uma por conta e mês, RN-300).</summary>
    Task<bool> Existe(string contaId, Competencia competencia, CancellationToken ct = default);

    /// <summary>Grava a fatura nova; false se a da conta e da competência já existe (índice único).</summary>
    Task<bool> Incluir(Fatura fatura, CancellationToken ct = default);

    /// <summary>Grava a mudança se a versão guardada ainda é a lida; false se outro mudou antes.</summary>
    Task<bool> Salvar(Fatura fatura, CancellationToken ct = default);

    /// <summary>
    /// Ocupa a fatura com o pagamento em aberto, numa só operação (RN-303): só
    /// um pedido entre os que chegam juntos consegue. Devolve null se ocupou, ou
    /// o pagamento que já a ocupa.
    /// </summary>
    Task<string?> Ocupar(string faturaId, string pagamentoId, CancellationToken ct = default);

    /// <summary>Libera a fatura, se ainda estava ocupada pelo pagamento dado.</summary>
    Task Liberar(string faturaId, string pagamentoId, CancellationToken ct = default);

    /// <summary>A versão do card das faturas da conta, ou de todas (null), somada no banco (RN-307, RNF-245).</summary>
    Task<VersaoDasFaturas> Versao(string? contaId, CancellationToken ct = default);

    /// <summary>As somas das competências dadas, de todas as contas, feitas no banco (RN-308, RNF-245).</summary>
    Task<IReadOnlyList<SomaDeFaturas>> Somas(IReadOnlyCollection<Competencia> competencias, CancellationToken ct = default);

    /// <summary>
    /// O recebido (o pago com juros menos o estornado) de todas as contas por
    /// dia de Brasília do pagamento, entre os dias dados, somado no banco (RN-307, RNF-245).
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, Dinheiro>> RecebidoPorDia(DateOnly de, DateOnly ate, CancellationToken ct = default);
}

/// <summary>Uma cobrança fechada do Catálogo (coleção cobrancas, só leitura).</summary>
/// <param name="Id">cob-ebfga-MOD-03-2026-11.</param>
/// <param name="ContaId">A conta cobrada.</param>
/// <param name="ModuloId">O módulo.</param>
/// <param name="Total">O valor.</param>
public sealed record CobrancaFechada(string Id, string ContaId, string ModuloId, Dinheiro Total);

/// <summary>As cobranças fechadas da competência, que viram os itens da fatura (RN-300).</summary>
public interface ICobrancasDoMes
{
    /// <summary>As cobranças da competência, de todas as contas.</summary>
    Task<IReadOnlyList<CobrancaFechada>> Fechadas(Competencia competencia, CancellationToken ct = default);
}

/// <summary>O que o Pagamento lê de outros módulos: contas, nomes de pessoas e de módulos (só leitura).</summary>
public interface ICadastroDoPagamento
{
    /// <summary>A conta como vai na fatura; null se não existe.</summary>
    Task<ContaDaFatura?> Conta(string contaId, CancellationToken ct = default);

    /// <summary>
    /// Quem paga, para o intermediador: o nome e o documento da conta e o login
    /// da conta master ou do autônomo (RF-186, RF-188); null se a conta não existe.
    /// </summary>
    Task<Pagador?> Pagador(string contaId, CancellationToken ct = default);

    /// <summary>O nome de cada usuário dado; os que não existem ficam de fora.</summary>
    Task<IReadOnlyDictionary<string, string>> Nomes(IEnumerable<string> usuarioIds, CancellationToken ct = default);

    /// <summary>O nome de cada módulo registrado (15_modulos).</summary>
    Task<IReadOnlyDictionary<string, string>> Modulos(CancellationToken ct = default);
}

/// <summary>Os contadores: FAT-, PAG- e EST- seguidos ($inc atômico).</summary>
public interface INumeracaoDoPagamento
{
    /// <summary>O próximo número da série (Numeros.Faturas, Pagamentos ou Estornos).</summary>
    Task<long> Proximo(string serie, CancellationToken ct = default);
}

/// <summary>Um aviso de atraso de uma fatura vencida (RN-311).</summary>
/// <param name="FaturaId">A fatura.</param>
/// <param name="ContaId">A conta avisada.</param>
/// <param name="Vencimento">O vencimento da fatura.</param>
/// <param name="Desde">00:00 de Brasília do dia seguinte ao fim da tolerância.</param>
public sealed record AvisoDeAtraso(string FaturaId, string ContaId, DateOnly Vencimento, DateTimeOffset Desde);

/// <summary>A coleção avisosPagamento: os avisos de atraso, só no módulo (RN-311).</summary>
public interface IAvisosDeAtraso
{
    /// <summary>Abre o aviso da fatura; false se já estava aberto.</summary>
    Task<bool> Abrir(AvisoDeAtraso aviso, CancellationToken ct = default);

    /// <summary>Encerra o aviso da fatura, se estava aberto.</summary>
    Task Encerrar(string faturaId, DateTimeOffset em, CancellationToken ct = default);

    /// <summary>Os avisos abertos da conta.</summary>
    Task<IReadOnlyList<AvisoDeAtraso>> Abertos(string contaId, CancellationToken ct = default);

    /// <summary>Os avisos abertos de todas as contas.</summary>
    Task<IReadOnlyList<AvisoDeAtraso>> TodosAbertos(CancellationToken ct = default);
}
