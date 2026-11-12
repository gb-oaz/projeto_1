using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Dtos;

/// <summary>
/// GET /usuarios/card (RN-189, 15 RN-276): o card no formato comum dos módulos,
/// com os 4 tamanhos e as 3 séries, sem o ícone (vem do Catálogo).
/// </summary>
/// <param name="Modulo">MOD-03.</param>
/// <param name="Perfil">DEBOAZ, INSTITUICAO, STAFF ou AUTONOMO.</param>
/// <param name="Versao">Sobe a cada mudança dos números.</param>
/// <param name="AtualizadoEm">A última mudança, em UTC.</param>
/// <param name="Vazio">True quando o módulo ainda não tem dados.</param>
/// <param name="Selo">O número do selo; 0 sem nada a avisar.</param>
/// <param name="Reduzida">O dado macro.</param>
/// <param name="Normal">O dado macro e até 3 de apoio.</param>
/// <param name="Expandida">Até 3 linhas.</param>
/// <param name="Series">Dia, semana e mês, 12 números cada, do mais antigo ao atual.</param>
public sealed record CardDoModuloDto(
    string Modulo, string Perfil, int Versao, DateTimeOffset AtualizadoEm, bool Vazio, int Selo,
    ReduzidaDto Reduzida, NormalDto Normal, ExpandidaDto Expandida, SeriesDto Series)
{
    /// <summary>O ID do módulo Usuários.</summary>
    public const string Usuarios = "MOD-03";

    /// <summary>O card guardado, com o que muda só com o tempo já refeito.</summary>
    public static CardDoModuloDto De(CardUsuarios card, NumerosDoCard numeros)
    {
        var rotulo = NomesDoCard.Rotulo(card.Perfil);
        var (dia, semana, mes) = card.Series();
        return new(Usuarios, card.Perfil.Texto(), card.Versao, card.AtualizadoEm, numeros.Vazio, numeros.Selo,
            new ReduzidaDto(numeros.Valor, rotulo),
            new NormalDto(numeros.Valor, rotulo, Linhas(numeros.Apoio)),
            new ExpandidaDto(Linhas(numeros.Lista)),
            new SeriesDto(dia, semana, mes));
    }

    static IReadOnlyList<LinhaDto> Linhas(IReadOnlyList<LinhaDoCard> linhas) =>
        linhas.Select(l => new LinhaDto(NomesDoCard.Nome(l.Chave), l.Valor)).ToList();
}

/// <summary>O tamanho reduzido: o dado macro.</summary>
/// <param name="Valor">O número.</param>
/// <param name="Rotulo">O que ele conta.</param>
public sealed record ReduzidaDto(int Valor, string Rotulo);

/// <summary>O tamanho normal: o dado macro e os números de apoio.</summary>
/// <param name="Valor">O número.</param>
/// <param name="Rotulo">O que ele conta.</param>
/// <param name="Apoio">Até 3, cada um com nome e valor.</param>
public sealed record NormalDto(int Valor, string Rotulo, IReadOnlyList<LinhaDto> Apoio);

/// <summary>O tamanho expandido: a lista.</summary>
/// <param name="Lista">Até 3 linhas, cada uma com nome e valor.</param>
public sealed record ExpandidaDto(IReadOnlyList<LinhaDto> Lista);

/// <summary>Uma linha do card.</summary>
/// <param name="Nome">O que conta.</param>
/// <param name="Valor">O número.</param>
public sealed record LinhaDto(string Nome, int Valor);

/// <summary>As séries do card (15 RN-277).</summary>
/// <param name="Dia">Os 12 últimos dias.</param>
/// <param name="Semana">As 12 últimas semanas.</param>
/// <param name="Mes">Os 12 últimos meses.</param>
public sealed record SeriesDto(IReadOnlyList<int> Dia, IReadOnlyList<int> Semana, IReadOnlyList<int> Mes);

/// <summary>GET e PUT /usuarios/eu/preferencias (RN-283).</summary>
/// <param name="AtualizacaoCards">NA_ABERTURA ou AO_VIVO.</param>
/// <param name="IntervaloAoVivo">5, 15, 30, 60 ou 300.</param>
public sealed record PreferenciasDto(string AtualizacaoCards, int IntervaloAoVivo)
{
    /// <summary>A escolha em texto.</summary>
    public static PreferenciasDto De(PreferenciasDeCards p) => new(PreferenciasDeCards.Texto(p.Atualizacao), p.IntervaloAoVivo);
}

/// <summary>PUT /usuarios/eu/preferencias: o corpo inteiro (RN-283).</summary>
/// <param name="QuemPede">Id de quem pede, do token.</param>
/// <param name="AtualizacaoCards">NA_ABERTURA ou AO_VIVO.</param>
/// <param name="IntervaloAoVivo">5, 15, 30, 60 ou 300.</param>
public sealed record NovasPreferenciasDto(string QuemPede, string? AtualizacaoCards, int? IntervaloAoVivo)
{
    /// <summary>A escolha conferida.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public PreferenciasDeCards Validar()
    {
        var atualizacao = PreferenciasDeCards.AtualizacaoDe(AtualizacaoCards) ?? throw FalhaDeUsuarios.DadosInvalidos("atualizacaoCards");
        return IntervaloAoVivo is { } intervalo && PreferenciasDeCards.Intervalos.Contains(intervalo)
            ? new PreferenciasDeCards(atualizacao, intervalo)
            : throw FalhaDeUsuarios.DadosInvalidos("intervaloAoVivo");
    }
}

/// <summary>GET /usuarios/card/ao-vivo (RN-281, 15 RN-279).</summary>
/// <param name="QuemPede">Id de quem pede, do token.</param>
/// <param name="Sid">A sessão do token, conferida a cada intervalo.</param>
/// <param name="Intervalo">Segundos, como veio na query; ausente é 300.</param>
public sealed record AberturaAoVivoDto(string QuemPede, string Sid, string? Intervalo)
{
    /// <summary>O intervalo conferido.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS em "intervalo".</exception>
    public TimeSpan Validar() =>
        Intervalo is null ? TimeSpan.FromSeconds(PreferenciasDeCards.Padrao.IntervaloAoVivo)
        : int.TryParse(Intervalo, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var s)
          && PreferenciasDeCards.Intervalos.Contains(s)
            ? TimeSpan.FromSeconds(s)
            : throw FalhaDeUsuarios.DadosInvalidos("intervalo");
}

/// <summary>O que o fluxo ao vivo manda em seguida.</summary>
/// <param name="Card">O card novo, se a versao mudou.</param>
/// <param name="Encerrado">Por que o servidor encerra; a conexão fecha depois dele.</param>
/// <param name="Ping">O comentário de manutenção de 30 s.</param>
public sealed record PassoAoVivoDto(CardDoModuloDto? Card = null, MotivoEncerramento? Encerrado = null, bool Ping = false);

/// <summary>Uma mudança no cadastro que mexe nos cards (RN-278, RN-280).</summary>
/// <param name="ContaId">A conta que mudou.</param>
/// <param name="UsuarioId">O usuário que mudou; null quando foi a conta (o DEBOAZ também é recontado).</param>
public sealed record MudancaNoCadastroDto(string ContaId, string? UsuarioId = null);
