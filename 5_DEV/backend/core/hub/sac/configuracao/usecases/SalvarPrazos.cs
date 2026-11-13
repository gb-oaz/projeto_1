using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.UseCases;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Ports.In;
using Boaz.Core.Hub.Sac.Configuracao.Ports.Out;

namespace Boaz.Core.Hub.Sac.Configuracao.UseCases;

/// <summary>
/// PUT /sac/configuracao (RF-181): o time com MOD-02 · CU (a rota confere a
/// aba); de 1 a 80 horas úteis. Auditado como SALVAR_PRAZOS_SAC (RN-297).
/// </summary>
public sealed class SalvarPrazos(IConfiguracaoDoSac configuracao, IPessoasDoSac pessoas, IAuditoria auditoria, IAuditoriaDeErros erros,
    TimeProvider relogio) : ISalvarPrazos
{
    readonly AuditoriaDoSac auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<PrazosDto> Executar(PedidoPrazos pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new ExecucaoDoSac(traceId, AcaoAuditada.SalvarPrazosSac, quem.ContaId, quem.UsuarioId, "prazos"), async () =>
        {
            quem.SoTime();
            var novos = pedido.Validar();
            var prazos = (await configuracao.Ler(ct)).Definir(novos, await pessoas.Nome(quem.UsuarioId, ct), relogio.GetUtcNow());
            await configuracao.Salvar(prazos, ct);
            return (PrazosDto.De(prazos), (string?)null);
        }, ct);
}
