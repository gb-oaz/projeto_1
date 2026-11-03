using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// Uma geração de senha (geracoesSenha, RN-181): o código de liberação pedido
/// por um superior vale 72 h e uma vez; com ele a pessoa vê a senha, e o
/// comprovante confirma que ela anotou (a confirmação é da autenticação, RF-143).
/// Só os resumos dos segredos ficam gravados.
/// </summary>
public sealed class GeracaoDeSenhaPedida
{
    /// <summary>Remonta uma geração gravada.</summary>
    public GeracaoDeSenhaPedida(
        string id, string usuarioId, string pedidaPor, string? resumoCodigo, DateTimeOffset pedidaEm,
        DateTimeOffset? senhaMostradaEm = null, string? resumoComprovante = null, DateTimeOffset? confirmadaEm = null)
    {
        Id = id;
        UsuarioId = usuarioId;
        PedidaPor = pedidaPor;
        ResumoCodigo = resumoCodigo;
        PedidaEm = pedidaEm;
        SenhaMostradaEm = senhaMostradaEm;
        ResumoComprovante = resumoComprovante;
        ConfirmadaEm = confirmadaEm;
    }

    /// <summary>Id da geração.</summary>
    public string Id { get; }

    /// <summary>Usuário cuja senha é gerada.</summary>
    public string UsuarioId { get; }

    /// <summary>Quem pediu: o superior, o time ou a própria conta master na criação.</summary>
    public string PedidaPor { get; }

    /// <summary>Resumo do código de liberação; null na criação da conta, que mostra a senha direto.</summary>
    public string? ResumoCodigo { get; }

    /// <summary>Quando foi pedida: o código vale 72 h a partir daqui (RN-182).</summary>
    public DateTimeOffset PedidaEm { get; }

    /// <summary>Quando a senha foi mostrada; depois disso o código não vale mais.</summary>
    public DateTimeOffset? SenhaMostradaEm { get; private set; }

    /// <summary>Resumo do comprovante entregue com a senha.</summary>
    public string? ResumoComprovante { get; private set; }

    /// <summary>Quando o comprovante foi usado (gravado pela autenticação).</summary>
    public DateTimeOffset? ConfirmadaEm { get; }

    /// <summary>Quando o código vence.</summary>
    public DateTimeOffset VenceEm => PedidaEm + CodigoDeLiberacao.Validade;

    /// <summary>Um pedido novo, com o código de liberação já resumido.</summary>
    public static (GeracaoDeSenhaPedida Geracao, string Codigo) Pedir(UsuarioCadastrado usuario, string pedidaPor, DateTimeOffset agora)
    {
        var codigo = CodigoDeLiberacao.Gerar();
        return (new(NovoId(usuario), usuario.Id, pedidaPor, Credenciais.Resumo(codigo), agora), codigo);
    }

    /// <summary>A geração da conta nova, com a senha mostrada na hora (RF-104).</summary>
    public static GeracaoDeSenhaPedida MostradaNaCriacao(UsuarioCadastrado master, string resumoComprovante, DateTimeOffset agora) =>
        new(NovoId(master), master.Id, master.Id, null, agora, agora, resumoComprovante);

    /// <summary>O código ainda pode mostrar a senha: não foi usado nem vale só na criação.</summary>
    public bool Aberta => ResumoCodigo is not null && SenhaMostradaEm is null;

    /// <summary>O código informado é o desta geração (compara os resumos).</summary>
    public bool Confere(string codigo) => ResumoCodigo is not null &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Credenciais.Resumo(codigo.Trim())), Convert.FromHexString(ResumoCodigo));

    /// <summary>Passou de 72 h (71 h ainda vale, 73 h não).</summary>
    public bool Vencida(DateTimeOffset agora) => agora >= VenceEm;

    /// <summary>Marca a senha como mostrada e guarda o resumo do comprovante.</summary>
    public void MostrarSenha(string resumoComprovante, DateTimeOffset agora)
    {
        SenhaMostradaEm = agora;
        ResumoComprovante = resumoComprovante;
    }

    static string NovoId(UsuarioCadastrado usuario) =>
        $"ger-{usuario.Sigla.ToLowerInvariant()}-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6))}";
}
