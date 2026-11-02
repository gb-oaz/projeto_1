using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>Sair deste aparelho (RF-138): só a sessão do token termina.</summary>
public sealed class SairDoAparelho(IConferirAcesso conferir, ISessoes sessoes) : ISairDoAparelho
{
    /// <inheritdoc />
    public async Task Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default)
    {
        var acesso = await conferir.Executar(pedido, ct);
        if (await sessoes.Buscar(acesso.Sid, ct) is { } sessao) await sessoes.Apagar(sessao, MotivoFim.Sair, ct);
    }
}
