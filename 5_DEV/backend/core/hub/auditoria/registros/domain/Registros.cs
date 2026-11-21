using System.Text;
using System.Text.RegularExpressions;
using Boaz.Core.Hub.Auditoria.Card.Domain;

namespace Boaz.Core.Hub.Auditoria.Registros.Domain;

/// <summary>De qual coleção veio o registro: "auditoria" ou "auditoriaErros".</summary>
public enum TipoDoRegistro
{
    /// <summary>A coleção auditoria: o INICIADO e o CONCLUIDO de uma execução.</summary>
    Auditoria,
    /// <summary>A coleção auditoriaErros: a recusa ou a falha de uma execução.</summary>
    Erro,
}

/// <summary>Quem fez, com o nome lido de usuarios.</summary>
/// <param name="Id">O id do usuário.</param>
/// <param name="Nome">O nome; o próprio id se o usuário não existe mais.</param>
public sealed record PessoaLida(string Id, string Nome);

/// <summary>A conta, com a sigla lida de contas.</summary>
/// <param name="Id">O id da conta.</param>
/// <param name="Sigla">A sigla; o próprio id se a conta não existe mais.</param>
public sealed record ContaLida(string Id, string Sigla);

/// <summary>
/// Um registro como a Auditoria o mostra (RF-199): da auditoria ou da auditoriaErros, com o nome da ação e do módulo,
/// quem e a conta com os nomes, e a hora com milissegundos.
/// </summary>
/// <param name="Id">O _id do documento.</param>
/// <param name="Tipo">De qual coleção veio.</param>
/// <param name="TraceId">A execução.</param>
/// <param name="Etapa">INICIADO ou CONCLUIDO; null nos erros.</param>
/// <param name="Nivel">ALERTA ou ERRO; null na auditoria.</param>
/// <param name="Codigo">O código da recusa; null na auditoria.</param>
/// <param name="Acao">O código da ação.</param>
/// <param name="AcaoNome">O nome da ação; o código se ela não está no catálogo.</param>
/// <param name="Modulo">O módulo da ação; null se ela não está no catálogo.</param>
/// <param name="Quem">Quem fez, se houve login.</param>
/// <param name="Conta">A conta do alvo, se há.</param>
/// <param name="Alvo">Sobre quem ou o quê.</param>
/// <param name="Quando">Quando, com milissegundos.</param>
public sealed record RegistroLido(
    string Id, TipoDoRegistro Tipo, string TraceId, string? Etapa, string? Nivel, string? Codigo, string Acao, string AcaoNome, string? Modulo,
    PessoaLida? Quem, ContaLida? Conta, string? Alvo, DateTimeOffset Quando)
{
    /// <summary>AUDITORIA ou ERRO.</summary>
    public string TipoTexto => Tipo == TipoDoRegistro.Auditoria ? "AUDITORIA" : "ERRO";

    /// <summary>A hora como "2026-11-20T13:41:07.311Z".</summary>
    public string QuandoTexto => Formato.ComMilissegundos(Quando);
}

