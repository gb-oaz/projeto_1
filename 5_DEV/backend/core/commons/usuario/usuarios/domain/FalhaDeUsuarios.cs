using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// Grupo de uma falha, para o adaptador de entrada escolher o status HTTP sem
/// conhecer cada código.
/// </summary>
public enum GrupoDeFalha
{
    /// <summary>401: o código de liberação não vale.</summary>
    NaoAutenticado,
    /// <summary>403: a pessoa não pode fazer isso.</summary>
    Proibido,
    /// <summary>404: o que foi pedido não existe para quem pediu.</summary>
    NaoEncontrado,
    /// <summary>409: o valor já está em uso ou o estado não permite.</summary>
    Conflito,
    /// <summary>410: o código de liberação venceu.</summary>
    Vencido,
    /// <summary>422: um campo do pedido está errado.</summary>
    DadosInvalidos,
    /// <summary>429: tentativas ou envios demais; vem com tentarEm.</summary>
    Bloqueado,
}

/// <summary>
/// Uma recusa de Usuários, com o código e a mensagem do contrato de API
/// (usuarios-contrato-api). O adaptador de entrada a transforma na resposta
/// { "codigo", "mensagem" } com os campos extras (campo, minimo, maximo, tentarEm).
/// </summary>
public sealed class FalhaDeUsuarios : Exception, IRecusaPrevista
{
    FalhaDeUsuarios(GrupoDeFalha grupo, string codigo, string mensagem) : base(mensagem) => (Grupo, Codigo) = (grupo, codigo);

    /// <summary>Grupo da falha, que decide o status HTTP.</summary>
    public GrupoDeFalha Grupo { get; }

    /// <summary>Código do contrato, como NOME_CURTO_EM_USO.</summary>
    public string Codigo { get; }

    /// <summary>O campo errado, em DADOS_INVALIDOS e FORA_DOS_LIMITES.</summary>
    public string? Campo { get; private init; }

    /// <summary>O menor valor aceito, em FORA_DOS_LIMITES (ISO 8601).</summary>
    public string? Minimo { get; private init; }

    /// <summary>O maior valor aceito, em FORA_DOS_LIMITES (ISO 8601).</summary>
    public string? Maximo { get; private init; }

    /// <summary>Quando tentar de novo, em ENTRADA_BLOQUEADA e LIMITE_ATENDIMENTO.</summary>
    public DateTimeOffset? TentarEm { get; private init; }

    /// <summary>Sem o tipo, o papel ou a ordem de superior (RN-164, RN-165, RN-169).</summary>
    public static FalhaDeUsuarios AcessoNegado() =>
        new(GrupoDeFalha.Proibido, "ACESSO_NEGADO", "Você não tem permissão para esta ação.");

    /// <summary>Usuário que não existe ou é de outra conta (RN-235): 404, e não 403.</summary>
    public static FalhaDeUsuarios UsuarioInexistente() =>
        new(GrupoDeFalha.NaoEncontrado, "USUARIO_INEXISTENTE", "Usuário não encontrado.");

    /// <summary>Conta que não existe para quem pede (RN-187).</summary>
    public static FalhaDeUsuarios ContaInexistente() =>
        new(GrupoDeFalha.NaoEncontrado, "CONTA_INEXISTENTE", "Conta não encontrada.");

    /// <summary>Sigla já usada por outra conta, em qualquer caixa (RN-179).</summary>
    public static FalhaDeUsuarios SiglaEmUso() =>
        new(GrupoDeFalha.Conflito, "SIGLA_EM_USO", "Essa sigla já é usada por outra conta.");

    /// <summary>Domínio já usado por outra conta (RF-104).</summary>
    public static FalhaDeUsuarios DominioEmUso() =>
        new(GrupoDeFalha.Conflito, "DOMINIO_EM_USO", "Esse domínio já é usado por outra conta.");

    /// <summary>Sigla fora do formato: 5 letras (RN-179).</summary>
    public static FalhaDeUsuarios SiglaComCincoLetras() =>
        new(GrupoDeFalha.DadosInvalidos, "SIGLA_INVALIDA", "A sigla precisa de 5 letras.") { Campo = "sigla" };

    /// <summary>A sigla DBOAZ, que é do time (RN-179).</summary>
    public static FalhaDeUsuarios SiglaReservada() =>
        new(GrupoDeFalha.DadosInvalidos, "SIGLA_INVALIDA", "A sigla DBOAZ é reservada ao time deboaz.") { Campo = "sigla" };

    /// <summary>Sigla iniciada por QA fora de homologação, que é das contas qa do time (RN-179).</summary>
    public static FalhaDeUsuarios SiglaQaReservada() =>
        new(GrupoDeFalha.DadosInvalidos, "SIGLA_INVALIDA", "Siglas QA são reservadas ao time deboaz.") { Campo = "sigla" };

    /// <summary>Nome curto já usado na mesma conta (RF-106, RF-153).</summary>
    public static FalhaDeUsuarios NomeCurtoEmUso() =>
        new(GrupoDeFalha.Conflito, "NOME_CURTO_EM_USO", "Esse nome curto já existe na escola.") { Campo = "nomeCurto" };

