using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>Sair de todos os aparelhos (RF-157), inclusive deste.</summary>
public sealed class EncerrarTodasAsSessoes(IConferirAcesso conferir, ISessoes sessoes) : IEncerrarTodasAsSessoes
{
    /// <inheritdoc />
    public async Task Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default)
    {
        var acesso = await conferir.Executar(pedido, ct);
        foreach (var sessao in await sessoes.DoUsuario(acesso.Sub, ct))
        {
            await sessoes.Apagar(sessao, MotivoFim.SairDeTodos, ct);
        }
    }
}
