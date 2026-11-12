using System.Globalization;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>O perfil de quem vê o card Usuários (RN-189): cada um tem o seu card guardado.</summary>
public enum PerfilCard
{
    /// <summary>Administrador e membros do time: um card só, na conta do time.</summary>
    Deboaz,
    /// <summary>A conta master da instituição.</summary>
    Instituicao,
    /// <summary>A staff com o Usuários: só os usuários que ela criou.</summary>
    Staff,
    /// <summary>O educador autônomo.</summary>
    Autonomo,
}

/// <summary>Por que o servidor encerra a conexão ao vivo (RN-281, 15 RN-279).</summary>
public enum MotivoEncerramento
{
    /// <summary>4ª conexão do usuário: a mais antiga sai.</summary>
    LimiteConexoes,
    /// <summary>A sessão da conexão terminou.</summary>
    SessaoTerminada,
    /// <summary>O MOD-03 saiu de quem está conectado.</summary>
    AcessoNegado,
}

/// <summary>Os textos de perfil e motivo, como no contrato.</summary>
public static class TextosDoCard
{
    /// <summary>DEBOAZ, INSTITUICAO, STAFF ou AUTONOMO.</summary>
    public static string Texto(this PerfilCard perfil) => perfil switch
    {
        PerfilCard.Deboaz => "DEBOAZ",
        PerfilCard.Instituicao => "INSTITUICAO",
        PerfilCard.Staff => "STAFF",
        _ => "AUTONOMO",
    };

    /// <summary>O perfil pelo texto gravado; null se não for nenhum.</summary>
    public static PerfilCard? PerfilDe(string? texto) =>
        Enum.GetValues<PerfilCard>().Cast<PerfilCard?>().FirstOrDefault(p => p!.Value.Texto() == texto);

    /// <summary>LIMITE_CONEXOES, SESSAO_TERMINADA ou ACESSO_NEGADO.</summary>
    public static string Texto(this MotivoEncerramento motivo) => motivo switch
    {
        MotivoEncerramento.LimiteConexoes => "LIMITE_CONEXOES",
        MotivoEncerramento.SessaoTerminada => "SESSAO_TERMINADA",
        _ => "ACESSO_NEGADO",
    };

    /// <summary>O motivo pelo texto; null se não for nenhum.</summary>
    public static MotivoEncerramento? MotivoDe(string? texto) =>
        Enum.GetValues<MotivoEncerramento>().Cast<MotivoEncerramento?>().FirstOrDefault(m => m!.Value.Texto() == texto);
}

/// <summary>Um número do apoio ou da lista do card: a chave gravada e o valor.</summary>
/// <param name="Chave">A chave em cardUsuarios (staff, semAcesso30Dias…).</param>
/// <param name="Valor">O número.</param>
public sealed record LinhaDoCard(string Chave, int Valor);

/// <summary>
/// Os números do card agora (RN-280): o selo, o dado macro, até 3 de apoio e até
/// 3 da lista, na ordem do contrato.
/// </summary>
/// <param name="Selo">O selo; 0 sem nada a avisar.</param>
/// <param name="Valor">O dado macro.</param>
/// <param name="Apoio">Os números de apoio (normal).</param>
/// <param name="Lista">Os números da lista (expandida).</param>
public sealed record NumerosDoCard(int Selo, int Valor, IReadOnlyList<LinhaDoCard> Apoio, IReadOnlyList<LinhaDoCard> Lista)
{
    /// <summary>Sem nenhum número: o módulo ainda não tem dados.</summary>
    public bool Vazio => Selo == 0 && Valor == 0 && Apoio.All(l => l.Valor == 0) && Lista.All(l => l.Valor == 0);

    /// <summary>Com o que muda só com o tempo refeito agora (RN-280), sem mexer no resto.</summary>
    public NumerosDoCard ComTempo(NumerosNoTempo tempo) => this with
    {
        Selo = tempo.Selo,
        Lista = Lista.Select(l => tempo.Lista.TryGetValue(l.Chave, out var v) ? l with { Valor = v } : l).ToList(),
    };

    /// <summary>Os mesmos números.</summary>
    public bool Iguais(NumerosDoCard outros) =>
        Selo == outros.Selo && Valor == outros.Valor && Apoio.SequenceEqual(outros.Apoio) && Lista.SequenceEqual(outros.Lista);
}

