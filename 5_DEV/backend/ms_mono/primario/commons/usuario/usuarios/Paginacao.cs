using System.Globalization;

namespace Boaz.MsMono.Primario.Commons.Usuario.Usuarios;

static class Paginacao
{
    // A página da lista: ausente é a primeira; texto que não é número, a página 0,
    // que o caso de uso recusa com DADOS_INVALIDOS em "pagina".
    public static int? Pagina(string? pagina) =>
        pagina is null ? null : int.TryParse(pagina, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
