namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Erros seguidos numa entrada, num código de acesso ou num código de liberação
/// (tentativas:&lt;alvo&gt;:&lt;chave&gt;, RNF-204). Cinco erros seguidos bloqueiam por
/// 15 min; cada bloqueio seguido dobra o tempo, até 24 h. A chave é a entrada ou o
/// código, nunca o usuário: a resposta não revela se ele existe (RNF-217).
/// </summary>
public sealed class TentativasDeEntrada
{
    /// <summary>Erros seguidos que bloqueiam.</summary>
    public const int ErrosParaBloquear = 5;

    /// <summary>Primeiro bloqueio.</summary>
    public static readonly TimeSpan PrimeiroBloqueio = TimeSpan.FromMinutes(15);

    /// <summary>Maior bloqueio.</summary>
    public static readonly TimeSpan MaiorBloqueio = TimeSpan.FromHours(24);

    /// <summary>Os erros e bloqueios são esquecidos 24 h depois do último erro.</summary>
    public static readonly TimeSpan Memoria = TimeSpan.FromHours(24);

    /// <summary>Remonta as tentativas gravadas, ou começa do zero.</summary>
    public TentativasDeEntrada(AlvoTentativa alvo, string chave, int errosSeguidos = 0, int bloqueiosSeguidos = 0,
        DateTimeOffset? bloqueadoAte = null, DateTimeOffset? ultimoErro = null)
    {
        Alvo = alvo;
        Chave = chave;
        ErrosSeguidos = errosSeguidos;
        BloqueiosSeguidos = bloqueiosSeguidos;
        BloqueadoAte = bloqueadoAte;
        UltimoErro = ultimoErro;
    }

    /// <summary>O que é tentado.</summary>
    public AlvoTentativa Alvo { get; }

    /// <summary>A entrada ou o código, já na forma única de <see cref="Credenciais"/>.</summary>
    public string Chave { get; }

    /// <summary>Erros desde o último acerto ou bloqueio, de 0 a 4.</summary>
    public int ErrosSeguidos { get; private set; }

    /// <summary>Bloqueios seguidos: dobram o próximo.</summary>
    public int BloqueiosSeguidos { get; private set; }

    /// <summary>Fim do bloqueio em curso; null sem bloqueio.</summary>
    public DateTimeOffset? BloqueadoAte { get; private set; }

    /// <summary>Hora do último erro.</summary>
    public DateTimeOffset? UltimoErro { get; private set; }

    /// <summary>Até quando guardar: 24 h depois do último erro, ou o fim do bloqueio, o que vier depois.</summary>
    public DateTimeOffset? ExpiraEm => UltimoErro is not { } erro ? null
        : BloqueadoAte is { } fim && fim > erro + Memoria ? fim : erro + Memoria;

    /// <summary>Bloqueada agora, mesmo com a credencial certa (RF-158).</summary>
    public bool EstaBloqueada(DateTimeOffset agora) => BloqueadoAte is { } fim && agora < fim;

    /// <summary>
    /// Conta um erro. O quinto erro seguido bloqueia: 15 min × 2^(bloqueios
    /// anteriores), até 24 h. Devolve true se este erro bloqueou.
    /// </summary>
    public bool RegistrarErro(DateTimeOffset agora)
    {
        UltimoErro = agora;
        if (++ErrosSeguidos < ErrosParaBloquear) return false;
        var minutos = PrimeiroBloqueio.TotalMinutes * Math.Pow(2, Math.Min(BloqueiosSeguidos, 16));
        BloqueadoAte = agora + TimeSpan.FromMinutes(Math.Min(minutos, MaiorBloqueio.TotalMinutes));
        BloqueiosSeguidos++;
        ErrosSeguidos = 0;
        return true;
    }
}
