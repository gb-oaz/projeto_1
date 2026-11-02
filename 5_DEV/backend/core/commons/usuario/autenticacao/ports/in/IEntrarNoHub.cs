using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/hub/entrar: entrada e senha abrem uma sessão do hub (RF-134).</summary>
public interface IEntrarNoHub
{
    /// <summary>Abre a sessão e devolve os tokens.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// DADOS_INVALIDOS, ENTRADA_INVALIDA, ENTRADA_BLOQUEADA ou ACESSO_INDISPONIVEL.
    /// </exception>
    Task<SessaoAbertaDto> Executar(EntradaNoHubDto pedido, CancellationToken ct = default);
}
