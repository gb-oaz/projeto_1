using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Encerra todas as sessões de um usuário por uma mudança de outro módulo
/// (RF-141, RNF-203): o próximo pedido e a próxima renovação recebem 401.
/// </summary>
public sealed class EncerrarSessoesDoUsuario(ISessoes sessoes) : IEncerrarSessoesDoUsuario
{
    /// <inheritdoc />
    public async Task Executar(string usuarioId, MotivoFim motivo, CancellationToken ct = default)
    {
        foreach (var sessao in await sessoes.DoUsuario(usuarioId, ct))
        {
            await sessoes.Apagar(sessao, motivo, ct);
        }
    }
}
