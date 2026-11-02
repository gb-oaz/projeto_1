using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

/// <summary>Conferências de campo comuns aos pedidos; cada uma lança DADOS_INVALIDOS com o campo.</summary>
static class Validacao
{
    /// <summary>O texto veio e não está em branco.</summary>
    public static string Obrigatorio(string? valor, string campo) =>
        string.IsNullOrWhiteSpace(valor) ? throw FalhaDeAutenticacao.DadosInvalidos(campo) : valor;

    /// <summary>O aparelho veio e é NAVEGADOR ou APP.</summary>
    public static Aparelho Aparelho(string? valor, string campo = "aparelho") =>
        Nomes.AparelhoDe(valor) ?? throw FalhaDeAutenticacao.DadosInvalidos(campo);
}
