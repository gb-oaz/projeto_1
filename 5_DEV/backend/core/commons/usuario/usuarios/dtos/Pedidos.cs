using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Dtos;

// Pedidos dos endereços de usuarios-contrato-api. QuemPede é o sub do token de
// acesso, já conferido pela autenticação; a rota já conferiu MOD-03 e a aba.

/// <summary>Um responsável no corpo do pedido.</summary>
/// <param name="Nome">Nome (Carla Andrade).</param>
/// <param name="Parentesco">MAE, PAI…</param>
public sealed record ResponsavelDto(string? Nome, string? Parentesco);

/// <summary>Um pedido que só identifica quem pede: GET /usuarios/card, GET /time/tempo-sessao.</summary>
/// <param name="QuemPede">Id de quem pede.</param>
public sealed record QuemPedeDto(string QuemPede);

/// <summary>Um pedido sobre um usuário: GET /usuarios/{id} e as ações dele.</summary>
/// <param name="QuemPede">Id de quem pede.</param>
/// <param name="UsuarioId">O id da rota.</param>
public sealed record PedidoDeUsuarioDto(string QuemPede, string UsuarioId);

/// <summary>Um pedido sobre uma conta: GET /contas/{id}, GET /contas/{id}/tempo-sessao.</summary>
/// <param name="QuemPede">Id de quem pede.</param>
/// <param name="ContaId">O id da rota.</param>
public sealed record PedidoDeContaDto(string QuemPede, string ContaId);

/// <summary>GET /usuarios?tipo=&amp;busca=&amp;estado=&amp;pagina= (RN-185).</summary>
/// <param name="QuemPede">Id de quem pede.</param>
/// <param name="Tipo">STAFF, PARTICIPANTE ou MEMBRO_TIME; null busca em todos os que quem pede pode listar.</param>
/// <param name="Busca">Nome, entrada, matrícula ou código.</param>
/// <param name="Estado">PENDENTE, ATIVO ou DESATIVADO.</param>
/// <param name="Pagina">A partir de 1.</param>
public sealed record ListaDeUsuariosDto(string QuemPede, string? Tipo, string? Busca = null, string? Estado = null, int? Pagina = null)
{
    /// <summary>Confere e devolve o filtro sem a conta, que vem de quem pede.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public (TipoUsuario? Tipo, string? Busca, EstadoUsuario? Estado, int Pagina) Validar()
    {
        var tipo = Tipo is null ? (TipoUsuario?)null
            : Textos.TipoDe(Tipo) is TipoUsuario t and (TipoUsuario.Staff or TipoUsuario.Participante or TipoUsuario.MembroTime)
                ? t : throw FalhaDeUsuarios.DadosInvalidos("tipo");
        var estado = Estado is null ? (EstadoUsuario?)null : Textos.EstadoDe(Estado) ?? throw FalhaDeUsuarios.DadosInvalidos("estado");
        return (tipo, Validacao.Busca(Busca), estado, Validacao.Pagina(Pagina));
    }
}

/// <summary>POST /usuarios (RF-106, RF-108, RN-188).</summary>
/// <param name="QuemPede">Id de quem pede.</param>
/// <param name="Tipo">STAFF, PARTICIPANTE ou MEMBRO_TIME.</param>
/// <param name="Nome">Nome completo.</param>
/// <param name="NomeCurto">Forma a entrada ou o código.</param>
/// <param name="Matricula">A da escola; null gera 8 números (o autônomo não manda).</param>
/// <param name="Responsaveis">Só no participante.</param>
public sealed record NovoUsuarioDto(
    string QuemPede, string? Tipo, string? Nome, string? NomeCurto, string? Matricula = null,
    IReadOnlyList<ResponsavelDto?>? Responsaveis = null)
{
    /// <summary>Confere os campos.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o primeiro campo errado.</exception>
    public (TipoUsuario Tipo, string Nome, string NomeCurto, string? Matricula, IReadOnlyList<Responsavel> Responsaveis) Validar()
    {
        var tipo = Textos.TipoDe(Tipo) is TipoUsuario t and (TipoUsuario.Staff or TipoUsuario.Participante or TipoUsuario.MembroTime)
            ? t : throw FalhaDeUsuarios.DadosInvalidos("tipo");
        var nome = Validacao.Obrigatorio(Nome, "nome");
        var nomeCurto = Logins.NomeCurto(NomeCurto);
        var matricula = Validacao.Opcional(Matricula, "matricula");
        if (Responsaveis is not null && tipo != TipoUsuario.Participante) throw FalhaDeUsuarios.DadosInvalidos("responsaveis");
        return (tipo, nome, nomeCurto, matricula, Validacao.Responsaveis(Responsaveis) ?? []);
    }
}

