using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>
/// A partida da aplicação, antes do primeiro pedido (RN-190, RN-265 a RN-267,
/// RNF-225, RNF-226): confere os segredos e garante as contas iniciais sem
/// duplicar.
/// </summary>
public interface IInicializarContas
{
    /// <summary>Confere os segredos e garante as contas.</summary>
    /// <exception cref="Domain.FalhaNaInicializacao">Segredo ausente ou senha fora da RNF-202, com o nome da variável.</exception>
    Task<ContasGarantidasDto> Executar(PartidaDto partida, CancellationToken ct = default);
}
