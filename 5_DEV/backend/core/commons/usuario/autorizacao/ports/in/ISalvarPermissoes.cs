using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>PUT /usuarios/{id}/permissoes: salva o conjunto inteiro, tudo ou nada (RF-161, RN-254).</summary>
public interface ISalvarPermissoes
{
    /// <summary>Salva as permissões.</summary>
    /// <exception cref="Domain.FalhaDeAutorizacao">
    /// USUARIO_INEXISTENTE, ACESSO_NEGADO, VERSAO_AUSENTE, DADOS_INVALIDOS,
    /// VERSAO_DESATUALIZADA, MODULO_NAO_CONTRATADO ou PERMISSAO_ACIMA.
    /// </exception>
    Task<PermissoesSalvasDto> Executar(SalvamentoDePermissoesDto pedido, CancellationToken ct = default);
}
