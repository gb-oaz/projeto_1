using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas/esqueci-senha-master, sem login: 202, e a mensagem vai ao SAC (RF-111, RNF-205).</summary>
public interface IPedirAjudaDaMaster
{
    /// <summary>Envia o pedido.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">DADOS_INVALIDOS ou LIMITE_ATENDIMENTO.</exception>
    Task Executar(AjudaDaMasterDto pedido, CancellationToken ct = default);
}
