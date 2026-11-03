using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A coleção auditoria (RN-178, RNF-206): só inclui; nada altera nem apaga.</summary>
public interface IAuditoria
{
    /// <summary>Guarda o registro até <see cref="RegistroDeAuditoria.GuardarAte"/>.</summary>
    Task Registrar(RegistroDeAuditoria registro, CancellationToken ct = default);
}
