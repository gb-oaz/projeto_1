using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>
/// Papel, módulos e pv que vão no token, a cada entrada e renovação (RF-144,
/// RF-145, RF-150, RN-232, RN-233). A Autenticação usa por um adaptador da porta
/// de saída IPermissoes dela.
/// </summary>
public interface ICalcularPermissoes
{
    /// <summary>As permissões atuais do usuário.</summary>
    /// <exception cref="Domain.FalhaDeAutorizacao">USUARIO_INEXISTENTE.</exception>
    Task<PermissoesNoTokenDto> Executar(string usuarioId, CancellationToken ct = default);
}
