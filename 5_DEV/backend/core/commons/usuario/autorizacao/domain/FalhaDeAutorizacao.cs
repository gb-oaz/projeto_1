namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>
/// Grupo de uma falha, para o adaptador de entrada escolher o status HTTP:
/// 403, 404, 412, 422 ou 428.
/// </summary>
public enum GrupoDeFalha
{
    /// <summary>403: a pessoa não pode fazer isso.</summary>
    Proibido,
    /// <summary>404: o que foi pedido não existe para quem pediu.</summary>
    NaoEncontrado,
    /// <summary>412: outra pessoa salvou antes.</summary>
    VersaoDesatualizada,
    /// <summary>422: o pedido traz um valor que não vale.</summary>
    DadosInvalidos,
    /// <summary>428: o pedido não trouxe a versão.</summary>
    VersaoAusente,
}

/// <summary>
/// Uma recusa da autorização, com o código e a mensagem do contrato de API
/// (autorizacao-contrato-api). O adaptador a transforma na resposta
/// { "codigo", "mensagem" } com os campos extras (mod, abas, versao, campo).
/// </summary>
public sealed class FalhaDeAutorizacao : Exception
{
    FalhaDeAutorizacao(GrupoDeFalha grupo, string codigo, string mensagem) : base(mensagem) => (Grupo, Codigo) = (grupo, codigo);

    /// <summary>Grupo da falha, que decide o status HTTP.</summary>
    public GrupoDeFalha Grupo { get; }

    /// <summary>Código do contrato, como PERMISSAO_ACIMA.</summary>
    public string Codigo { get; }

    /// <summary>O módulo, em PERMISSAO_ACIMA e MODULO_NAO_CONTRATADO.</summary>
    public string? Mod { get; private init; }

    /// <summary>As abas que quem concede não tem, em PERMISSAO_ACIMA.</summary>
    public string? Abas { get; private init; }

    /// <summary>A versão atual, em VERSAO_DESATUALIZADA.</summary>
    public int? Versao { get; private init; }

    /// <summary>O campo errado, em DADOS_INVALIDOS.</summary>
    public string? Campo { get; private init; }

    /// <summary>Sem o módulo, a aba ou o papel, ou a pessoa não está abaixo (RF-147, RN-252).</summary>
    public static FalhaDeAutorizacao AcessoNegado() =>
        new(GrupoDeFalha.Proibido, "ACESSO_NEGADO", "Você não tem permissão para esta ação.");

    /// <summary>Usuário que não existe ou é de outra conta (RN-235): 404, e não 403.</summary>
    public static FalhaDeAutorizacao UsuarioInexistente() =>
        new(GrupoDeFalha.NaoEncontrado, "USUARIO_INEXISTENTE", "Usuário não encontrado.");

    /// <summary>Quem concede não tem mais o módulo ou a aba (RF-161, RN-241).</summary>
    public static FalhaDeAutorizacao PermissaoAcima(string mod, string nome, Abas abas) =>
        new(GrupoDeFalha.Proibido, "PERMISSAO_ACIMA", $"Você não tem mais o módulo {nome} para conceder.") { Mod = mod, Abas = abas.ToString() };

    /// <summary>Outra pessoa salvou antes (RF-162, RNF-222).</summary>
    public static FalhaDeAutorizacao VersaoDesatualizada(int versao, string nome) =>
        new(GrupoDeFalha.VersaoDesatualizada, "VERSAO_DESATUALIZADA",
            $"As permissões de {nome} mudaram agora há pouco. Confira e salve.")
        { Versao = versao };

    /// <summary>Salvamento sem If-Match (RNF-222).</summary>
    public static FalhaDeAutorizacao VersaoAusente() =>
        new(GrupoDeFalha.VersaoAusente, "VERSAO_AUSENTE", "Envie a versão lida das permissões no If-Match.");

    /// <summary>Módulo contratável que a conta não contratou (RN-166).</summary>
    public static FalhaDeAutorizacao ModuloNaoContratado(string mod, string nome) =>
        new(GrupoDeFalha.DadosInvalidos, "MODULO_NAO_CONTRATADO", $"O módulo {nome} não está contratado.") { Mod = mod };

    /// <summary>Módulo que não está no registro (DADOS_INVALIDOS, campo <c>mods.MOD-NN</c>).</summary>
    public static FalhaDeAutorizacao ModuloInexistente(string mod) =>
        new(GrupoDeFalha.DadosInvalidos, "DADOS_INVALIDOS", $"O módulo {mod} não existe.") { Campo = $"mods.{mod}" };

    /// <summary>Módulo que a pessoa não usa pelo papel dela (DADOS_INVALIDOS, campo <c>mods.MOD-NN</c>).</summary>
    public static FalhaDeAutorizacao ModuloNaoUsado(string mod, string nome, string pessoa) =>
        new(GrupoDeFalha.DadosInvalidos, "DADOS_INVALIDOS", $"{pessoa} não usa o módulo {nome}.") { Campo = $"mods.{mod}" };

    /// <summary>Um campo do pedido faltou ou veio com valor fora do contrato.</summary>
    public static FalhaDeAutorizacao DadosInvalidos(string campo) =>
        new(GrupoDeFalha.DadosInvalidos, "DADOS_INVALIDOS", $"O campo \"{campo}\" está faltando ou é inválido.") { Campo = campo };
}
