using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Commons.Auditoria.Ports.Out;

/// <summary>A coleção auditoriaErros (RN-270, RNF-206): só inclui; nada altera nem apaga.</summary>
public interface IAuditoriaDeErros
{
    /// <summary>Guarda o registro até <see cref="RegistroDeErro.GuardarAte"/>.</summary>
    Task Registrar(RegistroDeErro registro, CancellationToken ct = default);
}
