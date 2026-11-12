using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using MongoDB.Driver;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Usuarios;

// Passos da RNF-227 de 1_Requisitos/17_modulo-usuario.md: coleções e índices
// prontos na partida, sem duplicar na segunda, e o índice que não pode ser
// criado vira aviso no log sem parar a partida. Índices como o diagrama
// identidade-acesso-documentos (v08) especifica.
//
// Sobem uma instância própria (suporte/Partida.cs), em homologação, com o Key
// Vault completo, e olham o banco dela (suporte/BancoDaPartida.cs): o esquema
// é o que a RNF-227 promete e não há endereço na API que o mostre. O "Quando a
// aplicação inicia" é o de ContasIniciaisPassos.
[Binding]
public sealed partial class IndicesPassos(Partida partida)
{
    // a coleção do Esquema do Cenário em conferência
    string colecao = "";
    // a primeira partida: índices e _id de cada coleção
    readonly Dictionary<string, List<Indice>> indicesAntes = [];
    readonly Dictionary<string, List<string>> idsAntes = [];
    // as duas contas com a mesma sigla: os valores que o log não pode trazer
    readonly List<string> valoresGravados = [];
    // o campo do índice que não pôde ser criado, que o log tem de citar
    string campoDoIndice = "";
    // a partida que o Quando sobe antes de pedir /health (RNF-227-api)
    Func<Task>? partir;
    Resposta? saude;
    Exception? erroDaSaude;

    // ── coleção pronta antes do primeiro pedido ────────────────────────────
    [Given(@"^o banco de homologação vazio$")]
    public async Task BancoVazio()
    {
        partida.KeyVaultCompleto("hml");
        await BancoDaPartida.Esvaziar();
    }

    // a instância só é dada como pronta no primeiro 200 de /health: a conferência
    // vem logo depois, sem esperar, e um índice criado depois disso não conta
    [Then(@"^antes de aceitar o primeiro pedido, a coleção ""(\w+)"" existe$")]
    public async Task ColecaoExiste(string nome)
    {
        ExigirPronta();
        colecao = nome;
        var nomes = await BancoDaPartida.NomesDasColecoes();
        Assert.True(nomes.Contains(nome), $"a coleção \"{nome}\" não existe na partida; há: {string.Join(", ", nomes)}");
    }

    [Then(@"^tem os índices (.+)$")]
    public async Task TemOsIndices(string descricao)
    {
        var (esperados, semExpiracao) = Esperados(descricao);
        var tem = await BancoDaPartida.Indices(colecao);
        foreach (var esperado in esperados)
        {
            var achado = tem.FirstOrDefault(i => i.Chave == esperado.Chave);
            Assert.True(achado is not null,
                $"\"{colecao}\" não tem o índice {esperado}; tem: {Listar(tem)}");
            Assert.True(achado.Unico == esperado.Unico,
                $"\"{colecao}\": o índice {{{esperado.Chave}}} devia {(esperado.Unico ? "" : "não ")}ser único; veio {achado}");
            if (esperado.Parcial)
            {
                Assert.True(achado.Parcial, $"\"{colecao}\": o índice {{{esperado.Chave}}} devia valer só com o campo presente; veio {achado}");
            }
        }
        if (semExpiracao)
        {
            Assert.False(tem.Any(i => i.Expira), $"\"{colecao}\" não pode ter prazo de expiração (TTL); tem: {Listar(tem)}");
        }
    }

    // "sigla único, dominio único, documentoNumeros"; "contaId+matricula único só
    // com matrícula"; "contaId+quando, sem prazo de expiração"; "aparelho+quando,
    // o limite por aparelho da RNF-205" (o motivo do índice é para quem lê)
    static (List<Indice> Esperados, bool SemExpiracao) Esperados(string descricao)
    {
        var esperados = new List<Indice>();
        var semExpiracao = false;
        // "; chamado": o índice novo depois do motivo (17 v109, pedidosAjuda)
        foreach (var parte in descricao.Split([", ", "; "], StringSplitOptions.TrimEntries))
        {
            if (parte == "sem prazo de expiração")
            {
                semExpiracao = true;
                continue;
            }
            if (parte.StartsWith("o limite", StringComparison.Ordinal)) continue;
            var m = DescricaoDoIndice().Match(parte);
            if (!m.Success) throw new InvalidOperationException($"índice \"{parte}\" fora das formas que o passo conhece (RNF-227)");
            esperados.Add(new Indice(Indice.ChaveDoRequisito(m.Groups["campos"].Value),
                m.Groups["unico"].Success, m.Groups["parcial"].Success, false));
        }
        return (esperados, semExpiracao);
    }

