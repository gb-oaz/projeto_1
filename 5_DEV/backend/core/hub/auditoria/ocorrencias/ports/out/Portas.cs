using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;

namespace Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;

/// <summary>Uma página de ocorrências, da mais nova para a mais antiga.</summary>
/// <param name="Itens">Até o limite.</param>
/// <param name="Proximo">De onde a página seguinte começa; null na última.</param>
public sealed record PaginaDeOcorrencias(IReadOnlyList<Ocorrencia> Itens, Cursor? Proximo);

/// <summary>A coleção ocorrencias (RF-205, RNF-248): uma por mudança de estado, guardada 31 dias.</summary>
public interface IOcorrencias
{
    /// <summary>Guarda a ocorrência; com o banco fora do ar, fica pendente e entra na volta (RNF-252).</summary>
    Task Gravar(Ocorrencia ocorrencia, CancellationToken ct = default);

    /// <summary>As ocorrências do período, da mais nova para a mais antiga.</summary>
    Task<PaginaDeOcorrencias> Listar(
        Periodo periodo, TipoDoAlvo? tipo, string? alvo, EstadoDeOcorrencia? para, Cursor? depois, int limite, CancellationToken ct = default);
}

/// <summary>
/// As ocorrências abertas e as voltas recentes no Redis (auditoria:ocorrencias:abertas e :voltaram): a faixa abre sem o banco
/// (RNF-252) e a deteção de mudança parte do que está aberto.
/// </summary>
public interface IOcorrenciasAbertas
{
    /// <summary>Guarda a mudança: fora de No ar, é a aberta do alvo; a volta a No ar a fecha e entra nas voltas dos últimos 10 min.</summary>
    Task Mudar(Ocorrencia ocorrencia, CancellationToken ct = default);

    /// <summary>A última mudança de cada alvo fora de No ar.</summary>
    Task<IReadOnlyList<Ocorrencia>> Abertas(CancellationToken ct = default);

    /// <summary>As voltas a No ar dos últimos 10 min até agora.</summary>
    Task<IReadOnlyList<Ocorrencia>> Voltaram(DateTimeOffset agora, CancellationToken ct = default);
}

/// <summary>
/// O aviso de cada mudança (canal auditoria:ocorrencias): todo ao vivo da Auditoria, de qualquer instância, o recebe.
/// </summary>
public interface IAvisosDeOcorrencias
{
    /// <summary>Avisa as conexões ao vivo; a falha é só do aviso, a ocorrência já está guardada.</summary>
    Task Publicar(Ocorrencia ocorrencia, CancellationToken ct = default);

    /// <summary>Assina o canal agora; a assinatura entrega as ocorrências avisadas até ser descartada.</summary>
    Task<IAssinatura<Ocorrencia>> Assinar(CancellationToken ct = default);
}

/// <summary>Uma assinatura de um canal de avisos: o que chega, em ordem, até o descarte.</summary>
/// <typeparam name="T">O que o canal entrega.</typeparam>
public interface IAssinatura<out T> : IAsyncDisposable
{
    /// <summary>Os avisos que chegam depois da assinatura, até o cancelamento.</summary>
    IAsyncEnumerable<T> Avisos(CancellationToken ct);
}
