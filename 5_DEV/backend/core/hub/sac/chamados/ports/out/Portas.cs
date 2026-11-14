using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Chamados.Ports.Out;

/// <summary>
/// Os filtros da lista, já conferidos (RF-172, RF-174). A conta vê só a dela;
/// o time, todas, na ordem de quem espera o time há mais tempo.
/// </summary>
/// <param name="ContaId">Só os desta conta; null para o time.</param>
/// <param name="Status">Só estes status; null, todos.</param>
/// <param name="Categoria">Só desta categoria.</param>
/// <param name="Busca">Número, sigla, nome da conta, instituição ou assunto.</param>
/// <param name="SemResponsavel">Só os sem responsável.</param>
/// <param name="ResponsavelId">Só os deste responsável.</param>
/// <param name="Atrasados">Só os atrasados agora (RN-289).</param>
/// <param name="NotaMaxima">Só os avaliados com nota até esta.</param>
/// <param name="Agora">A hora do atrasado.</param>
/// <param name="Pagina">A partir de 1.</param>
/// <param name="PorPagina">Até 50.</param>
public sealed record FiltroDeChamados(
    string? ContaId, IReadOnlyList<StatusDoChamado>? Status, Categoria? Categoria, string? Busca, bool SemResponsavel,
    string? ResponsavelId, bool Atrasados, int? NotaMaxima, DateTimeOffset Agora, int Pagina, int PorPagina)
{
    /// <summary>A ordem do time: quem espera o time há mais tempo primeiro.</summary>
    public bool OrdemDoTime => ContaId is null;
}

/// <summary>Uma página de chamados e o total com os filtros.</summary>
/// <param name="Itens">Os chamados da página.</param>
/// <param name="Total">Quantos há.</param>
public sealed record PaginaDeChamados(IReadOnlyList<DadosDoChamado> Itens, long Total);

/// <summary>As coleções chamados e mensagensChamado (sac-documentos v01).</summary>
public interface IChamados
{
    /// <summary>O chamado pelo número; null se não existe.</summary>
    Task<Chamado?> Obter(NumeroDoChamado id, CancellationToken ct = default);

    /// <summary>Grava o chamado novo e os itens dele.</summary>
    Task Incluir(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct = default);

    /// <summary>
    /// Grava a mudança se a versão guardada ainda é a lida (<see cref="Chamado.VersaoLida"/>);
    /// false se outro mudou antes: quem chama relê e tenta de novo.
    /// </summary>
    Task<bool> Salvar(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, CancellationToken ct = default);

    /// <summary>Abrir o chamado marca como lido do lado de quem lê (RF-173), sem mudar a versão.</summary>
    Task MarcarLido(NumeroDoChamado id, Visao visao, CancellationToken ct = default);

    /// <summary>A página da lista.</summary>
    Task<PaginaDeChamados> Listar(FiltroDeChamados filtro, CancellationToken ct = default);

    /// <summary>Quantos a conta tem em Aberto, Em atendimento ou Aguardando cliente (RN-294).</summary>
    Task<int> EmAndamentoDaConta(string contaId, CancellationToken ct = default);

    /// <summary>Os itens do chamado depois do número dado (todos, com null), na ordem.</summary>
    Task<IReadOnlyList<ItemDaConversa>> Itens(NumeroDoChamado id, long? depoisDe = null, CancellationToken ct = default);

    /// <summary>O número do último item do chamado; 0 se não há.</summary>
    Task<long> UltimoItem(NumeroDoChamado id, CancellationToken ct = default);

    /// <summary>Os chamados cujos 7 dias (Aguardando cliente ou Resolvido) venceram até agora (RN-290).</summary>
    Task<IReadOnlyList<Chamado>> VencidosEm(DateTimeOffset agora, CancellationToken ct = default);
}

/// <summary>A coleção contadores: um número por série, com $inc atômico.</summary>
public interface INumeracao
{
    /// <summary>A série dos chamados.</summary>
    const string Chamados = "chamados";

    /// <summary>A série das mensagens.</summary>
    const string Mensagens = "mensagensChamado";

    /// <summary>O próximo número da série.</summary>
    Task<long> Proximo(string serie, CancellationToken ct = default);
}

/// <summary>O aviso de mudança de um chamado às conexões ao vivo (RNF-230): canal sac:chamado:&lt;id&gt;.</summary>
public interface ICanalDoChamado
{
    /// <summary>Avisa que o chamado mudou.</summary>
    Task Publicar(NumeroDoChamado id, long versao, CancellationToken ct = default);

    /// <summary>Espera o aviso do chamado até o tempo dado; true se chegou.</summary>
    Task<bool> Esperar(NumeroDoChamado id, TimeSpan tempo, CancellationToken ct = default);
}

/// <summary>Os anexos do lado do chamado (RN-292): só os do próprio usuário, não usados nem vencidos.</summary>
public interface IAnexosDoChamado
{
    /// <summary>Todos existem, são de quem pede, não foram usados e não venceram.</summary>
    Task<bool> Disponiveis(IReadOnlyList<string> ids, string usuarioId, DateTimeOffset agora, CancellationToken ct = default);

    /// <summary>
    /// Liga os anexos ao chamado e à mensagem, com o expiraEm do ambiente
    /// (RNF-234) e, no armazenamento de objetos, a tag uso=chamado.
    /// </summary>
    Task Vincular(IReadOnlyList<string> ids, NumeroDoChamado chamado, string? mensagemId, bool interna, CancellationToken ct = default);

    /// <summary>
    /// O chamado fechou (RNF-234): em produção, os anexos dele ganham expiraEm =
    /// fechadoEm + 5 anos; no armazenamento de objetos, a tag uso=fechado e o metadado fechadoem.
    /// </summary>
    Task AoFechar(NumeroDoChamado chamado, DateTimeOffset fechadoEm, CancellationToken ct = default);

    /// <summary>O chamado reabriu: os anexos dele voltam ao uso no chamado (expiraEm do ambiente, tag uso=chamado).</summary>
    Task AoReabrir(NumeroDoChamado chamado, CancellationToken ct = default);
}

/// <summary>O prazo de primeira resposta de cada categoria (RF-181, RN-289).</summary>
public interface IPrazosDoSac
{
    /// <summary>As horas úteis da categoria.</summary>
    Task<int> HorasUteis(Categoria categoria, CancellationToken ct = default);
}

/// <summary>Os nomes do 17 (usuarios e contas) que o SAC copia no chamado e na conversa.</summary>
public interface IPessoasDoSac
{
    /// <summary>O nome do usuário.</summary>
    Task<string> Nome(string usuarioId, CancellationToken ct = default);

    /// <summary>A conta; null se não existe.</summary>
    Task<ContaDoChamado?> Conta(string contaId, CancellationToken ct = default);
}
