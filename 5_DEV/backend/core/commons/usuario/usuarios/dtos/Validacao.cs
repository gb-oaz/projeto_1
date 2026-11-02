using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Dtos;

/// <summary>Conferências de campo comuns aos pedidos; cada uma lança DADOS_INVALIDOS com o campo.</summary>
static class Validacao
{
    /// <summary>O texto veio e não está em branco; devolve sem os espaços das pontas.</summary>
    public static string Obrigatorio(string? valor, string campo) =>
        string.IsNullOrWhiteSpace(valor) ? throw FalhaDeUsuarios.DadosInvalidos(campo) : valor.Trim();

    /// <summary>Se veio, não está em branco; null fica null.</summary>
    public static string? Opcional(string? valor, string campo) => valor is null ? null : Obrigatorio(valor, campo);

    /// <summary>A página: 1 se não veio; senão um número a partir de 1.</summary>
    public static int Pagina(int? pagina) => pagina is null ? 1 : pagina >= 1 ? pagina.Value : throw FalhaDeUsuarios.DadosInvalidos("pagina");

    /// <summary>A busca sem espaços nas pontas; vazia vira null.</summary>
    public static string? Busca(string? busca) => string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();

    /// <summary>Uma duração ISO 8601 obrigatória.</summary>
    public static TimeSpan Duracao(string? valor, string campo) => Textos.IsoDe(valor) ?? throw FalhaDeUsuarios.DadosInvalidos(campo);

    /// <summary>Os responsáveis do participante, cada um com nome e parentesco.</summary>
    public static IReadOnlyList<Responsavel>? Responsaveis(IReadOnlyList<ResponsavelDto?>? responsaveis) =>
        responsaveis?.Select((r, i) => new Responsavel(
            Obrigatorio(r?.Nome, $"responsaveis[{i}].nome"),
            Obrigatorio(r?.Parentesco, $"responsaveis[{i}].parentesco").ToUpperInvariant())).ToList();
}
