using System.Globalization;

namespace Boaz.Core.Hub.Sac.Chamados.Domain;

/// <summary>
/// O número do chamado, que vai no endereço: "SAC-" e 6 dígitos, como
/// SAC-000042. O pedido de senha sem login tem o mesmo número em "ajuda-"
/// (17 RN-274, sac-contrato-api v01 decisão 1).
/// </summary>
/// <param name="Numero">O número da série contadores.chamados.</param>
public readonly record struct NumeroDoChamado(long Numero)
{
    const string Prefixo = "SAC-", PrefixoDoPedido = "ajuda-";

    /// <summary>SAC-000042.</summary>
    public string Valor => Prefixo + Numero.ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>ajuda-000042: o pedido de senha com o mesmo número.</summary>
    public string Pedido() => PrefixoDoPedido + Numero.ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>O número pelo texto do endereço; null se não for um número de chamado.</summary>
    public static NumeroDoChamado? Ler(string? texto) =>
        texto is { Length: >= 10 } && texto.StartsWith(Prefixo, StringComparison.Ordinal)
        && texto[Prefixo.Length..].All(char.IsAsciiDigit)
        && long.TryParse(texto[Prefixo.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? new NumeroDoChamado(n)
            : null;

    /// <inheritdoc />
    public override string ToString() => Valor;
}

/// <summary>Os limites dos campos do chamado (18 RN-291, RN-292, RN-294, RN-296).</summary>
public static class LimitesDoChamado
{
    /// <summary>Assunto: de 5 a 120 caracteres.</summary>
    public const int AssuntoMinimo = 5, AssuntoMaximo = 120;

    /// <summary>Descrição: de 20 a 5.000 caracteres.</summary>
    public const int DescricaoMinima = 20, DescricaoMaxima = 5000;

    /// <summary>Mensagem: de 1 a 5.000 caracteres.</summary>
    public const int MensagemMaxima = 5000;

    /// <summary>Comentário da avaliação: até 500 caracteres.</summary>
    public const int ComentarioMaximo = 500;

    /// <summary>Nota: de 1 a 5.</summary>
    public const int NotaMinima = 1, NotaMaxima = 5;

    /// <summary>Até 5 anexos no chamado ou na mensagem.</summary>
    public const int Anexos = 5;

    /// <summary>Até 10 chamados em Aberto, Em atendimento ou Aguardando cliente por conta.</summary>
    public const int EmAndamento = 10;

    /// <summary>Os 7 dias: Aguardando cliente e Resolvido sem resposta, reabrir e avaliar.</summary>
    public static readonly TimeSpan SeteDias = TimeSpan.FromDays(7);

    /// <summary>O que o chamado guarda depois de fechado (RNF-231).</summary>
    public const int AnosGuardado = 5;
}

/// <summary>Quem escreveu: a conta, o time ou o sistema.</summary>
/// <param name="Tipo">CONTA, TIME ou SISTEMA.</param>
/// <param name="UsuarioId">O usuário; null no sistema.</param>
/// <param name="Nome">O nome que a conversa mostra.</param>
public sealed record Autor(TipoDeAutor Tipo, string? UsuarioId, string Nome)
{
    /// <summary>O agendamento dos prazos (RN-297: "sistema" no lugar de quem).</summary>
    public static readonly Autor Sistema = new(TipoDeAutor.Sistema, null, "sistema");

    /// <summary>A mesma pessoa.</summary>
    public bool Mesmo(Autor outro) => Tipo == outro.Tipo && UsuarioId == outro.UsuarioId;
}

/// <summary>A conta dona do chamado, copiada na abertura (sac-documentos v01).</summary>
/// <param name="Id">O id da conta.</param>
/// <param name="Sigla">A sigla, como EBFGA.</param>
/// <param name="Nome">O nome, como Escola Bento Freitas.</param>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
public sealed record ContaDoChamado(string Id, string Sigla, string Nome, string Tipo);

/// <summary>De onde veio o chamado; o pedido sem login guarda quem pediu e o aparelho.</summary>
/// <param name="Tipo">HUB ou ENTRAR_SEM_LOGIN.</param>
/// <param name="Pedido">O pedido em pedidosAjuda ("ajuda-000087"); só sem login.</param>
/// <param name="Instituicao">A instituição informada; só sem login.</param>
/// <param name="Contato">O contato informado; só sem login.</param>
/// <param name="Aparelho">O aparelho, para o limite do 17 (RNF-205); só sem login.</param>
public sealed record Origem(TipoDeOrigem Tipo, string? Pedido = null, string? Instituicao = null, string? Contato = null, string? Aparelho = null)
{
    /// <summary>Aberto no hub.</summary>
    public static readonly Origem DoHub = new(TipoDeOrigem.Hub);

    /// <summary>Há conversa com quem abriu: o pedido sem login não tem (RF-179).</summary>
    public bool TemConversa => Tipo == TipoDeOrigem.Hub;
}

/// <summary>A avaliação única da conta (RN-296).</summary>
/// <param name="Nota">De 1 a 5.</param>
/// <param name="Comentario">Até 500 caracteres.</param>
/// <param name="Em">Quando.</param>
public sealed record Avaliacao(int Nota, string? Comentario, DateTimeOffset Em);

/// <summary>
/// Um item da conversa: mensagem, nota interna ou evento da linha do tempo.
/// Nada altera nem apaga um item (RN-293, RN-297).
/// </summary>
/// <param name="Id">"msg-000318" (contadores.mensagensChamado); vazio até numerar.</param>
/// <param name="Versao">A versão do chamado ao gravar.</param>
/// <param name="Tipo">MENSAGEM, NOTA_INTERNA ou EVENTO.</param>
/// <param name="Autor">Quem escreveu ou fez.</param>
/// <param name="Texto">Até 5.000 caracteres; null no evento.</param>
/// <param name="Anexos">Até 5 anexos.</param>
/// <param name="Evento">O que aconteceu; só no evento.</param>
/// <param name="De">O valor anterior do evento (status, responsável).</param>
/// <param name="Para">O valor novo do evento.</param>
/// <param name="Em">Quando.</param>
public sealed record ItemDaConversa(
    string Id, long Versao, TipoDeItem Tipo, Autor Autor, string? Texto, IReadOnlyList<string> Anexos,
    TipoDeEvento? Evento, string? De, string? Para, DateTimeOffset Em)
{
    const string Prefixo = "msg-";

    /// <summary>msg-000318.</summary>
    public static string IdDe(long numero) => Prefixo + numero.ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>O número do id ("msg-000318" → 318); null se não for um id de mensagem.</summary>
    public static long? NumeroDe(string? id) =>
        id is not null && id.StartsWith(Prefixo, StringComparison.Ordinal)
        && long.TryParse(id[Prefixo.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    /// <summary>A conta vê as mensagens e os eventos; a nota interna, só o time (RN-288).</summary>
    public bool VisivelParaConta => Tipo != TipoDeItem.NotaInterna;
}
