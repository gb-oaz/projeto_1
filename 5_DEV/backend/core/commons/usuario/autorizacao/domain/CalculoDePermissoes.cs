namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>Papel, módulos e pv que vão no token (RF-144, RNF-219). Não é guardado.</summary>
/// <param name="Papel">Papel da pessoa.</param>
/// <param name="Mods">Módulo → abas.</param>
/// <param name="Pv">Versão das permissões.</param>
public sealed record AutorizacaoNoToken(Papel Papel, IReadOnlyDictionary<string, Abas> Mods, int Pv);

/// <summary>
/// As regras de quem tem o quê (RN-233) e de quem concede o quê a quem (RN-252).
/// Sempre com as permissões atuais do servidor, não com as do token (RN-241).
/// </summary>
public static class CalculoDePermissoes
{
    /// <summary>"Administrar usuários" é este módulo com as duas abas (RN-251).</summary>
    public const string Usuarios = "MOD-03";

    /// <summary>
    /// As permissões de alguém (RF-145, RF-150, RN-233, RN-238):
    /// <list type="bullet">
    /// <item>administrador: os módulos do time no hub, C e U;</item>
    /// <item>membro do time e staff: os marcados que a conta tem;</item>
    /// <item>conta master e autônomo: os da conta e os contratados usados no hub, C e U;</item>
    /// <item>participante (e o responsável, com o código dele): os da conta e os contratados que o participante ou o responsável usam no quest, só U.</item>
    /// </list>
    /// </summary>
    public static AutorizacaoNoToken Calcular(UsuarioAutorizado usuario, PermissoesDoUsuario guardadas, ModulosDaConta modulos)
    {
        IReadOnlyDictionary<string, Abas> mods = usuario.Papel switch
        {
            Papel.Administrador => Todas(modulos.Onde(DoTimeNoHub), Abas.CU),
            Papel.Master or Papel.Autonomo => Todas(modulos.Onde(m => m.TemUsoNo(FrenteDeUso.Hub)), Abas.CU),
            Papel.Participante => Todas(modulos.Onde(m => m.Usa(Perfil.Participante, FrenteDeUso.Quest)
                || m.Usa(Perfil.Responsavel, FrenteDeUso.Quest)), Abas.U),
            _ => guardadas.Visiveis(modulos.Disponiveis),
        };
        return new AutorizacaoNoToken(usuario.Papel, mods, guardadas.Pv);
    }

    /// <summary>O acesso anônimo: só o módulo da sala, com Utilização (RF-151).</summary>
    public static AutorizacaoNoToken DoAnonimo(string moduloDaSala) =>
        new(Papel.Anonimo, new Dictionary<string, Abas> { [moduloDaSala] = Abas.U }, 0);

    /// <summary>
    /// Quem pode abrir e salvar as permissões de quem (RN-252, RN-169): o
    /// administrador as dos membros do time; a conta master as do staff da conta;
    /// o staff com Administrar usuários as do staff que está abaixo dele, criado
    /// por ele ou por alguém abaixo dele. Ninguém as de si mesmo, de um par, de
    /// um superior ou de um participante.
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="modsDeQuem">As permissões atuais de quem pede (RN-241).</param>
    /// <param name="alvo">De quem são as permissões.</param>
    /// <param name="acimaDoAlvo">A cadeia de quem criou o alvo, do criador para cima.</param>
    public static bool PodeConceder(UsuarioAutorizado quem, IReadOnlyDictionary<string, Abas> modsDeQuem,
        UsuarioAutorizado alvo, IEnumerable<string> acimaDoAlvo)
    {
        if (quem.Id == alvo.Id) return false;
        return (quem.Papel, alvo.Papel) switch
        {
            (Papel.Administrador, Papel.MembroTime) => true,
            (Papel.Master or Papel.Autonomo, Papel.Staff) => quem.ContaId == alvo.ContaId,
            (Papel.Staff, Papel.Staff) => quem.ContaId == alvo.ContaId
                && modsDeQuem.GetValueOrDefault(Usuarios).Utilizacao
                && acimaDoAlvo.Contains(quem.Id),
            _ => false,
        };
    }

    /// <summary>
    /// O que quem concede pode dar ao alvo (RF-160, RN-167): o administrador e a
    /// conta master, todos os módulos que o alvo usa no hub, com as duas abas; o
    /// staff, só os que ele mesmo tem, com as abas dele, inclusive Administrar
    /// usuários (RN-252).
    /// </summary>
    public static IReadOnlyDictionary<string, Abas> Concediveis(UsuarioAutorizado quem, IReadOnlyDictionary<string, Abas> modsDeQuem,
        UsuarioAutorizado alvo, ModulosDaConta modulos)
    {
        var doAlvo = modulos.Onde(m => alvo.Papel == Papel.MembroTime ? DoTimeNoHub(m) : m.Usa(Perfil.Staff, FrenteDeUso.Hub));
        return quem.Papel == Papel.Staff
            ? doAlvo.Where(m => modsDeQuem.ContainsKey(m.Id)).ToDictionary(m => m.Id, m => modsDeQuem[m.Id])
            : Todas(doAlvo, Abas.CU);
    }

    /// <summary>O alvo pode receber o módulo: ele o usa no hub (staff) ou é do time (membro).</summary>
    public static bool AlvoUsa(UsuarioAutorizado alvo, ModuloRegistrado modulo) =>
        alvo.Papel == Papel.MembroTime ? DoTimeNoHub(modulo) : modulo.Usa(Perfil.Staff, FrenteDeUso.Hub);

    static bool DoTimeNoHub(ModuloRegistrado m) =>
        m.Usa(Perfil.Administrador, FrenteDeUso.Hub) || m.Usa(Perfil.MembroTime, FrenteDeUso.Hub);

    static Dictionary<string, Abas> Todas(IEnumerable<ModuloRegistrado> modulos, Abas abas) => modulos.ToDictionary(m => m.Id, _ => abas);
}
