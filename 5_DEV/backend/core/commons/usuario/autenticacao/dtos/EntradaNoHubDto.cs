using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>Corpo de POST /auth/hub/entrar (RF-134).</summary>
/// <param name="Entrada">E-mail de entrada; não diferencia maiúsculas (RN-180).</param>
/// <param name="Senha">A senha em claro, que só é conferida com o resumo.</param>
/// <param name="Aparelho">NAVEGADOR ou APP.</param>
public sealed record EntradaNoHubDto(string? Entrada, string? Senha, string? Aparelho)
{
    /// <summary>Confere os campos e devolve os valores prontos para o caso de uso.</summary>
    /// <exception cref="FalhaDeAutenticacao">DADOS_INVALIDOS com o primeiro campo errado.</exception>
    public (string Entrada, string Senha, Aparelho Aparelho) Validar() => (
        Credenciais.Entrada(Validacao.Obrigatorio(Entrada, "entrada")),
        Validacao.Obrigatorio(Senha, "senha"),
        Validacao.Aparelho(Aparelho));
}
