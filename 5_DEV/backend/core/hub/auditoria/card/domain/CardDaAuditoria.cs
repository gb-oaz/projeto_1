using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Hub.Auditoria.Card.Domain;

/// <summary>
/// Um dia do card da Auditoria (cardAuditoria, RN-322): os erros, os alertas e as execuções interrompidas do dia de
/// Brasília, fechados pelo agendamento a cada minuto. Fica guardado 400 dias (15 RN-278).
/// </summary>
/// <param name="Dia">O dia, em Brasília.</param>
/// <param name="Erros">Registros de erro de nível ERRO.</param>
/// <param name="Alertas">Registros de erro de nível ALERTA.</param>
/// <param name="Interrompidas">Execuções interrompidas.</param>
/// <param name="Versao">Sobe a cada vez que o dia muda.</param>
public sealed record DiaDoCard(DateOnly Dia, int Erros, int Alertas, int Interrompidas, long Versao)
{
    /// <summary>Quantos dias o documento fica guardado.</summary>
    public const int DiasDeGuarda = CalendarioDoCard.Dias;

    /// <summary>O _id do documento: deboaz:2026-11-20.</summary>
    public string Id => $"deboaz:{CalendarioDoCard.ChaveDoDia(Dia)}";

    /// <summary>Até quando fica guardado: o dia mais 400 dias.</summary>
    public DateTimeOffset GuardarAte => new DateTimeOffset(Dia.AddDays(DiasDeGuarda).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>O dia mudou em relação ao guardado: os números são outros.</summary>
    public bool MudouDe(DiaDoCard? guardado) =>
        guardado is null ? Erros + Alertas + Interrompidas > 0 : guardado.Erros != Erros || guardado.Alertas != Alertas || guardado.Interrompidas != Interrompidas;
}

/// <summary>
/// O card da Auditoria (RN-322, 15 RN-276), só do time deboaz: o dado macro é os serviços fora do ar (o selo e a reduzida)
/// e os erros de hoje (a normal), com os alertas e as execuções interrompidas. Os serviços fora do ar vêm da última
/// verificação; os erros, do dia fechado pelo agendamento.
/// </summary>
/// <param name="ForaDoAr">Quantos serviços estão Fora do ar.</param>
/// <param name="Dias">Os dias guardados dos últimos 12 meses, com o de hoje.</param>
/// <param name="Hoje">O dia de hoje, de Brasília.</param>
/// <param name="Agora">A hora da leitura.</param>
public sealed record CardDaAuditoria(int ForaDoAr, IReadOnlyList<DiaDoCard> Dias, DateOnly Hoje, DateTimeOffset Agora)
{
    /// <summary>O módulo do card.</summary>
    public const string Modulo = "MOD-17";

    /// <summary>O perfil: o time deboaz.</summary>
    public const string Perfil = "DEBOAZ";

    /// <summary>O número de dias, semanas e meses de cada série.</summary>
    public const int NaSerie = CalendarioDoCard.NaSerie;

    /// <summary>Os números de hoje.</summary>
    public DiaDoCard DeHoje => Dias.FirstOrDefault(d => d.Dia == Hoje) ?? new DiaDoCard(Hoje, 0, 0, 0, 0);

    /// <summary>"serviço fora do ar" no singular, "serviços fora do ar" nos outros.</summary>
    public string Rotulo => ForaDoAr == 1 ? "serviço fora do ar" : "serviços fora do ar";

    /// <summary>Sem serviço fora do ar nem erro hoje: o card mostra "Tudo no ar".</summary>
    public bool Vazio => ForaDoAr == 0 && DeHoje.Erros == 0;

    /// <summary>
    /// A versão junta a do dia e os serviços fora do ar, que vêm da última verificação, para o card ao vivo avisar quando
    /// um deles muda.
    /// </summary>
    public long Versao => DeHoje.Versao * 100 + Math.Min(ForaDoAr, 99);

    /// <summary>Os erros de cada um dos últimos 12 dias, do mais antigo ao atual.</summary>
    public IReadOnlyList<int> SerieDoDia => [.. Enumerable.Range(0, NaSerie).Reverse().Select(i => ErrosEntre(Hoje.AddDays(-i), Hoje.AddDays(-i)))];

    /// <summary>Os erros de cada uma das últimas 12 semanas (de segunda a domingo), a atual até hoje.</summary>
    public IReadOnlyList<int> SerieDaSemana
    {
        get
        {
            var segunda = Hoje.AddDays(-(((int)Hoje.DayOfWeek + 6) % 7));
            return [.. Enumerable.Range(0, NaSerie).Reverse().Select(i => ErrosEntre(segunda.AddDays(-7 * i), segunda.AddDays(-7 * i + 6)))];
        }
    }

    /// <summary>Os erros de cada um dos últimos 12 meses, o atual até hoje.</summary>
    public IReadOnlyList<int> SerieDoMes
    {
        get
        {
            var primeiro = new DateOnly(Hoje.Year, Hoje.Month, 1);
            return [.. Enumerable.Range(0, NaSerie).Reverse().Select(i =>
            {
                var inicio = primeiro.AddMonths(-i);
                return ErrosEntre(inicio, inicio.AddMonths(1).AddDays(-1));
            })];
        }
    }

    /// <summary>O primeiro dia que a leitura precisa dos dias guardados: o começo do mês de 11 meses atrás.</summary>
    public static DateOnly Desde(DateOnly hoje) => new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-(NaSerie - 1));

    int ErrosEntre(DateOnly de, DateOnly ate) => Dias.Where(d => d.Dia >= de && d.Dia <= ate).Sum(d => d.Erros);
}
