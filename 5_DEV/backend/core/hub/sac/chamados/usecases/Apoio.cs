using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>Uma execução auditada do SAC (18 RN-297, 17 RN-178): o traceId, a ação, a conta, quem e o alvo.</summary>
/// <param name="TraceId">A execução, no X-Trace-Id.</param>
/// <param name="Acao">O que se faz.</param>
/// <param name="ContaId">A conta, se há.</param>
/// <param name="Quem">Quem pede; null no sistema.</param>
/// <param name="Alvo">O chamado, se já existe.</param>
sealed record ExecucaoDoSac(TraceId TraceId, AcaoAuditada Acao, string? ContaId, string? Quem, string? Alvo);

/// <summary>
/// A auditoria dos casos de uso do SAC (17 RN-178, RN-270, RN-271): o
/// "INICIADO" ao chegar, a regra, o "CONCLUIDO" com o alvo antes de responder
/// e, na recusa ou na falha, o registro de erro com o mesmo traceId.
/// </summary>
sealed class AuditoriaDoSac(IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
{
    /// <summary>Executa a regra auditada; a regra devolve a resposta e o alvo do "CONCLUIDO".</summary>
    public async Task<T> Auditar<T>(ExecucaoDoSac execucao, Func<Task<(T Resposta, string? Alvo)>> regra, CancellationToken ct)
    {
        await Registrar(execucao, EtapaDaExecucao.Iniciado, ct);
        try
        {
            var (resposta, alvo) = await regra();
            await Registrar(execucao with { Alvo = alvo ?? execucao.Alvo }, EtapaDaExecucao.Concluido, ct);
            return resposta;
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            var (codigo, nivel) = falha is FalhaDoSac recusa ? (recusa.Codigo, NivelDoErro.Alerta) : (RegistroDeErro.ErroInesperado, NivelDoErro.Erro);
            await erros.Registrar(new RegistroDeErro(execucao.TraceId, execucao.ContaId, execucao.Quem, execucao.Acao, execucao.Alvo, codigo, nivel,
                relogio.GetUtcNow()), ct);
            throw;
        }
    }

    Task Registrar(ExecucaoDoSac execucao, EtapaDaExecucao etapa, CancellationToken ct) =>
        auditoria.Registrar(new RegistroDeAuditoria(execucao.TraceId, etapa, execucao.ContaId, execucao.Quem, execucao.Acao, execucao.Alvo,
            relogio.GetUtcNow()), ct);
}

/// <summary>
/// O que os casos de uso do chamado repetem: carregar pelo lado de quem pede
/// (RN-288), aplicar a regra do domínio, numerar os itens novos, gravar com a
/// versão lida (outro mudou antes: relê e tenta mais uma vez) e avisar as
/// conexões ao vivo (RNF-230).
/// </summary>
sealed class Conversa(IChamados chamados, INumeracao numeracao, ICanalDoChamado canal, IPessoasDoSac pessoas)
{
    /// <summary>O chamado que quem pede pode ver; o de outra conta é como o que não existe (RN-288).</summary>
    public async Task<Chamado> Carregar(string id, QuemPede quem, CancellationToken ct)
    {
        var visao = quem.Visao;
        var numero = NumeroDoChamado.Ler(id) ?? throw FalhaDoSac.ChamadoInexistente();
        var chamado = await chamados.Obter(numero, ct) ?? throw FalhaDoSac.ChamadoInexistente();
        if (visao == Visao.Conta && chamado.Dados.Conta?.Id != quem.ContaId) throw FalhaDoSac.ChamadoInexistente();
        return chamado;
    }

    /// <summary>Quem pede, como autor na conversa.</summary>
    public async Task<Autor> Autor(QuemPede quem, CancellationToken ct) =>
        new(quem.Visao == Visao.Time ? TipoDeAutor.Time : TipoDeAutor.Conta, quem.UsuarioId, await pessoas.Nome(quem.UsuarioId, ct));

    /// <summary>
    /// Carrega, aplica a regra e grava; devolve o chamado mudado e os itens
    /// novos, já numerados. Uma releitura se outro mudou o chamado antes.
    /// </summary>
    public async Task<(Chamado Chamado, IReadOnlyList<ItemDaConversa> Itens)> Mudar(string id, QuemPede quem, Action<Chamado> regra,
        CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var chamado = await Carregar(id, quem, ct);
            regra(chamado);
            var itens = await Numerar(chamado.Novos, ct);
            if (await chamados.Salvar(chamado, itens, ct))
            {
                await canal.Publicar(chamado.Dados.Id, chamado.Dados.Versao, ct);
                return (chamado, itens);
            }
            if (tentativa == 2) throw new InvalidOperationException($"O chamado {id} mudou duas vezes durante a gravação.");
        }
    }

    /// <summary>Grava a mudança feita fora de um pedido (os prazos); false se outro mudou antes.</summary>
    public async Task<bool> Gravar(Chamado chamado, CancellationToken ct)
    {
        if (!await chamados.Salvar(chamado, await Numerar(chamado.Novos, ct), ct)) return false;
        await canal.Publicar(chamado.Dados.Id, chamado.Dados.Versao, ct);
        return true;
    }

    /// <summary>Grava o chamado novo, com os itens numerados.</summary>
    public async Task Incluir(Chamado chamado, CancellationToken ct)
    {
        await chamados.Incluir(chamado, await Numerar(chamado.Novos, ct), ct);
        await canal.Publicar(chamado.Dados.Id, chamado.Dados.Versao, ct);
    }

    /// <summary>Cada item novo ganha o número da série mensagensChamado.</summary>
    async Task<IReadOnlyList<ItemDaConversa>> Numerar(IReadOnlyList<ItemDaConversa> itens, CancellationToken ct)
    {
        var numerados = new List<ItemDaConversa>(itens.Count);
        foreach (var item in itens) numerados.Add(item with { Id = ItemDaConversa.IdDe(await numeracao.Proximo(INumeracao.Mensagens, ct)) });
        return numerados;
    }
}
