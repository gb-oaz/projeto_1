using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas, sem login: a conta e a conta master, com a senha (RF-104, RN-179, RN-242).</summary>
public interface ICriarConta
{
    /// <summary>Cria a conta, auditada do começo ao fim com o traceId da execução (RN-178, RN-270, RN-271).</summary>
    /// <param name="pedido">O formulário.</param>
    /// <param name="traceId">A execução, que o endereço devolve no cabeçalho X-Trace-Id.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS, SIGLA_INVALIDA, SIGLA_EM_USO ou DOMINIO_EM_USO.</exception>
    Task<ContaCriadaDto> Executar(NovaContaDto pedido, TraceId traceId, CancellationToken ct = default);
}
