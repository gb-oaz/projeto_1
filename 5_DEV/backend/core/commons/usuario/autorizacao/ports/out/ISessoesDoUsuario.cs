using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

/// <summary>
/// As sessões abertas, na Autenticação (17-a): o pv novo chega a elas e o token
/// antigo passa a receber PERMISSOES_DESATUALIZADAS (RN-230, RNF-223). O
/// adaptador chama a porta de entrada IAvisarPermissoesNovas da Autenticação.
/// </summary>
public interface ISessoesDoUsuario
{
    /// <summary>Grava o pv novo nas sessões do usuário, na execução de quem chama (RN-275).</summary>
    Task AvisarPv(string usuarioId, int pv, ExecucaoDeQuemChama chamada, CancellationToken ct = default);
}
