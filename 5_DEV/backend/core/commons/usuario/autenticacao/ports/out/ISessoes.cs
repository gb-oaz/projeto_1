using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>
/// As sessões (Redis: sessao:&lt;sid&gt; e o índice sessoes:&lt;usuário&gt;, RNF-217).
/// </summary>
public interface ISessoes
{
    /// <summary>A sessão pelo sid; null se não existir ou já tiver expirado.</summary>
    Task<Sessao?> Buscar(string sid, CancellationToken ct = default);

    /// <summary>
    /// Grava a sessão, com expiração em <see cref="Sessao.ExpiraEm"/> (sem expiração
    /// no acesso anônimo), e a põe no índice do usuário.
    /// </summary>
    Task Gravar(Sessao sessao, CancellationToken ct = default);

    /// <summary>Apaga a sessão e a tira do índice; o motivo fica para a auditoria.</summary>
    Task Apagar(Sessao sessao, MotivoFim motivo, CancellationToken ct = default);

    /// <summary>As sessões do usuário, de todas as frentes, que ainda existem.</summary>
    Task<IReadOnlyList<Sessao>> DoUsuario(string usuarioId, CancellationToken ct = default);
}
