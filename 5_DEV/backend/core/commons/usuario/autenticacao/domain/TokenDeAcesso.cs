using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// As claims do token de acesso (RNF-215): identidade, sessão e permissões, sem
/// nome, documento, matrícula nem senha. O token não é guardado no servidor (RN-229).
/// </summary>
/// <param name="Aud">"hub" ou "quest": o token de uma frente é recusado na outra (RN-227).</param>
/// <param name="Sub">Usuário, ou o acesso anônimo na sala.</param>
/// <param name="Sid">Sessão do aparelho.</param>
/// <param name="Conta">Id da conta.</param>
/// <param name="Sigla">Sigla da conta.</param>
/// <param name="Tipo">Tipo do usuário (MASTER, STAFF…) ou ANONIMO.</param>
/// <param name="Aparelho">NAVEGADOR ou APP.</param>
/// <param name="Quem">No quest, quem entrou com o código.</param>
/// <param name="Papel">Papel da autorização (17-b).</param>
/// <param name="Mods">Módulos e abas da autorização.</param>
/// <param name="Pv">Versão das permissões.</param>
/// <param name="Sala">Código da sala, só no acesso anônimo.</param>
/// <param name="Iat">Emissão.</param>
/// <param name="Exp">Vencimento: 15 min depois da emissão (RNF-209).</param>
/// <param name="Jti">Id único do token.</param>
public sealed record ClaimsDeAcesso(
    string Aud, string Sub, string Sid, string Conta, string Sigla, string Tipo, string Aparelho,
    QuemEntrou? Quem, string Papel, IReadOnlyDictionary<string, string> Mods, int Pv, string? Sala,
    DateTimeOffset Iat, DateTimeOffset Exp, string Jti)
{
    /// <summary>Emissor de todos os tokens.</summary>
    public const string Iss = "deboaz";

    /// <summary>Validade do token de acesso: 15 min (RNF-209).</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(15);

    /// <summary>A audiência de cada frente: a sala é do quest.</summary>
    public static string AudDe(Frente frente) => frente == Frente.Hub ? "hub" : "quest";

    /// <summary>As claims de um token novo para a sessão, válido por 15 min a partir de agora.</summary>
    public static ClaimsDeAcesso Para(Sessao sessao, string sub, string tipo, PermissoesNoToken permissoes, DateTimeOffset agora) =>
        new(AudDe(sessao.Frente), sub, sessao.Sid, sessao.ContaId, sessao.Sigla, tipo, sessao.Aparelho.Texto(),
            sessao.QuemEntrou, permissoes.Papel, permissoes.Mods, permissoes.Pv, sessao.Sala,
            agora, agora + Validade, Guid.NewGuid().ToString("N"));
}

/// <summary>
/// Monta e lê o token de acesso: um JWT RS256 com o kid no cabeçalho (RNF-207).
/// A assinatura é feita no Key Vault, pela porta de saída; aqui ficam o formato
/// e a conferência com a chave pública.
/// </summary>
public sealed class TokenDeAcesso
{
    TokenDeAcesso(string kid, ClaimsDeAcesso claims, string dadosAssinados, byte[] assinatura)
    {
        Kid = kid;
        Claims = claims;
        DadosAssinados = dadosAssinados;
        Assinatura = assinatura;
    }

    /// <summary>Chave que assinou o token.</summary>
    public string Kid { get; }

    /// <summary>O conteúdo do token.</summary>
    public ClaimsDeAcesso Claims { get; }

    /// <summary>Cabeçalho e carga em base64url, separados por ponto: o que a assinatura cobre.</summary>
    public string DadosAssinados { get; }

    /// <summary>A assinatura RS256.</summary>
    public byte[] Assinatura { get; }

    /// <summary>O que deve ser assinado: <c>base64url(cabeçalho).base64url(carga)</c>.</summary>
    public static string ParaAssinar(string kid, ClaimsDeAcesso claims)
    {
        var cabecalho = new JsonObject { ["alg"] = "RS256", ["typ"] = "JWT", ["kid"] = kid };
        return $"{B64(cabecalho)}.{B64(Carga(claims))}";
    }

