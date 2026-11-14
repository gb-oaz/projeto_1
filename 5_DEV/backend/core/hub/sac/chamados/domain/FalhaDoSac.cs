using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Hub.Sac.Chamados.Domain;

/// <summary>Grupo de uma recusa do SAC, para o adaptador de entrada escolher o status HTTP.</summary>
public enum GrupoDaFalha
{
    /// <summary>403: o papel, o MOD-02 ou a ação do outro lado.</summary>
    Proibido,
    /// <summary>404: não existe ou é de outra conta (RN-288).</summary>
    NaoEncontrado,
    /// <summary>409: o estado do chamado não permite.</summary>
    Conflito,
    /// <summary>413: anexo com mais de 10 MB.</summary>
    MuitoGrande,
    /// <summary>415: anexo que não é PNG nem JPG.</summary>
    TipoNaoAceito,
    /// <summary>422: a forma do pedido.</summary>
    DadosInvalidos,
    /// <summary>503: a verificação de segurança fora do ar (falha fechada).</summary>
    Indisponivel,
}

/// <summary>
/// Uma recusa do SAC, com o código e a mensagem do contrato (sac-contrato-api
/// v02). O adaptador de entrada a transforma na resposta { codigo, mensagem }
/// com o campo, os limites ou o responsável, quando há.
/// </summary>
public sealed class FalhaDoSac : Exception, IRecusaPrevista
{
    FalhaDoSac(GrupoDaFalha grupo, string codigo, string mensagem) : base(mensagem) => (Grupo, Codigo) = (grupo, codigo);

    /// <summary>O grupo, que decide o status HTTP.</summary>
    public GrupoDaFalha Grupo { get; }

    /// <summary>O código do contrato, como CHAMADO_FECHADO.</summary>
    public string Codigo { get; }

    /// <summary>O campo errado, em DADOS_INVALIDOS e FORA_DOS_LIMITES.</summary>
    public string? Campo { get; private init; }

    /// <summary>O menor valor aceito, em FORA_DOS_LIMITES.</summary>
    public string? Minimo { get; private init; }

    /// <summary>O maior valor aceito, em FORA_DOS_LIMITES.</summary>
    public string? Maximo { get; private init; }

    /// <summary>Quem já tem o chamado, em JA_ASSUMIDO.</summary>
    public string? Responsavel { get; private init; }

    /// <summary>O papel, o MOD-02 ou a ação do outro lado.</summary>
    public static FalhaDoSac AcessoNegado() => new(GrupoDaFalha.Proibido, "ACESSO_NEGADO", "Você não tem permissão para esta ação.");

    /// <summary>O chamado não existe ou é de outra conta: quem pede não sabe se existe (RN-288).</summary>
    public static FalhaDoSac ChamadoInexistente() => new(GrupoDaFalha.NaoEncontrado, "CHAMADO_INEXISTENTE", "Chamado não encontrado.");

    /// <summary>O anexo não existe, é de outra conta, foi descartado ou é de nota interna.</summary>
    public static FalhaDoSac AnexoInexistente() => new(GrupoDaFalha.NaoEncontrado, "ANEXO_INEXISTENTE", "Anexo não encontrado.");

    /// <summary>O chamado já fechou (RN-290).</summary>
    public static FalhaDoSac ChamadoFechado() => new(GrupoDaFalha.Conflito, "CHAMADO_FECHADO", "Chamado fechado. Abra um relacionado.");

    /// <summary>Outro do time já tem o chamado, e o pedido não pediu para substituir (RF-175).</summary>
    public static FalhaDoSac JaAssumido(string responsavel) =>
        new(GrupoDaFalha.Conflito, "JA_ASSUMIDO", $"Este chamado já está com {responsavel}.") { Responsavel = responsavel };

    /// <summary>A avaliação é única (RN-296).</summary>
    public static FalhaDoSac JaAvaliado() => new(GrupoDaFalha.Conflito, "JA_AVALIADO", "Este chamado já foi avaliado.");