    [GeneratedRegex(@"^(?<campos>[\w↓]+(?:\+[\w↓]+)*)(?<unico> único)?(?<parcial> só com matrícula)?$")]
    private static partial Regex DescricaoDoIndice();

    // ── segunda partida não duplica ────────────────────────────────────────
    // a data do exemplo é só a ordem das partidas: a suíte não muda o relógio
    [Given(@"^a aplicação de homologação iniciada em \d{4}-\d{2}-\d{2}, com as 8 coleções e seus índices$")]
    public async Task IniciadaComAsColecoes()
    {
        partida.KeyVaultCompleto("hml");
        await BancoDaPartida.Esvaziar();
        await partida.IniciarPronta("homologação, primeira partida");
        var nomes = await BancoDaPartida.NomesDasColecoes();
        foreach (var nome in BancoDaPartida.Colecoes)
        {
            Assert.True(nomes.Contains(nome), $"preparo: a primeira partida não criou a coleção \"{nome}\"; há: {string.Join(", ", nomes)}");
            var indices = await BancoDaPartida.Indices(nome);
            Assert.True(indices.Count > 0, $"preparo: a primeira partida não criou os índices de \"{nome}\"");
        }
        // um documento em cada coleção, para a segunda partida não poder apagar nem repetir
        foreach (var (nome, documento) in Marcadores())
        {
            await BancoDaPartida.Colecao(nome).InsertOneAsync(documento);
        }
        foreach (var nome in BancoDaPartida.Colecoes)
        {
            indicesAntes[nome] = await BancoDaPartida.Indices(nome);
            idsAntes[nome] = await BancoDaPartida.Ids(nome);
        }
    }

    [When(@"^ela inicia de novo em \d{4}-\d{2}-\d{2}$")]
    public Task IniciaDeNovo() => partida.Iniciar();

    [Then(@"^cada coleção continua uma só, com os mesmos índices, nenhum repetido$")]
    public async Task MesmosIndices()
    {
        ExigirPronta();
        var nomes = await BancoDaPartida.NomesDasColecoes();
        foreach (var nome in BancoDaPartida.Colecoes)
        {
            Assert.True(nomes.Count(n => string.Equals(n, nome, StringComparison.OrdinalIgnoreCase)) == 1,
                $"esperava uma só coleção \"{nome}\"; há: {string.Join(", ", nomes)}");
            var agora = await BancoDaPartida.Indices(nome);
            var repetidos = agora.GroupBy(i => i.Chave).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(repetidos.Count == 0, $"\"{nome}\" tem índice repetido na mesma chave: {string.Join(", ", repetidos)}");
            Assert.True(Listar(agora) == Listar(indicesAntes[nome]),
                $"\"{nome}\" mudou de índices na segunda partida: antes {Listar(indicesAntes[nome])}; agora {Listar(agora)}");
        }
    }

    [Then(@"^nenhum documento das coleções é apagado nem duplicado$")]
    public async Task NenhumDocumento()
    {
        foreach (var nome in BancoDaPartida.Colecoes)
        {
            var agora = await BancoDaPartida.Ids(nome);
            var sumidos = idsAntes[nome].Except(agora).ToList();
            Assert.True(sumidos.Count == 0, $"a segunda partida apagou de \"{nome}\": {string.Join(", ", sumidos)}");
        }
        // as contas e os usuários iniciais não nascem de novo (RN-267): nenhum _id novo
        foreach (var nome in new[] { "contas", "usuarios" })
        {
            var novos = (await BancoDaPartida.Ids(nome)).Except(idsAntes[nome]).ToList();
            Assert.True(novos.Count == 0, $"a segunda partida gravou de novo em \"{nome}\": {string.Join(", ", novos)}");
        }
    }

