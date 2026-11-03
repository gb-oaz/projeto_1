using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>PUT /contas/{id}/tempo-sessao: muda o tempo sem uso da conta (RF-114, RN-183).</summary>
public interface ISalvarTempoDaConta
{
    /// <summary>Salva o tempo.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">CONTA_INEXISTENTE, DADOS_INVALIDOS ou FORA_DOS_LIMITES.</exception>
    Task<TempoDaContaLidoDto> Executar(TempoDaContaDto pedido, CancellationToken ct = default);
}
