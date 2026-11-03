using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/renovar: troca o token de renovação por tokens novos (RF-137, RNF-213).</summary>
public interface IRenovarSessao
{
    /// <summary>Renova a sessão, com as permissões recalculadas.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    Task<SessaoAbertaDto> Executar(RenovacaoDto pedido, CancellationToken ct = default);
}
