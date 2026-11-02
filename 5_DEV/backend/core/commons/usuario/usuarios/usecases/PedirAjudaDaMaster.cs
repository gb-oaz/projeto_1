using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>POST /contas/esqueci-senha-master (RF-111, RNF-205): até 3 pedidos por aparelho em 24 h.</summary>
public sealed class PedirAjudaDaMaster(IPedidosDeAjuda pedidos, TimeProvider relogio) : IPedirAjudaDaMaster
{
    /// <inheritdoc />
    public async Task Executar(AjudaDaMasterDto pedido, CancellationToken ct = default)
    {
        var (instituicao, contato) = pedido.Validar();
        var agora = relogio.GetUtcNow();
        var anteriores = await pedidos.EnviosDoAparelho(pedido.Aparelho, agora - LimiteDeAtendimento.Janela, ct);
        if (LimiteDeAtendimento.TentarEm(anteriores, agora) is { } tentarEm) throw FalhaDeUsuarios.LimiteAtendimento(tentarEm);
        await pedidos.Enviar(pedido.Aparelho, instituicao, contato, agora, ct);
    }
}
