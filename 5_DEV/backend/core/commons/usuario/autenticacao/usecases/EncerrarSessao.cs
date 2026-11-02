using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Sair de um aparelho da lista (RF-157). A sessão de outra pessoa responde como
/// inexistente, para não revelar que existe.
/// </summary>
public sealed class EncerrarSessao(IConferirAcesso conferir, ISessoes sessoes) : IEncerrarSessao
{
    /// <inheritdoc />
    public async Task Executar(ConferenciaDeAcessoDto pedido, string sid, CancellationToken ct = default)
    {
        var acesso = await conferir.Executar(pedido, ct);
        var sessao = await sessoes.Buscar(sid, ct);
        if (sessao is null || sessao.UsuarioId != acesso.Sub) throw FalhaDeAutenticacao.SessaoNaoEncontrada();
        await sessoes.Apagar(sessao, MotivoFim.Sair, ct);
    }
}