/// <summary>O que muda só com o tempo (RN-280): o selo e alguns números da lista, refeitos na leitura.</summary>
/// <param name="Selo">O selo agora.</param>
/// <param name="Lista">Chave da lista → valor agora (semAcesso30Dias, codigosVencidos…).</param>
public sealed record NumerosNoTempo(int Selo, IReadOnlyDictionary<string, int> Lista);

/// <summary>Um ponto da série: o dia (2026-11-08), a semana ISO (2026-W45) ou o mês (2026-11).</summary>
/// <param name="Chave">O período.</param>
/// <param name="Valor">O dado macro no período.</param>
public sealed record PontoDaSerie(string Chave, int Valor);

/// <summary>
/// O calendário do card: os dias, semanas e meses contam no horário de Brasília
/// (15 RN-278), e o fechamento é à 00:00 de lá.
/// </summary>
public static class CalendarioDoCard
{
    /// <summary>Brasília, sem horário de verão desde 2019; -03:00 se o sistema não tiver o fuso.</summary>
    public static TimeZoneInfo Fuso { get; } = Achar();

    /// <summary>Dias, semanas e meses guardados (15 RN-278).</summary>
    public const int Dias = 400, Semanas = 106, Meses = 24;

    /// <summary>Quantos de cada recorte a API mostra (15 RN-277).</summary>
    public const int NaSerie = 12;

    /// <summary>Há quanto tempo sem acesso a pessoa (ou a conta) conta em "Sem acesso há 30 dias".</summary>
    public static readonly TimeSpan SemAcesso = TimeSpan.FromDays(30);

    /// <summary>O dia de Brasília da hora dada.</summary>
    public static DateOnly Dia(DateTimeOffset agora) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agora, Fuso).DateTime);

    /// <summary>A 00:00 de Brasília que começa o dia seguinte, em UTC.</summary>
    public static DateTimeOffset FimDoDia(DateTimeOffset agora)
    {
        var amanha = Dia(agora).AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(amanha, Fuso.GetUtcOffset(amanha)).ToUniversalTime();
    }

    /// <summary>2026-11-08.</summary>
    public static string ChaveDoDia(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>2026-W45: a semana ISO, que começa na segunda.</summary>
    public static string ChaveDaSemana(DateOnly dia)
    {
        var data = dia.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(data)}-W{ISOWeek.GetWeekOfYear(data):D2}";
    }

    /// <summary>2026-11.</summary>
    public static string ChaveDoMes(DateOnly dia) => dia.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    static TimeZoneInfo Achar()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (Exception erro) when (erro is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Brasilia", TimeSpan.FromHours(-3), "Brasília", "Brasília");
        }
    }
}

/// <summary>
/// O card Usuários guardado (cardUsuarios, RN-280, 15 RN-278): os números de
/// agora, a versao que sobe a cada mudança e as séries de dias, semanas e meses.
/// </summary>
public sealed class CardUsuarios
{
    readonly List<PontoDaSerie> dias;
    readonly List<PontoDaSerie> semanas;
    readonly List<PontoDaSerie> meses;

    /// <summary>Remonta um card gravado.</summary>
    public CardUsuarios(string id, string contaId, PerfilCard perfil, string? usuarioId, int versao, DateTimeOffset atualizadoEm,
        NumerosDoCard atual, IEnumerable<PontoDaSerie> dias, IEnumerable<PontoDaSerie> semanas, IEnumerable<PontoDaSerie> meses)
    {
        Id = id;
        ContaId = contaId;
        Perfil = perfil;
        UsuarioId = usuarioId;
        Versao = versao;
        AtualizadoEm = atualizadoEm;
        Atual = atual;
        this.dias = [.. dias];
        this.semanas = [.. semanas];
        this.meses = [.. meses];
    }

    /// <summary>Um card que ainda não foi contado: versao 0, sem números nem séries.</summary>
    public static CardUsuarios Novo(string sigla, PerfilCard perfil, string contaId, string? usuarioId, string? nomeCurto,
        DateTimeOffset agora) =>
        new(IdDo(sigla, perfil, nomeCurto), contaId, perfil, usuarioId, 0, agora, new NumerosDoCard(0, 0, [], []), [], [], []);

    /// <summary>card-&lt;sigla&gt;-&lt;PERFIL&gt;[-&lt;nomeCurto&gt;]: card-ebfga-INSTITUICAO, card-ebfga-STAFF-maria.</summary>
    public static string IdDo(string sigla, PerfilCard perfil, string? nomeCurto) =>
        $"card-{sigla.ToLowerInvariant()}-{perfil.Texto()}" + (perfil == PerfilCard.Staff ? $"-{nomeCurto}" : "");

