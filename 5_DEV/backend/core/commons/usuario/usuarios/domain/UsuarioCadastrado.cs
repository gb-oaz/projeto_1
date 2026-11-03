using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>Um responsável pelo participante, que entra no quest com o código dele (RF-108).</summary>
/// <param name="Nome">Nome do responsável (Carla Andrade).</param>
/// <param name="Parentesco">Como se apresenta: MAE, PAI…</param>
public sealed record Responsavel(string Nome, string Parentesco);

/// <summary>
/// Um usuário do cadastro (coleção usuarios, 17 exemplo-entidades). Nada é
/// apagado (RN-174): desativar e reativar mudam o estado. A senha nunca fica
/// aqui, só o resumo irreversível (RNF-201).
/// </summary>
public sealed class UsuarioCadastrado
{
    /// <summary>Remonta um usuário gravado.</summary>
    public UsuarioCadastrado(
        string id, string contaId, string sigla, TipoUsuario tipo, string nome, string nomeCurto, string login,
        string? matricula, EstadoUsuario estado, string? resumoSenha, IReadOnlyList<Responsavel> responsaveis,
        string? criadoPor, DateTimeOffset criadoEm, DateTimeOffset? codigoGeradoEm = null,
        DateTimeOffset? ultimoAcesso = null, string? ultimoAcessoQuem = null)
    {
        Id = id;
        ContaId = contaId;
        Sigla = sigla;
        Tipo = tipo;
        Nome = nome;
        NomeCurto = nomeCurto;
        Login = login;
        Matricula = matricula;
        Estado = estado;
        ResumoSenha = resumoSenha;
        Responsaveis = responsaveis;
        CriadoPor = criadoPor;
        CriadoEm = criadoEm;
        CodigoGeradoEm = codigoGeradoEm;
        UltimoAcesso = ultimoAcesso;
        UltimoAcessoQuem = ultimoAcessoQuem;
    }

    /// <summary>Id (usr-ebfga-maria).</summary>
    public string Id { get; }

    /// <summary>Conta a que pertence.</summary>
    public string ContaId { get; }

    /// <summary>Sigla da conta (EBFGA).</summary>
    public string Sigla { get; }

    /// <summary>Tipo do usuário (RN-163).</summary>
    public TipoUsuario Tipo { get; }

    /// <summary>Nome completo.</summary>
    public string Nome { get; private set; }

    /// <summary>Nome curto, que forma a entrada ou o código; único na conta.</summary>
    public string NomeCurto { get; private set; }

    /// <summary>Entrada do hub, ou o código de acesso do participante (RN-180, RN-184).</summary>
    public string Login { get; private set; }

    /// <summary>Matrícula, única na conta; null no autônomo (RN-188).</summary>
    public string? Matricula { get; private set; }

    /// <summary>PENDENTE até a primeira senha confirmada, ATIVO ou DESATIVADO.</summary>
    public EstadoUsuario Estado { get; private set; }

    /// <summary>Resumo da senha; null no participante e depois de uma nova geração.</summary>
    public string? ResumoSenha { get; private set; }

    /// <summary>Os responsáveis, só no participante.</summary>
    public IReadOnlyList<Responsavel> Responsaveis { get; private set; }

    /// <summary>Quem criou: define a ordem de superior (RN-169); null em quem se cadastrou.</summary>
    public string? CriadoPor { get; }

    /// <summary>Quando foi criado.</summary>
    public DateTimeOffset CriadoEm { get; }

    /// <summary>Quando o código de acesso atual foi gerado, só no participante.</summary>
    public DateTimeOffset? CodigoGeradoEm { get; private set; }

    /// <summary>Último acesso, gravado pela autenticação (RN-186).</summary>
    public DateTimeOffset? UltimoAcesso { get; }

    /// <summary>No participante, quem entrou por último: o próprio ou o parentesco do responsável.</summary>
    public string? UltimoAcessoQuem { get; }

    /// <summary>Tem senha: todos menos o participante (RF-110).</summary>
    public bool TemSenha => Tipo != TipoUsuario.Participante;

    /// <summary>Muda nome, nome curto (com a entrada ou o código), matrícula e responsáveis (RF-153, RN-244).</summary>
    /// <returns>True se o nome curto mudou: as sessões abertas terminam.</returns>
    public bool Editar(string? nome, string? nomeCurto, string? matricula, IReadOnlyList<Responsavel>? responsaveis, DateTimeOffset agora,
        Func<string> codigoNovo)
    {
        if (nome is not null) Nome = nome;
        if (matricula is not null) Matricula = matricula;
        if (responsaveis is not null) Responsaveis = responsaveis;
        if (nomeCurto is null || nomeCurto == NomeCurto) return false;
        NomeCurto = nomeCurto;
        if (Tipo == TipoUsuario.Participante) TrocarCodigo(codigoNovo(), agora);
        else Login = Logins.DoNomeCurto(nomeCurto, Sigla);
        return true;
    }

    /// <summary>Código de acesso novo: o anterior deixa de valer (RN-245).</summary>
    public void TrocarCodigo(string codigo, DateTimeOffset agora)
    {
        Login = codigo;
        CodigoGeradoEm = agora;
    }

    /// <summary>Nova geração de senha: a senha antiga deixa de valer (RF-110).</summary>
    public void EsquecerSenha() => ResumoSenha = null;

    /// <summary>A senha gerada foi mostrada: guarda só o resumo (RF-109).</summary>
    public void DefinirSenha(string resumo) => ResumoSenha = resumo;

    /// <summary>Tira o acesso sem apagar (RF-112).</summary>
    /// <exception cref="FalhaDeUsuarios">ESTADO_IGUAL se já está desativado.</exception>
    public void Desativar()
    {
        if (Estado == EstadoUsuario.Desativado) throw FalhaDeUsuarios.EstadoIgual(desativado: true);
        Estado = EstadoUsuario.Desativado;
    }

    /// <summary>
    /// Devolve o acesso com as mesmas permissões (RF-112): ATIVO, ou PENDENTE se
    /// ainda não confirmou a primeira senha.
    /// </summary>
    /// <exception cref="FalhaDeUsuarios">ESTADO_IGUAL se não está desativado.</exception>
    public void Reativar()
    {
        if (Estado != EstadoUsuario.Desativado) throw FalhaDeUsuarios.EstadoIgual(desativado: false);
        Estado = TemSenha && ResumoSenha is null && !JaEntrou ? EstadoUsuario.Pendente : EstadoUsuario.Ativo;
    }

    bool JaEntrou => UltimoAcesso is not null;
}
