using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/sair: encerra a sessão do token; as dos outros aparelhos continuam (RF-138).</summary>
public interface ISairDoAparelho
{
    /// <summary>Encerra a sessão do token.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">As da conferência do token.</exception>
    Task Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default);
}
