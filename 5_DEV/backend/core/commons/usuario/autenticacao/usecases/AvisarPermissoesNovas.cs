using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// As permissões do usuário mudaram (RN-230): as sessões continuam, com a versão
/// nova, e o token antigo recebe PERMISSOES_DESATUALIZADAS até ser renovado.
/// </summary>
public sealed class AvisarPermissoesNovas(ISessoes sessoes) : IAvisarPermissoesNovas
{
    /// <inheritdoc />
    public async Task Executar(string usuarioId, int pv, CancellationToken ct = default)
    {
        foreach (var sessao in await sessoes.DoUsuario(usuarioId, ct))
        {
            sessao.AtualizarPv(pv);
            await sessoes.Gravar(sessao, ct);
        }
    }
}
