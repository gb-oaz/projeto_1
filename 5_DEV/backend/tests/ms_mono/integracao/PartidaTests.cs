using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Infra.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Infra.Out.Commons;
using Boaz.MsMono.Infra.Out.Commons.Usuario.Autorizacao;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// A partida com o banco de verdade (17 RN-190, RN-265 a RN-268, RNF-226): as
// contas iniciais pelo _id, sem duplicar, e os índices do diagrama
// identidade-acesso-documentos. Cada teste roda a partida de novo no banco da
// fixture, com o Key Vault que ele monta.
public class PartidaTests(Backend backend) : IClassFixture<Backend>
{
    static Dictionary<string, string?> KeyVault(string ambiente) => new()
    {
        ["AMBIENTE"] = ambiente,
        ["USUARIO_ADMIN_ENTRADA"] = Backend.AdminEntrada,
        ["USUARIO_ADMIN_SENHA"] = Backend.AdminSenha,
        ["USUARIO_QA_ADMIN_ENTRADA"] = "qaadmin.dboaz@deboaz.com",
        ["USUARIO_QA_ADMIN_SENHA"] = "Qa#Admin123456",
        ["USUARIO_QA_MEMBRO_ENTRADA"] = "qamembro.dboaz@deboaz.com",
        ["USUARIO_QA_MEMBRO_SENHA"] = "Qa#Membro123456",
        ["USUARIO_QA_MASTER_ENTRADA"] = "qa_instituicao@deboaz.com",
        ["USUARIO_QA_MASTER_SENHA"] = "Qa#Master123456",
        ["USUARIO_QA_AUTONOMO_ENTRADA"] = "qa_autonomo@deboaz.com",
        ["USUARIO_QA_AUTONOMO_SENHA"] = "Qa#Autonomo123456",
        ["USUARIO_QA_STAFF_ENTRADA"] = "qastaff.qains@deboaz.com",
        ["USUARIO_QA_STAFF_SENHA"] = "Qa#Staff123456",
        ["USUARIO_QA_PARTICIPANTE_CODIGO"] = "QAINS-QAPART-0001",
        ["USUARIO_QA_MODULOS"] = """{"staff": {"mods": {"MOD-03": "CU"}}, "time": {"mods": {"MOD-14": "U"}}}""",
    };

    Partida Partida(Dictionary<string, string?> kv) => new(
        new ConfigurationBuilder().AddInMemoryCollection(kv).Build(),
        backend.Services.GetRequiredService<IInicializarContas>(),
        backend.Services.GetRequiredService<RegistroDeModulos>(),
        backend.Services.GetRequiredService<Indices>(),
        Options.Create(new OpcoesDoTime { Nome = Backend.NomeDoTime }),
        NullLogger<Partida>.Instance);

    IMongoCollection<BsonDocument> Usuarios => backend.Banco.GetCollection<BsonDocument>("usuarios");

    async Task<BsonDocument> Usuario(string id) => await Usuarios.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();

    [Fact]
    public async Task RN_265_RN_267_homologacao_garante_as_contas_qa_sem_duplicar()
    {
        _ = backend.Server;
        Assert.True(await Partida(KeyVault("hml")).Executar());
        await Usuarios.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", "usr-qains-qastaff"),
            Builders<BsonDocument>.Update.Set("estado", "DESATIVADO"));
        var kv = KeyVault("hml");
        kv["USUARIO_QA_MASTER_SENHA"] = "Qa#Nova987654";

        Assert.True(await Partida(kv).Executar());

        Assert.Equal(1, await Usuarios.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("login", "qastaff.qains@deboaz.com")));
        Assert.Equal("ATIVO", (await Usuario("usr-qains-qastaff"))["estado"].AsString);
        Assert.True(ResumoDeSenha.Confere("Qa#Nova987654", (await Usuario("usr-qains-master"))["resumoSenha"].AsString));
        Assert.Equal("PARTICIPANTE", (await Usuario("usr-qains-qapart"))["tipo"].AsString);
        var contas = backend.Banco.GetCollection<BsonDocument>("contas");
        var qaaut = await contas.Find(Builders<BsonDocument>.Filter.Eq("_id", "conta-qaaut")).FirstAsync();
        Assert.Equal(("QAAUT", "AUTONOMO", "qa_autonomo"), (qaaut["sigla"].AsString, qaaut["tipo"].AsString, qaaut["dominio"].AsString));
        Assert.Equal("CU", (await Usuario("usr-qains-qastaff"))["permissoes"]["mods"]["MOD-03"].AsString);
        Assert.Equal("U", (await Usuario("usr-dboaz-qamembro"))["permissoes"]["mods"]["MOD-14"].AsString);
        Assert.Equal(0, await backend.Banco.GetCollection<BsonDocument>("contratacoes").CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("moduloId", "MOD-14")));
        Assert.Equal(1, await backend.Banco.GetCollection<BsonDocument>("contratacoes").CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("contaId", "conta-qains") & Builders<BsonDocument>.Filter.Eq("moduloId", "MOD-03")));
        Assert.Equal(0, await backend.Banco.GetCollection<BsonDocument>("auditoria")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.In("alvo", new[] { "usr-qains-qastaff", "conta-qains" })));
    }

    [Theory]
    [InlineData("prd", "USUARIO_ADMIN_SENHA", null)]
    [InlineData("hml", "USUARIO_QA_PARTICIPANTE_CODIGO", null)]
    [InlineData("hml", "USUARIO_QA_STAFF_SENHA", "Gu#B5")]
    [InlineData("hml", "USUARIO_QA_ADMIN_SENHA", null)]
    [InlineData("hml", "USUARIO_QA_MODULOS", null)]
    public async Task RNF_226_segredo_ausente_ou_fraco_nao_inicia(string ambiente, string variavel, string? valor)
    {
        _ = backend.Server;
        var kv = KeyVault(ambiente);
        kv[variavel] = valor;

        Assert.False(await Partida(kv).Executar());
    }

    [Fact]
    public async Task Indices_do_diagrama_criados_na_partida()
    {
        _ = backend.Server;
        Assert.True(await Partida(KeyVault("prd")).Executar());

        var nomes = await (await Usuarios.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(nomes, i => i["name"] == "login_unico" && i["unique"].AsBoolean);
        Assert.Contains(nomes, i => i["name"] == "contaId_matricula_unica");
        // de novo: índice igual não muda nada
        Assert.Equal(0, await backend.Services.GetRequiredService<Indices>().Criar());
    }
}
