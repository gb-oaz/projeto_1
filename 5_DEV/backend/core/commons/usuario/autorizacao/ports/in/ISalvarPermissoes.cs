using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>PUT /usuarios/{id}/permissoes: salva o conjunto inteiro, tudo ou nada (RF-161, RN-254).</summary>
public interface ISalvarPermissoes
{
    /// <summary>Salva as permissões, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeAutorizacao">
    /// USUARIO_INEXISTENTE, ACESSO_NEGADO, VERSAO_AUSENTE, DADOS_INVALIDOS,
    /// VERSAO_DESATUALIZADA, MODULO_NAO_CONTRATADO ou PERMISSAO_ACIMA.
    /// </exception>
    Task<PermissoesSalvasDto> Executar(SalvamentoDePermissoesDto pedido, TraceId traceId, CancellationToken ct = default);
}
