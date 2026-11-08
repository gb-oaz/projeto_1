using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /usuarios/{id} (RN-163, RN-170, RN-186): o próprio ou quem pode listar aquele tipo.</summary>
public sealed class LerUsuario(IUsuarios usuarios, IContas contas, TimeProvider relogio) : ILerUsuario
{
    readonly Apoio apoio = new(usuarios, contas, relogio);

    /// <inheritdoc />
    public async Task<UsuarioLidoDto> Executar(PedidoDeUsuarioDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Alvo(quem, pedido.UsuarioId, ct);
        await GarantirQuePodeVer(quem, alvo, ct);
        return UsuarioLidoDto.De(alvo);
    }

    /// <summary>O próprio, ou quem pode listar o tipo do alvo (RN-170, RN-186).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO.</exception>
    async Task GarantirQuePodeVer(UsuarioCadastrado quem, UsuarioCadastrado alvo, CancellationToken ct)
    {
        var conta = await apoio.ContaDe(quem, ct);
        if (alvo.Id != quem.Id && !Hierarquia.PodeListar(quem, conta.DeAutonomo, alvo.Tipo)) throw FalhaDeUsuarios.AcessoNegado();
    }
}
