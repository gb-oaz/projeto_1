using System.Net;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Endereços da Autorização (autorizacao-contrato-api v05): permissões guardadas
// no documento do usuário, registro de mudanças e o pv nas sessões.
public class AutorizacaoApiTests(Backend backend) : IClassFixture<Backend>
{
    readonly Preparo preparo = new(backend);

    static string Sigla() => Preparo.SiglaSorteada();

    [Fact]
    public async Task RF_160_RF_161_le_salva_e_o_token_antigo_pede_renovacao()
    {
        var (contaId, master) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        await backend.Contratar(contaId, "MOD-06");
        using var api = backend.Cliente();

        var lidas = await api.Enviar("GET", $"/usuarios/{maria.Id}/permissoes", token: master.Token);
        var salvas = await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-03"] = "U", ["MOD-06"] = "CU" } }, master.Token, ifMatch: "\"1\"");
        var antigo = await api.Enviar("GET", "/usuarios?tipo=PARTICIPANTE", token: maria.Token);
        var renovada = await api.Enviar("POST", "/auth/renovar", new { tokenRenovacao = maria.Renovacao });
        var comONovo = await api.Enviar("GET", "/usuarios?tipo=PARTICIPANTE", token: renovada.Texto("tokenAcesso"));

        Assert.Equal((1, 0), (lidas.Corpo!["versao"]!.GetValue<int>(), lidas.Corpo["mods"]!.AsObject().Count));
        Assert.Contains(lidas.Corpo["concediveis"]!.AsArray(), c => c!["mod"]!.GetValue<string>() == "MOD-06");
        Assert.Equal(2, salvas.Corpo!["versao"]!.GetValue<int>());
        Assert.Equal((HttpStatusCode.Unauthorized, "PERMISSOES_DESATUALIZADAS"), (antigo.Status, antigo.Codigo));
        Assert.Equal(HttpStatusCode.OK, comONovo.Status);
    }

    [Fact]
    public async Task RNF_222_if_match_ausente_ou_antigo()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        using var api = backend.Cliente();
        var corpo = new { mods = new Dictionary<string, string> { ["MOD-08"] = "C" } };

        var semVersao = await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes", corpo, master.Token);
        var primeira = await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes", corpo with { mods = new() { ["MOD-08"] = "U" } }, master.Token, ifMatch: "\"1\"");
        var antiga = await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes", corpo, master.Token, ifMatch: "\"1\"");

        Assert.Equal((HttpStatusCode.PreconditionRequired, "VERSAO_AUSENTE"), (semVersao.Status, semVersao.Codigo));
        Assert.Equal(HttpStatusCode.OK, primeira.Status);
        Assert.Equal((HttpStatusCode.PreconditionFailed, 2), (antiga.Status, antiga.Corpo!["versao"]!.GetValue<int>()));

        // RN-178, RN-270, RN-271: EDITAR_PERMISSOES com X-Trace-Id; a recusa grava ALERTA
        var auditoria = backend.Banco.GetCollection<BsonDocument>("auditoria");
        var erros = backend.Banco.GetCollection<BsonDocument>("auditoriaErros");
        var etapas = await auditoria.Find(Builders<BsonDocument>.Filter.Eq("traceId", primeira.TraceId)).ToListAsync();
        Assert.Equal(new[] { "CONCLUIDO", "INICIADO" }, etapas.Select(d => d["etapa"].AsString).Order().ToArray());
        Assert.All(etapas, d => Assert.Equal(("EDITAR_PERMISSOES", maria.Id), (d["acao"].AsString, d["alvo"].AsString)));
        var erro = Assert.Single(await erros.Find(Builders<BsonDocument>.Filter.Eq("traceId", antiga.TraceId)).ToListAsync());
        Assert.Equal(("VERSAO_DESATUALIZADA", "ALERTA"), (erro["codigo"].AsString, erro["nivel"].AsString));
        Assert.NotNull(semVersao.TraceId);
    }

    [Fact]
    public async Task RN_166_RN_256_nao_contratado_e_historico()
    {
        var (_, master) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        using var api = backend.Cliente();

        var naoContratado = await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-12"] = "U" } }, master.Token, ifMatch: "\"1\"");
        var inexistente = await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-99"] = "U" } }, master.Token, ifMatch: "\"1\"");
        await api.Enviar("PUT", $"/usuarios/{maria.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-08"] = "C" } }, master.Token, ifMatch: "\"1\"");
        var historico = await api.Enviar("GET", $"/usuarios/{maria.Id}/permissoes/historico", token: master.Token);
        var antes = await api.Enviar("GET", $"/usuarios/{maria.Id}/permissoes/historico?ate=2000-01-01T00:00:00Z", token: master.Token);
        var dataRuim = await api.Enviar("GET", $"/usuarios/{maria.Id}/permissoes/historico?ate=ontem", token: master.Token);

        Assert.Equal(("MODULO_NAO_CONTRATADO", "MOD-12"), (naoContratado.Codigo, naoContratado.Texto("mod")));
        Assert.Equal(("DADOS_INVALIDOS", "mods.MOD-99"), (inexistente.Codigo, inexistente.Texto("campo")));
        var mudanca = Assert.Single(historico.Corpo!.AsArray())!;
        Assert.Equal((master.Id, 2, "C"), (mudanca["por"]!.GetValue<string>(), mudanca["versao"]!.GetValue<int>(),
            mudanca["depois"]!["MOD-08"]!.GetValue<string>()));
        Assert.Null(mudanca["antes"]!["MOD-08"]);
        Assert.Empty(antes.Corpo!.AsArray());
        Assert.Equal(("DADOS_INVALIDOS", "ate"), (dataRuim.Codigo, dataRuim.Texto("campo")));
    }

    [Fact]
    public async Task RN_252_staff_repassa_o_modulo_usuarios_so_com_as_abas_que_tem()
    {
        var (contaId, master) = await preparo.Conta(Sigla());
        var joao = await preparo.Usuario(master, "joao");
        using var api = backend.Cliente();
        await api.Enviar("PUT", $"/usuarios/{joao.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-03"] = "U", ["MOD-08"] = "U" } }, master.Token, ifMatch: "\"1\"");
        var deNovo = await preparo.Entrar(joao.Id, joao.Entrada, joao.Senha);
        var ana = await preparo.Usuario(deNovo, "ana");

        var acima = await api.Enviar("PUT", $"/usuarios/{ana.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-03"] = "CU" } }, deNovo.Token, ifMatch: "\"1\"");
        var repassado = await api.Enviar("PUT", $"/usuarios/{ana.Id}/permissoes",
            new { mods = new Dictionary<string, string> { ["MOD-08"] = "U", ["MOD-03"] = "U" } }, deNovo.Token, ifMatch: "\"1\"");

        Assert.Equal((HttpStatusCode.Forbidden, "PERMISSAO_ACIMA", "MOD-03"), (acima.Status, acima.Codigo, acima.Texto("mod")));
        Assert.Equal("Você não tem mais o módulo Usuários para conceder.", acima.Texto("mensagem"));
        Assert.Equal((HttpStatusCode.OK, "U"), (repassado.Status, repassado.Corpo!["mods"]!["MOD-03"]!.GetValue<string>()));
    }

    [Fact]
    public async Task RN_237_contratacao_que_muda_sobe_o_pv_da_conta()
    {
        var (contaId, master) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        using var api = backend.Cliente();

        await backend.Services.GetRequiredService<IAvisarContratacaoMudou>()
            .Executar(contaId, new ExecucaoDeQuemChama(TraceId.Novo(), "usr-dboaz-admin", contaId));

        Assert.Equal("PERMISSOES_DESATUALIZADAS", (await api.Enviar("GET", "/auth/sessoes", token: maria.Token)).Codigo);
        var usuario = await backend.Banco.GetCollection<BsonDocument>("usuarios").Find(Builders<BsonDocument>.Filter.Eq("_id", maria.Id)).FirstAsync();
        Assert.Equal(2, usuario["permissoes"]["pv"].AsInt32);
    }

    [Fact]
    public async Task Registro_de_modulos_completa_quem_usa_o_que_veio_sem()
    {
        // o qa grava módulos só com nome e tipo; quem usa vem do registro do backend
        await backend.Banco.GetCollection<BsonDocument>("modulos").ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", "MOD-08"),
            new BsonDocument { ["_id"] = "MOD-08", ["nome"] = "Turmas", ["tipo"] = "DA_CONTA" }, new ReplaceOptions { IsUpsert = true });
        await backend.Banco.GetCollection<BsonDocument>("modulos").ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", "MOD-50"),
            new BsonDocument
            {
                ["_id"] = "MOD-50",
                ["nome"] = "Novo",
                ["tipo"] = "DA_CONTA",
                ["quemUsa"] = new BsonArray { new BsonDocument { ["perfil"] = "STAFF", ["frente"] = "HUB" } },
            }, new ReplaceOptions { IsUpsert = true });
        var (_, master) = await preparo.Conta(Sigla());
        var maria = await preparo.Usuario(master, "maria");
        using var api = backend.Cliente();

        var lidas = await api.Enviar("GET", $"/usuarios/{maria.Id}/permissoes", token: master.Token);

        var concediveis = lidas.Corpo!["concediveis"]!.AsArray().Select(c => c!["mod"]!.GetValue<string>()).ToList();
        Assert.Contains("MOD-08", concediveis);
        Assert.Contains("MOD-50", concediveis);
    }
}
