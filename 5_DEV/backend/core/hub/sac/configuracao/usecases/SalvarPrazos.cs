using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.Core.Hub.Sac.Configuracao.Dtos;
using Boaz.Core.Hub.Sac.Configuracao.Ports.In;
using Boaz.Core.Hub.Sac.Configuracao.Ports.Out;

namespace Boaz.Core.Hub.Sac.Configuracao.UseCases;

/// <summary>
/// PUT /sac/configuracao (RF-181): o time com MOD-02 · CU (a rota confere a
/// aba); de 1 a 80 horas úteis. Auditado como SALVAR_PRAZOS_SAC (RN-297), pela
/// auditoria comum (commons/auditoria), com os prazos como alvo.
/// </summary>
public sealed class SalvarPrazos(IConfiguracaoDoSac configuracao, IPessoasDoSac pessoas, IAuditoria auditoria, IAuditoriaDeErros erros,
    TimeProvider relogio) : ISalvarPrazos
{
    // o alvo da auditoria: o documento "prazos" de configuracaoSac
    const string Alvo = "prazos";

    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<PrazosDto> Executar(PedidoPrazos pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.SalvarPrazosSac, quem.ContaId, quem.UsuarioId, Alvo);
        return auditada.Auditar(execucao, async () =>
        {
            quem.SoTime();
            var novos = pedido.Validar();
            var prazos = (await configuracao.Ler(ct)).Definir(novos, await pessoas.Nome(quem.UsuarioId, ct), relogio.GetUtcNow());
            await configuracao.Salvar(prazos, ct);
            await auditada.Concluir(execucao, ct);
            return PrazosDto.De(prazos);
        }, ct);
    }
}
