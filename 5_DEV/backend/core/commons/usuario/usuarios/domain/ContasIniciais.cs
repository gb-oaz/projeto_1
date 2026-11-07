using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// Uma conta assinante que a inicialização garante (RN-190, RN-265, RN-266).
/// O domínio da conta master sai da entrada dela no Key Vault.
/// </summary>
/// <param name="Id">Id da conta (conta-qains).</param>
/// <param name="Sigla">Sigla (QAINS).</param>
/// <param name="Tipo">Instituição ou autônomo.</param>
/// <param name="Nome">Nome da conta.</param>
/// <param name="Documento">CNPJ ou CPF.</param>
/// <param name="SoEmHomologacao">Só existe em homologação (RN-266).</param>
public sealed record ContaInicial(string Id, string Sigla, TipoConta Tipo, string Nome, string Documento, bool SoEmHomologacao);

/// <summary>
/// Um usuário que a inicialização garante, com as variáveis de ambiente de onde
/// vêm a entrada (ou o código, no participante) e a senha (RNF-225).
/// </summary>
/// <param name="Id">Id do usuário (usr-qains-qastaff).</param>
/// <param name="ContaId">Conta a que pertence.</param>
/// <param name="Sigla">Sigla da conta.</param>
/// <param name="Tipo">Tipo do usuário.</param>
/// <param name="Nome">Nome completo.</param>
/// <param name="NomeCurto">Nome curto; na conta master, o domínio sai da entrada.</param>
/// <param name="VariavelEntrada">Variável da entrada ou do código de acesso.</param>
/// <param name="VariavelSenha">Variável da senha; null no participante, que não tem senha.</param>
/// <param name="CriadoPor">Quem fica acima dele na ordem de superior (RN-169).</param>
/// <param name="SoEmHomologacao">Só existe em homologação (RN-266).</param>
public sealed record UsuarioInicial(
    string Id, string ContaId, string Sigla, TipoUsuario Tipo, string Nome, string NomeCurto,
    string VariavelEntrada, string? VariavelSenha, string? CriadoPor, bool SoEmHomologacao)
{
    /// <summary>Tem senha: todos menos o participante.</summary>
    public bool TemSenha => VariavelSenha is not null;
}

/// <summary>
/// As contas que a aplicação garante a cada partida, antes do primeiro pedido
/// (RN-190, RN-265 a RN-267; diagrama usuario-inicializacao-sequencia): o
/// administrador do deboaz em todo ambiente e, só em homologação, as contas
/// "QAINS" e "QAAUT" com uma conta qa para cada tipo de usuário. As entradas e
/// as senhas não ficam aqui: chegam do Key Vault como variáveis de ambiente.
/// </summary>
/// <remarks>
/// Os nomes e os documentos das contas qa são fictícios, no formato do projeto
/// (RN-269). Os módulos e as abas das contas qa vêm da lista USUARIO_QA_MODULOS
/// do Key Vault de homologação (RN-268): <see cref="ModulosIniciais"/>.
/// </remarks>
public static class ContasIniciais
{
    /// <summary>O ambiente de homologação, único em que as contas qa existem (RN-266).</summary>
    public const string Homologacao = "hml";

    /// <summary>A variável que diz o ambiente (hml ou prd).</summary>
    public const string VariavelDoAmbiente = "AMBIENTE";

    /// <summary>Id do administrador do deboaz.</summary>
    public const string AdminId = "usr-dboaz-admin";

    /// <summary>Id do administrador qa, só em homologação (RN-265).</summary>
    public const string QaAdminId = "usr-dboaz-qaadmin";

    /// <summary>A variável da lista de módulos e abas iniciais das contas qa (RN-268).</summary>
    public const string VariavelDosModulos = "USUARIO_QA_MODULOS";

    /// <summary>Id da conta da instituição qa.</summary>
    public const string ContaQains = "conta-qains";

    /// <summary>Id da conta do educador autônomo qa.</summary>
    public const string ContaQaaut = "conta-qaaut";

    static readonly string ContaDoTime = ContaAssinante.IdDa(Sigla.DoTime);

    /// <summary>As contas qa (a do time vem da configuração do time).</summary>
    public static IReadOnlyList<ContaInicial> Contas { get; } =
    [
        // nome e documento fictícios, no formato do projeto (RN-269)
        new(ContaQains, "QAINS", TipoConta.Instituicao, "Instituição QA de Testes", "98.765.432/0001-98", true),
        new(ContaQaaut, "QAAUT", TipoConta.Autonomo, "Educador QA de Testes", "987.654.321-00", true),
    ];

