using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

/// <summary>
/// Quem cria, vê e administra quem (RN-164, RN-165, RN-169). O módulo e a aba
/// (MOD-03 · U) a rota confere pelo token (17-b, RNF-208); aqui ficam o tipo de
/// quem pede, a conta e a ordem de superior.
/// </summary>
public static class Hierarquia
{
    /// <summary>Quem é do time deboaz: o administrador e os membros.</summary>
    public static bool DoTime(this TipoUsuario tipo) => tipo is TipoUsuario.Administrador or TipoUsuario.MembroTime;

    /// <summary>
    /// Pode criar o tipo (RN-164, RN-165): staff pela conta master da
    /// instituição ou pelo staff; participante também pela conta do autônomo;
    /// membro do time só pelo administrador.
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="contaDeAutonomo">A conta de quem pede é de educador autônomo.</param>
    /// <param name="tipo">O tipo a criar.</param>
    public static bool PodeCriar(UsuarioCadastrado quem, bool contaDeAutonomo, TipoUsuario tipo) => tipo switch
    {
        TipoUsuario.Staff => quem.Tipo is TipoUsuario.Master or TipoUsuario.Staff && !contaDeAutonomo,
        TipoUsuario.Participante => quem.Tipo is TipoUsuario.Master or TipoUsuario.Staff,
        TipoUsuario.MembroTime => quem.Tipo == TipoUsuario.Administrador,
        _ => false,
    };

    /// <summary>Pode listar o tipo: as mesmas regras de criar, e o membro do time vê os membros.</summary>
    public static bool PodeListar(UsuarioCadastrado quem, bool contaDeAutonomo, TipoUsuario tipo) =>
        tipo == TipoUsuario.MembroTime ? quem.Tipo.DoTime() : PodeCriar(quem, contaDeAutonomo, tipo);

    /// <summary>
    /// Pode administrar o alvo — editar, nova geração de senha, código novo,
    /// desativar e reativar (RN-169): o administrador sobre os membros; a conta
    /// master sobre o staff e os participantes; o staff sobre os participantes e
    /// sobre o staff que criou, direta ou indiretamente. Nunca sobre si, um par ou
    /// um superior.
    /// </summary>
    /// <param name="quem">Quem pede, da mesma conta do alvo.</param>
    /// <param name="alvo">Sobre quem.</param>
    /// <param name="acimaDoAlvo">Quem criou o alvo, quem criou esse, e assim por diante.</param>
    public static bool PodeAdministrar(UsuarioCadastrado quem, UsuarioCadastrado alvo, IReadOnlyList<string> acimaDoAlvo)
    {
        if (quem.Id == alvo.Id || quem.ContaId != alvo.ContaId) return false;
        return quem.Tipo switch
        {
            TipoUsuario.Administrador => alvo.Tipo == TipoUsuario.MembroTime,
            TipoUsuario.Master => alvo.Tipo is TipoUsuario.Staff or TipoUsuario.Participante,
            TipoUsuario.Staff => alvo.Tipo == TipoUsuario.Participante || (alvo.Tipo == TipoUsuario.Staff && acimaDoAlvo.Contains(quem.Id)),
            _ => false,
        };
    }
}

/// <summary>
/// Limite do pedido de ajuda da conta master sem login (RNF-205): 3 envios em
/// 24 h do mesmo aparelho; o quarto volta com tentarEm 24 h depois do primeiro.
/// </summary>
public static class LimiteDeAtendimento
{
    /// <summary>Envios aceitos na janela.</summary>
    public const int Envios = 3;

    /// <summary>A janela.</summary>
    public static readonly TimeSpan Janela = TimeSpan.FromHours(24);

    /// <summary>Null se pode enviar; senão, quando poderá.</summary>
    /// <param name="anteriores">Os envios do aparelho nas últimas 24 h.</param>
    /// <param name="agora">Agora.</param>
    public static DateTimeOffset? TentarEm(IEnumerable<DateTimeOffset> anteriores, DateTimeOffset agora)
    {
        var naJanela = anteriores.Where(e => e > agora - Janela).Order().ToList();
        return naJanela.Count >= Envios ? naJanela[^Envios] + Janela : null;
    }
}

/// <summary>
/// Um registro de auditoria (RN-178): quem, a ação, o alvo e a hora, guardado 5
/// anos e nunca alterado nem apagado (RNF-206).
/// </summary>
/// <param name="ContaId">Conta do alvo.</param>
/// <param name="Quem">Quem fez.</param>
/// <param name="Acao">O que fez.</param>
/// <param name="Alvo">Sobre quem ou o quê.</param>
/// <param name="Quando">Quando.</param>
public sealed record RegistroDeAuditoria(string ContaId, string Quem, AcaoAuditada Acao, string Alvo, DateTimeOffset Quando)
{
    /// <summary>Por quanto tempo fica guardado.</summary>
    public static readonly int Anos = 5;

    /// <summary>Até quando fica guardado.</summary>
    public DateTimeOffset GuardarAte => Quando.AddYears(Anos);
}
