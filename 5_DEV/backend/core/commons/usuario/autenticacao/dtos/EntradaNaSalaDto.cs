using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>Corpo de POST /auth/sala/entrar: acesso anônimo a uma sala ao vivo (RF-136).</summary>
/// <param name="Sala">Código da sala (482107).</param>
/// <param name="Aparelho">NAVEGADOR ou APP; sem ele, NAVEGADOR.</param>
public sealed record EntradaNaSalaDto(string? Sala, string? Aparelho = null)
{
    /// <summary>Confere os campos e devolve os valores prontos para o caso de uso.</summary>
    /// <exception cref="FalhaDeAutenticacao">DADOS_INVALIDOS em sala ou aparelho.</exception>
    public (string Sala, Aparelho Aparelho) Validar() => (
        Validacao.Obrigatorio(Sala, "sala").Trim(),
        Aparelho is null ? Domain.Aparelho.Navegador : Validacao.Aparelho(Aparelho));
}
