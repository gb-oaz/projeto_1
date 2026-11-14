using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Boaz.Qa.Integracao.Suporte;
using Reqnroll;
using static Boaz.Qa.Integracao.Suporte.Mundo;

namespace Boaz.Qa.Integracao.Hub.Sac;

// Os chamados dos exemplos de 1_Requisitos/18_modulo-sac.md, criados pela API
// (contrato sac-contrato-api v03) só quando um passo os cita. O número é
// gerado pelo backend (contadores.chamados): "SAC-000042" do exemplo vira o
// número real pelo Mundo.Apelidos, como "usr-ebfga-maria" no 17. O mesmo vale
// para os anexos ("anx-7f3a21") e as mensagens ("msg-000318").
//
// Donos: o SAC-000045 é do Pedro Reis (autônomo, conta PREIS); os outros, da
// conta master da EBFGA. O time é o Caio Lima (e a Ana Lima), com "MOD-02" "U".
//
// Ganchos de teste do backend, só com AMBIENTE=hml e Teste__Relogio=sim (o
// backend do qa no ci.sh), como o PUT /_teste/relogio (suporte/Relogio.cs):
//
//   POST /_teste/sac/agendamento                                     → 204
//       roda agora, com o relógio da aplicação, o que o AgendamentoDoSac faz
//       a cada 1 min (IAplicarPrazos: os 7 dias de Aguardando cliente e de
//       Resolvido; IDescartarAnexosVencidos: os anexos de expiraEm vencido,
//       24 h sem uso ou 30 dias em hml, RNF-234) e só responde ao terminar
//   PUT /_teste/sac/verificacao   {"disponivel": false}              → 204
//       a verificação de segurança dos anexos fica fora do ar (503
//       VERIFICACAO_INDISPONIVEL, falha fechada)
//   DELETE /_teste/sac/verificacao                                   → 204
//       volta ao normal
//
// e a verificação de segurança trata como ameaça (422 ANEXO_RECUSADO) o
// arquivo que contém a assinatura de teste EICAR, como o ClamAV faz. O tempo
// (prazos de 7 dias, horas úteis, 24 h dos anexos) anda pelo relógio de teste.
public sealed class ChamadosDoExemplo(Mundo mundo, Api api, Relogio relogio)
{
    public const string Assunto = "Presença não salva a chamada";
    public const string Descricao = "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.";
    public const string Pergunta = "Qual a turma?";
    public const string Solucao = "Corrigido. Pode salvar de novo.";
    public const string NotaDoTime = "Conferir o log da turma 7A";

    // número do exemplo → número real
    readonly Dictionary<string, string> exemplos = [];
    readonly Dictionary<string, Pessoa> donos = [];
    bool caioComSac, anaComSac, mundoDaEbfga;
    DateTimeOffset? relogioEm, relogioReal;

    // o chamado criado pelo Dado sem número ("Dado o chamado fechado há 6 dias"):
    // o número que o Quando citar é ele
    public string? DoDado { get; set; }
    public bool VerificacaoMudou { get; private set; }
    // a fila do time montada pelo cenário (RN-289-api): GET /sac/chamados?… do
    // Caio não prepara o MundoDaFila
    public bool FilaDoCenario { get; set; }

    // ── pessoas ────────────────────────────────────────────────────────────
    public async Task<Pessoa> Caio()
    {
        var caio = await mundo.Caio();
        if (!caioComSac) await mundo.Conceder(caio, new() { ["MOD-02"] = "U" });
        caioComSac = true;
        return caio;
    }

    public async Task<Pessoa> AnaLima()
    {
        var ana = await mundo.Membro("Ana Lima", "analima");
        if (!anaComSac) await mundo.Conceder(ana, new() { ["MOD-02"] = "U" });
        anaComSac = true;
        return ana;
    }

