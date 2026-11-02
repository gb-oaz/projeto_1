using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/gerar-senha, sem login: troca o código de liberação pela senha (RF-109, RN-171, RN-182).</summary>
public interface IGerarSenha
{
    /// <summary>Gera a senha.</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">CODIGO_INVALIDO, CODIGO_VENCIDO, ENTRADA_BLOQUEADA ou DADOS_INVALIDOS.</exception>
    Task<SenhaGeradaDto> Executar(GeracaoDeSenhaDto pedido, CancellationToken ct = default);
}
