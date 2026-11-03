using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// A contratação da conta mudou (RN-238, RN-239): o pv de todos sobe e as
/// sessões recebem o pv novo, para o próximo token vir com ou sem o módulo.
/// </summary>
public sealed class AvisarContratacaoMudou(IPermissoesGuardadas guardadas, ISessoesDoUsuario sessoes) : IAvisarContratacaoMudou
{
    /// <inheritdoc />
    public async Task Executar(string contaId, CancellationToken ct = default)
    {
        foreach (var (usuarioId, pv) in await guardadas.SubirPvDaConta(contaId, ct))
        {
            await sessoes.AvisarPv(usuarioId, pv, ct);
        }
    }
}
