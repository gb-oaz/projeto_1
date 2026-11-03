using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>PUT /time/tempo-sessao: muda o tempo do time e das contas master (RF-152, RN-243).</summary>
public interface ISalvarTempoDoTime
{
    /// <summary>Salva o tempo.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, DADOS_INVALIDOS ou FORA_DOS_LIMITES.</exception>
    Task<TempoDoTimeLidoDto> Executar(TempoDoTimeDto pedido, CancellationToken ct = default);
}
