using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;
using AuditoriaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.AuditoriaDaExecucao;
using Execucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.Execucao;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// Salva o conjunto inteiro das permissões, tudo ou nada (RF-161, RN-254). Ordem
/// das conferências: alvo visível (RN-235) → quem pode conceder (RN-252) →
/// If-Match (RNF-222) → campos → versão (RF-162) → módulos contratados
/// (RN-166) → só o que quem concede tem (RN-241). Com mudança, grava as
/// permissões, o registro e a versão juntos (RNF-223) e avisa as sessões.
/// Auditado com o traceId recebido, ação EDITAR_PERMISSOES: "INICIADO" com quem,
/// a conta dele e o alvo ao chegar, "CONCLUIDO" antes de responder (além de
/// "registrosPermissao", RN-256) e, na recusa (ALERTA) ou na falha (ERRO), o
/// registro de erro antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class SalvarPermissoes : ISalvarPermissoes
{
    readonly Leitor leitor;
    readonly Concessao concessao;
    readonly IPermissoesGuardadas guardadas;
    readonly ISessoesDoUsuario sessoes;
    readonly AuditoriaDaExecucao auditada;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public SalvarPermissoes(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes,
        ISessoesDoUsuario sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        leitor = new Leitor(usuarios, guardadas, modulos, contratacoes, relogio);
        concessao = new Concessao(leitor);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
        this.guardadas = guardadas;
        this.sessoes = sessoes;
        this.relogio = relogio;
    }

    /// <inheritdoc />
    public async Task<PermissoesSalvasDto> Executar(SalvamentoDePermissoesDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await ExecucaoDe(pedido, traceId, ct);
        return await auditada.Auditar(execucao, () => Salvar(pedido, execucao, ct), ct);
    }

    /// <summary>
    /// A execução auditada: quem pede, a conta dele e o alvo já no "INICIADO"; sem
    /// cadastro, vai sem conta, e a recusa sai da regra (RN-178).
    /// </summary>
    async Task<Execucao> ExecucaoDe(SalvamentoDePermissoesDto pedido, TraceId traceId, CancellationToken ct)
    {
        var contaId = (await leitor.Usuario(pedido.QuemPede, ct))?.ContaId;
        return new Execucao(traceId, AcaoAuditada.EditarPermissoes, contaId, pedido.QuemPede, pedido.UsuarioId);
    }

    /// <summary>A regra do PUT, de quem pede ao "CONCLUIDO".</summary>
    async Task<PermissoesSalvasDto> Salvar(SalvamentoDePermissoesDto pedido, Execucao execucao, CancellationToken ct)
    {
        var (quem, modsDeQuem, alvo) = await concessao.Conferir(pedido.QuemPede, pedido.UsuarioId, ct);
        var versaoLida = pedido.VersaoLida();
        var novos = pedido.ModsLidos();

        var permissoes = await PermissoesNaVersao(alvo, versaoLida, ct);
        var modulos = await leitor.ModulosDe(alvo, ct);
        GarantirQuePodeMudar(alvo, permissoes.Visiveis(modulos.Disponiveis), permissoes.Suspensos, novos, modulos,
            CalculoDePermissoes.Concediveis(quem, modsDeQuem, alvo, modulos));
        await Gravar(alvo, permissoes, novos, modulos, quem.Id, versaoLida, execucao, ct);
        await auditada.Concluir(execucao, ct);
        return new PermissoesSalvasDto(permissoes.Versao, Textos.Mods(permissoes.Visiveis(modulos.Disponiveis)));
    }

    /// <summary>As permissões guardadas do alvo, se ainda estão na versão lida (RF-162).</summary>
    /// <exception cref="FalhaDeAutorizacao">VERSAO_DESATUALIZADA.</exception>
    async Task<PermissoesDoUsuario> PermissoesNaVersao(UsuarioAutorizado alvo, int versaoLida, CancellationToken ct)
    {
        var permissoes = await leitor.Guardadas(alvo.Id, ct);
        return permissoes.Versao == versaoLida ? permissoes : throw FalhaDeAutorizacao.VersaoDesatualizada(permissoes.Versao, alvo.Nome);
    }

    /// <summary>
    /// Cada módulo que muda existe, está contratado e não está suspenso no alvo,
    /// o alvo usa e quem concede tem as abas (RN-166, RN-241, RN-286).
    /// </summary>
    /// <exception cref="FalhaDeAutorizacao">MODULO_INEXISTENTE, MODULO_NAO_CONTRATADO, MODULO_NAO_USADO ou PERMISSAO_ACIMA.</exception>
    static void GarantirQuePodeMudar(UsuarioAutorizado alvo, IReadOnlyDictionary<string, Abas> antes, IReadOnlyDictionary<string, Abas> suspensos,
        IReadOnlyDictionary<string, Abas> novos,
        ModulosDaConta modulos, IReadOnlyDictionary<string, Abas> concediveis)
    {
        foreach (var mod in antes.Keys.Union(novos.Keys).Order(StringComparer.Ordinal))
        {
            var depois = novos.TryGetValue(mod, out var d) ? d : (Abas?)null;
            var anterior = antes.TryGetValue(mod, out var a) ? a : (Abas?)null;
            if (depois == anterior) continue;
            var modulo = modulos.Registrado(mod) ?? throw FalhaDeAutorizacao.ModuloInexistente(mod);
            if (!modulos.Disponiveis.Contains(mod) || suspensos.ContainsKey(mod)) throw FalhaDeAutorizacao.ModuloNaoContratado(mod, modulo.Nome);
            if (!CalculoDePermissoes.AlvoUsa(alvo, modulo)) throw FalhaDeAutorizacao.ModuloNaoUsado(mod, modulo.Nome, alvo.Nome);
            var mexe = (depois ?? anterior!.Value).Com(anterior ?? depois!.Value);
            if (!concediveis.TryGetValue(mod, out var pode) || !pode.Contem(mexe))
            {
                throw FalhaDeAutorizacao.PermissaoAcima(mod, modulo.Nome, depois ?? anterior!.Value);
            }
        }
    }

    /// <summary>
    /// Com mudança, grava as permissões, o registro em "registrosPermissao" e a
    /// versão juntos (RN-256, RNF-223) e avisa as sessões do alvo, na mesma execução (RN-275).
    /// </summary>
    /// <exception cref="FalhaDeAutorizacao">VERSAO_DESATUALIZADA, se outra pessoa salvou no meio.</exception>
    async Task Gravar(UsuarioAutorizado alvo, PermissoesDoUsuario permissoes, IReadOnlyDictionary<string, Abas> novos, ModulosDaConta modulos,
        string quemId, int versaoLida, Execucao execucao, CancellationToken ct)
    {
        var mudanca = permissoes.Salvar(novos, modulos.Disponiveis, quemId, relogio.GetUtcNow());
        if (mudanca is null) return;
        if (!await guardadas.Salvar(permissoes, mudanca, versaoLida, ct))
        {
            var atual = await leitor.Guardadas(alvo.Id, ct);
            throw FalhaDeAutorizacao.VersaoDesatualizada(atual.Versao, alvo.Nome);
        }
        await sessoes.AvisarPv(alvo.Id, permissoes.Pv, execucao.Chamada, ct);
    }
}
