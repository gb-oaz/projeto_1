using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Sac;

// Passos dos Cenários de API que entraram em 1_Requisitos/18_modulo-sac.md v06
// pelo contrato sac-contrato-api v03: baixar o anexo (RN-292), o 405 sem login
// (RN-293), o card e o card ao vivo (RN-295: 401 só SESSAO_TERMINADA, 422 do
// intervalo, ": ping"), o X-Trace-Id e os pedidos sem auditoria (RN-297), os
// prazos padrão sem documento (RF-181), o ": ping" da conversa (RNF-230), o
// prazoPerto na fila do time (RN-289-api) e os ganchos de teste fora do teste
// (RN-292-api).
//
// O "Quando <quem> envia <MÉTODO> /sac/…" é o de SacPassos.
[Binding]
public sealed class SacContratoPassos(Mundo mundo, ChamadosDoExemplo sac, Execucoes execucoes, AoVivo aoVivo, AnexosDoCenario anexos,
    Partida partida)
{
    static readonly TimeSpan Folga = TimeSpan.FromSeconds(5);
    const string TokenInvalido = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJxYSJ9.assinatura-invalida";

    // as 6 categorias na ordem do requisito e o padrão em horas úteis (RN-289, RF-181)
    static readonly (string Categoria, int Horas)[] PrazosPadrao =
    [
        ("ACESSO_E_SENHA", 4), ("COBRANCA", 8), ("MODULO_COM_PROBLEMA", 8),
        ("DUVIDA_DE_USO", 16), ("SUGESTAO_AO_DEBOAZ", 40), ("SENHA_CONTA_MASTER", 4),
    ];

    string? tokenAntigo;
    Fluxo? conversa;

    // ── RN-292 · baixar o anexo ────────────────────────────────────────────
    [Given(@"^o ""erro\.png"" do SAC-000042, enviado como ""anx-7f3a21""$")]
    public async Task AnexoDoChamado()
    {
        await anexos.Enviar();
        await anexos.NoChamado();
    }

    [Then(@"^a resposta é 200 com o arquivo, igual ao enviado$")]
    [Then(@"^a resposta é 200 com o arquivo$")]
    [Then(@"^a resposta é 200 com os mesmos bytes enviados, sem conversão$")]
    public void ArquivoIgual()
    {
        Respostas.Confere(mundo.Ultima, 200);
        Assert.True(mundo.Ultima.Bytes is { } bytes && bytes.AsSpan().SequenceEqual(anexos.Enviado),
            $"o arquivo baixado ({mundo.Ultima.Bytes?.Length ?? 0} bytes) devia ser igual ao enviado ({anexos.Enviado.Length} bytes)");
    }

    [Then(@"^o cabeçalho Content-Type ""([^""]+)""$")]
    public void TipoDoConteudo(string tipo)
    {
        var veio = mundo.Ultima.Cabecalho("Content-Type");
        Assert.True(veio is not null && MediaTypeHeaderValue.Parse(veio).MediaType == tipo,
            $"esperava Content-Type \"{tipo}\", veio \"{veio}\"");
    }

    // o tipo e o nome do download; as aspas do nome são as do contrato
    [Then(@"^o cabeçalho Content-Disposition '(.+)'$")]
    public void Disposicao(string esperado)
    {
        var veio = mundo.Ultima.Cabecalho("Content-Disposition");
        Assert.True(veio is not null, $"esperava Content-Disposition '{esperado}', veio sem o cabeçalho: {mundo.Ultima.Status}");
        var (e, v) = (ContentDispositionHeaderValue.Parse(esperado), ContentDispositionHeaderValue.Parse(veio));
        Assert.True(e.DispositionType == v.DispositionType && e.FileName?.Trim('"') == v.FileName?.Trim('"'),
            $"esperava Content-Disposition '{esperado}', veio '{veio}'");
    }

    // ── RN-293 · sem login ─────────────────────────────────────────────────
    [When(@"^alguém sem token envia (PATCH|PUT|DELETE) (/sac/chamados/SAC-\d{6}/mensagens/msg-\d{6})$")]
    public async Task SemToken(string metodo, string caminho) =>
        await mundo.Enviar(metodo, caminho, metodo == "DELETE" ? null : new JsonObject { ["texto"] = "Turma 7B" }.ToJsonString());

    // sem corpo e sem método permitido: o Allow, se vier, vem vazio (o
    // HttpClient não distingue o cabeçalho vazio do ausente)
    [Then(@"^a resposta é 405, sem corpo, com o cabeçalho Allow vazio$")]
    public void SemCorpoEAllowVazio()
    {
        Respostas.Confere(mundo.Ultima, 405);
        Assert.True(mundo.Ultima.Texto.Length == 0, $"o 405 devia vir sem corpo, veio \"{mundo.Ultima.Texto}\"");
        Assert.True(string.IsNullOrEmpty(mundo.Ultima.Cabecalho("Allow")),
            $"o Allow devia vir vazio, veio \"{mundo.Ultima.Cabecalho("Allow")}\"");
    }

    // o mesmo pedido com um token que não vale continua 405, nunca 401
    [Then(@"^o token não é conferido$")]
    public async Task TokenNaoConferido()
    {
        var (metodo, caminho, corpo) = mundo.UltimoPedido;
        var r = await mundo.Enviar(metodo, caminho, corpo, TokenInvalido);
        Assert.True(r.Status == 405 && r.Texto.Length == 0, $"com um token inválido, esperava 405 sem corpo, veio {r}");
    }

    // ── RN-295 e RNF-230 · ao vivo ─────────────────────────────────────────
    [Then(@"^traz Content-Type ""text/event-stream"" e Cache-Control ""no-store""$")]
    public void FluxoSemCache()
    {
        var fluxo = aoVivo.Ultimo;
        Assert.True(fluxo.TipoDoConteudo == "text/event-stream", $"esperava text/event-stream, veio \"{fluxo.TipoDoConteudo}\" ({fluxo})");
        Assert.True(fluxo.CacheControl?.Contains("no-store", StringComparison.OrdinalIgnoreCase) == true,
            $"esperava Cache-Control no-store, veio \"{fluxo.CacheControl}\"");
    }

    [Then(@"^o 1º evento ""card"" chega logo ao abrir, com o corpo de GET /sac/card$")]
    public async Task PrimeiroCard()
    {
        var fluxo = aoVivo.Ultimo;
        var card = await fluxo.EsperarTipo("card", Folga);
        Assert.True(card is not null, $"o primeiro \"card\" não chegou em {Folga.TotalSeconds} s: {fluxo}");
        Assert.True(fluxo.Eventos.FirstOrDefault(e => e.Tipo != "ping") == card, $"o primeiro evento devia ser o \"card\": {fluxo}");
        var lido = Exigir(await mundo.EnviarComo(await mundo.Master(), "GET", "/sac/card"), 200, "GET /sac/card").Corpo;
        Comparacao.Confere(lido, card.Corpo);
    }

    [Then(@"^sem versão nova, a cada (\d+) s chega o comentário "": ping""$")]
    public async Task PingDoCard(int segundos)
    {
        var fluxo = aoVivo.Ultimo;
        var desde = fluxo.Eventos.Count;
        var ping = await fluxo.EsperarTipo("ping", TimeSpan.FromSeconds(segundos) + Folga, desde);
        Assert.True(ping is not null, $"nenhum \": ping\" em {segundos} s: {fluxo}");
        Assert.True(fluxo.Eventos.Skip(desde).All(e => e.Tipo == "ping"), $"sem versão nova, só \": ping\" devia chegar: {fluxo}");
    }

    [When(@"^alguém envia GET (/sac/card|/sac/card/ao-vivo\?intervalo=\d+) (sem o cabeçalho Authorization|com o token vencido|com o token adulterado|da sessão encerrada em outro aparelho)$")]
    public async Task CardSemSessao(string caminho, string problema)
    {
        var token = await TokenCom(problema);
        if (caminho == "/sac/card")
        {
            await mundo.Enviar("GET", caminho, token: token);
            return;
        }
        var fluxo = await aoVivo.Abrir(problema, caminho, token);
        await fluxo.EsperarFim(Folga);
        mundo.Guardar(new Resposta(fluxo.Status, fluxo.Texto, [], fluxo.Cabecalhos));
    }

    [Then(@"^a resposta é 401 SESSAO_TERMINADA, seja qual for o problema do token$")]
    public void SessaoTerminada() => Respostas.Confere(mundo.Ultima, 401, "SESSAO_TERMINADA");

    [Then(@"^nenhum fluxo fica aberto$")]
    public async Task NenhumFluxo()
    {
        foreach (var fluxo in aoVivo.Fluxos)
        {
            Assert.True(await fluxo.EsperarFim(Folga), $"a conexão ficou aberta: {fluxo}");
            Assert.True(fluxo.Status != 200, $"a recusa devia vir antes de abrir o fluxo, veio 200: {fluxo}");
        }
    }

    // RNF-230: a conversa do SAC-000042 aberta pela conta master, sem mensagem nova
    [Given(@"^a conexão ao vivo do SAC-000042 aberta, sem mensagem nova$")]
    public async Task ConversaAberta()
    {
        var id = await sac.Chamado("SAC-000042", "AGUARDANDO_CLIENTE");
        conversa = await aoVivo.Abrir("conta master", $"/sac/chamados/{id}/ao-vivo", await mundo.TokenDe(await mundo.Master()));
        Assert.True(conversa.Status == 200, $"preparo: a conversa ao vivo devia abrir: {conversa}");
    }

    // o Então espera: o ": ping" chega até 30 s depois de aberta
    [When(@"^passam (\d+) s$")]
    public static void Passam(int _)
    {
    }

    [Then(@"^chega o comentário "": ping"" e a conexão continua aberta$")]
    public async Task PingDaConversa()
    {
        var fluxo = conversa ?? aoVivo.Ultimo;
        var resta = TimeSpan.FromSeconds(30) + Folga - (DateTimeOffset.UtcNow - fluxo.Aberto);
        var ping = await fluxo.EsperarTipo("ping", resta > TimeSpan.Zero ? resta : TimeSpan.Zero);
        Assert.True(ping is not null, $"nenhum \": ping\" em 30 s: {fluxo}");
        Assert.False(fluxo.Fechado, $"a conexão devia continuar aberta: {fluxo}");
    }

    // ── RN-297 · X-Trace-Id e auditoria ────────────────────────────────────
    [Then(@"^a resposta é (\d{3}) com o cabeçalho X-Trace-Id, como ""trc-[0-9a-f]{8}""$")]
    public void SucessoComTraceIdComoOExemplo(int status) => ComTraceIdComoOExemplo(status, null);

    [Then(@"^a resposta é (\d{3}) ([A-Z_]+) com o cabeçalho X-Trace-Id, como ""trc-[0-9a-f]{8}""$")]
    public void ComTraceIdComoOExemplo(int status, string? codigo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        Assert.Matches("^trc-[0-9a-f]{8}$", traceId);
    }

    [Then(@"^esse é o traceId de ""INICIADO"" em ""auditoria"" e de ""CONCLUIDO""$")]
    public async Task MesmoTraceIdDoConcluido() => await MesmoTraceId(null);

    // nivel null: o CONCLUIDO; ALERTA ou ERRO: o registro de "auditoriaErros"
    [Then(@"^esse é o traceId de ""INICIADO"" em ""auditoria"" e de ""auditoriaErros"" com (ALERTA|ERRO)$")]
    public async Task MesmoTraceId(string? nivel)
    {
        var traceId = Auditoria.TraceIdDoCabecalho(mundo.Ultima);
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.True(novos.Any(d => Auditoria.Texto(d, "etapa") == "INICIADO" && Auditoria.Texto(d, "traceId") == traceId),
            $"esperava o INICIADO com o traceId {traceId}, vieram {Auditoria.Resumo(novos)}");
        if (nivel is null)
        {
            Assert.True(novos.Any(d => Auditoria.Texto(d, "etapa") == "CONCLUIDO" && Auditoria.TraceId(d) == traceId),
                $"esperava o CONCLUIDO com o traceId {traceId}, vieram {Auditoria.Resumo(novos)}");
            return;
        }
        var erro = await execucoes.UmNovoErro();
        Assert.Equal((traceId, nivel), (Auditoria.TraceId(erro), Auditoria.Texto(erro, "nivel")));
    }

    [Then(@"^a resposta não traz X-Trace-Id e nada é gravado em ""auditoria""$")]
    [Then(@"^sem X-Trace-Id, nada é gravado em ""auditoria""$")]
    public async Task SemAuditoria()
    {
        SemTraceId();
        var novos = (await execucoes.Novos(Auditoria.Registros)).Where(d => !Auditoria.Interna(d)).ToList();
        Assert.True(novos.Count == 0, $"nada devia ser gravado em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
    }

    [Then(@"^sem X-Trace-Id, nada é gravado em ""auditoria"" nem em ""auditoriaErros""$")]
    public async Task SemAuditoriaNemErro()
    {
        await SemAuditoria();
        var erros = await execucoes.Novos(Auditoria.Erros);
        Assert.True(erros.Count == 0, $"nada devia ser gravado em \"auditoriaErros\", vieram {Auditoria.Resumo(erros)}");
    }

    // o token tirado antes de o pv subir (17 RNF-224), como o TokenAntigo de
    // commons/autorizacao: a conta master não muda as próprias permissões pelo
    // PUT, então a contratação da EBFGA muda (POST /_teste/contratacao, o
    // AVISAR_CONTRATACAO_MUDOU), que sobe o pv de cada usuário da conta e
    // avisa as sessões (AvisarPv). Só gravar o pv no usuário não basta: o
    // backend compara o pv do token com o da sessão
    [Given(@"^o token da conta master com as permissões já mudadas$")]
    public async Task PermissoesMudadas()
    {
        var master = await mundo.Master();
        tokenAntigo = await mundo.TokenDe(master);
        // descontrata e recontrata: o gancho só avisa quando a contratação muda de
        // fato, e uma das duas muda, com ou sem a Presença contratada antes
        await mundo.ContratacaoMudou("MOD-06", contratada: false);
        await mundo.ContratacaoMudou("MOD-06", contratada: true);
    }

    [When(@"^envia POST /sac/chamados com o corpo ""(.*)"", que não é JSON$")]
    public async Task CorpoQueNaoEJson(string corpo) =>
        await execucoes.Executar(() => mundo.Enviar("POST", "/sac/chamados", corpo, tokenAntigo));

    [Then(@"^a resposta é 401 PERMISSOES_DESATUALIZADAS, não 422$")]
    public void PermissoesDesatualizadas() => Respostas.Confere(mundo.Ultima, 401, "PERMISSOES_DESATUALIZADAS");

    // ── RF-181 · prazos nunca salvos ───────────────────────────────────────
    // o banco começa vazio em cada cenário (Estado.Limpar): não há "configuracaoSac"
    [Given(@"^nenhum prazo salvo na aba Configuração$")]
    public static void NenhumPrazoSalvo()
    {
    }

    [Then(@"^a resposta é 200 com as 6 categorias, nesta ordem, nos prazos padrão:$")]
    public async Task PrazosPadraoNaOrdem(string json)
    {
        Respostas.Confere(mundo.Ultima, 200);
        Comparacao.Confere(JsonNode.Parse(await mundo.Traduzir(json)), mundo.Ultima.Corpo);
        var veio = (mundo.Ultima.Corpo?["prazos"]?.AsArray() ?? [])
            .Select(p => (p?["categoria"]?.GetValue<string>() ?? "", p?["horasUteis"]?.GetValue<int>() ?? 0)).ToArray();
        Assert.True(veio.SequenceEqual(PrazosPadrao),
            $"esperava as 6 categorias nesta ordem: {string.Join(", ", PrazosPadrao)}; vieram {string.Join(", ", veio)}");
    }

    // ── RN-289-api · prazo perto na fila do time (RF-174-api) ──────────────
    // o chamado é aberto (ACESSO_E_SENHA, 4 horas úteis) na hora que dá o
    // prazoResposta do exemplo; 20 s antes, para o prazo não passar da hora
    // pedida com o tempo do login e do envio
    [Given(@"^o (SAC-\d{6}) sem 1ª resposta, com prazoResposta ""([^""]+)""$")]
    public async Task SemPrimeiraResposta(string exemplo, string prazo)
    {
        var alvo = DateTimeOffset.Parse(prazo, CultureInfo.InvariantCulture);
        await AbrirNaFila(exemplo, alvo);
    }

    // a 1ª resposta do Caio entra antes do prazo; o relógio vai para 30 min antes dele
    [Given(@"^o SAC-000042 com a 1ª resposta do Caio e o prazoResposta daqui a 30 min$")]
    public async Task JaRespondido()
    {
        var alvo = DateTimeOffset.Parse("2026-11-12T17:00:00Z", CultureInfo.InvariantCulture);
        var id = await AbrirNaFila("SAC-000042", alvo);
        var caio = await sac.Caio();
        await sac.Levar(id, "EM_ATENDIMENTO", caio);
        await sac.Mensagem(id, caio, ChamadosDoExemplo.Pergunta);
        await sac.Saltar(alvo.AddMinutes(-30));
    }

    async Task<string> AbrirNaFila(string exemplo, DateTimeOffset prazo)
    {
        sac.FilaDoCenario = true;
        await sac.Saltar(AberturaPara(prazo, 4).AddSeconds(-20));
        var id = await sac.Abrir(await mundo.Master(), exemplo, categoria: "ACESSO_E_SENHA");
        var lido = await sac.Ler(id, await sac.Caio());
        var veio = DateTimeOffset.Parse(lido["prazoResposta"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"preparo: o chamado devia trazer prazoResposta: {lido.ToJsonString()}"),
            CultureInfo.InvariantCulture);
        Assert.True((prazo - veio).Duration() <= TimeSpan.FromMinutes(1),
            $"preparo: ACESSO_E_SENHA aberto em {AberturaPara(prazo, 4):u} devia ter o prazoResposta {prazo:u} (4 horas úteis, RNF-229); veio {veio:u}");
        return id;
    }

    // volta as horas úteis (seg a sex, 08h às 18h de Brasília, RNF-229) a partir do prazo
    static DateTimeOffset AberturaPara(DateTimeOffset prazo, int horasUteis)
    {
        var brt = TimeSpan.FromHours(-3);
        var t = prazo.ToOffset(brt);
        var falta = TimeSpan.FromHours(horasUteis);
        while (falta > TimeSpan.Zero)
        {
            var inicio = new DateTimeOffset(t.Date.AddHours(8), brt);
            var fim = new DateTimeOffset(t.Date.AddHours(18), brt);
            var util = t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            if (!util || t <= inicio)
            {
                t = new DateTimeOffset(t.Date.AddDays(-1).AddHours(18), brt);
                continue;
            }
            if (t > fim) t = fim;
            var trecho = t - inicio < falta ? t - inicio : falta;
            t -= trecho;
            falta -= trecho;
        }
        return t.ToUniversalTime();
    }

    [Then(@"^o item do (SAC-\d{6}) traz prazoPerto (true|false) e atrasado (true|false)$")]
    public void PrazoPerto(string exemplo, bool perto, bool atrasado) => ItemDaFila(exemplo, perto, atrasado);

    [Then(@"^o item do (SAC-\d{6}) traz prazoPerto true, com 1 hora útil até o prazo$")]
    public void PertoAtravessandoANoite(string exemplo) => ItemDaFila(exemplo, true, false);

    void ItemDaFila(string exemplo, bool perto, bool atrasado)
    {
        Respostas.Confere(mundo.Ultima, 200);
        var id = sac.Real(exemplo);
        var item = (mundo.Ultima.Corpo?["itens"]?.AsArray() ?? []).OfType<JsonObject>().FirstOrDefault(i => i["id"]?.GetValue<string>() == id);
        Assert.True(item is not null, $"a fila do time devia trazer o {id}: {mundo.Ultima}");
        Assert.True(item.ContainsKey("prazoPerto"), $"o item da fila do time devia trazer prazoPerto (RN-289-api): {item.ToJsonString()}");
        Assert.Equal((perto, atrasado), (item["prazoPerto"]?.GetValue<bool>(), item["atrasado"]?.GetValue<bool>()));
    }

    // ── RN-292-api · ganchos de teste do SAC ───────────────────────────────
    // "a aplicação iniciada em …" é o de hub/usuarios/ContratoPassos (instância de Partida)
    [When(@"^envia (POST|PUT|DELETE) (/_teste/sac/\w+)$")]
    public async Task GanchoNaPartida(string metodo, string caminho) =>
        mundo.Guardar(await partida.Api.Enviar(metodo, caminho,
            metodo == "PUT" ? new JsonObject { ["disponivel"] = false }.ToJsonString() : null));

    // o backend de API_URL já é o de homologação com Teste__Relogio=sim (ci.sh)
    [Given(@"^a aplicação de homologação, com Teste__Relogio ""sim""$")]
    public static void HomologacaoComRelogio()
    {
    }

    // se o backend aceitar sem o campo, a verificação volta ao normal antes do Então
    [When(@"^envia PUT /_teste/sac/verificacao sem ""disponivel""$")]
    public async Task VerificacaoSemOCampo()
    {
        var r = await mundo.Enviar("PUT", "/_teste/sac/verificacao", "{}");
        if (r.Status is >= 200 and < 300) await mundo.Enviar("DELETE", "/_teste/sac/verificacao");
        mundo.Guardar(r);
    }

    // ── apoio ──────────────────────────────────────────────────────────────
    void SemTraceId() =>
        Assert.True(string.IsNullOrEmpty(mundo.Ultima.Cabecalho("X-Trace-Id")),
            $"a resposta não devia trazer X-Trace-Id, veio \"{mundo.Ultima.Cabecalho("X-Trace-Id")}\": {mundo.Ultima.Status}");

    // o token de cada problema do Esquema do Cenário (RN-295)
    async Task<string?> TokenCom(string problema)
    {
        if (problema == "sem o cabeçalho Authorization") return null;
        var master = await mundo.Master();
        if (problema == "da sessão encerrada em outro aparelho")
        {
            var sessao = await mundo.SessaoDe(master, Navegador);
            await Estado.ExpirarSessao(sessao.Sid);
            return sessao.Acesso;
        }
        var token = await mundo.TokenDe(master);
        var carga = Jwt.Carga(token);
        if (problema == "com o token adulterado")
        {
            // a carga muda e a assinatura fica a do token original
            carga["sub"] = "usr-adulterado";
            var partes = token.Split('.');
            return $"{partes[0]}.{B64(Encoding.UTF8.GetBytes(carga.ToJsonString()))}.{partes[2]}";
        }
        if (Ambiente.ChavePrivada.Length == 0)
        {
            throw new PendingStepException("CHAVE_PRIVADA ausente: sem ela não há como assinar o token vencido do cenário");
        }
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        carga["iat"] = agora - 960;
        carga["exp"] = agora - 60;
        return Jwt.Assinar(Jwt.Cabecalho(token), carga);
    }

    static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
