using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>
/// Papel, módulos e versão das permissões que vão no token, calculados pela
/// Autorização (17-b) a cada entrada e renovação (RF-145, RN-230).
/// </summary>
public interface IPermissoes
{
    /// <summary>As permissões atuais do usuário.</summary>
    Task<PermissoesNoToken> DoUsuario(UsuarioDaEntrada usuario, CancellationToken ct = default);

    /// <summary>As do acesso anônimo: papel ANONIMO, só o módulo da sala (RF-136).</summary>
    Task<PermissoesNoToken> DoAnonimo(SalaAberta sala, CancellationToken ct = default);
}