    /// <summary>O token completo, com a assinatura que veio do Key Vault.</summary>
    public static string Completar(string paraAssinar, byte[] assinatura) => $"{paraAssinar}.{Base64Url.EncodeToString(assinatura)}";

    /// <summary>
    /// Lê um token recebido, sem conferir a assinatura (veja <see cref="ConfereCom"/>).
    /// Token fora do formato lança TOKEN_INVALIDO.
    /// </summary>
    public static TokenDeAcesso Ler(string token)
    {
        try
        {
            var partes = token.Split('.');
            if (partes.Length != 3) throw FalhaDeAutenticacao.TokenInvalido();
            var cabecalho = JsonNode.Parse(Base64Url.DecodeFromChars(partes[0]))!;
            var carga = JsonNode.Parse(Base64Url.DecodeFromChars(partes[1]))!;
            if (cabecalho["alg"]?.GetValue<string>() != "RS256" || carga["iss"]?.GetValue<string>() != ClaimsDeAcesso.Iss)
            {
                throw FalhaDeAutenticacao.TokenInvalido();
            }
            return new(cabecalho["kid"]!.GetValue<string>(), ClaimsDe(carga), $"{partes[0]}.{partes[1]}",
                Base64Url.DecodeFromChars(partes[2]));
        }
        catch (Exception erro) when (erro is FormatException or JsonException or InvalidOperationException or NullReferenceException)
        {
            throw FalhaDeAutenticacao.TokenInvalido();
        }
    }

    /// <summary>A assinatura confere com a chave pública do kid?</summary>
    public bool ConfereCom(ChavePublica chave) =>
        chave.Kid == Kid && chave.Confere(Encoding.ASCII.GetBytes(DadosAssinados), Assinatura);

    static JsonObject Carga(ClaimsDeAcesso c)
    {
        var carga = new JsonObject
        {
            ["iss"] = ClaimsDeAcesso.Iss,
            ["aud"] = c.Aud,
            ["sub"] = c.Sub,
            ["sid"] = c.Sid,
            ["conta"] = c.Conta,
            ["sigla"] = c.Sigla,
            ["tipo"] = c.Tipo,
            ["aparelho"] = c.Aparelho,
        };
        if (c.Quem is { } quem)
        {
            var objeto = new JsonObject { ["tipo"] = quem.Tipo.Texto() };
            if (quem.Parentesco is { } parentesco) objeto["parentesco"] = parentesco;
            carga["quem"] = objeto;
        }
        carga["papel"] = c.Papel;
        carga["mods"] = new JsonObject(c.Mods.Select(m => KeyValuePair.Create(m.Key, (JsonNode?)m.Value)));
        carga["pv"] = c.Pv;
        if (c.Sala is { } sala) carga["sala"] = sala;
        carga["iat"] = c.Iat.ToUnixTimeSeconds();
        carga["exp"] = c.Exp.ToUnixTimeSeconds();
        carga["jti"] = c.Jti;
        return carga;
    }

    static ClaimsDeAcesso ClaimsDe(JsonNode carga)
    {
        var quem = carga["quem"] is { } q
            ? new QuemEntrou(Nomes.QuemEntrouDe(q["tipo"]?.GetValue<string>()) ?? throw FalhaDeAutenticacao.TokenInvalido(),
                q["parentesco"]?.GetValue<string>())
            : null;
        var mods = carga["mods"]!.AsObject().ToDictionary(m => m.Key, m => m.Value!.GetValue<string>());
        return new(
            carga["aud"]!.GetValue<string>(), carga["sub"]!.GetValue<string>(), carga["sid"]!.GetValue<string>(),
            carga["conta"]!.GetValue<string>(), carga["sigla"]!.GetValue<string>(), carga["tipo"]!.GetValue<string>(),
            carga["aparelho"]!.GetValue<string>(), quem, carga["papel"]!.GetValue<string>(), mods,
            carga["pv"]!.GetValue<int>(), carga["sala"]?.GetValue<string>(),
            DateTimeOffset.FromUnixTimeSeconds(carga["iat"]!.GetValue<long>()),
            DateTimeOffset.FromUnixTimeSeconds(carga["exp"]!.GetValue<long>()), carga["jti"]!.GetValue<string>());
    }

    static string B64(JsonNode json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json.ToJsonString()));
}
