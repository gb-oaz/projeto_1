using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>As conexões ao vivo de cada usuário, entre todas as réplicas (aoVivo:&lt;usuarioId&gt; e o canal aoVivo:encerrar).</summary>
public interface IConexoesAoVivo
{
    /// <summary>Conta a conexão entre as abertas do usuário.</summary>
    /// <returns>As que passaram de 3, das mais antigas; já fora das abertas.</returns>
    Task<IReadOnlyList<string>> Abrir(string usuarioId, string conexaoId, CancellationToken ct = default);

    /// <summary>Avisa a réplica dona da conexão que ela termina pelo motivo.</summary>
    Task Encerrar(string conexaoId, MotivoEncerramento motivo, CancellationToken ct = default);

    /// <summary>Renova o prazo das conexões abertas do usuário (a cada intervalo).</summary>
    Task Renovar(string usuarioId, string conexaoId, CancellationToken ct = default);

    /// <summary>A conexão fechou.</summary>
    Task Fechar(string usuarioId, string conexaoId, CancellationToken ct = default);

    /// <summary>Espera até o tempo dado; antes, se a conexão for encerrada por outra.</summary>
    /// <returns>O motivo do encerramento; null se o tempo passou.</returns>
    Task<MotivoEncerramento?> Esperar(string conexaoId, TimeSpan tempo, CancellationToken ct = default);
}