    // o Rui do 18 (RN-288-api) é membro do time, sem o SAC
    public async Task<Pessoa> RuiSemSac()
    {
        var rui = await mundo.Membro("Rui Alves", "rui");
        var mods = (await mundo.Permissoes(rui, mundo.Admin()))["mods"]!.AsObject()
            .ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());
        if (mods.Remove("MOD-02")) await mundo.Definir(rui, mods, mundo.Admin());
        return rui;
    }

    public async Task<Pessoa> Dono(string exemplo) => exemplo == "SAC-000045" ? await mundo.Pedro() : await mundo.Master();

    public Pessoa DonoDe(string id) => donos[id];

    // ── números ────────────────────────────────────────────────────────────
    public bool Conhecido(string exemplo) => exemplos.ContainsKey(exemplo);

    public string Real(string exemplo) => exemplos.TryGetValue(exemplo, out var id) ? id
        : throw new InvalidOperationException($"o {exemplo} do exemplo ainda não foi criado neste cenário");

    public void Anotar(string exemplo, string real)
    {
        exemplos[exemplo] = real;
        mundo.Apelidos[exemplo] = () => Task.FromResult(real);
    }

    // ── chamados ───────────────────────────────────────────────────────────
    // o chamado do exemplo, criado no estado pedido se ainda não existe
    public async Task<string> Chamado(string exemplo, string estado)
    {
        if (exemplos.TryGetValue(exemplo, out var id)) return id;
        if (DoDado is { } doDado)
        {
            Anotar(exemplo, doDado);
            return doDado;
        }
        var dono = await Dono(exemplo);
        id = await Abrir(dono, exemplo);
        await Levar(id, estado);
        return id;
    }

    public static JsonObject Corpo(string assunto = Assunto, string categoria = "MODULO_COM_PROBLEMA", params string[] anexos) => new()
    {
        ["categoria"] = categoria,
        ["assunto"] = assunto,
        ["descricao"] = Descricao,
        ["relacionadoA"] = null,
        ["anexos"] = new JsonArray([.. anexos.Select(a => (JsonNode)a)]),
    };

    public async Task<string> Abrir(Pessoa dono, string? exemplo = null, string assunto = Assunto,
        string categoria = "MODULO_COM_PROBLEMA", params string[] anexos)
    {
        var r = Exigir(await mundo.EnviarComo(dono, "POST", "/sac/chamados", Corpo(assunto, categoria, anexos).ToJsonString()),
            201, $"POST /sac/chamados de {dono.Nome}");
        var id = Texto(r, "id");
        donos[id] = dono;
        if (exemplo is not null) Anotar(exemplo, id);
        return id;
    }

    // ABERTO → EM_ATENDIMENTO → AGUARDANDO_CLIENTE ou RESOLVIDO → FECHADO
    // (confirmado com nota 5); ENCERRADO: fechado pela conta, sem resposta
    public async Task Levar(string id, string estado, Pessoa? membro = null)
    {
        membro ??= await Caio();
        var dono = donos[id];
        switch (estado)
        {
            case "ABERTO":
                break;
            case "EM_ATENDIMENTO":
                await Assumir(id, membro);
                break;
            case "AGUARDANDO_CLIENTE":
                await Assumir(id, membro);
                await Status(id, membro, "AGUARDANDO_CLIENTE", Pergunta);
                break;
            case "RESOLVIDO":
                await Assumir(id, membro);
                await Status(id, membro, "RESOLVIDO", Solucao);
                break;
            case "FECHADO":
                await Levar(id, "RESOLVIDO", membro);
                await Avaliar(id, dono, 5);
                break;
            case "ENCERRADO":
                await Encerrar(id, dono);
                break;
            default:
                throw new ArgumentException($"estado sem preparo: {estado}", nameof(estado));
        }
    }

    public async Task Assumir(string id, Pessoa membro) =>
        Exigir(await mundo.EnviarComo(membro, "POST", $"/sac/chamados/{id}/assumir", new JsonObject { ["substituir"] = true }.ToJsonString()),
            200, $"POST /sac/chamados/{id}/assumir de {membro.Nome}");

    public async Task Status(string id, Pessoa membro, string status, string mensagem) =>
        Exigir(await mundo.EnviarComo(membro, "POST", $"/sac/chamados/{id}/status",
            new JsonObject { ["status"] = status, ["mensagem"] = mensagem }.ToJsonString()), 200, $"POST /sac/chamados/{id}/status {status}");

    public async Task<string> Mensagem(string id, Pessoa quem, string texto, bool interna = false, params string[] anexos) =>
        Texto(Exigir(await mundo.EnviarComo(quem, "POST", $"/sac/chamados/{id}/mensagens", Mensagem(texto, interna, anexos).ToJsonString()),
            201, $"POST /sac/chamados/{id}/mensagens de {quem.Nome}"), "id");

    public static JsonObject Mensagem(string texto, bool interna = false, params string[] anexos) => new()
    {
        ["texto"] = texto,
        ["anexos"] = new JsonArray([.. anexos.Select(a => (JsonNode)a)]),
        ["interna"] = interna,
    };

    public async Task Encerrar(string id, Pessoa dono) =>
        Exigir(await mundo.EnviarComo(dono, "POST", $"/sac/chamados/{id}/encerrar"), 200, $"POST /sac/chamados/{id}/encerrar");

    public async Task Avaliar(string id, Pessoa dono, int nota) =>
        Exigir(await mundo.EnviarComo(dono, "POST", $"/sac/chamados/{id}/avaliacao",
            new JsonObject { ["nota"] = nota, ["comentario"] = "Obrigado" }.ToJsonString()), 201, $"POST /sac/chamados/{id}/avaliacao");

    public async Task<JsonObject> Ler(string id, Pessoa quem) =>
        Exigir(await mundo.EnviarComo(quem, "GET", $"/sac/chamados/{id}"), 200, $"GET /sac/chamados/{id} de {quem.Nome}").Corpo!.AsObject();

    // os textos da conversa (mensagens) de um GET do chamado
    public static List<JsonObject> Mensagens(JsonNode? chamado) =>
        [.. (chamado?["mensagens"]?.AsArray() ?? []).OfType<JsonObject>()];

    public static bool Interna(JsonObject m) =>
        m["interna"]?.GetValueKind() == System.Text.Json.JsonValueKind.True || m["tipo"]?.GetValue<string>() == "NOTA_INTERNA";

    // ── os exemplos da EBFGA (RF-172, RN-295, RNF-230) ─────────────────────
    // 3 resolvidos no mês (já lidos), o SAC-000039 em atendimento com o Caio
    // (lido) e, por último, o SAC-000042 aguardando a conta, com a pergunta do
    // Caio ainda não lida: 2 em andamento, 1 aguardando você, 1 em atendimento,
    // 3 resolvidos e o selo 1 do card do exemplo
    public async Task MundoDaEbfga()
    {
        if (mundoDaEbfga) return;
        mundoDaEbfga = true;
        var master = await mundo.Master();
        var lidos = new List<string>();
        for (var i = 1; i <= 3; i++)
        {
            var resolvido = await Abrir(master, assunto: $"Dúvida resolvida número {i}", categoria: "DUVIDA_DE_USO");
            await Levar(resolvido, "RESOLVIDO");
            lidos.Add(resolvido);
        }
        var sac39 = await Abrir(master, "SAC-000039", "Boleto de novembro não chegou", "COBRANCA");
        await Levar(sac39, "EM_ATENDIMENTO");
        lidos.Add(sac39);
        foreach (var id in lidos) await Ler(id, master);
        var sac42 = await Abrir(master, "SAC-000042");
        await Levar(sac42, "AGUARDANDO_CLIENTE");
    }

    // ── a fila do time (RF-174-api) ────────────────────────────────────────
    // 14 em andamento de 3 contas (EBFGA, COLSA e o Pedro): 2 abertos na
    // quinta 10:00 e, na sexta 10:00, já atrasados (8 h úteis); 3 abertos sem
    // responsável; 3 do Caio e 2 da Ana em atendimento; 4 aguardando a conta (2
    // do Caio). E 3 fechados pela Ana, com as notas 2, 1 e 5.
    public Dictionary<string, HashSet<string>> Fila { get; } = [];
    public string PrimeiroDaFila { get; private set; } = "";

    public async Task MundoDaFila()
    {
        if (Fila.Count > 0) return;
        var (ebfga, colsa, pedro) = (await mundo.Master(), await mundo.MasterDe("COLSA"), await mundo.Pedro());
        var (caio, ana) = (await Caio(), await AnaLima());
        foreach (var nome in new[] { "andamento", "nenhum", "eu", "atrasado", "aguardando", "nota", "EBFGA" }) Fila[nome] = [];
        void Na(string id, Pessoa dono, params string[] listas)
        {
            if (dono == ebfga) Fila["EBFGA"].Add(id);
            foreach (var l in listas) Fila[l].Add(id);
        }

        await Saltar(DateTimeOffset.Parse("2026-11-12T13:00:00Z"));
        PrimeiroDaFila = await Abrir(ebfga, assunto: "Atrasado da EBFGA");
        Na(PrimeiroDaFila, ebfga, "andamento", "nenhum", "atrasado");
        var atrasado = await Abrir(colsa, assunto: "Atrasado do Colégio");
        Na(atrasado, colsa, "andamento", "nenhum", "atrasado");

        await Saltar(DateTimeOffset.Parse("2026-11-13T13:00:00Z"));
        foreach (var dono in new[] { ebfga, pedro, colsa })
        {
            Na(await Abrir(dono, assunto: "Aberto sem responsável"), dono, "andamento", "nenhum");
        }
        foreach (var (dono, membro, estado, listas) in new (Pessoa, Pessoa, string, string[])[]
        {
            (ebfga, caio, "EM_ATENDIMENTO", ["andamento", "eu"]),
            (pedro, caio, "EM_ATENDIMENTO", ["andamento", "eu"]),
            (colsa, caio, "EM_ATENDIMENTO", ["andamento", "eu"]),
            (ebfga, ana, "EM_ATENDIMENTO", ["andamento"]),
            (pedro, ana, "EM_ATENDIMENTO", ["andamento"]),
            (ebfga, caio, "AGUARDANDO_CLIENTE", ["andamento", "eu", "aguardando"]),
            (colsa, caio, "AGUARDANDO_CLIENTE", ["andamento", "eu", "aguardando"]),
            (ebfga, ana, "AGUARDANDO_CLIENTE", ["andamento", "aguardando"]),
            (pedro, ana, "AGUARDANDO_CLIENTE", ["andamento", "aguardando"]),
        })
        {
            var id = await Abrir(dono, assunto: $"Chamado {estado.ToLowerInvariant()}");
            await Levar(id, estado, membro);
            Na(id, dono, listas);
        }
        foreach (var (dono, nota) in new[] { (ebfga, 2), (pedro, 1), (colsa, 5) })
        {
            var id = await Abrir(dono, assunto: $"Fechado com nota {nota}");
            await Levar(id, "RESOLVIDO", ana);
            await Avaliar(id, dono, nota);
            Na(id, dono, nota <= 2 ? ["nota"] : []);
        }
    }

    // ── relógio e ganchos de teste ─────────────────────────────────────────
    // a hora do relógio da aplicação (o real, se o cenário não o mudou)
    public DateTimeOffset Agora() =>
        relogioEm is { } em && relogioReal is { } real ? em + (DateTimeOffset.UtcNow - real) : DateTimeOffset.UtcNow;

    // o relógio da aplicação vai para a hora pedida; as sessões abertas antes
    // ficam para trás (o token vence pelo relógio da aplicação) e cada pessoa
    // entra de novo no próximo pedido
    public async Task Saltar(DateTimeOffset agora)
    {
        await relogio.Definir(agora);
        (relogioEm, relogioReal) = (agora, DateTimeOffset.UtcNow);
        mundo.Sessoes.Clear();
        mundo.Atual = null;
    }

    public async Task Agendamento() =>
        Exigir(await api.Enviar("POST", "/_teste/sac/agendamento"), 204,
            "POST /_teste/sac/agendamento (agendamento do SAC sob demanda, Teste__Relogio=sim)");

    public async Task VerificacaoForaDoAr()
    {
        VerificacaoMudou = true;
        Exigir(await api.Enviar("PUT", "/_teste/sac/verificacao", new JsonObject { ["disponivel"] = false }.ToJsonString()), 204,
            "PUT /_teste/sac/verificacao (verificação dos anexos fora do ar, Teste__Relogio=sim)");
    }

    public async Task VerificacaoNormal()
    {
        if (!VerificacaoMudou) return;
        VerificacaoMudou = false;
        try { await api.Enviar("DELETE", "/_teste/sac/verificacao"); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // o próximo cenário que a derrubar a define de novo
        }
    }

    // ── anexos ─────────────────────────────────────────────────────────────
    public async Task<Resposta> EnviarAnexo(Pessoa quem, string nome, byte[] conteudo) =>
        await mundo.EnviarArquivoComo(quem, "/sac/anexos", nome, conteudo, TipoPelaExtensao(nome));

    public async Task<string> Anexo(Pessoa quem, string? exemplo = null, string nome = "erro.png", byte[]? conteudo = null)
    {
        var id = Texto(Exigir(await EnviarAnexo(quem, nome, conteudo ?? Arquivos.Png(4096)), 201, $"POST /sac/anexos de {quem.Nome}"), "id");
        if (exemplo is not null) mundo.Apelidos[exemplo] = () => Task.FromResult(id);
        return id;
    }

    static string TipoPelaExtensao(string nome) => Path.GetExtension(nome) switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream",
    };
}