/// <summary>Os formatos de data da Auditoria.</summary>
public static class Formato
{
    /// <summary>2026-11-20T13:41:07.311Z: a hora de um registro, com milissegundos, em UTC.</summary>
    public static string ComMilissegundos(DateTimeOffset data) =>
        data.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// O filtro dos registros (RF-200): tipo, nível, módulo, ação, conta (pela sigla, sem diferença de caixa), quem, código
/// e traceId, mais a página. Na leitura o adaptador aplica o que o banco consegue; <see cref="Aceita"/> é o mesmo
/// filtro, um registro de cada vez, para o ao vivo.
/// </summary>
/// <param name="Tipo">AUDITORIA ou ERRO.</param>
/// <param name="Nivel">ALERTA ou ERRO (só existe nos erros).</param>
/// <param name="Modulo">MOD-NN.</param>
/// <param name="Acao">O código da ação.</param>
/// <param name="Conta">A sigla da conta, como veio.</param>
/// <param name="Quem">O id de quem fez.</param>
/// <param name="Codigo">O código do erro.</param>
/// <param name="TraceId">A execução; vale sem período.</param>
public sealed record FiltroDosRegistros(
    TipoDoRegistro? Tipo, string? Nivel, string? Modulo, string? Acao, string? Conta, string? Quem, string? Codigo, string? TraceId)
{
    /// <summary>O mínimo de registros por página.</summary>
    public const int LimiteMinimo = 1;

    /// <summary>O máximo de registros por página.</summary>
    public const int LimiteMaximo = 200;

    /// <summary>A página padrão.</summary>
    public const int LimitePadrao = 50;

    /// <summary>Sem nenhum filtro.</summary>
    public static FiltroDosRegistros Nenhum { get; } = new(null, null, null, null, null, null, null, null);

    /// <summary>Lê e confere os filtros do pedido; DADOS_INVALIDOS no campo errado.</summary>
    public static FiltroDosRegistros Ler(
        string? tipo, string? nivel, string? modulo, string? acao, string? conta, string? quem, string? codigo, string? traceId)
    {
        TipoDoRegistro? t = tipo switch
        {
            null => null,
            "AUDITORIA" => TipoDoRegistro.Auditoria,
            "ERRO" => TipoDoRegistro.Erro,
            _ => throw FalhaDaAuditoria.DadosInvalidos("tipo"),
        };
        if (nivel is not (null or "ALERTA" or "ERRO")) throw FalhaDaAuditoria.DadosInvalidos("nivel");
        if (modulo is not null && !Metricas.Domain.ModuloDoEndereco.Valido(modulo)) throw FalhaDaAuditoria.DadosInvalidos("modulo");
        if (acao is not null && !CatalogoDeAcoes.Existe(acao)) throw FalhaDaAuditoria.DadosInvalidos("acao");
        if (traceId is not null && !Regex.IsMatch(traceId, "^trc-[0-9a-f]{8}$")) throw FalhaDaAuditoria.DadosInvalidos("traceId");
        return new FiltroDosRegistros(t, nivel, modulo, acao, conta, quem, codigo, traceId);
    }

    /// <summary>As ações que o filtro de módulo e de ação deixam passar; null se nenhum dos dois pede.</summary>
    public IReadOnlyList<string>? Acoes()
    {
        if (Modulo is null) return Acao is null ? null : [Acao];
        var doModulo = CatalogoDeAcoes.DoModulo(Modulo);
        return Acao is null ? doModulo : doModulo.Contains(Acao) ? [Acao] : [];
    }

    /// <summary>
    /// O registro passa pelo filtro (para o ao vivo): o nível e o código só existem nos erros, e a conta é comparada pela
    /// sigla sem diferença de caixa.
    /// </summary>
    public bool Aceita(RegistroLido registro)
    {
        if (Tipo is { } t && registro.Tipo != t) return false;
        if (Nivel is not null && registro.Nivel != Nivel) return false;
        if (Codigo is not null && registro.Codigo != Codigo) return false;
        if (Quem is not null && registro.Quem?.Id != Quem) return false;
        if (Acao is not null && registro.Acao != Acao) return false;
        if (Modulo is not null && registro.Modulo != Modulo) return false;
        if (TraceId is not null && registro.TraceId != TraceId) return false;
        return Conta is null || string.Equals(registro.Conta?.Sigla, Conta, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// O ponto onde a página seguinte começa: a hora e o _id do último registro da página, em base64. Quem lê só repete o
/// que recebeu; fora desse formato, DADOS_INVALIDOS em "depois".
/// </summary>
/// <param name="Quando">A hora do último registro da página.</param>
/// <param name="Id">O _id dele.</param>
public readonly record struct Cursor(DateTimeOffset Quando, string Id)
{
    /// <summary>O cursor como texto: base64 de "2026-11-20T13:41:07.311Z|id".</summary>
    public string Texto => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Formato.ComMilissegundos(Quando)}|{Id}"));

    /// <summary>
    /// Lê o cursor do pedido: o que não é base64 é DADOS_INVALIDOS em "depois"; um base64 que não traz a hora (o
    /// "MjAy" dos exemplos do requisito, só o começo de um cursor) começa pela primeira página.
    /// </summary>
    public static Cursor? Ler(string? texto)
    {
        if (texto is null) return null;
        string decodificado;
        try
        {
            decodificado = Encoding.UTF8.GetString(Convert.FromBase64String(texto));
        }
        catch (FormatException)
        {
            throw FalhaDaAuditoria.DadosInvalidos("depois");
        }
        var partes = decodificado.Split('|', 2);
        return DateTimeOffset.TryParse(partes[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var quando)
            ? new Cursor(quando.ToUniversalTime(), partes.Length == 2 ? partes[1] : "￿")
            : null;
    }

    /// <summary>O limite da página: 1 a 200, 50 por padrão; DADOS_INVALIDOS em "limite" fora disso.</summary>
    public static int LerLimite(string? texto, int maximo = FiltroDosRegistros.LimiteMaximo, int padrao = FiltroDosRegistros.LimitePadrao)
    {
        if (texto is null) return padrao;
        return int.TryParse(texto, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            && n >= FiltroDosRegistros.LimiteMinimo && n <= maximo
                ? n
                : throw FalhaDaAuditoria.DadosInvalidos("limite");
    }
}

/// <summary>
/// A consulta registrada (RN-325): o alvo é o endereço lido com os parâmetros em ordem alfabética, sem "depois" nem
/// "limite", para a mesma consulta dar o mesmo alvo.
/// </summary>
/// <param name="Quem">O usuário que consulta.</param>
/// <param name="Alvo">O endereço com os parâmetros.</param>
public readonly record struct ConsultaDaAuditoria(string Quem, string Alvo)
{
    /// <summary>De quanto em quanto tempo a mesma consulta da mesma pessoa grava de novo.</summary>
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(5);

    static readonly string[] Ignorados = ["depois", "limite"];

    /// <summary>O alvo do endereço com os parâmetros que vieram.</summary>
    public static ConsultaDaAuditoria De(string quem, string endereco, IEnumerable<KeyValuePair<string, string>> parametros)
    {
        var pares = parametros
            .Where(p => !Ignorados.Contains(p.Key, StringComparer.Ordinal))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}")
            .ToList();
        return new ConsultaDaAuditoria(quem, pares.Count == 0 ? endereco : $"{endereco}?{string.Join('&', pares)}");
    }
}
