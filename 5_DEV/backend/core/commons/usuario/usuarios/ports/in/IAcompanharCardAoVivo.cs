using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>GET /usuarios/card/ao-vivo: o card por SSE (RN-281, 15 RN-279).</summary>
public interface IAcompanharCardAoVivo
{
    /// <summary>
    /// Confere o intervalo e o card de quem pede e abre a conexão; na 4ª do
    /// usuário, a mais antiga é encerrada com LIMITE_CONEXOES.
    /// </summary>
    /// <returns>A conexão e o primeiro card.</returns>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS em "intervalo" ou ACESSO_NEGADO.</exception>
    Task<(ConexaoAoVivo Conexao, CardDoModuloDto Card)> Abrir(AberturaAoVivoDto pedido, CancellationToken ct = default);

    /// <summary>
    /// Espera o que mandar em seguida: o ": ping" a cada 30 s, o card quando a
    /// versao muda (conferida a cada intervalo, com a sessão e o MOD-03) ou o encerrado.
    /// </summary>
    Task<PassoAoVivoDto> Proximo(ConexaoAoVivo conexao, CancellationToken ct = default);

    /// <summary>Tira a conexão das abertas do usuário.</summary>
    Task Fechar(ConexaoAoVivo conexao, CancellationToken ct = default);
}