    // documentos só com os campos dos índices, valores que não colidem com as contas iniciais
    static IEnumerable<(string, BsonDocument)> Marcadores()
    {
        const string conta = "conta-qa-rnf227";
        var agora = DateTime.UtcNow;
        yield return ("contas", new BsonDocument
        {
            ["_id"] = conta,
            ["sigla"] = "MARCA",
            ["dominio"] = "marca_rnf227",
            ["documentoNumeros"] = "00000000000191",
            ["nome"] = "Marca RNF-227",
            ["tipo"] = "INSTITUICAO",
            ["estado"] = "ATIVA",
            ["criadaEm"] = agora,
        });
        yield return ("usuarios", new BsonDocument
        {
            ["_id"] = "usr-marca-rnf227",
            ["contaId"] = conta,
            ["sigla"] = "MARCA",
            ["tipo"] = "STAFF",
            ["estado"] = "ATIVO",
            ["nome"] = "Marca RNF-227",
            ["nomeCurto"] = "rnf227",
            ["login"] = "rnf227.marca@deboaz.com",
            ["criadoEm"] = agora,
        });
        yield return ("auditoria", new BsonDocument
        {
            ["_id"] = "qa-rnf227-auditoria",
            ["traceId"] = "trc-qa-rnf227",
            ["etapa"] = "CONCLUIDO",
            ["contaId"] = conta,
            ["quem"] = "usr-marca-rnf227",
            ["acao"] = "CRIAR_USUARIO",
            ["alvo"] = "usr-marca-rnf227",
            ["quando"] = agora,
            ["guardarAte"] = agora.AddYears(5),
        });
        yield return ("auditoriaErros", new BsonDocument
        {
            ["_id"] = "qa-rnf227-erro",
            ["traceId"] = "trc-qa-rnf227",
            ["contaId"] = conta,
            ["quem"] = "usr-marca-rnf227",
            ["acao"] = "CRIAR_USUARIO",
            ["codigo"] = "NOME_CURTO_EM_USO",
            ["nivel"] = "ALERTA",
            ["quando"] = agora,
            ["guardarAte"] = agora.AddYears(5),
        });
        yield return ("geracoesSenha", new BsonDocument
        {
            ["_id"] = "qa-rnf227-geracao",
            ["sigla"] = "MARCA",
            ["contaId"] = conta,
            ["usuarioId"] = "usr-marca-rnf227",
            ["pedidaPor"] = "usr-marca-rnf227",
            ["pedidaEm"] = agora,
        });
        yield return ("registrosPermissao", new BsonDocument
        {
            ["_id"] = "qa-rnf227-registro",
            ["sigla"] = "MARCA",
            ["usuarioId"] = "usr-marca-rnf227",
            ["por"] = "usr-marca-rnf227",
            ["em"] = agora,
            ["versao"] = 1,
        });
        yield return ("cardUsuarios", new BsonDocument
        {
            ["_id"] = "card-marca-INSTITUICAO",
            ["contaId"] = conta,
            ["perfil"] = "INSTITUICAO",
            ["usuarioId"] = BsonNull.Value,
            ["versao"] = 1,
            ["atualizadoEm"] = agora,
        });
        yield return ("pedidosAjuda", new BsonDocument
        {
            ["_id"] = "qa-rnf227-pedido",
            ["aparelho"] = "qa-rnf227",
            ["instituicao"] = "Marca RNF-227",
            ["quando"] = agora,
        });
    }

    // ── índice que não pode ser criado ─────────────────────────────────────
    // os documentos vão direto para o banco, antes da partida: pela API o
    // segundo seria recusado (RN-179). Os outros campos únicos diferem, para só
    // o índice do exemplo ficar sem poder ser criado
    [Given(@"^duas contas com ""(\w+)"" na coleção ""(contas)""$")]
    public async Task DuasContas(string sigla, string nome)
    {
        await Preparar(nome);
        foreach (var n in new[] { 1, 2 })
        {
            var id = $"conta-{sigla.ToLowerInvariant()}-rnf227-{n}";
            var dominio = $"{sigla.ToLowerInvariant()}_rnf227_{n}";
            await BancoDaPartida.Colecao(nome).InsertOneAsync(new BsonDocument
            {
                ["_id"] = id,
                ["sigla"] = sigla,
                ["dominio"] = dominio,
                ["documentoNumeros"] = $"1234567800019{n}",
                ["nome"] = $"Escola Repetida {n}",
                ["tipo"] = "INSTITUICAO",
                ["estado"] = "ATIVA",
                ["criadaEm"] = DateTime.UtcNow,
            });
            valoresGravados.AddRange([id, dominio]);
        }
        valoresGravados.Add(sigla);
    }

