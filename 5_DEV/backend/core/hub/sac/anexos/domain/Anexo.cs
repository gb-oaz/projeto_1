using System.Security.Cryptography;

namespace Boaz.Core.Hub.Sac.Anexos.Domain;

/// <summary>
/// O tipo do anexo, pelo conteúdo e não pelo nome (RN-292): PNG, JPG ou PDF.
/// </summary>
public static class TipoDeAnexo
{
    /// <summary>image/png.</summary>
    public const string Png = "image/png";

    /// <summary>image/jpeg.</summary>
    public const string Jpg = "image/jpeg";

    /// <summary>application/pdf.</summary>
    public const string Pdf = "application/pdf";

    static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly byte[] AssinaturaJpg = [0xFF, 0xD8, 0xFF];
    static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();

    /// <summary>O tipo pelos primeiros bytes; null se não é PNG, JPG nem PDF.</summary>
    public static string? Do(ReadOnlySpan<byte> conteudo) =>
        conteudo.StartsWith(AssinaturaPng) ? Png
        : conteudo.StartsWith(AssinaturaJpg) ? Jpg
        : conteudo.StartsWith(AssinaturaPdf) ? Pdf
        : null;
}

/// <summary>O resultado da verificação de segurança do arquivo (RN-292).</summary>
public enum ResultadoDaVerificacao
{
    /// <summary>Sem ameaça.</summary>
    Limpo,
    /// <summary>Ameaça encontrada: o arquivo é recusado.</summary>
    Ameaca,
    /// <summary>A verificação não respondeu: nenhum arquivo entra (falha fechada).</summary>
    Indisponivel,
}

/// <summary>Os limites dos anexos (RN-292).</summary>
public static class LimitesDoAnexo
{
    /// <summary>Até 10 MB por arquivo.</summary>
    public const long Tamanho = 10L * 1024 * 1024;

    /// <summary>O anexo não usado num chamado ou mensagem em 24 h é descartado.</summary>
    public static readonly TimeSpan SemUso = TimeSpan.FromHours(24);

    /// <summary>O nome guardado: até 200 caracteres.</summary>
    public const int Nome = 200;
}

/// <summary>
/// Um anexo do SAC (sac-documentos v01, coleção anexosSac): enviado antes,
/// verificado, e ligado a um chamado ou mensagem em até 24 h.
/// </summary>
/// <param name="Id">"anx-7f3a21".</param>
/// <param name="Nome">O nome do arquivo.</param>
/// <param name="Tipo">image/png, image/jpeg ou application/pdf, pelo conteúdo.</param>
/// <param name="Tamanho">Em bytes, até 10 MB.</param>
/// <param name="Sha256">O resumo do conteúdo.</param>
/// <param name="EnviadoPor">O usuário que enviou.</param>
/// <param name="ContaId">A conta de quem enviou.</param>
/// <param name="EnviadoEm">Quando.</param>
/// <param name="ExpiraEm">Até quando pode ser usado; null depois de usado.</param>
/// <param name="ChamadoId">O chamado em que foi usado.</param>
/// <param name="MensagemId">A mensagem em que foi usado; null na abertura.</param>
/// <param name="Interna">Usado numa nota interna: a conta não o vê.</param>
public sealed record Anexo(
    string Id, string Nome, string Tipo, long Tamanho, string Sha256, string EnviadoPor, string ContaId, DateTimeOffset EnviadoEm,
    DateTimeOffset? ExpiraEm, string? ChamadoId, string? MensagemId, bool Interna)
{
    /// <summary>O caminho do arquivo no armazenamento de objetos.</summary>
    public string Objeto => $"sac/anexos/{Id}";

    /// <summary>Um anexo novo, verificado: vence em 24 h se não for usado.</summary>
    public static Anexo Novo(string nome, string tipo, byte[] conteudo, string enviadoPor, string contaId, DateTimeOffset agora) =>
        new("anx-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3)), nome, tipo, conteudo.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(conteudo)), enviadoPor, contaId, agora, agora + LimitesDoAnexo.SemUso, null, null, false);

    /// <summary>Ainda não usado e já vencido: some no descarte e não aparece a ninguém.</summary>
    public bool Vencido(DateTimeOffset agora) => ChamadoId is null && ExpiraEm is { } expira && agora >= expira;

    /// <summary>O nome de exibição: sem pastas, sem espaços nas pontas, até 200 caracteres.</summary>
    public static string NomeDe(string? nome)
    {
        var limpo = Path.GetFileName((nome ?? "").Replace('\\', '/')).Trim();
        if (limpo.Length == 0) limpo = "arquivo";
        return limpo.Length > LimitesDoAnexo.Nome ? limpo[..LimitesDoAnexo.Nome] : limpo;
    }
}
