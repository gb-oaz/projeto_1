using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Boaz.Qa.Integracao.Suporte;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Sac;

// O anexo "anx-7f3a21" do cenário e onde ele foi guardado (18 RN-292,
// RNF-233, RNF-234; sac-documentos v03). O Reqnroll cria um por cenário.
//
// Os cenários de RNF-233 mudam o destino dos arquivos (Sac__Arquivos__Destino
// BLOB ou GRIDFS) e por isso rodam numa instância própria (suporte/Partida.cs),
// em homologação, com a conta master inicial da QAINS; os de RNF-234 rodam no
// backend de API_URL, com o relógio de teste e o agendamento do SAC na hora.
// O "Quando a conta master envia …" de SacPassos passa por Atender: na
// Partida, o pedido vai a ela; "… em <data e hora>" move o relógio antes.
public sealed partial class AnexosDoCenario(Mundo mundo, ChamadosDoExemplo sac, Partida partida)
{
    public const string Exemplo = "anx-7f3a21";

    // Brasília, a hora dos exemplos do 18
    static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    string? tokenNaPartida;

    public string Id { get; private set; } = "";
    public byte[] Enviado { get; private set; } = [];
    public bool NaPartida { get; private set; }
    // o chamado lido antes de o arquivo sair (RNF-234, homologação)
    public JsonNode? ChamadoAntes { get; set; }

    public ArquivosGuardados Guardados => NaPartida ? ArquivosGuardados.DaPartida() : ArquivosGuardados.DoBackend();

    // "2026-11-12 10:00" do exemplo, em Brasília
    public static DateTimeOffset Hora(string exemplo) =>
        new(DateTime.ParseExact(exemplo, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Brasilia);

    // ── no backend de API_URL ──────────────────────────────────────────────
    // "erro.png" enviado pela conta master da EBFGA, como "anx-7f3a21"
    public async Task<string> Enviar(byte[]? conteudo = null)
    {
        conteudo ??= Arquivos.Png(4096);
        var id = await sac.Anexo(await mundo.Master(), Exemplo, "erro.png", conteudo);
        Lembrar(id, conteudo);
        return id;
    }

    // o anexo usado na abertura do SAC-000042
    public async Task<string> NoChamado() =>
        await sac.Abrir(await mundo.Master(), "SAC-000042", ChamadosDoExemplo.Assunto, "MODULO_COM_PROBLEMA", Id);

    // ── na instância de Partida (RNF-233) ──────────────────────────────────
    // homologação, com o Key Vault completo, o relógio de teste e o destino
    public async Task Partir(string destino)
    {
        partida.KeyVaultCompleto("hml");
        partida.KeyVault["Sac__Arquivos__Destino"] = destino;
        partida.KeyVault["Teste__Relogio"] = "sim";
        await partida.IniciarPronta($"homologação, Sac__Arquivos__Destino={destino}");
        NaPartida = true;
        tokenNaPartida = await partida.Token(ContasIniciais.Master);
    }

    // a mesma instância sobe de novo com outro destino: o banco, o Key Vault
    // e as contas ficam (o anexo antigo é lido de onde foi guardado)
    public async Task Reiniciar(string destino)
    {
        partida.KeyVault["Sac__Arquivos__Destino"] = destino;
        await partida.IniciarPronta($"homologação de novo, Sac__Arquivos__Destino={destino}");
        tokenNaPartida = await partida.Token(ContasIniciais.Master);
    }

    public async Task<Resposta> EnviarNaPartida(byte[] conteudo)
    {
        var r = await partida.Api.EnviarArquivo("/sac/anexos", "arquivo", "erro.png", conteudo, "image/png", tokenNaPartida);
        if (r.Status == 201) Lembrar(Texto(r, "id"), conteudo);
        return mundo.Guardar(r);
    }

    public async Task VerificacaoForaDoArNaPartida() =>
        Exigir(await partida.Api.Enviar("PUT", "/_teste/sac/verificacao", new JsonObject { ["disponivel"] = false }.ToJsonString()), 204,
            "PUT /_teste/sac/verificacao na instância de Partida (Teste__Relogio=sim)");

    // ── o pedido do Quando ─────────────────────────────────────────────────
    // true se atendeu o pedido aqui: na Partida, ou com o relógio movido antes
    public async Task<bool> Atender(Pessoa pessoa, string metodo, string caminho, string resto)
    {
        if (NaPartida)
        {
            switch (metodo, caminho)
            {
                case ("GET", _) when caminho.StartsWith("/sac/anexos/", StringComparison.Ordinal):
                    mundo.Guardar(await partida.Api.Enviar("GET", $"/sac/anexos/{Id}", token: tokenNaPartida));
                    return true;
                case ("POST", "/sac/anexos"):
                    await EnviarNaPartida(Arquivos.Png(4096));
                    return true;
                default:
                    throw new InvalidOperationException($"pedido sem tradução para a instância de Partida: {metodo} {caminho} {resto}");
            }
        }
        if (Em().Match(resto) is not { Success: true } em) return false;
        // RNF-234: o relógio vai à hora do exemplo e o agendamento roda, como o
        // de 1 min faria antes dela; o pedido sai em seguida, com uma sessão nova
        await sac.Saltar(Hora(em.Groups[1].Value));
        await sac.Agendamento();
        mundo.Guardar(await mundo.EnviarComo(pessoa, metodo, await mundo.Traduzir(caminho)));
        return true;
    }

    void Lembrar(string id, byte[] conteudo)
    {
        Id = id;
        Enviado = conteudo;
        mundo.Apelidos[Exemplo] = () => Task.FromResult(id);
    }

    [GeneratedRegex(@"^em (\d{4}-\d{2}-\d{2} \d{2}:\d{2})$")]
    private static partial Regex Em();
}