/// <summary>PATCH /usuarios/{id} (RF-153, RN-244): o que vier null fica como está.</summary>
/// <param name="QuemPede">Id de quem pede.</param>
/// <param name="UsuarioId">O id da rota.</param>
/// <param name="Nome">Nome novo.</param>
/// <param name="NomeCurto">Nome curto novo.</param>
/// <param name="Matricula">Matrícula nova.</param>
/// <param name="Responsaveis">A lista inteira, só no participante: quem fica fora sai.</param>
public sealed record EdicaoDeUsuarioDto(
    string QuemPede, string UsuarioId, string? Nome = null, string? NomeCurto = null, string? Matricula = null,
    IReadOnlyList<ResponsavelDto?>? Responsaveis = null)
{
    /// <summary>Confere os campos que vieram.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o primeiro campo errado.</exception>
    public (string? Nome, string? NomeCurto, string? Matricula, IReadOnlyList<Responsavel>? Responsaveis) Validar() => (
        Validacao.Opcional(Nome, "nome"),
        NomeCurto is null ? null : Logins.NomeCurto(NomeCurto),
        Validacao.Opcional(Matricula, "matricula"),
        Validacao.Responsaveis(Responsaveis));
}

/// <summary>POST /usuarios/gerar-senha, sem login (RF-109).</summary>
/// <param name="Entrada">Entrada do hub; não diferencia maiúsculas.</param>
/// <param name="CodigoLiberacao">Os 6 números recebidos do superior.</param>
public sealed record GeracaoDeSenhaDto(string? Entrada, string? CodigoLiberacao)
{
    /// <summary>Confere os campos.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public (string Entrada, string Codigo) Validar() => (
        Credenciais.Entrada(Validacao.Obrigatorio(Entrada, "entrada")),
        Validacao.Obrigatorio(CodigoLiberacao, "codigoLiberacao"));
}

/// <summary>POST /contas, sem login (RF-104, RN-179, RN-242).</summary>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
/// <param name="Nome">Nome da instituição ou do educador.</param>
/// <param name="Documento">CNPJ na instituição, CPF no autônomo.</param>
/// <param name="Responsavel">Quem cadastra: a senha sai do nome dele.</param>
/// <param name="Telefone">Telefone de contato.</param>
/// <param name="Dominio">Forma a entrada da conta master.</param>
/// <param name="Sigla">5 letras.</param>
public sealed record NovaContaDto(
    string? Tipo, string? Nome, string? Documento, string? Responsavel, string? Telefone, string? Dominio, string? Sigla)
{
    /// <summary>Confere os campos, na ordem do formulário.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS ou SIGLA_INVALIDA.</exception>
    public (TipoConta Tipo, string Nome, string Documento, string Responsavel, string Telefone, string Dominio, string Sigla) Validar()
    {
        var tipo = Textos.TipoDaContaDe(Tipo) ?? throw FalhaDeUsuarios.DadosInvalidos("tipo");
        var nome = Validacao.Obrigatorio(Nome, "nome");
        var documento = Validacao.Obrigatorio(Documento, "documento");
        if (!Domain.Documento.ServePara(tipo, documento)) throw FalhaDeUsuarios.DadosInvalidos("documento");
        return (tipo, nome, documento, Validacao.Obrigatorio(Responsavel, "responsavel"), Validacao.Obrigatorio(Telefone, "telefone"),
            Logins.NomeCurto(Dominio, "dominio"), Domain.Sigla.Validar(Sigla));
    }
}

/// <summary>POST /contas/esqueci-senha-master, sem login (RF-111, RNF-205).</summary>
/// <param name="Instituicao">Nome da instituição.</param>
/// <param name="Contato">Telefone ou e-mail para o time responder.</param>
/// <param name="Aparelho">Identifica o aparelho, para o limite; vem do adaptador, não do corpo.</param>
public sealed record AjudaDaMasterDto(string? Instituicao, string? Contato, string Aparelho)
{
    /// <summary>Confere os campos.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public (string Instituicao, string Contato) Validar() =>
        (Validacao.Obrigatorio(Instituicao, "instituicao"), Validacao.Obrigatorio(Contato, "contato"));
}

