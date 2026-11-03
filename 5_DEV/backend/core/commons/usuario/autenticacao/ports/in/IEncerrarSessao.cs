using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>DELETE /auth/sessoes/{sid}: sair de um aparelho da lista (RF-157).</summary>
public interface IEncerrarSessao
{
    /// <summary>Encerra a sessão <paramref name="sid"/> de quem pede.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// SESSAO_NAO_ENCONTRADA, se a sessão não existir ou for de outra pessoa, e as da conferência.
    /// </exception>
    Task Executar(ConferenciaDeAcessoDto pedido, string sid, CancellationToken ct = default);
}
