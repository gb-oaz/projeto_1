using System.Security.Cryptography;

namespace Boaz.Qa.Integracao.Suporte;

// Uma conta que a inicialização cria a partir do Key Vault (17 RN-190, RN-265,
// RN-266, RN-268). Entrada e senha (ou código, no participante) chegam ao backend como
// variáveis de ambiente (RNF-225); a suíte lê as mesmas variáveis, nunca um
// valor fixo. A entrada do exemplo é a do requisito: é por ela que o cenário
// cita a conta.
public sealed record ContaInicial(
    string Nome,
    string Id,
    string ContaId,
    string EntradaDoExemplo,
    string VariavelEntrada,
    string? VariavelSenha,
    string Papel,
    string Tipo,
    bool SoEmHomologacao)
{
    public bool Participante => VariavelSenha is null;

    // entrada (ou código) entregue pelo Key Vault; sem a variável, a do requisito
    public string Entrada => Ambiente.Segredo(VariavelEntrada) is { Length: > 0 } e ? e : EntradaDoExemplo;

    // senha entregue pelo Key Vault ao ambiente que a suíte testa
    public string Senha => VariavelSenha is null ? ""
        : Ambiente.Segredo(VariavelSenha) is { Length: > 0 } s ? s
        : throw new InvalidOperationException($"{VariavelSenha} ausente: a suíte lê a senha da conta inicial da mesma variável que o backend (RNF-225)");
}

public static class ContasIniciais
{
    public const string ContaDoTime = "conta-dboaz", ContaQains = "conta-qains", ContaQaaut = "conta-qaaut";

    // ids definidos no diagrama usuario-inicializacao-sequencia
    public static readonly ContaInicial Admin = new("administrador do deboaz", "usr-dboaz-admin", ContaDoTime,
        "admin.dboaz@deboaz.com", "USUARIO_ADMIN_ENTRADA", "USUARIO_ADMIN_SENHA", "ADMINISTRADOR", "ADMINISTRADOR", false);
    // conta qa com o papel de administrador, só em homologação: os testes a usam
    // no lugar do administrador real (RN-265)
    public static readonly ContaInicial QaAdmin = new("administrador qa do deboaz", "usr-dboaz-qaadmin", ContaDoTime,
        "qaadmin.dboaz@deboaz.com", "USUARIO_QA_ADMIN_ENTRADA", "USUARIO_QA_ADMIN_SENHA", "ADMINISTRADOR", "ADMINISTRADOR", true);
    public static readonly ContaInicial Membro = new("membro do time qa", "usr-dboaz-qamembro", ContaDoTime,
        "qamembro.dboaz@deboaz.com", "USUARIO_QA_MEMBRO_ENTRADA", "USUARIO_QA_MEMBRO_SENHA", "MEMBRO_TIME", "MEMBRO_TIME", true);
    public static readonly ContaInicial Master = new("conta master da QAINS", "usr-qains-master", ContaQains,
        "qa_instituicao@deboaz.com", "USUARIO_QA_MASTER_ENTRADA", "USUARIO_QA_MASTER_SENHA", "MASTER", "MASTER", true);
    public static readonly ContaInicial Autonomo = new("educador autônomo QAAUT", "usr-qaaut-master", ContaQaaut,
        "qa_autonomo@deboaz.com", "USUARIO_QA_AUTONOMO_ENTRADA", "USUARIO_QA_AUTONOMO_SENHA", "AUTONOMO", "MASTER", true);
    public static readonly ContaInicial Staff = new("staff qa da QAINS", "usr-qains-qastaff", ContaQains,
        "qastaff.qains@deboaz.com", "USUARIO_QA_STAFF_ENTRADA", "USUARIO_QA_STAFF_SENHA", "STAFF", "STAFF", true);
    public static readonly ContaInicial Participante = new("participante qa da QAINS", "usr-qains-qapart", ContaQains,
        "QAINS-QAPART-0001", "USUARIO_QA_PARTICIPANTE_CODIGO", null, "PARTICIPANTE", "PARTICIPANTE", true);

    public static readonly ContaInicial[] Todas = [Admin, QaAdmin, Membro, Master, Autonomo, Staff, Participante];
    public static readonly ContaInicial[] Qa = [.. Todas.Where(c => c.SoEmHomologacao)];

    // a lista de módulos e abas iniciais das contas qa, no Key Vault de
    // homologação (RN-268): o exemplo do requisito, em JSON
    // {"staff": {"mods": {…}}, "time": {"mods": {…}}}. A parte "staff" vai para
    // o qastaff e é o que "QAINS" e "QAAUT" contratam; a parte "time", para o
    // qamembro, sem virar contratação
    public const string VariavelModulos = "USUARIO_QA_MODULOS";
    public static readonly Dictionary<string, string> ModulosDaStaff = new()
    {
        ["MOD-03"] = "CU",
        ["MOD-04"] = "U",
        ["MOD-05"] = "U",
    };
    public static readonly Dictionary<string, string> ModulosDoTime = new()
    {
        ["MOD-03"] = "U",
        ["MOD-14"] = "U",
    };

    // os _id que a inicialização cria (contas e usuários): o que a limpeza do
    // banco preserva entre os cenários (Estado.Limpar)
    public static readonly string[] Ids = [ContaDoTime, ContaQains, ContaQaaut, .. Todas.Select(c => c.Id)];

    // a conta citada pelo cenário: pela entrada do requisito ou pela do Key Vault
    // (ou pelo apelido curto do requisito: "qastaff.qains", "qastaff")
    public static ContaInicial PorEntrada(string entrada) =>
        Todas.FirstOrDefault(c => string.Equals(c.EntradaDoExemplo, entrada, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(c.Entrada, entrada, StringComparison.OrdinalIgnoreCase))
        ?? Todas.FirstOrDefault(c => c.EntradaDoExemplo.Split('@')[0] is var local
                                     && (string.Equals(local, entrada, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(local.Split('.')[0], entrada, StringComparison.OrdinalIgnoreCase)))
        ?? throw new InvalidOperationException($"\"{entrada}\" não é uma conta inicial do requisito 17 (RN-190, RN-265)");

    // senha nova, gerada na hora, que segue a RNF-202 (8+, maiúscula, minúscula,
    // número e especial): o Key Vault de uma instância de Partida
    public static string SenhaNova()
    {
        const string letras = "abcdefghjkmnpqrstuvwxyz";
        var meio = string.Concat(Enumerable.Range(0, 5).Select(_ => letras[RandomNumberGenerator.GetInt32(letras.Length)]));
        return $"Qa#{char.ToUpperInvariant(meio[0])}{meio[1..]}{RandomNumberGenerator.GetInt32(100000, 999999)}";
    }
}
