using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using MongoDB.Bson;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Commons.Autenticacao;

// Passos da auditoria de 1_Requisitos/17_modulo-usuario-a-autenticacao.md:
// RN-272 (entradas e sessões com "INICIADO" e "CONCLUIDO" e o X-Trace-Id) e
// RN-273 (recusas em "auditoriaErros" e nenhum segredo nos registros).
//
// Cada pedido prepara antes o que cita (a Maria, o código do Lucas, a sessão,
// o comprovante) e só ele passa por Execucoes, entre as marcas das coleções
// (suporte/Auditoria.cs). As frases comuns às features ("auditoria" ganha
// INICIADO e CONCLUIDO, "auditoriaErros" ganha o código…) ficam em
// suporte/PassosDeAuditoria.cs; o POST /auth/hub/entrar da Maria e o POST
// /auth/renovar são os de AutenticacaoPassos.cs e AutorizacaoPassos.cs,
// também auditados.
[Binding]
public sealed class AuditoriaPassos(Mundo mundo, Execucoes execucoes)
{
    const string SenhaErrada = "Senha#Errada1";
    const string SemSala = "abrir uma sala não está no contrato da Autenticação: a sala 482107 aberta não dá para preparar";

    // o que nenhum registro pode trazer (RN-273), juntado pelo pedido
    readonly List<string> segredos = [];

    // ── pedidos que mudam a sessão (RN-272) ────────────────────────────────
    // o POST /auth/hub/entrar sem mais nada é o de AutorizacaoPassos.cs, também auditado
    [When(@"^envia (POST|DELETE) /auth/(quest/entrar|sala/entrar|hub/confirmar-senha|sair|sessoes/3f9c2a71|sessoes)$")]
    public async Task Envia(string metodo, string endereco)
    {
        switch (endereco)
        {
            case "quest/entrar":
                await EntraNoQuest(null);
                break;
            case "sala/entrar":
                throw new PendingStepException(SemSala);
            case "hub/confirmar-senha":
                await ConfirmaASenha();
                break;
            case "sair":
                {
                    var sessao = await mundo.SessaoDe(await mundo.Maria(), Navegador);
                    await execucoes.Executar(() => mundo.Enviar(metodo, "/auth/sair", token: sessao.Acesso));
                    break;
                }
            case "sessoes/3f9c2a71":
                {
                    // a sessão do navegador encerrada pelo app
                    var maria = await mundo.Maria();
                    var navegador = await mundo.SessaoDe(maria, Navegador);
                    var app = await mundo.SessaoDe(maria, App);
                    await execucoes.Executar(() => mundo.Enviar(metodo, $"/auth/sessoes/{navegador.Sid}", token: app.Acesso));
                    break;
                }
            default:
                {
                    var maria = await mundo.Maria();
                    var navegador = await mundo.SessaoDe(maria, Navegador);
                    await mundo.SessaoDe(maria, App);
                    await execucoes.Executar(() => mundo.Enviar(metodo, "/auth/sessoes", token: navegador.Acesso));
                    break;
                }
        }
    }

    // os pedidos do "nenhum segredo na auditoria" (RN-273)
    [When(@"^envia POST /auth/(hub/entrar|quest/entrar|renovar) da (Maria|Carla)$")]
    public async Task EnviaDe(string endereco, string _)
    {
        switch (endereco)
        {
            case "hub/entrar":
                var maria = await MariaEntra();
                segredos.Add(maria.Senha);
                segredos.Add(Texto(mundo.Ultima, "tokenAcesso"));
                if (CookieDeRenovacao(mundo.Ultima) is { Length: > 0 } cookie) segredos.Add(cookie);
                break;
            case "quest/entrar":
                await EntraNoQuest(Carla());
                segredos.Add(await mundo.CodigoDoLucas());
                break;
            default:
                var sessao = mundo.Atual = await mundo.SessaoDe(await mundo.Maria(), Navegador);
                segredos.Add(sessao.Renovacao);
                await execucoes.Executar(() => mundo.RenovarEGuardar(sessao));
                segredos.Add(sessao.Renovacao);
                segredos.Add(sessao.Acesso);
                break;
        }
    }

    [Then(@"^o ""CONCLUIDO"" vai com quem ""(.+)"", contaId ""(.+)"" e alvo ""(.+)""$")]
    public async Task ConcluidoCom(string quem, string conta, string alvo)
    {
        var concluido = await Concluido();
        Assert.Equal(await mundo.Traduzir(quem), Auditoria.Texto(concluido, "quem"));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(await mundo.Traduzir(alvo), Auditoria.Texto(concluido, "alvo"));
    }

