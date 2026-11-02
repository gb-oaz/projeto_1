using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;

/// <summary>POST /auth/hub/confirmar-senha: o comprovante da senha gerada abre a sessão (RF-143).</summary>
public interface IConfirmarSenha
{
    /// <summary>Confirma a geração e abre a sessão do hub.</summary>
    /// <exception cref="Domain.FalhaDeAutenticacao">
    /// DADOS_INVALIDOS, COMPROVANTE_INVALIDO ou ACESSO_INDISPONIVEL.
    /// </exception>
    Task<SessaoAbertaDto> Executar(ConfirmacaoDeSenhaDto pedido, CancellationToken ct = default);
}
