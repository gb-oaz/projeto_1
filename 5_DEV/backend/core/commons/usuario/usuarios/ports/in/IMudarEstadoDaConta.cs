using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas/{id}/estado: ativa, suspensa, desativada ou encerrada (RF-113, RN-173 a RN-175).</summary>
public interface IMudarEstadoDaConta
{
    /// <summary>Muda o estado.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, CONTA_INEXISTENTE, DADOS_INVALIDOS ou TRANSICAO_INVALIDA.</exception>
    Task<ContaComEstadoDto> Executar(EstadoDaContaDto pedido, CancellationToken ct = default);
}
