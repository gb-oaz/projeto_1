using System.Security.Cryptography;

namespace Boaz.Core.Hub.Sac.Anexos.Domain;

/// <summary>
/// O tipo do anexo, pelo conteúdo e não pelo nome (RN-292): só PNG e JPG,
/// guardados sem conversão.
/// </summary>
public static class TipoDeAnexo
{
    /// <summary>image/png.</summary>
    public const string Png = "image/png";

    /// <summary>image/jpeg.</summary>
    public const string Jpg = "image/jpeg";

    static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly byte[] AssinaturaJpg = [0xFF, 0xD8, 0xFF];

    /// <summary>O tipo pelos primeiros bytes; null se não é PNG nem JPG.</summary>
    public static string? Do(ReadOnlySpan<byte> conteudo) =>
        conteudo.StartsWith(AssinaturaPng) ? Png
        : conteudo.StartsWith(AssinaturaJpg) ? Jpg
        : null;
}

/// <summary>Onde o arquivo do anexo foi guardado (RNF-233): lido sempre de lá, sem migração.</summary>
public enum ArmazenamentoDoAnexo
{
    /// <summary>O balde sacAnexos do MongoDB: o destino com o armazenamento de objetos desligado.</summary>
    GridFs,
    /// <summary>O contêiner anexos-sac do armazenamento de objetos (Azure Blob Storage).</summary>
    Blob,
}

/// <summary>A tag de índice "uso" do arquivo no armazenamento de objetos (RNF-234, 05 RNF-236).</summary>
public enum UsoDoArquivo
{
    /// <summary>Enviado e ainda não usado.</summary>
    Pendente,
    /// <summary>Usado num chamado ou mensagem; de volta a ele ao reabrir.</summary>
    Chamado,
    /// <summary>O chamado fechou: vale a guarda do ambiente.</summary>
    Fechado,
}

/// <summary>Os textos guardados e a configuração (Sac:Arquivos:Destino, tag "uso").</summary>
public static class TextosDoAnexo
{
    /// <summary>BLOB ou GRIDFS.</summary>
    public static string Texto(this ArmazenamentoDoAnexo a) => a == ArmazenamentoDoAnexo.Blob ? "BLOB" : "GRIDFS";

    /// <summary>De BLOB ou GRIDFS; o que não é BLOB é GRIDFS (o anexo de antes do BLOB não tem o campo).</summary>
    public static ArmazenamentoDoAnexo ArmazenamentoDe(string? texto) =>
        string.Equals(texto?.Trim(), "BLOB", StringComparison.OrdinalIgnoreCase) ? ArmazenamentoDoAnexo.Blob : ArmazenamentoDoAnexo.GridFs;

    /// <summary>pendente, chamado ou fechado.</summary>
    public static string Texto(this UsoDoArquivo u) => u switch
    {
        UsoDoArquivo.Pendente => "pendente",
        UsoDoArquivo.Chamado => "chamado",
        _ => "fechado",
    };
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

    /// <summary>Usado em homologação: o arquivo fica 30 dias desde o envio (RNF-234).</summary>
    public static readonly TimeSpan UsadoEmHml = TimeSpan.FromDays(30);

    /// <summary>Usado em produção: o arquivo fica 5 anos depois do fechamento do chamado (RNF-234, RNF-231).</summary>
    public const int GuardaEmPrdAnos = 5;
}

