namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>
/// As abas de uma permissão (RN-251): Configuração (C), Utilização (U) ou as duas
/// (CU). Uma permissão sem aba não existe: o módulo sai do mapa.
/// </summary>
public readonly record struct Abas
{
    Abas(bool configuracao, bool utilizacao) => (Configuracao, Utilizacao) = (configuracao, utilizacao);

    /// <summary>Só a aba Configuração.</summary>
    public static Abas C { get; } = new(true, false);

    /// <summary>Só a aba Utilização.</summary>
    public static Abas U { get; } = new(false, true);

    /// <summary>As duas abas.</summary>
    public static Abas CU { get; } = new(true, true);

    /// <summary>Tem a aba Configuração.</summary>
    public bool Configuracao { get; }

    /// <summary>Tem a aba Utilização.</summary>
    public bool Utilizacao { get; }

    /// <summary>Lê "C", "U" ou "CU"; qualquer outro texto é null.</summary>
    public static Abas? De(string? texto) => texto switch
    {
        "C" => C,
        "U" => U,
        "CU" => CU,
        _ => null,
    };

    /// <summary>Tem todas as abas de <paramref name="outras"/>.</summary>
    public bool Contem(Abas outras) => (Configuracao || !outras.Configuracao) && (Utilizacao || !outras.Utilizacao);

    /// <summary>As abas das duas.</summary>
    public Abas Com(Abas outras) => new(Configuracao || outras.Configuracao, Utilizacao || outras.Utilizacao);

    /// <summary>"C", "U" ou "CU".</summary>
    public override string ToString() => (Configuracao ? "C" : "") + (Utilizacao ? "U" : "");
}
