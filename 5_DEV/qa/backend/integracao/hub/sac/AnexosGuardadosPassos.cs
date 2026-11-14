using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Sac;

// Passos de RNF-233, RNF-233-api, RNF-234 e RNF-234-api de 1_Requisitos/18_modulo-sac.md v06 (contrato
// sac-contrato-api v03, modelo sac-documentos v03): o mesmo arquivo no
// armazenamento de objetos (BLOB) e no GridFS, a verificação fora do ar com o
// BLOB ligado, e o descarte pelo expiraEm de cada ambiente.
//
// RNF-233 roda numa instância própria (AnexosDoCenario.Partir), com
// Sac__Arquivos__Destino BLOB ("ligado") ou GRIDFS ("desligado"). RNF-234 roda
// no backend de API_URL, em homologação, com o relógio de teste; produção não
// tem relógio de teste (os ganchos /_teste só existem com AMBIENTE=hml), e as
// linhas de produção ficam pendentes: o expiraEm de produção (nulo até o
// FECHADO, depois fechadoEm + 5 anos) é conferido nos testes do backend
// (ArquivosDosAnexosTests).
[Binding]
public sealed partial class AnexosGuardadosPassos(Mundo mundo, ChamadosDoExemplo sac, AnexosDoCenario anexos)
{
    const string SemRelogioEmProducao =
        "produção não tem relógio de teste (/_teste só com AMBIENTE=hml): o expiraEm de produção, nulo até o FECHADO e " +
        "fechadoEm + 5 anos depois, é conferido nos testes do backend (ArquivosDosAnexosTests)";

    bool ligado;
    long guardadosAntes;

    // ── RNF-233 · o mesmo arquivo nos dois armazenamentos ──────────────────
    [Given(@"^o armazenamento de objetos (ligado|desligado) no ambiente$")]
    public async Task Armazenamento(string estado)
    {
        ligado = estado == "ligado";
        await anexos.Partir(ligado ? "BLOB" : "GRIDFS");
    }

    // o arquivo vai para o destino do ambiente, e só para ele
    [Given(@"^o ""erro\.png"" de 2 MB enviado por POST /sac/anexos como ""anx-7f3a21""$")]
    public async Task EnviadoNaPartida()
    {
        Exigir(await anexos.EnviarNaPartida(Arquivos.Png(2 * Arquivos.MB)), 201, "POST /sac/anexos na instância de Partida");
        var (noBlob, noGridFs) = (await anexos.Guardados.NoBlob(anexos.Id), await anexos.Guardados.NoGridFs(anexos.Id));
        Assert.True(noBlob == ligado && noGridFs != ligado,
            $"com o armazenamento de objetos {(ligado ? "ligado" : "desligado")}, o arquivo devia ir só para o " +
            $"{(ligado ? "Blob" : "GridFS")}: no Blob {noBlob}, no GridFS {noGridFs}");
    }

    [Given(@"^o armazenamento de objetos ligado e a verificação de segurança fora do ar$")]
    public async Task LigadoSemVerificacao()
    {
        ligado = true;
        await anexos.Partir("BLOB");
        await anexos.VerificacaoForaDoArNaPartida();
        guardadosAntes = await anexos.Guardados.Total();
    }

    [Then(@"^a resposta é 503 VERIFICACAO_INDISPONIVEL e nada é guardado$")]
    public async Task NadaGuardado()
    {
        Respostas.Confere(mundo.Ultima, 503, "VERIFICACAO_INDISPONIVEL");
        var depois = await anexos.Guardados.Total();
        Assert.True(depois == guardadosAntes, $"com a verificação fora do ar, nenhum arquivo devia ser guardado: eram {guardadosAntes}, ficaram {depois}");
    }

    // ── RNF-234 · quando o arquivo do anexo é removido ─────────────────────
    [Given(@"^o ""erro\.png"" enviado como ""anx-7f3a21"" em (\d{4}-\d{2}-\d{2} \d{2}:\d{2}), em (homologação|produção)$")]
    public async Task EnviadoEm(string quando, string ambiente)
    {
        if (ambiente == "produção") throw new PendingStepException(SemRelogioEmProducao);
        await sac.Saltar(AnexosDoCenario.Hora(quando));
        await anexos.Enviar();
    }

    [Given(@"^sem chamado nem mensagem em 24 h$")]
    [Given(@"^sem chamado nem mensagem que o use$")]
    public static void SemUso()
    {
    }

    [Given(@"^no SAC-000042$")]
    public async Task NoChamado() => await anexos.NoChamado();

    [Given(@"^no SAC-000042, fechado em (\d{4}-\d{2}-\d{2})$")]
    public static void NoChamadoFechado(string _) => throw new PendingStepException(SemRelogioEmProducao);

