using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Domain;

/// <summary>RNF-207 e RNF-215: JWT RS256 com kid, só identidade, sessão e permissões.</summary>
public class TokenDeAcessoTests
{
    static readonly DateTimeOffset Nove = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    static readonly RSA Chave = RSA.Create(2048);

    static ClaimsDeAcesso Claims(QuemEntrou? quem = null, string? sala = null) => new(
        "hub", "usr-ebfga-maria", "3f9c2a71", "conta-ebfga", "EBFGA", "STAFF", "NAVEGADOR", quem, "STAFF",
        new Dictionary<string, string> { ["MOD-03"] = "CU", ["MOD-04"] = "U" }, 7, sala, Nove, Nove.AddMinutes(15), "a81c0e5d");

    static string Assinado(ClaimsDeAcesso claims, string kid = "deboaz-2026-11")
    {
        var dados = TokenDeAcesso.ParaAssinar(kid, claims);
        return TokenDeAcesso.Completar(dados, Chave.SignData(Encoding.ASCII.GetBytes(dados), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    static string Parte(string token, int i) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token.Split('.')[i]));

    [Fact]
    public void Monta_o_cabecalho_e_a_carga_do_exemplo()
    {
        var token = Assinado(Claims());
        Assert.Equal("""{"alg":"RS256","typ":"JWT","kid":"deboaz-2026-11"}""", Parte(token, 0));
        Assert.Equal(
            """{"iss":"deboaz","aud":"hub","sub":"usr-ebfga-maria","sid":"3f9c2a71","conta":"conta-ebfga","sigla":"EBFGA","tipo":"STAFF","aparelho":"NAVEGADOR","papel":"STAFF","mods":{"MOD-03":"CU","MOD-04":"U"},"pv":7,"iat":1793523600,"exp":1793524500,"jti":"a81c0e5d"}""",
            Parte(token, 1));
    }

    [Fact]
    public void Le_de_volta_e_confere_com_a_chave_publica()
    {
        var claims = Claims(new QuemEntrou(TipoQuemEntrou.Responsavel, "Mãe"), "482107");
        var lido = TokenDeAcesso.Ler(Assinado(claims));
        Assert.Equal("deboaz-2026-11", lido.Kid);
        Assert.Equal(claims with { Mods = lido.Claims.Mods }, lido.Claims);
        Assert.Equal(claims.Mods, lido.Claims.Mods);
        Assert.True(lido.ConfereCom(ChavePublica.De("deboaz-2026-11", Chave)));
        Assert.False(lido.ConfereCom(ChavePublica.De("deboaz-2027-02", Chave)));
        Assert.False(lido.ConfereCom(ChavePublica.De("deboaz-2026-11", RSA.Create(2048))));
    }

    [Fact]
    public void Quem_entrou_participante_vai_sem_parentesco()
    {
        var lido = TokenDeAcesso.Ler(Assinado(Claims(new QuemEntrou(TipoQuemEntrou.Participante))));
        Assert.Equal(new QuemEntrou(TipoQuemEntrou.Participante), lido.Claims.Quem);
        Assert.DoesNotContain("parentesco", Parte(Assinado(Claims(new QuemEntrou(TipoQuemEntrou.Participante))), 1));
    }

    [Fact]
    public void Carga_alterada_nao_confere()
    {
        var partes = Assinado(Claims()).Split('.');
        var outra = Assinado(Claims() with { Sub = "usr-eejsa-joao" }).Split('.');
        var trocado = TokenDeAcesso.Ler($"{partes[0]}.{outra[1]}.{partes[2]}");
        Assert.False(trocado.ConfereCom(ChavePublica.De("deboaz-2026-11", Chave)));
    }

    [Fact]
    public void Assinatura_com_tamanho_errado_nao_confere()
    {
        var partes = Assinado(Claims()).Split('.');
        var curto = TokenDeAcesso.Ler($"{partes[0]}.{partes[1]}.AAAA");
        Assert.False(curto.ConfereCom(ChavePublica.De("deboaz-2026-11", Chave)));
    }

    public static TheoryData<string> Malformados()
    {
        static string B(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
        var carga = Assinado(Claims()).Split('.')[1];
        return new()
        {
            "",
            "abc",
            "a.b",
            "***.***.***",
            $"{B("""{"alg":"none","kid":"k"}""")}.{carga}.AAAA",
            $"{B("""{"alg":"RS256","kid":"k"}""")}.{B("""{"iss":"outro"}""")}.AAAA",
            $"{B("""{"alg":"RS256"}""")}.{carga}.AAAA",
            $"{B("""{"alg":"RS256","kid":"k"}""")}.{B("""{"iss":"deboaz","aud":"hub"}""")}.AAAA",
            $"{B("""{"alg":"RS256","kid":"k"}""")}.{B("""{"iss":"deboaz","quem":{"tipo":"TIO"}}""")}.AAAA",
            $"{B("[1]")}.{carga}.AAAA",
            $"{B("""{"alg":"RS256","kid":"k"}""")}.{carga}.***",
        };
    }

    [Theory]
    [MemberData(nameof(Malformados))]
    public void Token_fora_do_formato_e_token_invalido(string token)
    {
        var falha = Assert.Throws<FalhaDeAutenticacao>(() => TokenDeAcesso.Ler(token));
        Assert.Equal("TOKEN_INVALIDO", falha.Codigo);
    }

    [Fact]
    public void Audiencia_e_validade()
    {
        Assert.Equal("hub", ClaimsDeAcesso.AudDe(Frente.Hub));
        Assert.Equal("quest", ClaimsDeAcesso.AudDe(Frente.Quest));
        Assert.Equal("quest", ClaimsDeAcesso.AudDe(Frente.Sala));
        Assert.Equal(TimeSpan.FromMinutes(15), ClaimsDeAcesso.Validade);
    }
}