    [Then(@"^a resposta traz o cabeçalho X-Trace-Id com esse traceId$")]
    public async Task CabecalhoComOTraceId()
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.NotEmpty(novos);
        Assert.Equal(Auditoria.MesmoTraceId(novos), Auditoria.TraceIdDoCabecalho(mundo.Ultima));
    }

    // quem ainda não foi identificado (RN-272): o INICIADO da entrada vai vazio
    [Then(@"^o registro ""INICIADO"" da ação ""(\w+)"" vai com quem, contaId e alvo null$")]
    public async Task IniciadoSemNinguem(string acao)
    {
        var iniciado = execucoes.Registro = await execucoes.Etapa("INICIADO", acao);
        Assert.True(Auditoria.Texto(iniciado, "quem") is null && Auditoria.Texto(iniciado, "contaId") is null
            && Auditoria.Texto(iniciado, "alvo") is null, $"esperava quem, contaId e alvo null no INICIADO, veio {iniciado.ToJson()}");
    }

    [Then(@"^só o ""CONCLUIDO"", depois da senha conferida, vai com a Maria, a conta e a sessão$")]
    public async Task ConcluidoComAMaria()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var concluido = await Concluido();
        if (execucoes.Registro is { } iniciado) Assert.Equal(Auditoria.TraceId(iniciado), Auditoria.TraceId(concluido));
        Assert.Equal((await mundo.Maria()).Id, Auditoria.Texto(concluido, "quem"));
        Assert.Equal(await mundo.Conta(), Auditoria.Texto(concluido, "contaId"));
        Assert.Equal(Jwt.Carga(Texto(mundo.Ultima, "tokenAcesso"))["sid"]!.GetValue<string>(), Auditoria.Texto(concluido, "alvo"));
    }

    // ── recusas (RN-273) ───────────────────────────────────────────────────
    [When(@"^chega (senha errada em /auth/hub/entrar|5ª senha errada em /auth/hub/entrar|comprovante repetido|sessão sem uso há 9 h renovada|entrada na sala fechada|Redis fora do ar em /auth/hub/entrar)$")]
    public async Task Chega(string pedido)
    {
        switch (pedido)
        {
            case "senha errada em /auth/hub/entrar":
                await SenhaErradaDaMaria();
                break;
            case "5ª senha errada em /auth/hub/entrar":
                for (var i = 0; i < 4; i++)
                {
                    Exigir(await mundo.Enviar("POST", "/auth/hub/entrar", Entrada((await mundo.Maria()).Entrada, SenhaErrada)), 401, $"{i + 1}ª senha errada");
                }
                await SenhaErradaDaMaria();
                break;
            case "comprovante repetido":
                {
                    var comprovante = new JsonObject { ["comprovante"] = await mundo.MariaPendente() }.ToJsonString();
                    Exigir(await mundo.Enviar("POST", "/auth/hub/confirmar-senha", comprovante), 200, "POST /auth/hub/confirmar-senha");
                    await execucoes.Executar(() => mundo.Enviar("POST", "/auth/hub/confirmar-senha", comprovante));
                    break;
                }
            case "sessão sem uso há 9 h renovada":
                {
                    var sessao = mundo.Atual = await mundo.SessaoDe(await mundo.Maria(), Navegador);
                    await Estado.ExpirarSessao(sessao.Sid);
                    await execucoes.Executar(() => mundo.Renovar(sessao));
                    break;
                }
            case "entrada na sala fechada":
                await execucoes.Executar(() => mundo.Enviar("POST", "/auth/sala/entrar", new JsonObject { ["sala"] = "000000" }.ToJsonString()));
                break;
            default:
                throw new PendingStepException(
                    "derrubar o Redis no meio de um pedido não dá para provocar de fora, numa suíte caixa-preta: " +
                    "o 500 ERRO_INESPERADO com nivel ERRO fica nos testes do backend, como o do banco caindo");
        }
    }

    // nos dois registros da recusa: o INICIADO e o de "auditoriaErros"
    [Then(@"^quem e contaId vão null, porque a pessoa não foi identificada$")]
    public void SemQuemNemConta()
    {
        Assert.NotNull(execucoes.Registro);
        Assert.NotNull(execucoes.Erro);
        foreach (var d in new[] { execucoes.Registro, execucoes.Erro })
        {
            Assert.True(Auditoria.Texto(d, "quem") is null && Auditoria.Texto(d, "contaId") is null,
                $"esperava quem e contaId null, veio {d.ToJson()}");
        }
    }

    // a recusa depois de conferir a senha (403 ACESSO_INDISPONIVEL) já sabe quem é
    [Then(@"^esse registro vai com quem ""(.+)"" e contaId ""(.+)""$")]
    public async Task ErroComQuem(string quem, string conta)
    {
        Assert.NotNull(execucoes.Erro);
        Assert.Equal(await mundo.Traduzir(quem), Auditoria.Texto(execucoes.Erro, "quem"));
        Assert.Equal(await mundo.Traduzir(conta), Auditoria.Texto(execucoes.Erro, "contaId"));
    }

    // ── nenhum segredo (RN-273) ────────────────────────────────────────────
    // só vale se a execução foi auditada: sem registro, não há o que conferir
    [Then(@"^nenhum registro de ""auditoria"" nem de ""auditoriaErros"" traz (.+)$")]
    public async Task SemSegredo(string _)
    {
        var registros = await execucoes.Novos(Auditoria.Registros);
        Assert.True(registros.Count > 0, $"a execução não gravou nada em \"auditoria\": {mundo.Ultima}");
        var todos = registros.Concat(await execucoes.Novos(Auditoria.Erros)).ToList();
        foreach (var segredo in segredos.Where(s => s.Length > 0).Distinct())
        {
            Assert.All(todos, d => Assert.False(d.ToJson().Contains(segredo, StringComparison.Ordinal),
                $"o registro traz um segredo: {d.ToJson()}"));
        }
    }

    // ── apoio ──────────────────────────────────────────────────────────────
    async Task<Pessoa> MariaEntra()
    {
        var maria = await mundo.Maria();
        var r = await execucoes.Executar(() => mundo.Enviar("POST", "/auth/hub/entrar", new JsonObject
        {
            ["entrada"] = maria.Entrada,
            ["senha"] = maria.Senha,
            ["aparelho"] = Navegador,
        }.ToJsonString()));
        if (mundo.Registrar(maria, Navegador, r) is { } sessao) mundo.Atual = sessao;
        return maria;
    }

    // o Lucas, ou a Carla com o código dele; a sessão nova é a "7a20d5e4" dos exemplos
    async Task EntraNoQuest(JsonObject? quem)
    {
        await mundo.CodigoDoLucas();
        var r = await execucoes.Executar(() => mundo.TentarQuest(App, quem));
        if (r.Status == 200)
        {
            var sid = Jwt.Carga(Texto(r, "tokenAcesso"))["sid"]!.GetValue<string>();
            mundo.Apelidos["7a20d5e4"] = () => Task.FromResult(sid);
        }
    }

    // a Maria com a senha gerada confirma; a sessão aberta é a "3f9c2a71" dos exemplos
    async Task ConfirmaASenha()
    {
        var comprovante = await mundo.MariaPendente();
        segredos.Add(comprovante);
        mundo.Apelidos["c7e1…"] = () => Task.FromResult(comprovante);
        var r = await execucoes.Executar(() => mundo.Enviar("POST", "/auth/hub/confirmar-senha",
            new JsonObject { ["comprovante"] = comprovante }.ToJsonString()));
        if (r.Status == 200)
        {
            var sid = Jwt.Carga(Texto(r, "tokenAcesso"))["sid"]!.GetValue<string>();
            mundo.Apelidos["3f9c2a71"] = () => Task.FromResult(sid);
        }
    }

    async Task SenhaErradaDaMaria()
    {
        var maria = await mundo.Maria();
        await execucoes.Executar(() => mundo.Enviar("POST", "/auth/hub/entrar", Entrada(maria.Entrada, SenhaErrada)));
    }

    async Task<BsonDocument> Concluido()
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        var concluido = novos.FirstOrDefault(d => Auditoria.Texto(d, "etapa") == "CONCLUIDO");
        Assert.True(concluido is not null, $"esperava o registro CONCLUIDO em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        return concluido;
    }

    static string Entrada(string entrada, string senha) =>
        new JsonObject { ["entrada"] = entrada, ["senha"] = senha, ["aparelho"] = Navegador }.ToJsonString();
}
