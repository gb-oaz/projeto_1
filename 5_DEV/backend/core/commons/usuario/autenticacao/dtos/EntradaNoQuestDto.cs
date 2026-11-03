using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>Quem entrou, no corpo de POST /auth/quest/entrar (RN-228).</summary>
/// <param name="Tipo">PARTICIPANTE ou RESPONSAVEL.</param>
/// <param name="Parentesco">Obrigatório no responsável ("Mãe").</param>
public sealed record QuemEntrouDto(string? Tipo, string? Parentesco)
{
    /// <summary>Confere os campos e devolve o valor do domínio.</summary>
    /// <exception cref="FalhaDeAutenticacao">DADOS_INVALIDOS em quem.tipo ou quem.parentesco.</exception>
    public QuemEntrou Validar()
    {
        var tipo = Nomes.QuemEntrouDe(Tipo) ?? throw FalhaDeAutenticacao.DadosInvalidos("quem.tipo");
        return tipo == TipoQuemEntrou.Responsavel
            ? new QuemEntrou(tipo, Validacao.Obrigatorio(Parentesco, "quem.parentesco").Trim())
            : new QuemEntrou(tipo);
    }
}

/// <summary>Corpo de POST /auth/quest/entrar (RF-135).</summary>
/// <param name="Codigo">Código de acesso do participante; aceita minúsculas (RN-184).</param>
/// <param name="Aparelho">NAVEGADOR ou APP.</param>
/// <param name="Quem">O participante ou um responsável.</param>
public sealed record EntradaNoQuestDto(string? Codigo, string? Aparelho, QuemEntrouDto? Quem)
{
    /// <summary>Confere os campos e devolve os valores prontos para o caso de uso.</summary>
    /// <exception cref="FalhaDeAutenticacao">DADOS_INVALIDOS com o primeiro campo errado.</exception>
    public (string Codigo, Aparelho Aparelho, QuemEntrou Quem) Validar() => (
        Credenciais.CodigoDeAcesso(Validacao.Obrigatorio(Codigo, "codigo")),
        Validacao.Aparelho(Aparelho),
        (Quem ?? throw FalhaDeAutenticacao.DadosInvalidos("quem")).Validar());
}
