using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/sala/entrar: acesso anônimo a uma sala aberta (RF-136).</summary>
public interface IEntrarNaSala
{
    /// <summary>Abre a sessão anônima e devolve os tokens.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">DADOS_INVALIDOS ou SALA_FECHADA.</exception>
    Task<SessaoAbertaDto> Executar(EntradaNaSalaDto pedido, CancellationToken ct = default);
}