/// <summary>GET /contas?busca=&amp;estado=&amp;pagina= (RF-113, RN-185).</summary>
/// <param name="QuemPede">Id de quem pede, do time.</param>
/// <param name="Busca">Nome, sigla ou documento.</param>
/// <param name="Estado">ATIVA, SUSPENSA, DESATIVADA ou ENCERRADA.</param>
/// <param name="Pagina">A partir de 1.</param>
public sealed record ListaDeContasDto(string QuemPede, string? Busca = null, string? Estado = null, int? Pagina = null)
{
    /// <summary>Confere e devolve o filtro.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public FiltroDeContas Validar() => new(
        Validacao.Busca(Busca),
        Estado is null ? null : Textos.EstadoDaContaDe(Estado) ?? throw FalhaDeUsuarios.DadosInvalidos("estado"),
        Validacao.Pagina(Pagina));
}

/// <summary>POST /contas/{id}/estado (RF-113, RN-173).</summary>
/// <param name="QuemPede">Id de quem pede, do time.</param>
/// <param name="ContaId">O id da rota.</param>
/// <param name="Estado">ATIVA, SUSPENSA, DESATIVADA ou ENCERRADA.</param>
public sealed record EstadoDaContaDto(string QuemPede, string ContaId, string? Estado)
{
    /// <summary>O estado pedido.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS em estado.</exception>
    public EstadoConta Validar() => Textos.EstadoDaContaDe(Estado) ?? throw FalhaDeUsuarios.DadosInvalidos("estado");
}

/// <summary>POST /contas/{id}/codigo-master (RN-172): o documento e 2 de 3.</summary>
/// <param name="QuemPede">Id de quem pede, do time.</param>
/// <param name="ContaId">O id da rota.</param>
/// <param name="Documento">CNPJ ou CPF.</param>
/// <param name="Responsavel">Quem cadastrou, se conferido.</param>
/// <param name="Telefone">Telefone, se conferido.</param>
/// <param name="UltimoPagamento">AAAA-MM-DD, se conferido.</param>
public sealed record ConferenciaDaMasterDto(
    string QuemPede, string ContaId, string? Documento, string? Responsavel = null, string? Telefone = null, string? UltimoPagamento = null)
{
    /// <summary>Confere os campos e devolve o que o time informou.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS com o campo.</exception>
    public ConferenciaDaMaster Validar() => new(
        Validacao.Obrigatorio(Documento, "documento"),
        Validacao.Busca(Responsavel),
        Validacao.Busca(Telefone),
        UltimoPagamento is null ? null
            : DateOnly.TryParseExact(UltimoPagamento, "yyyy-MM-dd", out var data) ? data : throw FalhaDeUsuarios.DadosInvalidos("ultimoPagamento"));
}

/// <summary>PUT /contas/{id}/tempo-sessao (RF-114, RN-183): durações ISO 8601.</summary>
/// <param name="QuemPede">Id de quem pede.</param>
/// <param name="ContaId">O id da rota.</param>
/// <param name="StaffNavegador">Como PT8H.</param>
/// <param name="StaffApp">Como P7D.</param>
/// <param name="Participante">Como P30D.</param>
public sealed record TempoDaContaDto(string QuemPede, string ContaId, string? StaffNavegador, string? StaffApp, string? Participante)
{
    /// <summary>Confere o formato e os limites.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS ou FORA_DOS_LIMITES com o campo.</exception>
    public TempoDaConta Validar() => new TempoDaConta(
        Validacao.Duracao(StaffNavegador, "staffNavegador"),
        Validacao.Duracao(StaffApp, "staffApp"),
        Validacao.Duracao(Participante, "participante")).Conferido();
}

/// <summary>PUT /time/tempo-sessao (RF-152, RN-243): durações ISO 8601.</summary>
/// <param name="QuemPede">Id de quem pede, do time.</param>
/// <param name="MembrosNavegador">Como PT8H.</param>
/// <param name="MembrosApp">Como PT8H.</param>
/// <param name="MastersNavegador">Como PT4H.</param>
/// <param name="MastersApp">Como PT8H.</param>
public sealed record TempoDoTimeDto(
    string QuemPede, string? MembrosNavegador, string? MembrosApp, string? MastersNavegador, string? MastersApp)
{
    /// <summary>Confere o formato e os limites.</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS ou FORA_DOS_LIMITES com o campo.</exception>
    public TempoDoTime Validar() => new TempoDoTime(
        Validacao.Duracao(MembrosNavegador, "membrosNavegador"),
        Validacao.Duracao(MembrosApp, "membrosApp"),
        Validacao.Duracao(MastersNavegador, "mastersNavegador"),
        Validacao.Duracao(MastersApp, "mastersApp")).Conferido();
}

/// <summary>GET /contas/sigla-sugerida?nome=, sem login (RN-179).</summary>
/// <param name="Nome">Nome da instituição ou do educador.</param>
public sealed record NomeDaContaDto(string? Nome);
