namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>
/// O que uma rota exige do token (RF-147, RNF-208): um módulo com uma aba, um
/// papel, ou os dois. A rota sem exigência é recusada (RNF-220). Quem está
/// abaixo de quem é conferido no caso de uso, com as permissões atuais.
/// </summary>
/// <param name="Modulo">O módulo exigido (MOD-03); null se a rota só pede papel.</param>
/// <param name="Aba">A aba exigida no módulo; null aceita qualquer uma.</param>
/// <param name="Papeis">Os papéis aceitos; null aceita qualquer um.</param>
public sealed record ExigenciaDaRota(string? Modulo = null, Abas? Aba = null, IReadOnlySet<Papel>? Papeis = null)
{
    /// <summary>A rota declarou o que exige.</summary>
    public bool Declarada => Modulo is not null || Papeis is not null;

    /// <summary>O papel e os módulos do token atendem à exigência.</summary>
    public bool Atende(Papel papel, IReadOnlyDictionary<string, Abas> mods) =>
        Declarada
        && (Papeis is null || Papeis.Contains(papel))
        && (Modulo is null || (mods.TryGetValue(Modulo, out var abas) && (Aba is not { } aba || abas.Contem(aba))));
}
