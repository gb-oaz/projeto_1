using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>Corpo de POST /auth/hub/confirmar-senha: o comprovante da senha gerada (RF-143).</summary>
/// <param name="Comprovante">O comprovante recebido junto com a senha gerada.</param>
/// <param name="Aparelho">NAVEGADOR ou APP; sem ele, NAVEGADOR (o contrato ainda não o traz).</param>
public sealed record ConfirmacaoDeSenhaDto(string? Comprovante, string? Aparelho = null)
{
    /// <summary>Confere os campos e devolve os valores prontos para o caso de uso.</summary>
    /// <exception cref="FalhaDeAutenticacao">DADOS_INVALIDOS em comprovante ou aparelho.</exception>
    public (string Comprovante, Aparelho Aparelho) Validar() => (
        Validacao.Obrigatorio(Comprovante, "comprovante").Trim(),
        Aparelho is null ? Domain.Aparelho.Navegador : Validacao.Aparelho(Aparelho));
}
