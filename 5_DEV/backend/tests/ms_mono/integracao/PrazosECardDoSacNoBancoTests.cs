using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Card.Domain;
using Boaz.Core.Hub.Sac.Card.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.Core.Hub.Sac.Configuracao.Domain;
using Boaz.Core.Hub.Sac.Configuracao.Ports.Out;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Os adaptadores de saída das features configuracao e card com o banco de
// verdade (sac-hexagonal v01, sac-documentos v02): PrazosNoBanco
// (IConfiguracaoDoSac e IPrazosDoSac, documento "prazos" de configuracaoSac) e
// CardDoSacNoBanco (IContagemDoSac e ISeriesDoSac: o dia corrente contado em
// chamados e os dias fechados em cardSac). No padrão de ContasNoBancoTests.
public class PrazosECardDoSacNoBancoTests(Backend backend) : IClassFixture<Backend>
{
    static readonly Autor Caio = new(TipoDeAutor.Time, "usr-dboaz-caio", "Caio Lima");

    T Porta<T>() where T : notnull => backend.Services.GetRequiredService<T>();

    // RF-181, RN-289: sem nada salvo, o padrão 4 · 8 · 8 · 16 · 40 · 4 h úteis;
    // salvar uma categoria guarda as 6, com quem salvou, e vale para quem abre
    [Fact]
    public async Task RF_181_RN_289_prazos_padrao_e_salvos()
    {
        _ = backend.Server;
        var configuracao = Porta<IConfiguracaoDoSac>();
        var prazos = Porta<IPrazosDoSac>();

        var padrao = await configuracao.Ler();
        var duvidaAntes = await prazos.HorasUteis(Categoria.DuvidaDeUso);
        await configuracao.Salvar(padrao.Definir([(Categoria.DuvidaDeUso, 12)], "usr-dboaz-admin", DateTimeOffset.UtcNow));
        var salvos = await configuracao.Ler();

        Assert.Equal((4, 8, 8, 16, 40, 4), (padrao.HorasDe(Categoria.AcessoESenha), padrao.HorasDe(Categoria.Cobranca),
            padrao.HorasDe(Categoria.ModuloComProblema), padrao.HorasDe(Categoria.DuvidaDeUso), padrao.HorasDe(Categoria.SugestaoAoDeboaz),
            padrao.HorasDe(Categoria.SenhaContaMaster)));
        Assert.Equal(((DateTimeOffset?)null, (string?)null, 16), (padrao.SalvaEm, padrao.SalvaPor, duvidaAntes));
        Assert.Equal((12, 4, "usr-dboaz-admin"), (salvos.HorasDe(Categoria.DuvidaDeUso), salvos.HorasDe(Categoria.AcessoESenha), salvos.SalvaPor));
        Assert.NotNull(salvos.SalvaEm);
        Assert.Equal(12, await prazos.HorasUteis(Categoria.DuvidaDeUso));
        var documento = await backend.Banco.GetCollection<BsonDocument>("configuracaoSac")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "prazos")).SingleAsync();
        Assert.Equal(6, documento["prazos"].AsBsonArray.Count);
    }

    // RN-295: a contagem do dia corrente sai de chamados, só da conta pedida,
    // com a versão somada dos chamados do escopo
    [Fact]
    public async Task RN_295_contagem_da_conta_em_chamados()
    {
        _ = backend.Server;
        var chamados = Porta<IChamados>();
        var numeracao = Porta<INumeracao>();
        var sigla = Preparo.SiglaSorteada();
        var conta = new ContaDoChamado($"conta-{sigla.ToLowerInvariant()}", sigla, $"Escola {sigla}", "INSTITUICAO");
        var autor = new Autor(TipoDeAutor.Conta, $"usr-{sigla.ToLowerInvariant()}-master", conta.Nome);
        var agora = DateTimeOffset.UtcNow;

        async Task<Chamado> Abrir(DateTimeOffset quando)
        {
            var numero = new NumeroDoChamado(await numeracao.Proximo(INumeracao.Chamados));
            var chamado = Chamado.Abrir(numero, conta, autor, Categoria.DuvidaDeUso, "Como lançar notas",
                "Onde fica o lançamento de notas do bimestre da turma?", null, [], 16, quando);
            await chamados.Incluir(chamado, await Numerar(numeracao, chamado));
            return (await chamados.Obter(numero))!;
        }

        // um atrasado (aberto há 30 dias, sem resposta), um no prazo e um à espera da conta
        await Abrir(agora.AddDays(-30));
        await Abrir(agora);
        var aguardando = await Abrir(agora.AddHours(-1));
        aguardando.Assumir(Caio, false, agora);
        await chamados.Salvar(aguardando, await Numerar(numeracao, aguardando));
        aguardando = (await chamados.Obter(aguardando.Dados.Id))!;
        aguardando.MudarStatus(Caio, StatusDoChamado.AguardandoCliente, "Qual a turma?", agora);
        await chamados.Salvar(aguardando, await Numerar(numeracao, aguardando));

        var contagem = await Porta<IContagemDoSac>().Contar(conta.Id, agora.AddMinutes(1));

        Assert.Equal((3, 3, 1, 0, 1, 1), (contagem.Total, contagem.EmAndamento, contagem.AguardandoCliente, contagem.EmAtendimento,
            contagem.NaoLidosPelaConta, contagem.Atrasados));
        Assert.Equal(1 + 1 + 3, contagem.Versao);
        Assert.Null(contagem.NotaMedia);
        Assert.Contains(conta.Id, await Porta<IContagemDoSac>().ContasComChamado());
        var vazia = await Porta<IContagemDoSac>().Contar($"conta-sem-chamado-{Guid.NewGuid():N}", agora);
        Assert.Equal((0, 0L), (vazia.Total, vazia.Versao));
    }

    // 15 RN-278: o fechamento do dia grava um documento "<escopo>:<AAAA-MM-DD>"
    // em cardSac; fechar de novo troca, não duplica; as séries leem os fechados
    [Fact]
    public async Task RN_295_fechamento_do_dia_em_cardSac()
    {
        _ = backend.Server;
        var series = Porta<ISeriesDoSac>();
        var escopo = $"conta-card-{Guid.NewGuid():N}";
        var dia = CalendarioDoCard.Dia(DateTimeOffset.UtcNow).AddDays(-1);
        static ContagemDoSac Contagem(int emAndamento) => new(emAndamento, emAndamento, 0, emAndamento, 2, 0, 1, 0, 4.5m, 9, null);

        await series.Fechar(escopo, dia, Contagem(3));
        await series.Fechar(escopo, dia, Contagem(5));
        var fechados = await series.Fechados(escopo, dia.AddDays(-11));

        var documento = Assert.Single(await backend.Banco.GetCollection<BsonDocument>("cardSac")
            .Find(Builders<BsonDocument>.Filter.Eq("escopo", escopo)).ToListAsync());
        Assert.Equal($"{escopo}:{CalendarioDoCard.ChaveDoDia(dia)}", documento["_id"].AsString);
        Assert.Equal((5, 2, 1, 0), (documento["emAndamento"].ToInt32(), documento["resolvidos"].ToInt32(), documento["semResposta"].ToInt32(),
            documento["atrasados"].ToInt32()));
        Assert.Equal(4.5m, documento["notaMedia"].ToDecimal());
        Assert.Equal(5, Assert.Single(fechados, f => f.Key == dia).Value);
    }

    static async Task<IReadOnlyList<ItemDaConversa>> Numerar(INumeracao numeracao, Chamado chamado)
    {
        var itens = new List<ItemDaConversa>();
        foreach (var item in chamado.Novos) itens.Add(item with { Id = ItemDaConversa.IdDe(await numeracao.Proximo(INumeracao.Mensagens)) });
        return itens;
    }
}