    [Given(@"^dois usuários com um login na coleção ""(usuarios)""$")]
    public async Task DoisUsuarios(string nome)
    {
        await Preparar(nome);
        const string login = "repetido.rnf227@deboaz.com";
        foreach (var n in new[] { 1, 2 })
        {
            var id = $"usr-rnf227-repetido{n}";
            await BancoDaPartida.Colecao(nome).InsertOneAsync(new BsonDocument
            {
                ["_id"] = id,
                ["contaId"] = $"conta-rnf227-{n}",
                ["sigla"] = "RPTDO",
                ["tipo"] = "STAFF",
                ["estado"] = "ATIVO",
                ["nome"] = $"Repetido {n}",
                ["nomeCurto"] = $"repetido{n}",
                ["login"] = login,
                ["criadoEm"] = DateTime.UtcNow,
            });
            valoresGravados.Add(id);
        }
        valoresGravados.Add(login);
    }

    // índice único já existe na mesma chave com outro nome: o banco recusa o da
    // aplicação (IndexOptionsConflict), e a partida tem de parar como nos dados repetidos
    [Given(@"^índice único de outro nome na coleção ""(contas)""$")]
    public async Task UnicoDeOutroNome(string nome)
    {
        await Preparar(nome);
        await BancoDaPartida.CriarIndiceQueImpede(nome, "sigla", unico: true);
    }

    // índice não único: o banco o recusa porque a suíte criou antes um índice
    // na mesma chave com outro nome (BancoDaPartida.CriarIndiceQueImpede)
    [Given(@"^o banco recusa o índice na coleção ""(geracoesSenha)""$")]
    public Task BancoRecusa(string nome) => RecusarNaPartida("resumoComprovante", nome);

    [Then(@"^o índice (.+) não é criado$")]
    public async Task IndiceNaoCriado(string descricao)
    {
        var (esperados, _) = Esperados(descricao);
        var tem = (await BancoDaPartida.Indices(colecao)).Where(i => i.Nome != BancoDaPartida.IndiceQueImpede).ToList();
        foreach (var esperado in esperados)
        {
            Assert.False(tem.Any(i => i.Chave == esperado.Chave && i.Unico == esperado.Unico),
                $"\"{colecao}\" ganhou o índice {esperado} mesmo com o impedimento: {Listar(tem)}");
        }
        campoDoIndice = esperados[0].Chave.Split(':')[0];
    }

    [Then(@"^o log cita o nome do índice e da coleção, nunca o valor que impediu$")]
    public void LogCita()
    {
        var registro = Registros().FirstOrDefault(Cita);
        Assert.True(registro is not null,
            $"o log não cita o índice de {campoDoIndice} e a coleção \"{colecao}\" num mesmo registro. Log:\n{partida.Log}");
        SemValorNoLog();
    }

    [Then(@"^a aplicação para antes do 1º pedido$")]
    public void ParaAntes()
    {
        Assert.False(partida.AtendeuPedido, $"a aplicação aceitou pedidos (/health 200) sem o índice único de {campoDoIndice} em \"{colecao}\"");
        Assert.True(partida.CodigoDeSaida == 1, $"esperava código de saída 1, veio {partida.CodigoDeSaida?.ToString() ?? "nenhum"}");
    }

    [Then(@"^a aplicação aceita pedidos, com aviso$")]
    public void AceitaComAviso()
    {
        ExigirPronta();
        Assert.True(Registros().Any(r => Aviso(r) && Cita(r)),
            $"o log não traz um aviso com o índice de {campoDoIndice} e a coleção \"{colecao}\". Log:\n{partida.Log}");
    }

