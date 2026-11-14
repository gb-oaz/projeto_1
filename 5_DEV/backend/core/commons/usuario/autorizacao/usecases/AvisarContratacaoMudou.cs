using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Auditoria.Domain.AcaoAuditada;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// A contratação da conta mudou (RN-238, RN-239, RN-285 a RN-287): em cada
/// usuário da conta, o módulo descontratado sai de mods e fica suspenso com as
/// abas, e o recontratado volta; quem mudou sobe versão e pv e ganha o registro
/// em registrosPermissao com "por" igual a quem mudou a contratação; os demais
/// só sobem o pv. As sessões de cada um recebem o pv novo. Auditado na execução
/// do Catálogo, ação AVISAR_CONTRATACAO_MUDOU: o traceId, quem e a conta dela,
/// a conta como alvo; cada usuário avisado grava o AVISAR_PERMISSOES_NOVAS dele
/// na mesma execução; na falha, o ERRO_INESPERADO antes de a exceção voltar ao
/// Catálogo (RN-275).
/// </summary>
public sealed class AvisarContratacaoMudou : IAvisarContratacaoMudou
{
    /// <summary>Quantas vezes relê e tenta de novo quando outra pessoa salvou no meio.</summary>
    const int Tentativas = 3;

    readonly IUsuarios usuarios;
    readonly IPermissoesGuardadas guardadas;
    readonly ISessoesDoUsuario sessoes;
    readonly Leitor leitor;
    readonly AuditoriaDaExecucao auditada;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public AvisarContratacaoMudou(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes,
        ISessoesDoUsuario sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        leitor = new Leitor(usuarios, guardadas, modulos, contratacoes, relogio);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
        this.usuarios = usuarios;
        this.guardadas = guardadas;
        this.sessoes = sessoes;
        this.relogio = relogio;
    }

    /// <inheritdoc />
    public Task Executar(string contaId, ExecucaoDeQuemChama chamada, CancellationToken ct = default)
    {
        var execucao = Execucao.Interna(chamada, AcaoAuditada.AvisarContratacaoMudou, contaId);
        return auditada.Auditar(execucao, () => Avisar(contaId, execucao, ct), ct);
    }

    /// <summary>Acompanha a contratação em cada usuário da conta, avisa as sessões e grava o "CONCLUIDO".</summary>
    async Task Avisar(string contaId, Execucao execucao, CancellationToken ct)
    {
        foreach (var usuario in await usuarios.DaConta(contaId, ct))
        {
            var pv = await Acompanhar(usuario, execucao.Chamada.Quem ?? "", ct);
            await sessoes.AvisarPv(usuario.Id, pv, execucao.Chamada, ct);
        }
        await auditada.Concluir(execucao, ct);
    }

    /// <summary>
    /// Suspende ou reativa os módulos do usuário e grava com o registro (RN-285,
    /// RN-286); sem módulo a mover, só sobe o pv (RN-239). Devolve o pv novo.
    /// </summary>
    /// <exception cref="FalhaDeAutorizacao">VERSAO_DESATUALIZADA, se outra pessoa salvou no meio em todas as tentativas.</exception>
    async Task<int> Acompanhar(UsuarioAutorizado usuario, string por, CancellationToken ct)
    {
        var modulos = await leitor.ModulosDe(usuario, ct);
        for (var tentativa = 1; ; tentativa++)
        {
            var permissoes = await leitor.Guardadas(usuario.Id, ct);
            var versaoLida = permissoes.Versao;
            var mudanca = permissoes.AcompanharContratacao(modulos, por, relogio.GetUtcNow());
            if (mudanca is null) return await guardadas.SubirPv(usuario.Id, ct);
            if (await guardadas.Salvar(permissoes, mudanca, versaoLida, ct)) return permissoes.Pv;
            if (tentativa == Tentativas) throw FalhaDeAutorizacao.VersaoDesatualizada(versaoLida, usuario.Nome);
        }
    }
}