    /// <summary>Os usuários, na ordem em que nascem (quem cria vem antes de quem é criado).</summary>
    public static IReadOnlyList<UsuarioInicial> Usuarios { get; } =
    [
        new(AdminId, ContaDoTime, Sigla.DoTime, TipoUsuario.Administrador, "Administrador do deboaz", "admin",
            "USUARIO_ADMIN_ENTRADA", "USUARIO_ADMIN_SENHA", null, false),
        // nomes fictícios das pessoas qa (RN-269)
        new(QaAdminId, ContaDoTime, Sigla.DoTime, TipoUsuario.Administrador, "Administrador QA", "qaadmin",
            "USUARIO_QA_ADMIN_ENTRADA", "USUARIO_QA_ADMIN_SENHA", null, true),
        new("usr-dboaz-qamembro", ContaDoTime, Sigla.DoTime, TipoUsuario.MembroTime, "Membro QA", "qamembro",
            "USUARIO_QA_MEMBRO_ENTRADA", "USUARIO_QA_MEMBRO_SENHA", QaAdminId, true),
        new("usr-qains-master", ContaQains, "QAINS", TipoUsuario.Master, "Instituição QA de Testes", "",
            "USUARIO_QA_MASTER_ENTRADA", "USUARIO_QA_MASTER_SENHA", null, true),
        new("usr-qaaut-master", ContaQaaut, "QAAUT", TipoUsuario.Master, "Educador QA de Testes", "",
            "USUARIO_QA_AUTONOMO_ENTRADA", "USUARIO_QA_AUTONOMO_SENHA", null, true),
        new("usr-qains-qastaff", ContaQains, "QAINS", TipoUsuario.Staff, "Staff QA", "qastaff",
            "USUARIO_QA_STAFF_ENTRADA", "USUARIO_QA_STAFF_SENHA", "usr-qains-master", true),
        new("usr-qains-qapart", ContaQains, "QAINS", TipoUsuario.Participante, "Participante QA", "qapart",
            "USUARIO_QA_PARTICIPANTE_CODIGO", null, "usr-qains-master", true),
    ];

    /// <summary>Em homologação (AMBIENTE=hml); qualquer outro valor vale como produção.</summary>
    public static bool EmHomologacao(string? ambiente) =>
        string.Equals(ambiente?.Trim(), Homologacao, StringComparison.OrdinalIgnoreCase);

    /// <summary>Os usuários que o ambiente garante (RN-266): em produção, só o administrador.</summary>
    public static IReadOnlyList<UsuarioInicial> UsuariosDo(string? ambiente) =>
        [.. Usuarios.Where(u => !u.SoEmHomologacao || EmHomologacao(ambiente))];

    /// <summary>As contas qa que o ambiente garante: nenhuma em produção.</summary>
    public static IReadOnlyList<ContaInicial> ContasDo(string? ambiente) =>
        [.. Contas.Where(c => !c.SoEmHomologacao || EmHomologacao(ambiente))];

    /// <summary>
    /// A parte da lista de cada usuário qa que recebe módulos e abas (RN-268):
    /// a staff fica com a parte "staff", o membro do time com a parte "time".
    /// As contas master usam o que a conta contratou.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? ModsDaLista(ModulosIniciais lista, string usuarioId) => usuarioId switch
    {
        "usr-qains-qastaff" => lista.Staff,
        "usr-dboaz-qamembro" => lista.Time,
        _ => null,
    };

    /// <summary>
    /// As variáveis que o ambiente exige, na ordem (RNF-225, RNF-226): em
    /// homologação, também a lista de módulos iniciais (RN-268).
    /// </summary>
    public static IReadOnlyList<string> VariaveisDo(string? ambiente) =>
    [
        .. UsuariosDo(ambiente).SelectMany(u => u.VariavelSenha is { } senha ? new[] { u.VariavelEntrada, senha } : [u.VariavelEntrada]),
        .. EmHomologacao(ambiente) ? new[] { VariavelDosModulos } : [],
    ];