    // ── GET /health (RNF-227-api, contrato usuarios-contrato-api v05, SISTEMA) ─
    [Given(@"^a partida de homologação concluída, com as 8 coleções e seus índices prontos$")]
    public async Task PartidaConcluida()
    {
        partida.KeyVaultCompleto("hml");
        await BancoDaPartida.Esvaziar();
        await partida.IniciarPronta("homologação");
        var nomes = await BancoDaPartida.NomesDasColecoes();
        foreach (var nome in BancoDaPartida.Colecoes)
        {
            Assert.True(nomes.Contains(nome), $"preparo: a partida não criou a coleção \"{nome}\"");
            Assert.True((await BancoDaPartida.Indices(nome)).Count > 0, $"preparo: a partida não criou os índices de \"{nome}\"");
        }
    }

    // as contas iniciais entram pela API; os módulos são os do registro (15_modulos)
    [Given(@"^os módulos gravados e as contas iniciais garantidas$")]
    public async Task ModulosEContas()
    {
        Assert.True(await BancoDaPartida.Colecao("modulos").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) > 0,
            "preparo: a partida não gravou os módulos");
        foreach (var conta in ContasIniciais.Todas)
        {
            var r = await partida.Entrar(conta);
            Assert.True(r.Status == 200, $"preparo: {conta.Nome} não entra na instância: {r}");
        }
    }

    [Given(@"^a aplicação de homologação ainda na partida$")]
    public void AindaNaPartida()
    {
        partida.KeyVaultCompleto("hml");
        partir = () =>
        {
            partida.Lancar();
            return Task.CompletedTask;
        };
    }

    [Given(@"^a aplicação de homologação sem o índice único de sigla em ""(contas)""$")]
    public async Task SemIndiceDeSigla(string nome)
    {
        await DuasContas("EBFGA", nome);
        partir = () => partida.Iniciar();
    }

    [Given(@"^a aplicação de homologação sem a senha do administrador, como na RNF-226$")]
    public void SemSenhaDoAdmin()
    {
        partida.KeyVaultCompleto("hml");
        partida.KeyVault[ContasIniciais.Admin.VariavelSenha!] = null;
        partir = () => partida.Iniciar();
    }

    [Given(@"^a aplicação de homologação com a senha fraca ""(.+)"" de ""(.+)""$")]
    public void SenhaFraca(string senha, string entrada)
    {
        partida.KeyVaultCompleto("hml");
        var variavel = ContasIniciais.PorEntrada(entrada).VariavelSenha!;
        partida.KeyVault[variavel] = senha;
        partir = () => partida.Iniciar();
    }

    // o Mongo da instância numa porta sem ninguém: as mesmas variáveis de
    // conexão que as PARTIDA_* entregam, apontando para lugar nenhum
    [Given(@"^a aplicação de homologação com o banco fora do ar$")]
    public void BancoForaDoAr()
    {
        partida.KeyVaultCompleto("hml");
        var conexoes = Ambiente.DaPartida().Select(v => v.Key).Where(n => n.StartsWith("ConnectionStrings__Mongo", StringComparison.Ordinal)).ToList();
        if (conexoes.Count == 0) throw new PendingStepException("PARTIDA_ConnectionStrings__Mongo* ausente: a suíte não sabe qual conexão derrubar");
        foreach (var nome in conexoes)
        {
            partida.KeyVault[nome] = $"mongodb://127.0.0.1:{PortaSemNinguem()}/?serverSelectionTimeoutMS=3000&connectTimeoutMS=3000";
        }
        partir = () => partida.Iniciar();
    }

    [Given(@"^o banco recusa o índice resumoComprovante em ""(geracoesSenha)"" na partida$")]
    public async Task RecusaNaPartida(string nome)
    {
        await RecusarNaPartida("resumoComprovante", nome);
        partir = () => partida.Iniciar();
    }

    [When(@"^envia GET /health, sem login, sem corpo nem cabeçalho$")]
    [When(@"^envia GET /health, sem login$")]
    public async Task Saude()
    {
        if (partir is not null) await partir();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var r = await http.GetAsync($"{partida.Url}/health");
            saude = new Resposta((int)r.StatusCode, await r.Content.ReadAsStringAsync(), []);
            erroDaSaude = null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            saude = null;
            erroDaSaude = e;
        }
    }

    [Then(@"^a resposta é 200, sem corpo$")]
    public void SaudeSemCorpo()
    {
        Assert.True(saude is not null, $"GET /health sem resposta: {erroDaSaude?.Message}. Log:\n{partida.Log}");
        Assert.True(saude.Status == 200, $"GET /health: esperava 200, veio {saude}");
        Assert.True(saude.Texto.Length == 0, $"GET /health: esperava sem corpo, veio \"{saude.Texto}\"");
    }

    [Then(@"^não há resposta, a conexão é recusada$")]
    public void ConexaoRecusada()
    {
        Assert.True(saude is null, $"GET /health respondeu {saude} antes ou sem a partida");
        var socket = erroDaSaude?.InnerException as System.Net.Sockets.SocketException ?? erroDaSaude as System.Net.Sockets.SocketException;
        Assert.True(socket?.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused,
            $"esperava conexão recusada, veio {erroDaSaude?.GetType().Name}: {erroDaSaude?.Message}");
    }

    // "ainda na partida": o pedido sai logo depois de lançar o processo, que
    // tem de continuar rodando (sem gancho de atraso, depende de a partida
    // levar mais que o pedido)
    [Then(@"^o processo segue na partida$")]
    public void SegueNaPartida() =>
        Assert.True(partida.Rodando, $"o processo terminou com {partida.CodigoDeSaida} em vez de seguir na partida. Log:\n{partida.Log}");

    [Then(@"^o processo termina com código 1$")]
    public void TerminaComUm()
    {
        Assert.False(partida.Rodando || partida.AtendeuPedido, "a aplicação seguiu e aceitou pedidos");
        Assert.True(partida.CodigoDeSaida == 1, $"esperava código de saída 1, veio {partida.CodigoDeSaida?.ToString() ?? "nenhum"}. Log:\n{partida.Log}");
    }

    [Then(@"^o log traz um aviso com o nome do índice e da coleção, nunca o valor$")]
    public void AvisoNoLog()
    {
        Assert.True(Registros().Any(r => Aviso(r) && Cita(r)),
            $"o log não traz um aviso com o índice de {campoDoIndice} e a coleção \"{colecao}\". Log:\n{partida.Log}");
        SemValorNoLog();
    }

    // ── apoio do log e do preparo ──────────────────────────────────────────
    async Task Preparar(string nome)
    {
        partida.KeyVaultCompleto("hml");
        await BancoDaPartida.Esvaziar();
        colecao = nome;
    }

    async Task RecusarNaPartida(string campo, string nome)
    {
        await Preparar(nome);
        await BancoDaPartida.CriarIndiceQueImpede(nome, campo);
        campoDoIndice = campo;
    }

    // o nome do índice é interno (caixa-preta, RN-151): o registro tem de citar
    // o campo do índice e a coleção
    bool Cita(string registro) =>
        registro.Contains(colecao, StringComparison.Ordinal) && registro.Contains(campoDoIndice, StringComparison.OrdinalIgnoreCase);

    IEnumerable<string> Registros() => InicioDoRegistro().Split(partida.Log);

    void SemValorNoLog()
    {
        foreach (var valor in valoresGravados)
        {
            Assert.False(partida.Log.Contains(valor, StringComparison.Ordinal), $"o log traz um valor gravado: \"{valor}\"");
        }
    }

    // console simples ("warn: Categoria[0]" e a mensagem nas linhas de baixo) ou JSON
    static bool Aviso(string registro) =>
        registro.StartsWith("warn:", StringComparison.Ordinal)
        || registro.Contains("\"Warning\"", StringComparison.Ordinal)
        || registro.Contains("[WRN]", StringComparison.Ordinal);

    [GeneratedRegex(@"(?m)^(?=(?:trce|dbug|info|warn|fail|crit): )")]
    private static partial Regex InicioDoRegistro();

    static int PortaSemNinguem()
    {
        using var ouvinte = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        ouvinte.Start();
        return ((System.Net.IPEndPoint)ouvinte.LocalEndpoint).Port;
    }

    // ── apoio ──────────────────────────────────────────────────────────────
    void ExigirPronta()
    {
        if (!partida.AtendeuPedido)
        {
            throw new InvalidOperationException(
                $"a aplicação de {partida.AmbienteDaPartida} terminou com {partida.CodigoDeSaida} antes de atender. Log:\n{partida.Log}");
        }
    }

    static string Listar(IEnumerable<Indice> indices) => string.Join(", ", indices.Select(i => i.ToString()).Order(StringComparer.Ordinal));
}