    /// <summary>Matrícula já usada na mesma conta (RN-188, RN-244).</summary>
    public static FalhaDeUsuarios MatriculaEmUso() =>
        new(GrupoDeFalha.Conflito, "MATRICULA_EM_USO", "Essa matrícula já existe na escola.") { Campo = "matricula" };

    /// <summary>Entrada ou código de liberação errados, com a mesma resposta (RF-109).</summary>
    public static FalhaDeUsuarios CodigoInvalido() =>
        new(GrupoDeFalha.NaoAutenticado, "CODIGO_INVALIDO", "Entrada ou código de liberação incorretos.");

    /// <summary>Código de liberação pedido há 72 h ou mais (RN-182).</summary>
    public static FalhaDeUsuarios CodigoVencido() =>
        new(GrupoDeFalha.Vencido, "CODIGO_VENCIDO", "O código de liberação venceu. Peça uma nova geração de senha.");

    /// <summary>Cinco códigos errados seguidos (RNF-204).</summary>
    public static FalhaDeUsuarios EntradaBloqueada(DateTimeOffset tentarEm) =>
        new(GrupoDeFalha.Bloqueado, "ENTRADA_BLOQUEADA", "Muitas tentativas erradas. Tente de novo mais tarde.") { TentarEm = tentarEm };

    /// <summary>Três pedidos de ajuda da conta master em 24 h do mesmo aparelho (RNF-205).</summary>
    public static FalhaDeUsuarios LimiteAtendimento(DateTimeOffset tentarEm) =>
        new(GrupoDeFalha.Bloqueado, "LIMITE_ATENDIMENTO", "Você já enviou 3 pedidos em 24 h. Tente de novo mais tarde.") { TentarEm = tentarEm };

    /// <summary>Nova geração de senha para participante, que entra pelo código (RF-110).</summary>
    public static FalhaDeUsuarios SemSenha() =>
        new(GrupoDeFalha.DadosInvalidos, "SEM_SENHA", "O participante entra pelo código de acesso, sem senha.");

    /// <summary>Código de acesso novo para quem não é participante (RF-154).</summary>
    public static FalhaDeUsuarios SoParticipante() =>
        new(GrupoDeFalha.DadosInvalidos, "SO_PARTICIPANTE", "Só o participante tem código de acesso.");

    /// <summary>Desativar quem já está desativado, ou reativar quem está ativo (RF-112).</summary>
    public static FalhaDeUsuarios EstadoIgual(bool desativado) =>
        new(GrupoDeFalha.Conflito, "ESTADO_IGUAL", desativado ? "O usuário já está desativado." : "O usuário já está ativo.");

    /// <summary>Conta encerrada não muda de estado (RN-173).</summary>
    public static FalhaDeUsuarios TransicaoInvalida() =>
        new(GrupoDeFalha.Conflito, "TRANSICAO_INVALIDA", "Conta encerrada não volta.");

    /// <summary>CNPJ errado ou menos de 2 de 3 conferidos (RN-172).</summary>
    public static FalhaDeUsuarios ConferenciaIncompleta() =>
        new(GrupoDeFalha.DadosInvalidos, "CONFERENCIA_INCOMPLETA", "Os dados conferidos não batem com o cadastro.");

    /// <summary>Tempo de sessão fora dos limites (RN-183, RN-243).</summary>
    /// <param name="campo">O campo do corpo.</param>
    /// <param name="minimo">O menor tempo aceito.</param>
    /// <param name="maximo">O maior tempo aceito.</param>
    /// <param name="emHoras">O campo é contado em horas (PT24H, "24 h"), não em dias.</param>
    public static FalhaDeUsuarios ForaDosLimites(string campo, TimeSpan minimo, TimeSpan maximo, bool emHoras = false) =>
        new(GrupoDeFalha.DadosInvalidos, "FORA_DOS_LIMITES",
            $"O tempo precisa ficar entre {Falar(minimo, emHoras)} e {Falar(maximo, emHoras)}.")
        {
            Campo = campo,
            Minimo = emHoras ? Textos.IsoEmHoras(minimo) : Textos.Iso(minimo),
            Maximo = emHoras ? Textos.IsoEmHoras(maximo) : Textos.Iso(maximo),
        };

    /// <summary>Um campo do pedido faltou ou veio com valor fora do contrato.</summary>
    public static FalhaDeUsuarios DadosInvalidos(string campo) =>
        new(GrupoDeFalha.DadosInvalidos, "DADOS_INVALIDOS", $"O campo \"{campo}\" está faltando ou é inválido.") { Campo = campo };

    // "15 min", "12 h", "30 dias"
    static string Falar(TimeSpan t, bool emHoras) =>
        !emHoras && t.TotalDays >= 1 && t.TotalDays % 1 == 0 ? $"{t.TotalDays:0} {(t.TotalDays == 1 ? "dia" : "dias")}"
        : t.TotalHours >= 1 && t.TotalHours % 1 == 0 ? $"{t.TotalHours:0} h"
        : $"{t.TotalMinutes:0} min";
}