    /// <summary>card-ebfga-INSTITUICAO.</summary>
    public string Id { get; }

    /// <summary>A conta do card (no DEBOAZ, a do time).</summary>
    public string ContaId { get; }

    /// <summary>O perfil.</summary>
    public PerfilCard Perfil { get; }

    /// <summary>A staff dona do card; null nos outros perfis.</summary>
    public string? UsuarioId { get; }

    /// <summary>Sobe a cada mudança dos números e a cada fechamento.</summary>
    public int Versao { get; private set; }

    /// <summary>A última mudança.</summary>
    public DateTimeOffset AtualizadoEm { get; private set; }

    /// <summary>Os números de agora.</summary>
    public NumerosDoCard Atual { get; private set; }

    /// <summary>Os dias guardados, do mais antigo ao atual.</summary>
    public IReadOnlyList<PontoDaSerie> Dias => dias;

    /// <summary>As semanas guardadas, do mais antigo ao atual.</summary>
    public IReadOnlyList<PontoDaSerie> Semanas => semanas;

    /// <summary>Os meses guardados, do mais antigo ao atual.</summary>
    public IReadOnlyList<PontoDaSerie> Meses => meses;

    /// <summary>
    /// Os números recontados na execução que mudou o cadastro (RN-278): grava os
    /// números e o ponto de hoje das séries e sobe a versao, se algo mudou.
    /// </summary>
    /// <returns>True se mudou (e precisa ser gravado).</returns>
    public bool Aplicar(NumerosDoCard numeros, DateTimeOffset agora)
    {
        var mudou = Versao == 0 | !Atual.Iguais(numeros);
        mudou |= Marcar(CalendarioDoCard.Dia(agora), numeros.Valor);
        if (!mudou) return false;
        Subir(numeros, agora);
        return true;
    }

    /// <summary>
    /// O fechamento da 00:00 de Brasília (15 RN-278): abre o dia, a semana e o
    /// mês de hoje, corta em 400, 106 e 24 e sobe a versao.
    /// </summary>
    public void FecharDia(NumerosDoCard numeros, DateTimeOffset agora)
    {
        Marcar(CalendarioDoCard.Dia(agora), numeros.Valor);
        Subir(numeros, agora);
    }

    /// <summary>Os últimos 12 de cada recorte (15 RN-277), com zeros antes do primeiro guardado.</summary>
    public (IReadOnlyList<int> Dia, IReadOnlyList<int> Semana, IReadOnlyList<int> Mes) Series() =>
        (Ultimos(dias), Ultimos(semanas), Ultimos(meses));

    void Subir(NumerosDoCard numeros, DateTimeOffset agora)
    {
        Atual = numeros;
        Versao++;
        AtualizadoEm = agora;
    }

    // o ponto de hoje em cada recorte: novo período entra no fim; true se algo mudou
    bool Marcar(DateOnly hoje, int valor) =>
        Marcar(dias, CalendarioDoCard.ChaveDoDia(hoje), valor, CalendarioDoCard.Dias)
        | Marcar(semanas, CalendarioDoCard.ChaveDaSemana(hoje), valor, CalendarioDoCard.Semanas)
        | Marcar(meses, CalendarioDoCard.ChaveDoMes(hoje), valor, CalendarioDoCard.Meses);

    static bool Marcar(List<PontoDaSerie> serie, string chave, int valor, int limite)
    {
        if (serie.Count > 0 && serie[^1].Chave == chave)
        {
            if (serie[^1].Valor == valor) return false;
            serie[^1] = new PontoDaSerie(chave, valor);
            return true;
        }
        serie.Add(new PontoDaSerie(chave, valor));
        if (serie.Count > limite) serie.RemoveRange(0, serie.Count - limite);
        return true;
    }

    static IReadOnlyList<int> Ultimos(List<PontoDaSerie> serie)
    {
        var valores = serie.TakeLast(CalendarioDoCard.NaSerie).Select(p => p.Valor).ToList();
        return [.. Enumerable.Repeat(0, CalendarioDoCard.NaSerie - valores.Count), .. valores];
    }
}

