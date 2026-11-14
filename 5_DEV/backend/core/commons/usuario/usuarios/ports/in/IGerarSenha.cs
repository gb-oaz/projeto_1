using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.In;

/// <summary>POST /usuarios/gerar-senha, sem login: troca o código de liberação pela senha (RF-109, RN-171, RN-182).</summary>
public interface IGerarSenha
{
    /// <summary>Gera a senha, auditado com o traceId da execução, devolvido no X-Trace-Id (RN-178, RN-270, RN-271).</summary>
    /// <exception cref="Domain.FalhaDeUsuarios">CODIGO_INVALIDO, CODIGO_VENCIDO, ENTRADA_BLOQUEADA ou DADOS_INVALIDOS.</exception>
    Task<SenhaGeradaDto> Executar(GeracaoDeSenhaDto pedido, TraceId traceId, CancellationToken ct = default);
}