// o relógio, a verificação e as sessões voltam ao normal no fim do cenário
[Binding]
public sealed class ChamadosNoFim(ChamadosDoExemplo chamados)
{
    [AfterScenario]
    public Task VerificacaoNormal() => chamados.VerificacaoNormal();
}

// Arquivos dos exemplos (RN-292), do tamanho pedido e com o tipo no conteúdo
// (o backend confere pelo conteúdo, não pelo nome)
public static class Arquivos
{
    public const int MB = 1024 * 1024;

    // PNG 1×1 válido, completado até o tamanho por um bloco tEXt
    public static byte[] Png(int tamanho)
    {
        using var saida = new MemoryStream();
        saida.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Bloco(saida, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 0, 0, 0, 0]);
        using (var pixel = new MemoryStream())
        {
            using (var z = new ZLibStream(pixel, CompressionLevel.Optimal, leaveOpen: true)) z.Write([0, 0x80]);
            Bloco(saida, "IDAT", pixel.ToArray());
        }
        var resta = tamanho - (int)saida.Length - 12 - 12;
        if (resta < 3) throw new ArgumentOutOfRangeException(nameof(tamanho), "PNG pequeno demais");
        var texto = new byte[resta];
        Encoding.ASCII.GetBytes("qa\0").CopyTo(texto, 0);
        Array.Fill(texto, (byte)'a', 3, resta - 3);
        Bloco(saida, "tEXt", texto);
        Bloco(saida, "IEND", []);
        return saida.ToArray();
    }

    // JPEG: marca de início, cabeçalho JFIF, enchimento e marca de fim
    public static byte[] Jpg(int tamanho)
    {
        var jpg = new byte[tamanho];
        byte[] inicio = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0];
        inicio.CopyTo(jpg, 0);
        jpg[^2] = 0xFF;
        jpg[^1] = 0xD9;
        return jpg;
    }

    // MP4: a caixa ftyp de um vídeo (tipo que o SAC não aceita)
    public static byte[] Mp4()
    {
        var mp4 = new byte[1024];
        byte[] ftyp = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2',
            0, 0, 0, 0, (byte)'m', (byte)'p', (byte)'4', (byte)'2', (byte)'i', (byte)'s', (byte)'o', (byte)'m'];
        ftyp.CopyTo(mp4, 0);
        return mp4;
    }

    // JPEG com a assinatura de teste EICAR num comentário (não é vírus: todo
    // antivírus a reconhece como teste). Montada aqui para o código-fonte não a
    // conter inteira. Só PNG e JPG entram (RN-292), então a ameaça vai num JPG
    public static byte[] JpgComAmeaca()
    {
        var eicar = Encoding.ASCII.GetBytes(string.Concat(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$", "EICAR-STANDARD-", "ANTIVIRUS-TEST-FILE!$H+H*"));
        using var saida = new MemoryStream();
        saida.Write([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        // segmento COM (0xFFFE): o tamanho conta os 2 bytes dele
        var tamanho = eicar.Length + 2;
        saida.Write([0xFF, 0xFE, (byte)(tamanho >> 8), (byte)tamanho]);
        saida.Write(eicar);
        saida.Write([0xFF, 0xD9]);
        return saida.ToArray();
    }

    public static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

    static void Bloco(Stream saida, string tipo, byte[] dados)
    {
        var tamanho = BitConverter.GetBytes(dados.Length);
        if (BitConverter.IsLittleEndian) Array.Reverse(tamanho);
        saida.Write(tamanho);
        var corpo = Encoding.ASCII.GetBytes(tipo).Concat(dados).ToArray();
        saida.Write(corpo);
        var crc = BitConverter.GetBytes(Crc32(corpo));
        if (BitConverter.IsLittleEndian) Array.Reverse(crc);
        saida.Write(crc);
    }

    static uint Crc32(byte[] dados)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in dados)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
