using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Hub.Sac.Card.Domain;

/// <summary>O perfil do card do SAC (RN-295).</summary>
public enum PerfilDoCard
{
    /// <summary>O time deboaz: todas as contas.</summary>
    Deboaz,
    /// <summary>A conta master de uma instituição.</summary>
    Instituicao,
    /// <summary>O educador autônomo.</summary>
    Autonomo,
}

/// <summary>
/// Os números do card num escopo (a conta, ou DEBOAZ para todas), contados
/// agora em chamados (sac-documentos v01).
/// </summary>
/// <param name="Total">Quantos chamados o escopo tem: 0 é o card vazio.</param>
/// <param name="EmAndamento">Aberto, Em atendimento ou Aguardando cliente.</param>
/// <param name="AguardandoCliente">Aguardando a conta.</param>
/// <param name="EmAtendimento">Só Em atendimento.</param>
/// <param name="ResolvidosNoMes">Resolvidos no mês de Brasília.</param>
/// <param name="NaoLidosPelaConta">Com resposta nova que a conta não abriu.</param>
/// <param name="SemResposta">Esperando o time, em Aberto ou Em atendimento.</param>
/// <param name="Atrasados">Sem a primeira resposta depois do prazo.</param>
/// <param name="NotaMedia">A média das avaliações; null sem avaliação.</param>
/// <param name="Versao">Sobe a cada mudança num chamado do escopo.</param>
/// <param name="AtualizadoEm">A última mudança; null sem chamado.</param>
public sealed record ContagemDoSac(
    int Total, int EmAndamento, int AguardandoCliente, int EmAtendimento, int ResolvidosNoMes, int NaoLidosPelaConta, int SemResposta,
    int Atrasados, decimal? NotaMedia, long Versao, DateTimeOffset? AtualizadoEm);

/// <summary>Uma linha do card: o que conta e o número.</summary>
/// <param name="Nome">"Aguardando você".</param>
/// <param name="Valor">O número.</param>
public sealed record LinhaDoCard(string Nome, decimal Valor);

/// <summary>
/// O card do SAC (RN-295, 15 RN-276): o dado macro (chamados em andamento),
/// os números de apoio e a lista de cada perfil, o selo e as séries de dia,
/// semana e mês, 12 números cada.
/// </summary>
/// <param name="Perfil">DEBOAZ, INSTITUICAO ou AUTONOMO.</param>
/// <param name="Contagem">Os números agora.</param>
/// <param name="Dia">12 dias.</param>
/// <param name="Semana">12 semanas.</param>
/// <param name="Mes">12 meses.</param>
/// <param name="Agora">A hora da leitura.</param>
public sealed record CardDoSac(PerfilDoCard Perfil, ContagemDoSac Contagem, IReadOnlyList<int> Dia, IReadOnlyList<int> Semana,
    IReadOnlyList<int> Mes, DateTimeOffset Agora)
{
    /// <summary>O ID do módulo SAC.</summary>
    public const string Modulo = "MOD-02";

    /// <summary>O rótulo do dado macro.</summary>
    public const string Rotulo = "chamados em andamento";

    /// <summary>O escopo do time.</summary>
    public const string EscopoDoTime = "DEBOAZ";

    /// <summary>DEBOAZ, INSTITUICAO ou AUTONOMO.</summary>
    public string PerfilTexto => Perfil switch
    {
        PerfilDoCard.Deboaz => "DEBOAZ",
        PerfilDoCard.Instituicao => "INSTITUICAO",
        _ => "AUTONOMO",
    };

    /// <summary>O time: sem resposta; a instituição: respostas não lidas; o autônomo, sem selo.</summary>
    public int Selo => Perfil switch
    {
        PerfilDoCard.Deboaz => Contagem.SemResposta,
        PerfilDoCard.Instituicao => Contagem.NaoLidosPelaConta,
        _ => 0,
    };

    /// <summary>Sem nenhum chamado.</summary>
    public bool Vazio => Contagem.Total == 0;

    /// <summary>A última mudança, ou agora.</summary>
    public DateTimeOffset AtualizadoEm => Contagem.AtualizadoEm ?? Agora;

    /// <summary>Os números de apoio da normal, na ordem da RN-295.</summary>
    public IReadOnlyList<LinhaDoCard> Apoio => Perfil == PerfilDoCard.Deboaz
        ? [new("Sem resposta", Contagem.SemResposta), new("Atrasados", Contagem.Atrasados), new("Nota média", Contagem.NotaMedia ?? 0)]
        : DaConta;

    /// <summary>A lista da expandida, na ordem da RN-295.</summary>
    public IReadOnlyList<LinhaDoCard> Lista => Perfil == PerfilDoCard.Deboaz
        ? [new("Sem resposta", Contagem.SemResposta), new("Atrasados", Contagem.Atrasados), new("Aguardando cliente", Contagem.AguardandoCliente)]
        : DaConta;

    IReadOnlyList<LinhaDoCard> DaConta =>
        [new("Aguardando você", Contagem.AguardandoCliente), new("Em atendimento", Contagem.EmAtendimento),
            new("Resolvidos no mês", Contagem.ResolvidosNoMes)];

    /// <summary>
    /// As séries (RN-295: os chamados em andamento no fim de cada dia, semana e
    /// mês), dos dias fechados à 00:00 e o número de agora no último ponto.
    /// </summary>
    /// <param name="fechados">Em andamento no fim de cada dia fechado.</param>
    /// <param name="hoje">O dia de agora, em Brasília.</param>
    /// <param name="agora">Em andamento agora.</param>
    public static (IReadOnlyList<int> Dia, IReadOnlyList<int> Semana, IReadOnlyList<int> Mes) Series(
        IReadOnlyDictionary<DateOnly, int> fechados, DateOnly hoje, int agora)
    {
        const int n = CalendarioDoCard.NaSerie;
        int NoFim(DateOnly ultimo) =>
            fechados.Where(f => f.Key <= ultimo).OrderByDescending(f => f.Key).Select(f => f.Value).FirstOrDefault();
        var dia = Enumerable.Range(1, n - 1).Reverse().Select(i => fechados.GetValueOrDefault(hoje.AddDays(-i))).Append(agora).ToList();
        var domingo = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7) - 1);
        var semana = Enumerable.Range(0, n - 1).Reverse().Select(i => NoFim(domingo.AddDays(-7 * i))).Append(agora).ToList();
        var primeiro = new DateOnly(hoje.Year, hoje.Month, 1);
        var mes = Enumerable.Range(0, n - 1).Reverse().Select(i => NoFim(primeiro.AddMonths(-i).AddDays(-1))).Append(agora).ToList();
        return (dia, semana, mes);
    }
}