    /// <summary>A conta já tem 10 chamados em andamento (RN-294).</summary>
    public static FalhaDoSac LimiteChamados() => new(GrupoDaFalha.Conflito, "LIMITE_CHAMADOS",
        "Você já tem 10 chamados em andamento. Acompanhe-os em Meus chamados.");

    /// <summary>Resolvido há mais de 7 dias (RN-290).</summary>
    public static FalhaDoSac PrazoReaberturaVencido() =>
        new(GrupoDaFalha.Conflito, "PRAZO_REABERTURA_VENCIDO", "Prazo para reabrir encerrado.");

    /// <summary>Fechado pelo prazo há mais de 7 dias (RN-296).</summary>
    public static FalhaDoSac PrazoAvaliacaoVencido() =>
        new(GrupoDaFalha.Conflito, "PRAZO_AVALIACAO_VENCIDO", "Prazo para avaliar encerrado.");

    /// <summary>O pedido sem login não tem conversa com quem pediu (RF-179).</summary>
    public static FalhaDoSac SemConversa() =>
        new(GrupoDaFalha.Conflito, "SEM_CONVERSA", "Quem pediu não tem login: use nota interna e resolva o pedido.");

    /// <summary>Uma mudança fora da tabela da RN-290.</summary>
    public static FalhaDoSac TransicaoInvalida(string mensagem = "Essa mudança de status não é permitida.") =>
        new(GrupoDaFalha.Conflito, "TRANSICAO_INVALIDA", mensagem);

    /// <summary>Anexo com mais de 10 MB (RN-292).</summary>
    public static FalhaDoSac AnexoGrande() => new(GrupoDaFalha.MuitoGrande, "ANEXO_GRANDE", "Cada arquivo com até 10 MB.");

    /// <summary>Anexo que não é PNG nem JPG pelo conteúdo (RN-292).</summary>
    public static FalhaDoSac TipoNaoAceito() => new(GrupoDaFalha.TipoNaoAceito, "TIPO_NAO_ACEITO", "Envie PNG ou JPG.");

    /// <summary>A verificação de segurança achou ameaça (RN-292).</summary>
    public static FalhaDoSac AnexoRecusado() =>
        new(GrupoDaFalha.DadosInvalidos, "ANEXO_RECUSADO", "Arquivo recusado pela verificação de segurança.");

    /// <summary>Mais de 5 anexos numa mensagem (RN-292).</summary>
    public static FalhaDoSac LimiteAnexos() =>
        new(GrupoDaFalha.DadosInvalidos, "LIMITE_ANEXOS", "Até 5 arquivos por mensagem.") { Campo = "anexos" };

    /// <summary>A verificação de segurança fora do ar: nenhum anexo entra sem ela (falha fechada).</summary>
    public static FalhaDoSac VerificacaoIndisponivel() => new(GrupoDaFalha.Indisponivel, "VERIFICACAO_INDISPONIVEL",
        "A verificação de segurança dos arquivos está fora do ar. Tente de novo em instantes.");

    /// <summary>Um campo do pedido está faltando ou é inválido.</summary>
    public static FalhaDoSac DadosInvalidos(string campo, string? mensagem = null) =>
        new(GrupoDaFalha.DadosInvalidos, "DADOS_INVALIDOS", mensagem ?? $"O campo \"{campo}\" está faltando ou é inválido.") { Campo = campo };

    /// <summary>Um número fora dos limites do campo.</summary>
    public static FalhaDoSac ForaDosLimites(string campo, int minimo, int maximo) =>
        new(GrupoDaFalha.DadosInvalidos, "FORA_DOS_LIMITES", $"O campo \"{campo}\" deve ter de {minimo} a {maximo}.")
        {
            Campo = campo,
            Minimo = minimo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Maximo = maximo.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
}
