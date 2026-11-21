using Boaz.Core.Hub.Auditoria.Saude.Domain;

namespace Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;

/// <summary>O que mudou de estado: um serviço ou um módulo.</summary>
public enum TipoDoAlvo
{
    /// <summary>Um dos 8 serviços.</summary>
    Servico,
    /// <summary>Um módulo.</summary>
    Modulo,
}

/// <summary>Os estados de uma ocorrência: só os três que mudam entre si (RN-324).</summary>
public enum EstadoDeOcorrencia
{
    /// <summary>No ar.</summary>
    NoAr,
    /// <summary>Lento.</summary>
    Lento,
    /// <summary>Fora do ar.</summary>
    ForaDoAr,
}

/// <summary>Os textos e as conversões das ocorrências, como no contrato.</summary>
public static class TextosDaOcorrencia
{
    /// <summary>SERVICO ou MODULO.</summary>
    public static string Texto(this TipoDoAlvo tipo) => tipo == TipoDoAlvo.Servico ? "SERVICO" : "MODULO";

    /// <summary>NO_AR, LENTO ou FORA_DO_AR.</summary>
    public static string Texto(this EstadoDeOcorrencia estado) => estado switch
    {
        EstadoDeOcorrencia.NoAr => "NO_AR",
        EstadoDeOcorrencia.Lento => "LENTO",
        _ => "FORA_DO_AR",
    };

    /// <summary>O tipo pelo texto; null se não for nenhum.</summary>
    public static TipoDoAlvo? TipoDe(string? texto) =>
        Enum.GetValues<TipoDoAlvo>().Cast<TipoDoAlvo?>().FirstOrDefault(t => t!.Value.Texto() == texto);

    /// <summary>O estado pelo texto; null se não for nenhum.</summary>
    public static EstadoDeOcorrencia? EstadoDe(string? texto) =>
        Enum.GetValues<EstadoDeOcorrencia>().Cast<EstadoDeOcorrencia?>().FirstOrDefault(e => e!.Value.Texto() == texto);

    /// <summary>
    /// O estado do serviço para as ocorrências: só No ar, Lento e Fora do ar mudam de estado; Sem verificação e
    /// Desligado não abrem nem fecham ocorrência (RN-324).
    /// </summary>
    public static EstadoDeOcorrencia Ocorre(this EstadoDoServico estado) => estado switch
    {
        EstadoDoServico.Lento => EstadoDeOcorrencia.Lento,
        EstadoDoServico.ForaDoAr => EstadoDeOcorrencia.ForaDoAr,
        _ => EstadoDeOcorrencia.NoAr,
    };

    /// <summary>O estado do módulo para as ocorrências: Sem pedidos não abre nem fecha ocorrência (RN-324).</summary>
    public static EstadoDeOcorrencia Ocorre(this EstadoDoModulo estado) => estado switch
    {
        EstadoDoModulo.Lento => EstadoDeOcorrencia.Lento,
        EstadoDoModulo.ForaDoAr => EstadoDeOcorrencia.ForaDoAr,
        _ => EstadoDeOcorrencia.NoAr,
    };
}

/// <summary>
/// Uma mudança de estado de um serviço ou de um módulo (RF-204, RN-324, coleção ocorrencias): de onde, para onde, quando
/// saiu de No ar, quando mudou e, na volta a No ar, quanto durou. Uma só por mudança, mesmo com várias instâncias.
/// Fica guardada 31 dias (RNF-248).
/// </summary>
/// <param name="Id">O identificador (GUID, 32 hexadecimais).</param>
/// <param name="Tipo">Serviço ou módulo.</param>
/// <param name="Alvo">O id do serviço ("cache") ou do módulo ("MOD-01").</param>
/// <param name="Nome">O nome do alvo.</param>
/// <param name="De">O estado de antes.</param>
/// <param name="Para">O estado de agora.</param>
/// <param name="Inicio">Quando o alvo saiu de No ar.</param>
/// <param name="Quando">Quando a mudança foi vista, na verificação.</param>
/// <param name="Falha">Por que falhou, se há.</param>
/// <param name="DuracaoMin">Quantos minutos durou, só na volta a No ar.</param>
public sealed record Ocorrencia(
    string Id, TipoDoAlvo Tipo, string Alvo, string Nome, EstadoDeOcorrencia De, EstadoDeOcorrencia Para, DateTimeOffset Inicio,
    DateTimeOffset Quando, FalhaDaVerificacao? Falha, int? DuracaoMin)
{
    /// <summary>Quantos dias a ocorrência fica guardada.</summary>
    public const int DiasDeGuarda = 31;

    /// <summary>Quantos minutos as voltas a No ar ficam na faixa.</summary>
    public const int MinutosDasVoltas = 10;

    /// <summary>Até quando fica guardada.</summary>
    public DateTimeOffset GuardarAte => Quando.AddDays(DiasDeGuarda);

    /// <summary>
    /// A ocorrência de uma verificação, se o estado mudou: <paramref name="aberta"/> é a última mudança do alvo fora de
    /// No ar (null quando ele estava No ar). A volta a No ar fecha a ocorrência com a duração desde que saiu de No ar.
    /// </summary>
    public static Ocorrencia? Detectar(
        Ocorrencia? aberta, TipoDoAlvo tipo, string alvo, string nome, EstadoDeOcorrencia atual, FalhaDaVerificacao? falha, DateTimeOffset agora,
        string id)
    {
        var antes = aberta?.Para ?? EstadoDeOcorrencia.NoAr;
        if (antes == atual) return null;
        var inicio = aberta?.Inicio ?? agora;
        // minutos inteiros, com 1 s de folga: a hora de cada verificação varia em milissegundos
        int? duracao = atual == EstadoDeOcorrencia.NoAr ? Math.Max(0, (int)Math.Floor((agora - inicio).TotalMinutes + (1.0 / 60))) : null;
        return new Ocorrencia(id, tipo, alvo, nome, antes, atual, inicio, agora, atual == EstadoDeOcorrencia.NoAr ? null : falha, duracao);
    }

    /// <summary>A posição entre as abertas: FORA_DO_AR antes de LENTO.</summary>
    public int Gravidade => Para == EstadoDeOcorrencia.ForaDoAr ? 0 : 1;
}
