using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

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
