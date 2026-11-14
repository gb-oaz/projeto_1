using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Os adaptadores de saída da feature chamados com o banco de verdade
// (sac-hexagonal v01, sac-documentos v02): ChamadosNoBanco (IChamados e
// INumeracao) e PessoasDoSacNoBanco (IPessoasDoSac). No padrão de
// ContasNoBancoTests: o que o comportamento pela API não mostra direito —
// numeração atômica, gravação pela versão lida, a ordem da fila, a busca, os
// vencidos do agendamento e a guarda de 5 anos.
//
// As datas do agendamento (7 dias) ficam no futuro do relógio real, para o
// AgendamentoDoSac do próprio backend não mexer nos chamados do teste.
public class ChamadosNoBancoTests(Backend backend) : IClassFixture<Backend>
{
    static readonly Autor Caio = new(TipoDeAutor.Time, "usr-dboaz-caio", "Caio Lima");
    static readonly Autor Ana = new(TipoDeAutor.Time, "usr-dboaz-ana", "Ana Lima");

    readonly Preparo preparo = new(backend);

    IChamados Chamados => backend.Services.GetRequiredService<IChamados>();

    INumeracao Numeracao => backend.Services.GetRequiredService<INumeracao>();

    IMongoCollection<BsonDocument> Colecao(string nome) => backend.Banco.GetCollection<BsonDocument>(nome);

    // uma conta só do teste; o nome leva uma marca única, para a busca achar só os dela
    static ContaDoChamado Conta(string nome = "Escola Bento Freitas")
    {
        var sigla = Preparo.SiglaSorteada();
        return new ContaDoChamado($"conta-{sigla.ToLowerInvariant()}", sigla, $"{nome} {Guid.NewGuid():N}", "INSTITUICAO");
    }

    static Autor DaConta(ContaDoChamado conta) => new(TipoDeAutor.Conta, $"usr-{conta.Sigla.ToLowerInvariant()}-master", conta.Nome);

    // os itens novos com o número de mensagensChamado, como os casos de uso
    async Task<IReadOnlyList<ItemDaConversa>> Numerados(Chamado chamado)
    {
        var itens = new List<ItemDaConversa>();
        foreach (var item in chamado.Novos) itens.Add(item with { Id = ItemDaConversa.IdDe(await Numeracao.Proximo(INumeracao.Mensagens)) });
        return itens;
    }

    // a conta abre o chamado; devolve o lido de volta do banco
    async Task<Chamado> Aberto(ContaDoChamado conta, DateTimeOffset quando, string assunto = "Presença não salva a chamada")
    {
        _ = backend.Server;
        var numero = new NumeroDoChamado(await Numeracao.Proximo(INumeracao.Chamados));
        var chamado = Chamado.Abrir(numero, conta, DaConta(conta), Categoria.ModuloComProblema, assunto,
            "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.", null, [], 8, quando);
        await Chamados.Incluir(chamado, await Numerados(chamado));
        return (await Chamados.Obter(numero))!;
    }

    // grava a mudança feita no chamado lido; devolve o lido de novo
    async Task<Chamado> Gravar(Chamado chamado)
    {
        Assert.True(await Chamados.Salvar(chamado, await Numerados(chamado)), $"{chamado.Dados.Id} não foi gravado");
        return (await Chamados.Obter(chamado.Dados.Id))!;
    }

    // aberto, assumido pelo Caio e à espera da conta desde "quando"
    async Task<Chamado> Aguardando(ContaDoChamado conta, DateTimeOffset quando)
    {
        var chamado = await Aberto(conta, quando.AddHours(-1));
        chamado.Assumir(Caio, false, quando);
        chamado = await Gravar(chamado);
        chamado.MudarStatus(Caio, StatusDoChamado.AguardandoCliente, "Qual a turma?", quando);
        return await Gravar(chamado);
    }

