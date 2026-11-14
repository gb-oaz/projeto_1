using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;

/// <summary>
/// O mundo dos exemplos do requisito 17-a: a escola EBFGA, a staff Maria, o
/// participante Lucas, o João de outra escola e uma conta master. Senhas
/// fictícias, nunca reais.
/// </summary>
public sealed class Cenario
{
    /// <summary>Senha da Maria nos testes.</summary>
    public const string SenhaDaMaria = "Ma!Silva204871";

    /// <summary>Entrada da Maria.</summary>
    public const string EntradaDaMaria = "maria.ebfga@deboaz.com";

    /// <summary>Código de acesso do Lucas.</summary>
    public const string CodigoDoLucas = "EBFGA-LUCAS-0412";

    /// <summary>Monta o mundo.</summary>
    public Cenario()
    {
        Mock.NovaConta("conta-ebfga", "EBFGA");
        Mock.NovaConta("conta-eejsa", "EEJSA");
        Mock.NovoUsuario("usr-ebfga-maria", "conta-ebfga", TipoUsuario.Staff, EntradaDaMaria, SenhaDaMaria);
        Mock.NovoUsuario("usr-ebfga-lucas", "conta-ebfga", TipoUsuario.Participante, codigo: CodigoDoLucas);
        Mock.NovoUsuario("usr-ebfga-master", "conta-ebfga", TipoUsuario.Master, "master.ebfga@deboaz.com", "Xu@Xa182482");
        Mock.NovoUsuario("usr-eejsa-joao", "conta-eejsa", TipoUsuario.Staff, "joao.eejsa@deboaz.com", "Xu@Xa182482");
        Mock.DefinirPermissoes("usr-ebfga-maria", new PermissoesNoToken("STAFF",
            new Dictionary<string, string> { ["MOD-03"] = "CU", ["MOD-04"] = "U", ["MOD-05"] = "U" }, 7));
    }

    /// <summary>As portas em memória e os casos de uso.</summary>
    public AutenticacaoEmMemoria Mock { get; } = new();

    /// <summary>A Maria entra no hub.</summary>
    public Task<SessaoAbertaDto> MariaEntra(string aparelho = "NAVEGADOR", string senha = SenhaDaMaria) =>
        Mock.EntrarNoHub.Executar(new EntradaNoHubDto(EntradaDaMaria, senha, aparelho), Trace);

    /// <summary>Alguém entra no quest com o código do Lucas.</summary>
    public Task<SessaoAbertaDto> LucasEntra(string codigo = CodigoDoLucas, string tipo = "PARTICIPANTE", string? parentesco = null) =>
        Mock.EntrarNoQuest.Executar(new EntradaNoQuestDto(codigo, "APP", new QuemEntrouDto(tipo, parentesco)), Trace);

    /// <summary>Um pedido ao hub com o token.</summary>
    public Task<AcessoConferido> PedidoAoHub(SessaoAbertaDto sessao) =>
        Mock.ConferirAcesso.Executar(new ConferenciaDeAcessoDto(sessao.TokenAcesso, Frente.Hub));

    /// <summary>Renova com o token de renovação.</summary>
    public Task<SessaoAbertaDto> Renovar(SessaoAbertaDto sessao) =>
        Mock.RenovarSessao.Executar(new RenovacaoDto(sessao.TokenRenovacao), Trace);

    /// <summary>O traceId de toda execução dos testes.</summary>
    public static readonly TraceId Trace = new("trc-7f3a9c21");

    /// <summary>A execução de quem chama os casos de uso internos: a conta master da EBFGA (RN-275).</summary>
    public static readonly ExecucaoDeQuemChama Chamada = new(Trace, "usr-ebfga-master", "conta-ebfga");

    /// <summary>O token conferido, como o endereço entrega ao caso de uso.</summary>
    public Task<AcessoConferido> Conferido(SessaoAbertaDto sessao, Frente frente = Frente.Hub) =>
        Mock.ConferirAcesso.Executar(new ConferenciaDeAcessoDto(sessao.TokenAcesso, frente));

    /// <summary>As claims do token de acesso.</summary>
    public static ClaimsDeAcesso Claims(SessaoAbertaDto sessao) => TokenDeAcesso.Ler(sessao.TokenAcesso).Claims;

    /// <summary>Confere que a ação falha com o código dado.</summary>
    public static async Task<FalhaDeAutenticacao> Falha(string codigo, Func<Task> acao)
    {
        var falha = await Assert.ThrowsAsync<FalhaDeAutenticacao>(acao);
        Assert.Equal(codigo, falha.Codigo);
        return falha;
    }
}
