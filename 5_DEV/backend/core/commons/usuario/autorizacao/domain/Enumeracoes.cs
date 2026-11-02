namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>Papel de quem usa o deboaz, que vai no token (RN-232).</summary>
public enum Papel
{
    /// <summary>O administrador do deboaz.</summary>
    Administrador,
    /// <summary>Membro do time deboaz.</summary>
    MembroTime,
    /// <summary>Conta master de uma instituição.</summary>
    Master,
    /// <summary>Educador autônomo: a conta master da própria conta.</summary>
    Autonomo,
    /// <summary>Staff da instituição.</summary>
    Staff,
    /// <summary>Participante, também quando o responsável entra com o código dele.</summary>
    Participante,
    /// <summary>Acesso anônimo a uma sala ao vivo.</summary>
    Anonimo,
}

/// <summary>Perfil citado em "Quem usa" no registro de módulos (15_modulos, RN-205).</summary>
public enum Perfil
{
    /// <summary>Administrador do deboaz.</summary>
    Administrador,
    /// <summary>Membro do time deboaz.</summary>
    MembroTime,
    /// <summary>Conta master da instituição.</summary>
    Master,
    /// <summary>Educador autônomo.</summary>
    Autonomo,
    /// <summary>Staff.</summary>
    Staff,
    /// <summary>Participante.</summary>
    Participante,
    /// <summary>Responsável pelo participante.</summary>
    Responsavel,
    /// <summary>Acesso anônimo.</summary>
    Anonimo,
}

/// <summary>Frente onde um perfil usa o módulo.</summary>
public enum FrenteDeUso
{
    /// <summary>O hub.</summary>
    Hub,
    /// <summary>O quest.</summary>
    Quest,
}

/// <summary>Como o módulo entra numa conta (15_modulos, RN-203).</summary>
public enum TipoDeModulo
{
    /// <summary>Entra sozinho com a conta e não sai.</summary>
    DaConta,
    /// <summary>Entra quando a conta master contrata.</summary>
    Contratavel,
    /// <summary>Só na conta do time deboaz.</summary>
    DoTime,
}

/// <summary>Os papéis como aparecem no token e no contrato.</summary>
public static class Papeis
{
    /// <summary>"ADMINISTRADOR", "MEMBRO_TIME", "MASTER", "AUTONOMO", "STAFF", "PARTICIPANTE" ou "ANONIMO".</summary>
    public static string Texto(this Papel papel) => papel switch
    {
        Papel.Administrador => "ADMINISTRADOR",
        Papel.MembroTime => "MEMBRO_TIME",
        Papel.Master => "MASTER",
        Papel.Autonomo => "AUTONOMO",
        Papel.Staff => "STAFF",
        Papel.Participante => "PARTICIPANTE",
        _ => "ANONIMO",
    };

    /// <summary>Lê o papel do token; qualquer outro texto é null.</summary>
    public static Papel? De(string? texto) => Enum.GetValues<Papel>().Cast<Papel?>().FirstOrDefault(p => p!.Value.Texto() == texto);

    /// <summary>
    /// O papel pelo tipo do cadastro de Usuários (RN-232). A conta master de uma
    /// conta de educador autônomo tem o papel AUTONOMO.
    /// </summary>
    /// <param name="tipo">MASTER, STAFF, PARTICIPANTE, MEMBRO_TIME ou ADMINISTRADOR.</param>
    /// <param name="contaDeAutonomo">A conta é de um educador autônomo.</param>
    public static Papel DoTipo(string tipo, bool contaDeAutonomo) => tipo switch
    {
        "ADMINISTRADOR" => Papel.Administrador,
        "MEMBRO_TIME" => Papel.MembroTime,
        "MASTER" => contaDeAutonomo ? Papel.Autonomo : Papel.Master,
        "STAFF" => Papel.Staff,
        "PARTICIPANTE" => Papel.Participante,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "tipo de usuário desconhecido"),
    };

    /// <summary>Do time deboaz: enxerga outras contas (RN-235).</summary>
    public static bool DoTime(this Papel papel) => papel is Papel.Administrador or Papel.MembroTime;
}
