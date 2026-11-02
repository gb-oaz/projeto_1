namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Os valores das enumerações como aparecem no contrato, no token e nos documentos
/// (HUB, NAVEGADOR, MEMBRO_TIME…), e o caminho de volta.
/// </summary>
public static class Nomes
{
    /// <summary>"HUB", "QUEST" ou "SALA".</summary>
    public static string Texto(this Frente frente) => frente switch
    {
        Frente.Hub => "HUB",
        Frente.Quest => "QUEST",
        _ => "SALA",
    };

    /// <summary>"NAVEGADOR" ou "APP".</summary>
    public static string Texto(this Aparelho aparelho) => aparelho == Aparelho.Navegador ? "NAVEGADOR" : "APP";

    /// <summary>Lê "NAVEGADOR" ou "APP"; qualquer outro valor é null.</summary>
    public static Aparelho? AparelhoDe(string? texto) => texto switch
    {
        "NAVEGADOR" => Aparelho.Navegador,
        "APP" => Aparelho.App,
        _ => null,
    };

    /// <summary>"MASTER", "STAFF", "PARTICIPANTE", "MEMBRO_TIME" ou "ADMINISTRADOR".</summary>
    public static string Texto(this TipoUsuario tipo) => tipo switch
    {
        TipoUsuario.Master => "MASTER",
        TipoUsuario.Staff => "STAFF",
        TipoUsuario.Participante => "PARTICIPANTE",
        TipoUsuario.MembroTime => "MEMBRO_TIME",
        _ => "ADMINISTRADOR",
    };

    /// <summary>"PARTICIPANTE" ou "RESPONSAVEL".</summary>
    public static string Texto(this TipoQuemEntrou tipo) => tipo == TipoQuemEntrou.Participante ? "PARTICIPANTE" : "RESPONSAVEL";

    /// <summary>Lê "PARTICIPANTE" ou "RESPONSAVEL"; qualquer outro valor é null.</summary>
    public static TipoQuemEntrou? QuemEntrouDe(string? texto) => texto switch
    {
        "PARTICIPANTE" => TipoQuemEntrou.Participante,
        "RESPONSAVEL" => TipoQuemEntrou.Responsavel,
        _ => null,
    };

    /// <summary>"ENTRADA", "CODIGO_ACESSO" ou "CODIGO_LIBERACAO", como na chave tentativas:&lt;alvo&gt;:&lt;chave&gt;.</summary>
    public static string Texto(this AlvoTentativa alvo) => alvo switch
    {
        AlvoTentativa.Entrada => "ENTRADA",
        AlvoTentativa.CodigoAcesso => "CODIGO_ACESSO",
        _ => "CODIGO_LIBERACAO",
    };
}
