using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios: staff, participante ou membro do time (RF-106, RF-108, RN-188).</summary>
public interface ICriarUsuario
{
    /// <summary>Cria o usuário, auditado do começo ao fim com o traceId da execução (RN-178, RN-270, RN-271).</summary>
    /// <param name="pedido">O formulário e quem pede.</param>
    /// <param name="traceId">A execução, que o endereço devolve no cabeçalho X-Trace-Id.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, DADOS_INVALIDOS, NOME_CURTO_EM_USO ou MATRICULA_EM_USO.</exception>
    Task<UsuarioCriadoDto> Executar(NovoUsuarioDto pedido, TraceId traceId, CancellationToken ct = default);
}