/// <summary>Os nomes e o rótulo que o card mostra em cada perfil (contrato v14, RN-280).</summary>
public static class NomesDoCard
{
    /// <summary>Chaves gravadas de cada número.</summary>
    public const string Staff = "staff", Participantes = "participantes", Pendentes = "pendentes", CriadosNoMes = "criadosNoMes",
        SemAcesso30Dias = "semAcesso30Dias", Desativados = "desativados", CodigosVencidos = "codigosVencidos",
        Instituicoes = "instituicoes", Autonomos = "autonomos", Suspensas = "suspensas",
        MasterSemAcesso30Dias = "masterSemAcesso30Dias", Desativadas = "desativadas", Encerradas = "encerradas";

    static readonly Dictionary<string, string> Nomes = new()
    {
        [Staff] = "staff",
        [Participantes] = "participantes",
        [Pendentes] = "pendentes",
        [CriadosNoMes] = "criados no mês",
        [SemAcesso30Dias] = "Sem acesso há 30 dias",
        [Desativados] = "Desativados",
        [CodigosVencidos] = "Códigos vencidos",
        [Instituicoes] = "instituições",
        [Autonomos] = "autônomos",
        [Suspensas] = "suspensas",
        [MasterSemAcesso30Dias] = "Master sem acesso há 30 dias",
        [Desativadas] = "Desativadas",
        [Encerradas] = "Encerradas",
    };

    /// <summary>O nome que a pessoa lê (a própria chave, se não for conhecida).</summary>
    public static string Nome(string chave) => Nomes.GetValueOrDefault(chave, chave);

    /// <summary>O rótulo do dado macro: contas ativas, usuários ativos ou participantes ativos.</summary>
    public static string Rotulo(PerfilCard perfil) => perfil switch
    {
        PerfilCard.Deboaz => "contas ativas",
        PerfilCard.Autonomo => "participantes ativos",
        _ => "usuários ativos",
    };
}

/// <summary>Quem vê o card e qual card é o dele (RN-189).</summary>
public static class DonoDoCard
{
    /// <summary>
    /// O perfil de quem pede: o time vê o DEBOAZ, a conta master o da instituição
    /// ou o do autônomo, a staff o dela; o participante não tem o card.
    /// </summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO para o participante.</exception>
    public static PerfilCard Perfil(UsuarioCadastrado quem, ContaAssinante conta) => quem.Tipo switch
    {
        TipoUsuario.Administrador or TipoUsuario.MembroTime => PerfilCard.Deboaz,
        TipoUsuario.Master => conta.DeAutonomo ? PerfilCard.Autonomo : PerfilCard.Instituicao,
        TipoUsuario.Staff => PerfilCard.Staff,
        _ => throw FalhaDeUsuarios.AcessoNegado(),
    };

    /// <summary>O perfil do card da conta: o do autônomo ou o da instituição.</summary>
    public static PerfilCard DaConta(ContaAssinante conta) => conta.DeAutonomo ? PerfilCard.Autonomo : PerfilCard.Instituicao;
}

/// <summary>Como a pessoa quer os cards atualizados (RN-283).</summary>
public enum AtualizacaoCards
{
    /// <summary>Lidos ao abrir Módulos.</summary>
    NaAbertura,
    /// <summary>Pela conexão ao vivo, no intervalo escolhido.</summary>
    AoVivo,
}

/// <summary>A escolha de atualização dos cards (usuarios.preferencias, RN-283).</summary>
/// <param name="Atualizacao">Na abertura ou ao vivo.</param>
/// <param name="IntervaloAoVivo">Segundos entre as conferências ao vivo: 5, 15, 30, 60 ou 300.</param>
public sealed record PreferenciasDeCards(AtualizacaoCards Atualizacao, int IntervaloAoVivo)
{
    /// <summary>Os intervalos aceitos, em segundos.</summary>
    public static readonly IReadOnlySet<int> Intervalos = new HashSet<int> { 5, 15, 30, 60, 300 };

    /// <summary>Sem escolha salva: na abertura, 300 s.</summary>
    public static PreferenciasDeCards Padrao { get; } = new(AtualizacaoCards.NaAbertura, 300);

    /// <summary>NA_ABERTURA ou AO_VIVO.</summary>
    public static string Texto(AtualizacaoCards a) => a == AtualizacaoCards.AoVivo ? "AO_VIVO" : "NA_ABERTURA";

    /// <summary>A atualização pelo texto; null se não for nenhuma.</summary>
    public static AtualizacaoCards? AtualizacaoDe(string? texto) => texto switch
    {
        "NA_ABERTURA" => AtualizacaoCards.NaAbertura,
        "AO_VIVO" => AtualizacaoCards.AoVivo,
        _ => null,
    };
}