    /// <summary>
    /// Confere os segredos (RNF-226): cada variável exigida presente e cada senha
    /// dentro da RNF-202.
    /// </summary>
    /// <param name="ambiente">O valor de AMBIENTE.</param>
    /// <param name="ler">Lê o valor de uma variável; null ou vazio se ausente.</param>
    /// <exception cref="FalhaNaInicializacao">Com o nome das variáveis, nunca os valores.</exception>
    public static void Conferir(string? ambiente, Func<string, string?> ler)
    {
        var ausentes = VariaveisDo(ambiente).Where(v => string.IsNullOrWhiteSpace(ler(v))).ToList();
        if (ausentes.Count > 0) throw FalhaNaInicializacao.SegredoAusente(ausentes);
        var fracas = UsuariosDo(ambiente).Where(u => u.VariavelSenha is { } v && !Senha.Forte(ler(v))).Select(u => u.VariavelSenha!).ToList();
        if (fracas.Count > 0) throw FalhaNaInicializacao.SenhaFraca(fracas);
        if (EmHomologacao(ambiente)) _ = ModulosIniciais.Ler(ler(VariavelDosModulos));
    }

    /// <summary>
    /// A lista de módulos iniciais das contas qa (RN-268): só em homologação;
    /// em produção, nenhum módulo vem da partida.
    /// </summary>
    /// <exception cref="FalhaNaInicializacao">Lista ausente ou inválida, com o nome da variável.</exception>
    public static ModulosIniciais? ModulosDo(string? ambiente, Func<string, string?> ler) =>
        EmHomologacao(ambiente) ? ModulosIniciais.Ler(ler(VariavelDosModulos)) : null;

    /// <summary>A entrada como fica no banco: em minúsculas; o código do participante, em maiúsculas.</summary>
    public static string Login(UsuarioInicial usuario, string valor) =>
        usuario.Tipo == TipoUsuario.Participante ? Credenciais.CodigoDeAcesso(valor) : Credenciais.Entrada(valor);

    /// <summary>O domínio da conta master: a parte da entrada antes do @ (qa_instituicao@deboaz.com → qa_instituicao).</summary>
    public static string DominioDa(string entrada) => entrada.Split('@')[0];
}

/// <summary>
/// O ambiente em que a aplicação roda (AMBIENTE: hml ou prd). Só homologação
/// tem as contas qa; fora dela, as siglas iniciadas por "QA" são reservadas ao
/// time deboaz (RN-179, RN-266).
/// </summary>
/// <param name="Nome">O valor de AMBIENTE; qualquer valor que não seja hml vale como produção.</param>
public sealed record AmbienteDaAplicacao(string? Nome)
{
    /// <summary>Em homologação.</summary>
    public bool EmHomologacao => ContasIniciais.EmHomologacao(Nome);
}

/// <summary>
/// A inicialização não pode seguir (RNF-226, RNF-227): falta um segredo, uma
/// senha não segue a RNF-202 ou um índice único não pôde ser criado. A mensagem
/// traz só o nome das variáveis ou dos índices e coleções, nunca um valor
/// (RNF-225).
/// </summary>
public sealed class FalhaNaInicializacao : Exception
{
    FalhaNaInicializacao(string mensagem, IReadOnlyList<string> variaveis) : base(mensagem) => Variaveis = variaveis;

    /// <summary>As variáveis com problema.</summary>
    public IReadOnlyList<string> Variaveis { get; }

    /// <summary>Faltam variáveis.</summary>
    public static FalhaNaInicializacao SegredoAusente(IReadOnlyList<string> variaveis) =>
        new($"Segredo ausente no Key Vault: defina {string.Join(", ", variaveis)} (RNF-226).", variaveis);

    /// <summary>A lista de módulos iniciais fora do formato {"mods": {"MOD-NN": "C" | "U" | "CU"}} (RN-268).</summary>
    public static FalhaNaInicializacao ListaInvalida(string variavel) =>
        new($"Lista de módulos iniciais inválida em {variavel}: use {{\"staff\": {{\"mods\": {{\"MOD-NN\": \"C\" | \"U\" | \"CU\"}}}}, \"time\": {{\"mods\": {{…}}}}}} (RN-268, RNF-226).",
            [variavel]);

    /// <summary>
    /// Índices únicos que não puderam ser criados (RNF-227), como "sigla_unica em
    /// contas": a unicidade não está garantida no banco. Sem variável.
    /// </summary>
    public static FalhaNaInicializacao IndiceUnico(IReadOnlyList<string> indices) =>
        new($"Índice único não criado: {string.Join(", ", indices)}; corrija os documentos que ferem a unicidade e inicie de novo (RNF-227).",
            []);

    /// <summary>Senhas fora da RNF-202.</summary>
    public static FalhaNaInicializacao SenhaFraca(IReadOnlyList<string> variaveis) =>
        new($"Senha fora da RNF-202 (8 ou mais caracteres, com maiúscula, número e especial) em {string.Join(", ", variaveis)} (RNF-226).",
            variaveis);
}
