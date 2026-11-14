using Boaz.Core.Hub.Sac.Chamados.Domain;

namespace Boaz.Core.Hub.Sac.Configuracao.Domain;

/// <summary>
/// Os prazos de primeira resposta de cada categoria, em horas úteis (RF-181,
/// RN-289): de 1 a 80; o novo vale para os chamados abertos depois.
/// </summary>
/// <param name="Horas">As horas úteis de cada uma das 6 categorias.</param>
/// <param name="SalvaEm">Quando foram salvos; null no padrão.</param>
/// <param name="SalvaPor">O nome de quem salvou; null no padrão.</param>
public sealed record PrazosDoSac(IReadOnlyDictionary<Categoria, int> Horas, DateTimeOffset? SalvaEm, string? SalvaPor)
{
    /// <summary>De 1 a 80 horas úteis.</summary>
    public const int Minimo = 1, Maximo = 80;

    /// <summary>O padrão da RN-289.</summary>
    public static PrazosDoSac Padrao { get; } = new(new Dictionary<Categoria, int>
    {
        [Categoria.AcessoESenha] = 4,
        [Categoria.Cobranca] = 8,
        [Categoria.ModuloComProblema] = 8,
        [Categoria.DuvidaDeUso] = 16,
        [Categoria.SugestaoAoDeboaz] = 40,
        [Categoria.SenhaContaMaster] = 4,
    }, null, null);

    /// <summary>As horas da categoria; a que faltar no guardado vem do padrão.</summary>
    public int HorasDe(Categoria categoria) =>
        Horas.TryGetValue(categoria, out var horas) ? horas : Padrao.Horas[categoria];

    /// <summary>Os prazos com as horas novas das categorias dadas.</summary>
    /// <exception cref="FalhaDoSac">FORA_DOS_LIMITES fora de 1 a 80.</exception>
    public PrazosDoSac Definir(IReadOnlyList<(Categoria Categoria, int Horas)> novos, string salvaPor, DateTimeOffset agora)
    {
        var horas = Enum.GetValues<Categoria>().ToDictionary(c => c, HorasDe);
        foreach (var (categoria, valor) in novos)
        {
            if (valor is < Minimo or > Maximo) throw FalhaDoSac.ForaDosLimites("horasUteis", Minimo, Maximo);
            horas[categoria] = valor;
        }
        return new PrazosDoSac(horas, agora, salvaPor);
    }
}