    async Task<BsonDocument> Documento(NumeroDoChamado id) =>
        await Colecao("chamados").Find(Builders<BsonDocument>.Filter.Eq("_id", id.Valor)).SingleAsync();

    // RF-171: o contador "chamados" com $inc atômico — pedidos juntos não repetem número
    [Fact]
    public async Task RF_171_numeracao_sem_repetir_com_pedidos_juntos()
    {
        _ = backend.Server;
        var serie = $"teste-{Guid.NewGuid():N}";

        var numeros = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Numeracao.Proximo(serie)));

        Assert.Equal(Enumerable.Range(1, 20).Select(n => (long)n), numeros.Order());
    }

    // RF-171: o _id é o número do endereço ("SAC-000042") e "numero" guarda a ordem
    [Fact]
    public async Task RF_171_chamado_gravado_pelo_numero()
    {
        var chamado = await Aberto(Conta(), DateTimeOffset.UtcNow);

        var documento = await Documento(chamado.Dados.Id);

        Assert.Matches("^SAC-[0-9]{6}$", documento["_id"].AsString);
        Assert.Equal(chamado.Dados.Id.Numero, documento["numero"].ToInt64());
        Assert.Equal(1, documento["versao"].ToInt64());
        Assert.Equal(("ABERTO", "MODULO_COM_PROBLEMA", "HUB"),
            (documento["status"].AsString, documento["categoria"].AsString, documento["origem"]["tipo"].AsString));
        var evento = Assert.Single(await Colecao("mensagensChamado")
            .Find(Builders<BsonDocument>.Filter.Eq("chamadoId", chamado.Dados.Id.Valor)).ToListAsync());
        Assert.Matches("^msg-[0-9]{6}$", evento["_id"].AsString);
        Assert.Equal(("EVENTO", "ABERTO"), (evento["tipo"].AsString, evento["evento"].AsString));
    }

    // Decisão 2 de sac-documentos (RN-293, RN-297): a gravação só vale com a
    // versão lida; a segunda mudança feita sobre a mesma leitura é recusada e
    // não inclui nada na conversa
    [Fact]
    public async Task RN_293_gravacao_com_a_versao_antiga_e_recusada()
    {
        var aberto = await Aberto(Conta(), DateTimeOffset.UtcNow);
        var primeiro = (await Chamados.Obter(aberto.Dados.Id))!;
        var segundo = (await Chamados.Obter(aberto.Dados.Id))!;

        primeiro.Assumir(Caio, false, DateTimeOffset.UtcNow);
        var gravouPrimeiro = await Chamados.Salvar(primeiro, await Numerados(primeiro));
        segundo.Assumir(Ana, false, DateTimeOffset.UtcNow);
        var itensDoSegundo = await Numerados(segundo);
        var gravouSegundo = await Chamados.Salvar(segundo, itensDoSegundo);

        Assert.True(gravouPrimeiro);
        Assert.False(gravouSegundo);
        var gravado = (await Chamados.Obter(aberto.Dados.Id))!;
        Assert.Equal(("Caio Lima", 2L), (gravado.Dados.Responsavel!.Nome, gravado.Dados.Versao));
        var conversa = (await Chamados.Itens(aberto.Dados.Id)).Select(i => i.Id).ToList();
        Assert.All(itensDoSegundo, i => Assert.DoesNotContain(i.Id, conversa));
    }

    // RNF-230: a conversa na ordem dos números, e "depois de" o último id lido
    // traz só o que chegou depois (a conexão ao vivo e a reconexão)
    [Fact]
    public async Task RNF_230_itens_na_ordem_e_depois_do_ultimo_lido()
    {
        var chamado = await Aberto(Conta(), DateTimeOffset.UtcNow);
        var ultimoAntes = await Chamados.UltimoItem(chamado.Dados.Id);

        chamado.Assumir(Caio, false, DateTimeOffset.UtcNow);
        await Gravar(chamado);

        var todos = await Chamados.Itens(chamado.Dados.Id);
        var depois = await Chamados.Itens(chamado.Dados.Id, ultimoAntes);
        Assert.Equal(todos.Select(i => ItemDaConversa.NumeroDe(i.Id)).Order(), todos.Select(i => ItemDaConversa.NumeroDe(i.Id)));
        Assert.Equal(todos.Skip(1).Select(i => i.Id), depois.Select(i => i.Id));
        Assert.Equal(ItemDaConversa.NumeroDe(todos[^1].Id), await Chamados.UltimoItem(chamado.Dados.Id));
    }

    // RN-288, RF-174: a fila do time põe primeiro quem espera o time há mais
    // tempo; o chamado na vez da conta vai para o fim (ordemDaFila no fim dos
    // tempos, nunca nulo)
    [Fact]
    public async Task RN_288_fila_do_time_por_quem_espera_ha_mais_tempo()
    {
        var conta = Conta();
        var agora = DateTimeOffset.UtcNow;
        var recente = await Aberto(conta, agora.AddHours(-1));
        var antigo = await Aberto(conta, agora.AddHours(-3));
        var daConta = await Aguardando(conta, agora.AddHours(-4));

        var fila = await Chamados.Listar(new FiltroDeChamados(null, null, null, conta.Nome, false, null, false, null, agora, 1, 50));

        Assert.Equal(new[] { antigo.Dados.Id, recente.Dados.Id, daConta.Dados.Id }, fila.Itens.Select(d => d.Id));
        Assert.Equal(3, fila.Total);
        var ordem = (await Documento(daConta.Dados.Id))["ordemDaFila"];
        Assert.Equal(new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc), ordem.ToUniversalTime());
    }

    // RF-172: Meus chamados da conta, o atualizado por último primeiro
    [Fact]
    public async Task RF_172_meus_chamados_pelo_ultimo_atualizado()
    {
        var conta = Conta();
        var agora = DateTimeOffset.UtcNow;
        var antigo = await Aberto(conta, agora.AddHours(-3));
        var novo = await Aberto(conta, agora.AddHours(-2));
        antigo.Assumir(Caio, false, agora.AddHours(-1));
        await Gravar(antigo);

        var meus = await Chamados.Listar(new FiltroDeChamados(conta.Id, null, null, null, false, null, false, null, agora, 1, 50));

        Assert.Equal(new[] { antigo.Dados.Id, novo.Dados.Id }, meus.Itens.Select(d => d.Id));
    }

    // RF-174: a busca acha pelo nome da conta sem acento e sem maiúsculas, e pelo número
    [Fact]
    public async Task RF_174_busca_sem_acento_e_pelo_numero()
    {
        var conta = Conta("Escola São João");
        var chamado = await Aberto(conta, DateTimeOffset.UtcNow, "Relatório não abre");
        var marca = conta.Nome[^32..];

        var peloNome = await Chamados.Listar(new FiltroDeChamados(null, null, null, $"SAO JOAO {marca}", false, null, false, null,
            DateTimeOffset.UtcNow, 1, 50));
        var peloNumero = await Chamados.Listar(new FiltroDeChamados(null, null, null, chamado.Dados.Id.Valor.ToLowerInvariant(), false, null,
            false, null, DateTimeOffset.UtcNow, 1, 50));

        Assert.Equal(chamado.Dados.Id, Assert.Single(peloNome.Itens).Id);
        Assert.Contains(peloNumero.Itens, d => d.Id == chamado.Dados.Id);
    }

    // RN-290: Aguardando cliente e Resolvido há 7 dias vencem; antes disso, não
    [Fact]
    public async Task RN_290_vencidos_em_sete_dias()
    {
        var conta = Conta();
        var agora = DateTimeOffset.UtcNow;
        var aguardando = await Aguardando(conta, agora);
        var resolvido = await Aberto(conta, agora.AddHours(-1));
        resolvido.Assumir(Caio, false, agora);
        resolvido = await Gravar(resolvido);
        resolvido.MudarStatus(Caio, StatusDoChamado.Resolvido, "Corrigido.", agora);
        await Gravar(resolvido);

        var aosSeisDias = (await Chamados.VencidosEm(agora.AddDays(6))).Select(c => c.Dados.Id).ToList();
        var aosSeteDias = (await Chamados.VencidosEm(agora.AddDays(7).AddMinutes(1))).Select(c => c.Dados.Id).ToList();

        Assert.DoesNotContain(aguardando.Dados.Id, aosSeisDias);
        Assert.DoesNotContain(resolvido.Dados.Id, aosSeisDias);
        Assert.Contains(aguardando.Dados.Id, aosSeteDias);
        Assert.Contains(resolvido.Dados.Id, aosSeteDias);
    }

    // RN-294: o limite conta só ABERTO, EM_ATENDIMENTO e AGUARDANDO_CLIENTE da conta
    [Fact]
    public async Task RN_294_em_andamento_da_conta()
    {
        var conta = Conta();
        var agora = DateTimeOffset.UtcNow;
        await Aberto(conta, agora);
        await Aguardando(conta, agora);
        var encerrado = await Aberto(conta, agora);
        encerrado.Encerrar(DaConta(conta), agora);
        await Gravar(encerrado);

        Assert.Equal(2, await Chamados.EmAndamentoDaConta(conta.Id));
        Assert.Equal(0, await Chamados.EmAndamentoDaConta(Conta().Id));
    }

    // RNF-231: o fechado fica guardado por 5 anos (guardarAte = fechadoEm + 5 anos)
    [Fact]
    public async Task RNF_231_fechado_guardado_por_cinco_anos()
    {
        var conta = Conta();
        var chamado = await Aberto(conta, DateTimeOffset.UtcNow);
        var aberto = await Documento(chamado.Dados.Id);

        chamado.Encerrar(DaConta(conta), DateTimeOffset.UtcNow);
        await Gravar(chamado);

        Assert.True(aberto["guardarAte"].IsBsonNull);
        var fechado = await Documento(chamado.Dados.Id);
        Assert.Equal(("FECHADO", "PELA_CONTA"), (fechado["status"].AsString, fechado["fechamento"].AsString));
        Assert.Equal(fechado["fechadoEm"].ToUniversalTime().AddYears(5), fechado["guardarAte"].ToUniversalTime());
    }

    // RF-172, RF-174: "lido" de um lado não muda o do outro
    [Fact]
    public async Task RF_172_marca_lido_so_do_lado_que_le()
    {
        var chamado = await Aguardando(Conta(), DateTimeOffset.UtcNow);
        var antes = await Documento(chamado.Dados.Id);

        await Chamados.MarcarLido(chamado.Dados.Id, Visao.Conta);

        var depois = await Documento(chamado.Dados.Id);
        Assert.True(antes["naoLidoPelaConta"].AsBoolean);
        Assert.False(depois["naoLidoPelaConta"].AsBoolean);
        Assert.Equal(antes["naoLidoPeloTime"], depois["naoLidoPeloTime"]);
    }

    // PessoasDoSacNoBanco: a conta dona e o nome do autor vêm das coleções do 17
    [Fact]
    public async Task Pessoas_do_sac_leem_a_conta_e_o_nome_do_17()
    {
        var sigla = Preparo.SiglaSorteada();
        var (contaId, master) = await preparo.Conta(sigla);
        var pessoas = backend.Services.GetRequiredService<IPessoasDoSac>();

        var conta = await pessoas.Conta(contaId);
        var nome = await pessoas.Nome(master.Id);

        Assert.Equal((contaId, sigla, $"Escola {sigla}", "INSTITUICAO"), (conta!.Id, conta.Sigla, conta.Nome, conta.Tipo));
        Assert.Equal($"Escola {sigla}", nome);
        Assert.Null(await pessoas.Conta("conta-inexistente"));
    }
}