/// <summary>
/// Um anexo do SAC (sac-documentos v03, coleção anexosSac): enviado antes,
/// verificado, guardado no destino da configuração e ligado a um chamado ou
/// mensagem em até 24 h. O expiraEm diz até quando o arquivo fica (RNF-234).
/// </summary>
/// <param name="Id">"anx-7f3a21".</param>
/// <param name="Nome">O nome do arquivo.</param>
/// <param name="Tipo">image/png, image/jpeg ou application/pdf, pelo conteúdo.</param>
/// <param name="Tamanho">Em bytes, até 10 MB.</param>
/// <param name="Sha256">O resumo do conteúdo.</param>
/// <param name="EnviadoPor">O usuário que enviou.</param>
/// <param name="ContaId">A conta de quem enviou.</param>
/// <param name="EnviadoEm">Quando.</param>
/// <param name="ExpiraEm">Até quando o arquivo fica: 24 h sem uso; usado, 30 dias do envio em hml, ou null em prd até o FECHADO.</param>
/// <param name="ChamadoId">O chamado em que foi usado.</param>
/// <param name="MensagemId">A mensagem em que foi usado; null na abertura.</param>
/// <param name="Interna">Usado numa nota interna: a conta não o vê.</param>
/// <param name="Armazenamento">Onde o arquivo foi guardado (RNF-233).</param>
/// <param name="RemovidoEm">Quando o arquivo do anexo usado saiu pelo descarte; o registro fica.</param>
public sealed record Anexo(
    string Id, string Nome, string Tipo, long Tamanho, string Sha256, string EnviadoPor, string ContaId, DateTimeOffset EnviadoEm,
    DateTimeOffset? ExpiraEm, string? ChamadoId, string? MensagemId, bool Interna,
    ArmazenamentoDoAnexo Armazenamento = ArmazenamentoDoAnexo.GridFs, DateTimeOffset? RemovidoEm = null)
{
    /// <summary>O caminho do arquivo no armazenamento de objetos.</summary>
    public string Objeto => $"sac/anexos/{Id}";

    /// <summary>Um anexo novo, verificado: vence em 24 h se não for usado.</summary>
    public static Anexo Novo(string nome, string tipo, byte[] conteudo, string enviadoPor, string contaId, DateTimeOffset agora,
        ArmazenamentoDoAnexo armazenamento = ArmazenamentoDoAnexo.GridFs) =>
        new("anx-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3)), nome, tipo, conteudo.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(conteudo)), enviadoPor, contaId, agora, agora + LimitesDoAnexo.SemUso, null, null, false,
            armazenamento);

    /// <summary>
    /// Vencido pelo expiraEm, usado ou não, ou já sem o arquivo: não aparece a
    /// ninguém (404), mesmo antes de o descarte tirar o arquivo (RNF-234).
    /// </summary>
    public bool Vencido(DateTimeOffset agora) => RemovidoEm is not null || (ExpiraEm is { } expira && agora >= expira);

    /// <summary>
    /// O expiraEm ao usar o anexo num chamado ou mensagem (RNF-234): em
    /// homologação, 30 dias do envio; em produção, nenhum até o FECHADO.
    /// </summary>
    public static DateTimeOffset? PrazoAoUsar(bool emHomologacao, DateTimeOffset enviadoEm) =>
        emHomologacao ? enviadoEm + LimitesDoAnexo.UsadoEmHml : null;

    /// <summary>
    /// O expiraEm quando o chamado fecha (RNF-234): em produção, 5 anos do
    /// fechamento (o guardarAte do chamado); em homologação, os 30 dias do envio.
    /// </summary>
    public static DateTimeOffset? PrazoAoFechar(bool emHomologacao, DateTimeOffset enviadoEm, DateTimeOffset fechadoEm) =>
        emHomologacao ? PrazoAoUsar(true, enviadoEm) : fechadoEm.AddYears(LimitesDoAnexo.GuardaEmPrdAnos);

    /// <summary>O nome de exibição: sem pastas, sem espaços nas pontas, até 200 caracteres.</summary>
    public static string NomeDe(string? nome)
    {
        var limpo = Path.GetFileName((nome ?? "").Replace('\\', '/')).Trim();
        if (limpo.Length == 0) limpo = "arquivo";
        return limpo.Length > LimitesDoAnexo.Nome ? limpo[..LimitesDoAnexo.Nome] : limpo;
    }
}