    [Given(@"^o ""erro\.png"" do SAC-000042 enviado em (\d{4}-\d{2}-\d{2} \d{2}:\d{2}), em (homologação|produção)$")]
    public async Task DoChamadoEnviadoEm(string quando, string ambiente)
    {
        await EnviadoEm(quando, ambiente);
        await anexos.NoChamado();
    }

    // o GET responde 404 pelo expiraEm, e o agendamento do minuto já tirou o
    // arquivo de onde ele foi guardado (Blob ou GridFS)
    [Then(@"^a resposta é 404 ANEXO_INEXISTENTE e o arquivo já foi removido$")]
    public async Task Removido()
    {
        Respostas.Confere(mundo.Ultima, 404, "ANEXO_INEXISTENTE");
        await ArquivoFora("depois do expiraEm e do agendamento");
    }

    // ── RNF-234 · conversa sem o arquivo em homologação ────────────────────
    [Given(@"^o arquivo do ""anx-7f3a21"" removido 30 dias depois do envio, em homologação$")]
    public async Task RemovidoEm30Dias()
    {
        var envio = AnexosDoCenario.Hora("2026-11-12 10:00");
        await sac.Saltar(envio);
        await anexos.Enviar();
        var id = await anexos.NoChamado();
        // a pergunta do time vira mensagem na conversa
        await sac.Levar(id, "AGUARDANDO_CLIENTE");
        anexos.ChamadoAntes = await sac.Ler(id, await mundo.Master());
        await sac.Saltar(envio.AddDays(30).AddMinutes(1));
        await sac.Agendamento();
        await ArquivoFora("preparo: 30 dias depois do envio, em homologação");
    }

    [Then(@"^a resposta é 200 com o chamado e as mensagens, sem nada removido$")]
    public void SemNadaRemovido()
    {
        Respostas.Confere(mundo.Ultima, 200);
        var antes = anexos.ChamadoAntes ?? throw new InvalidOperationException("o chamado não foi lido antes de o arquivo sair");
        var depois = mundo.Ultima.Corpo;
        // nada sai: as mensagens e os anexos de antes continuam (o prazo de 7
        // dias pode ter acrescentado eventos do sistema, RN-290)
        var mensagens = Ids(depois, MensagemId());
        Assert.All(Ids(antes, MensagemId()), m => Assert.Contains(m, mensagens));
        var anexosDepois = Ids(depois, AnexoId());
        Assert.Contains(anexos.Id, anexosDepois);
        Assert.All(Ids(antes, AnexoId()), a => Assert.Contains(a, anexosDepois));
    }

    // ── RNF-233-api · lido de onde foi guardado, sem migração ──────────────
    [Given(@"^o ""erro\.png"" enviado como ""anx-7f3a21"" com o armazenamento de objetos desligado$")]
    public async Task EnviadoComODesligado()
    {
        await Armazenamento("desligado");
        Exigir(await anexos.EnviarNaPartida(Arquivos.Png(4096)), 201, "POST /sac/anexos na instância de Partida");
    }

    [Given(@"^a aplicação reinicia com o armazenamento de objetos ligado$")]
    public async Task ReiniciaLigado() => await anexos.Reiniciar("BLOB");

    [Then(@"^o arquivo continua só no GridFS, sem migração$")]
    public async Task SoNoGridFs()
    {
        var (noBlob, noGridFs) = (await anexos.Guardados.NoBlob(anexos.Id), await anexos.Guardados.NoGridFs(anexos.Id));
        Assert.True(noGridFs && !noBlob, $"o anexo antigo devia continuar só no GridFS: no Blob {noBlob}, no GridFS {noGridFs}");
    }

    // ── RNF-234-api · vencido responde 404 pelo expiraEm ───────────────────
    // só o relógio anda: o GET vem logo depois, antes do agendamento do minuto
    [Given(@"^o relógio em (\d{4}-\d{2}-\d{2} \d{2}:\d{2}), antes de o agendamento rodar$")]
    public async Task RelogioSemAgendamento(string quando) => await sac.Saltar(AnexosDoCenario.Hora(quando));

    // ── apoio ──────────────────────────────────────────────────────────────
    async Task ArquivoFora(string quando)
    {
        var (noBlob, noGridFs) = (await anexos.Guardados.NoBlob(anexos.Id), await anexos.Guardados.NoGridFs(anexos.Id));
        Assert.True(!noBlob && !noGridFs, $"{quando}, o arquivo do {anexos.Id} devia ter saído: no Blob {noBlob}, no GridFS {noGridFs}");
    }

    // os ids (mensagens ou anexos) citados no chamado, na ordem e sem repetir
    static List<string> Ids(JsonNode? chamado, Regex padrao) =>
        [.. padrao.Matches(chamado?.ToJsonString() ?? "").Select(m => m.Value).Distinct()];

    [GeneratedRegex(@"msg-\d{6}")]
    private static partial Regex MensagemId();

    [GeneratedRegex(@"anx-[0-9a-f]{6}")]
    private static partial Regex AnexoId();
}
