using Boaz.Core.Hub.Sac.Anexos.Domain;

namespace Boaz.Core.Hub.Sac.Anexos.Dtos;

/// <summary>201 de POST /sac/anexos (RN-292).</summary>
/// <param name="Id">"anx-7f3a21".</param>
/// <param name="Nome">O nome do arquivo.</param>
/// <param name="Tipo">O tipo pelo conteúdo.</param>
/// <param name="Tamanho">Em bytes.</param>
public sealed record AnexoDto(string Id, string Nome, string Tipo, long Tamanho)
{
    /// <summary>Do anexo.</summary>
    public static AnexoDto De(Anexo a) => new(a.Id, a.Nome, a.Tipo, a.Tamanho);
}

/// <summary>O arquivo de GET /sac/anexos/{id}: os dados e o conteúdo.</summary>
/// <param name="Anexo">Nome, tipo e tamanho.</param>
/// <param name="Conteudo">O conteúdo, para ler até o fim.</param>
public sealed record ArquivoDoAnexo(AnexoDto Anexo, Stream Conteudo);
