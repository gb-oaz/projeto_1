using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>Uma página de uma lista (RN-185): 50 itens, de A a Z, com o total.</summary>
/// <param name="Itens">Os itens da página.</param>
/// <param name="Pagina">A página, começando em 1.</param>
/// <param name="Total">O total que atende ao filtro.</param>
public sealed record PaginaDe<T>(IReadOnlyList<T> Itens, int Pagina, int Total)
{
    /// <summary>Itens por página.</summary>
    public const int Tamanho = 50;
}

/// <summary>
/// Filtro da lista de usuários (RN-185). A busca acha nome, entrada, matrícula e
/// código de acesso, sem diferenciar maiúsculas.
/// </summary>
/// <param name="ContaId">Só os da conta de quem pede.</param>
/// <param name="Tipos">Os tipos listados: o pedido, ou todos os que quem pede pode listar.</param>
/// <param name="Busca">Termo buscado; null lista todos.</param>
/// <param name="Estado">Só os do estado; null lista todos.</param>
/// <param name="Pagina">Página, começando em 1.</param>
public sealed record FiltroDeUsuarios(string ContaId, IReadOnlySet<TipoUsuario> Tipos, string? Busca, EstadoUsuario? Estado, int Pagina);

/// <summary>
/// Filtro da lista de contas do time (RN-185): a busca acha nome, sigla e o
/// documento (com ou sem pontuação). A conta do time não entra.
/// </summary>
/// <param name="Busca">Termo buscado; null lista todas.</param>
/// <param name="Estado">Só as do estado; null lista todas.</param>
/// <param name="Pagina">Página, começando em 1.</param>
public sealed record FiltroDeContas(string? Busca, EstadoConta? Estado, int Pagina);

/// <summary>
/// Os números do card Usuários (RN-189), lidos dos indicadores: a série dos
/// últimos 5 meses termina no valor de agora.
/// </summary>
/// <param name="Selo">Códigos de liberação que vencem hoje.</param>
/// <param name="Meses">Ativos no fim de cada um dos 5 últimos meses; o último é agora.</param>
/// <param name="Staff">Staff ativo; null no autônomo e no time.</param>
/// <param name="Participantes">Participantes ativos; null no time.</param>
public sealed record NumerosDoCard(int Selo, IReadOnlyList<int> Meses, int? Staff, int? Participantes)
{
    /// <summary>O dado principal: o valor de agora.</summary>
    public int Dado => Meses.Count > 0 ? Meses[^1] : 0;

    /// <summary>Quanto subiu ou desceu desde o fim do mês anterior.</summary>
    public int NoMes => Meses.Count > 1 ? Meses[^1] - Meses[^2] : Dado;

    /// <summary>
    /// O rótulo do dado para quem vê: usuários ativos na instituição,
    /// participantes ativos no autônomo e contas ativas no time.
    /// </summary>
    public static string Rotulo(TipoUsuario quem, bool contaDeAutonomo) =>
        quem.DoTime() ? "contas ativas" : contaDeAutonomo ? "participantes ativos" : "usuários ativos";
}
