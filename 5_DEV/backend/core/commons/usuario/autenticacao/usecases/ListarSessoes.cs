using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Aparelhos conectados (RF-156): as sessões ativas de quem pede, na frente do
/// token, marcando a deste aparelho.
/// </summary>
public sealed class ListarSessoes(IConferirAcesso conferir, ISessoes sessoes, TimeProvider relogio) : IListarSessoes
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SessaoListadaDto>> Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default)
    {
        var acesso = await conferir.Executar(pedido, ct);
        var agora = relogio.GetUtcNow();
        return (await sessoes.DoUsuario(acesso.Sub, ct))
            .Where(s => s.Frente == acesso.Frente && s.EstaAtiva(agora))
            .OrderByDescending(s => s.UltimaAcao)
            .Select(s => new SessaoListadaDto(s.Sid, s.Aparelho.Texto(), s.UltimaAcao, s.Sid == acesso.Sid))
            .ToList();
    }
}
