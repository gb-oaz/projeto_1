using Boaz.Qa.Integracao.Hub.Usuarios;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using MongoDB.Driver;
using Reqnroll;

namespace Boaz.Qa.Integracao.Hub.Sac;

// Passos de RNF-232 de 1_Requisitos/18_modulo-sac.md v06: as coleções do SAC
// e os índices delas prontos na partida, antes do primeiro pedido; o índice
// único de "cardSac" que não pode ser criado para a partida (código 1, como a
// RNF-227 do 17); o não único de "mensagensChamado" vira aviso no log. Os
// índices são os do modelo sac-documentos v03.
//
// Como os de RNF-227 (hub/usuarios/IndicesPassos.cs): uma instância própria
// (suporte/Partida.cs), em homologação, e o banco dela
// (suporte/BancoDaPartida.cs), só nos metadados. "Quando envia GET /health,
// sem login" e "o log traz um aviso …" são os de IndicesPassos; "Quando a
// aplicação de homologação inicia", o de ContasIniciaisPassos.
[Binding]
public sealed class PartidaDoSacPassos(Partida partida, IndicesPassos indices)
{
    // as coleções do SAC criadas na partida, com os índices (Indices.cs):
    // configuracaoSac e contadores nascem no primeiro uso (RNF-232); ↓ é -1,
    // como no IndicesPassos do 17 (Indice.ChaveDoRequisito)
    static readonly Dictionary<string, string[]> IndicesDoSac = new()
    {
        ["chamados"] =
        [
            "contaId+atualizadoEm↓", "contaId+status", "status+ordemDaFila", "responsavel.usuarioId+status", "status+prazoResposta",
            "status+aguardandoDesde", "status+resolvidoEm", "avaliacao.nota", "guardarAte",
        ],
        ["mensagensChamado"] = ["chamadoId+numero"],
        ["anexosSac"] = ["chamadoId", "expiraEm", "enviadoPor+enviadoEm"],
        ["cardSac"] = ["escopo+dia"],
    };

    [Given(@"^a partida de homologação concluída, com as coleções do SAC e seus índices prontos$")]
    public async Task PartidaConcluida()
    {
        partida.KeyVaultCompleto("hml");
        await partida.IniciarPronta("homologação");
        var nomes = await BancoDaPartida.NomesDasColecoes();
        foreach (var (colecao, chaves) in IndicesDoSac)
        {
            Assert.True(nomes.Contains(colecao), $"a partida não criou a coleção \"{colecao}\"");
            var tem = (await BancoDaPartida.Indices(colecao)).Select(i => i.Chave).ToList();
            foreach (var chave in chaves.Select(Indice.ChaveDoRequisito))
            {
                Assert.True(tem.Contains(chave), $"a partida não criou o índice {{{chave}}} em \"{colecao}\"; tem {string.Join(", ", tem)}");
            }
        }
        var cardSac = await BancoDaPartida.Indices("cardSac");
        Assert.True(cardSac.Any(i => i.Chave == Indice.ChaveDoRequisito("escopo+dia") && i.Unico),
            "o índice de escopo e dia em \"cardSac\" devia ser único (RN-295, RNF-227)");
    }

    // os dois documentos vão direto para o banco, antes da partida: pela API
    // o segundo card do mesmo dia nunca seria gravado
    [Given(@"^dois documentos de ""(cardSac)"" com o escopo ""(\w+)"" e o dia (\d{4}-\d{2}-\d{2})$")]
    public async Task DoisCardsDoDia(string colecao, string escopo, string dia)
    {
        partida.KeyVaultCompleto("hml");
        foreach (var n in new[] { 1, 2 })
        {
            await BancoDaPartida.Colecao(colecao).InsertOneAsync(new BsonDocument
            {
                ["_id"] = $"{escopo}:{dia}:rnf232-{n}",
                ["escopo"] = escopo,
                ["dia"] = dia,
                ["emAndamento"] = 0,
                ["resolvidos"] = 0,
                ["semResposta"] = 0,
                ["atrasados"] = 0,
                ["notaMedia"] = BsonNull.Value,
            });
        }
    }

    [Then(@"^GET /health, sem login, não tem resposta, a conexão é recusada$")]
    public async Task SaudeRecusada()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var r = await http.GetAsync($"{partida.Url}/health");
            Assert.Fail($"GET /health respondeu {(int)r.StatusCode} sem o índice único de \"cardSac\". Log:\n{partida.Log}");
        }
        catch (HttpRequestException e)
        {
            var socket = e.InnerException as System.Net.Sockets.SocketException;
            Assert.True(socket?.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused,
                $"esperava conexão recusada, veio {e.GetType().Name}: {e.Message}");
        }
    }

    [Then(@"^o processo termina com código 1, como na RNF-227$")]
    public void TerminaComUm()
    {
        Assert.False(partida.Rodando || partida.AtendeuPedido, $"a aplicação seguiu e aceitou pedidos. Log:\n{partida.Log}");
        Assert.True(partida.CodigoDeSaida == 1, $"esperava código de saída 1, veio {partida.CodigoDeSaida?.ToString() ?? "nenhum"}. Log:\n{partida.Log}");
    }

    // um índice da suíte na mesma chave, com outro nome: o banco recusa o da
    // aplicação (IndexOptionsConflict), sem a suíte saber o nome interno dele
    [Given(@"^o banco recusa o índice chamadoId_numero em ""(mensagensChamado)"" na partida$")]
    public async Task RecusaNaPartida(string colecao)
    {
        partida.KeyVaultCompleto("hml");
        await BancoDaPartida.Colecao(colecao).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("chamadoId").Ascending("numero"),
            new CreateIndexOptions { Name = BancoDaPartida.IndiceQueImpede }));
        indices.RecusaPreparada(colecao, "chamadoId");
    }
}
