using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas/{id}/codigo-master: o SAC confere a escola e gera o código da conta master (RN-172).</summary>
public interface IGerarCodigoDaMaster
{
    /// <summary>Gera o código.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, CONTA_INEXISTENTE, DADOS_INVALIDOS ou CONFERENCIA_INCOMPLETA.</exception>
    Task<CodigoDaMasterDto> Executar(ConferenciaDaMasterDto pedido, CancellationToken ct = default);
}
