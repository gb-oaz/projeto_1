using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios: staff, participante ou membro do time (RF-106, RF-108, RN-188).</summary>
public interface ICriarUsuario
{
    /// <summary>Cria o usuário.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">ACESSO_NEGADO, DADOS_INVALIDOS, NOME_CURTO_EM_USO ou MATRICULA_EM_USO.</exception>
    Task<UsuarioCriadoDto> Executar(NovoUsuarioDto pedido, CancellationToken ct = default);
}
