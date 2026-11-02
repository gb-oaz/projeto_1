using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// A conta de uma instituição ou de um educador autônomo (coleção contas). A
/// sigla não muda depois de criada (RN-179); nada é apagado, salvo no
/// encerramento, que guarda o registro mínimo (RN-174).
/// </summary>
public sealed class ContaAssinante
{
    /// <summary>Remonta uma conta gravada.</summary>
    public ContaAssinante(
        string id, string sigla, string dominio, string nome, TipoConta tipo, EstadoConta estado, string documento,
        string responsavel, string telefone, DateTimeOffset criadaEm, DateTimeOffset? ultimoAcessoMaster = null)
    {
        Id = id;
        Sigla = sigla;
        Dominio = dominio;
        Nome = nome;
        Tipo = tipo;
        Estado = estado;
        Documento = documento;
        Responsavel = responsavel;
        Telefone = telefone;
        CriadaEm = criadaEm;
        UltimoAcessoMaster = ultimoAcessoMaster;
    }

    /// <summary>Id da conta (conta-ebfga).</summary>
    public string Id { get; }

    /// <summary>Sigla de 5 letras, em maiúsculas.</summary>
    public string Sigla { get; }

    /// <summary>Domínio da entrada da conta master (objetivo_unidade1).</summary>
    public string Dominio { get; }

    /// <summary>Nome da instituição ou do educador.</summary>
    public string Nome { get; }

    /// <summary>Instituição ou autônomo.</summary>
    public TipoConta Tipo { get; }

    /// <summary>ATIVA, SUSPENSA, DESATIVADA ou ENCERRADA (RN-173).</summary>
    public EstadoConta Estado { get; private set; }

    /// <summary>CNPJ ou CPF, como foi informado.</summary>
    public string Documento { get; }

    /// <summary>Quem cadastrou a conta.</summary>
    public string Responsavel { get; }

    /// <summary>Telefone de contato: o código da conta master vai para ele (RN-172).</summary>
    public string Telefone { get; }

    /// <summary>Quando a conta foi criada.</summary>
    public DateTimeOffset CriadaEm { get; }

    /// <summary>Último acesso da conta master (RN-186).</summary>
    public DateTimeOffset? UltimoAcessoMaster { get; }

    /// <summary>A conta do educador autônomo, sem staff (RN-165).</summary>
    public bool DeAutonomo => Tipo == TipoConta.Autonomo;

    /// <summary>Id de uma conta nova pela sigla (conta-ebfga).</summary>
    public static string IdDa(string sigla) => $"conta-{sigla.ToLowerInvariant()}";

    /// <summary>
    /// Muda o estado (RN-173): qualquer um, menos sair de ENCERRADA. Devolve true
    /// se mudou.
    /// </summary>
    /// <exception cref="FalhaDeUsuarios">TRANSICAO_INVALIDA na conta encerrada.</exception>
    public bool MudarEstado(EstadoConta novo)
    {
        if (Estado == EstadoConta.Encerrada) throw FalhaDeUsuarios.TransicaoInvalida();
        if (novo == Estado) return false;
        Estado = novo;
        return true;
    }
}

/// <summary>
/// Os dados que o time confere antes de gerar o código da conta master que
/// esqueceu a senha (RN-172): o CNPJ, e 2 de 3 entre quem cadastrou, o telefone
/// e a data do último pagamento.
/// </summary>
/// <param name="Documento">CNPJ ou CPF informado.</param>
/// <param name="Responsavel">Quem cadastrou, se informado.</param>
/// <param name="Telefone">Telefone, se informado.</param>
/// <param name="UltimoPagamento">Data do último pagamento, se informada.</param>
public sealed record ConferenciaDaMaster(string Documento, string? Responsavel, string? Telefone, DateOnly? UltimoPagamento)
{
    /// <summary>Confere com o cadastro: o documento e pelo menos 2 dos outros 3.</summary>
    public bool Confere(ContaAssinante conta, DateOnly? ultimoPagamento)
    {
        if (Documento == "" || Domain.Documento.Numeros(Documento) != Domain.Documento.Numeros(conta.Documento)) return false;
        var batem = 0;
        if (Responsavel is { } nome && string.Equals(nome.Trim(), conta.Responsavel.Trim(), StringComparison.OrdinalIgnoreCase)) batem++;
        if (Telefone is { } fone && Domain.Documento.Numeros(fone) is { Length: > 0 } numeros && numeros == Domain.Documento.Numeros(conta.Telefone)) batem++;
        if (UltimoPagamento is { } data && data == ultimoPagamento) batem++;
        return batem >= 2;
    }
}
