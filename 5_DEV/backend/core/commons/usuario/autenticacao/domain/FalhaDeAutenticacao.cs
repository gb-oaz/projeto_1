namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>
/// Grupo de uma falha, para o adaptador de entrada escolher o status HTTP sem
/// conhecer cada código: não autenticado (401), proibido (403), não encontrado
/// (404), dados inválidos (422) e bloqueado (429).
/// </summary>
public enum GrupoDeFalha
{
    /// <summary>401: credencial, token ou sessão que não vale.</summary>
    NaoAutenticado,
    /// <summary>403: a pessoa existe, mas não pode entrar agora.</summary>
    Proibido,
    /// <summary>404: o que foi pedido não existe para quem pediu.</summary>
    NaoEncontrado,
    /// <summary>422: um campo do pedido está errado.</summary>
    DadosInvalidos,
    /// <summary>429: tentativas demais; vem com tentarEm.</summary>
    Bloqueado,
}

/// <summary>
/// Uma recusa da autenticação, com o código e a mensagem do contrato de API
/// (autenticacao-contrato-api). Os casos de uso a lançam; o adaptador de entrada
/// a transforma na resposta de erro { "codigo", "mensagem" }.
/// </summary>
public sealed class FalhaDeAutenticacao : Exception
{
    FalhaDeAutenticacao(GrupoDeFalha grupo, string codigo, string mensagem, DateTimeOffset? tentarEm = null, string? campo = null)
        : base(mensagem)
    {
        Grupo = grupo;
        Codigo = codigo;
        TentarEm = tentarEm;
        Campo = campo;
    }

    /// <summary>Grupo da falha, que decide o status HTTP.</summary>
    public GrupoDeFalha Grupo { get; }

    /// <summary>Código do contrato, como ENTRADA_INVALIDA.</summary>
    public string Codigo { get; }

    /// <summary>Quando a entrada volta a ser aceita, só em ENTRADA_BLOQUEADA.</summary>
    public DateTimeOffset? TentarEm { get; }

    /// <summary>O campo errado, só em DADOS_INVALIDOS.</summary>
    public string? Campo { get; }

    /// <summary>Senha errada ou entrada inexistente, com a mesma resposta (RF-139).</summary>
    public static FalhaDeAutenticacao EntradaInvalida() =>
        new(GrupoDeFalha.NaoAutenticado, "ENTRADA_INVALIDA", "Entrada ou senha incorretas.");

    /// <summary>Código de acesso errado ou inexistente (RF-135).</summary>
    public static FalhaDeAutenticacao CodigoInvalido() =>
        new(GrupoDeFalha.NaoAutenticado, "CODIGO_INVALIDO", "Código de acesso inválido.");

    /// <summary>Usuário desativado ou conta fora de ATIVA (RF-140, RN-175).</summary>
    public static FalhaDeAutenticacao AcessoIndisponivel() =>
        new(GrupoDeFalha.Proibido, "ACESSO_INDISPONIVEL", "Acesso indisponível. Fale com quem administra a sua conta.");

    /// <summary>Cinco erros seguidos: a entrada fica bloqueada até tentarEm (RF-158, RNF-204).</summary>
    public static FalhaDeAutenticacao EntradaBloqueada(DateTimeOffset tentarEm) =>
        new(GrupoDeFalha.Bloqueado, "ENTRADA_BLOQUEADA", "Muitas tentativas erradas. Tente de novo mais tarde.", tentarEm);

    /// <summary>Comprovante vencido ou já usado (RF-143).</summary>
    public static FalhaDeAutenticacao ComprovanteInvalido() =>
        new(GrupoDeFalha.NaoAutenticado, "COMPROVANTE_INVALIDO", "Comprovante inválido. Gere a senha de novo.");

    /// <summary>Sala inexistente ou já fechada (RF-136).</summary>
    public static FalhaDeAutenticacao SalaFechada() =>
        new(GrupoDeFalha.NaoEncontrado, "SALA_FECHADA", "Essa sala não está aberta.");

    /// <summary>A sessão terminou: tempo sem uso, saída ou renovação copiada (RF-137, RNF-213).</summary>
    public static FalhaDeAutenticacao SessaoTerminada() =>
        new(GrupoDeFalha.NaoAutenticado, "SESSAO_TERMINADA", "Sua sessão terminou. Entre de novo.");

    /// <summary>Sessão que não existe ou é de outra pessoa (RF-157).</summary>
    public static FalhaDeAutenticacao SessaoNaoEncontrada() =>
        new(GrupoDeFalha.NaoEncontrado, "SESSAO_NAO_ENCONTRADA", "Sessão não encontrada.");

    /// <summary>Token malformado, com a assinatura errada, vencido ou de outra frente (RNF-216).</summary>
    public static FalhaDeAutenticacao TokenInvalido() =>
        new(GrupoDeFalha.NaoAutenticado, "TOKEN_INVALIDO", "Token inválido.");

    /// <summary>O token traz uma versão de permissões antiga: renove (RN-230).</summary>
    public static FalhaDeAutenticacao PermissoesDesatualizadas() =>
        new(GrupoDeFalha.NaoAutenticado, "PERMISSOES_DESATUALIZADAS", "Suas permissões mudaram. Renove a sessão.");

    /// <summary>Um campo do pedido faltou ou veio com valor fora do contrato.</summary>
    public static FalhaDeAutenticacao DadosInvalidos(string campo) =>
        new(GrupoDeFalha.DadosInvalidos, "DADOS_INVALIDOS", $"O campo \"{campo}\" está faltando ou é inválido.", campo: campo);
}
