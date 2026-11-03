using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /contas, sem login: a conta e a conta master, com a senha (RF-104, RN-179, RN-242).</summary>
public interface ICriarConta
{
    /// <summary>Cria a conta.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">DADOS_INVALIDOS, SIGLA_INVALIDA, SIGLA_EM_USO ou DOMINIO_EM_USO.</exception>
    Task<ContaCriadaDto> Executar(NovaContaDto pedido, CancellationToken ct = default);
}
