using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>DELETE /auth/sessoes: sair de todos os aparelhos (RF-157).</summary>
public interface IEncerrarTodasAsSessoes
{
    /// <summary>Encerra todas as sessões de quem pede, inclusive a do próprio token.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">As da conferência do token.</exception>
    Task Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default);
}
