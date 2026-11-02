using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

// Peças internas que mais de um caso de uso usa. Não são casos de uso: não têm
// porta de entrada e não aparecem fora do core.

/// <summary>Lê o que a autorização precisa saber de uma pessoa: módulos da conta e permissões atuais.</summary>
sealed class Leitor(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes, TimeProvider relogio)
{
    /// <summary>O usuário; null se não existir.</summary>
    public Task<UsuarioAutorizado?> Usuario(string id, CancellationToken ct) => usuarios.Buscar(id, ct);

    /// <summary>As permissões guardadas, ou as vazias de quem não tem nada salvo.</summary>
    public async Task<PermissoesDoUsuario> Guardadas(string usuarioId, CancellationToken ct) =>
        await guardadas.Buscar(usuarioId, ct) ?? PermissoesDoUsuario.Vazias(usuarioId);

    /// <summary>Os módulos que a conta do usuário tem agora.</summary>
    public async Task<ModulosDaConta> ModulosDe(UsuarioAutorizado usuario, CancellationToken ct) =>
        new(await modulos.Registrados(ct), await contratacoes.EmVigor(usuario.ContaId, relogio.GetUtcNow(), ct), usuario.Papel.DoTime());

    /// <summary>A cadeia de quem criou o usuário, do criador para cima (no máximo 32 níveis).</summary>
    public async Task<IReadOnlyList<string>> Acima(UsuarioAutorizado usuario, CancellationToken ct)
    {
        var cadeia = new List<string>();
        for (var id = usuario.CriadoPor; id is not null && cadeia.Count < 32 && !cadeia.Contains(id); id = (await usuarios.Buscar(id, ct))?.CriadoPor)
        {
            cadeia.Add(id);
        }
        return cadeia;
    }
}

/// <summary>
/// Confere se quem pede pode abrir e salvar as permissões do alvo, com as
/// permissões atuais do servidor (RN-235, RN-241, RN-252).
/// </summary>
sealed class Concessao(Leitor leitor)
{
    /// <summary>Quem pede, o alvo e as permissões atuais de quem pede.</summary>
    /// <exception cref="FalhaDeAutorizacao">USUARIO_INEXISTENTE ou ACESSO_NEGADO.</exception>
    public async Task<(UsuarioAutorizado Quem, IReadOnlyDictionary<string, Abas> ModsDeQuem, UsuarioAutorizado Alvo)> Conferir(
        string quemId, string alvoId, CancellationToken ct)
    {
        var quem = await leitor.Usuario(quemId, ct) ?? throw FalhaDeAutorizacao.AcessoNegado();
        var alvo = await leitor.Usuario(alvoId, ct);
        if (alvo is null || (alvo.ContaId != quem.ContaId && !quem.Papel.DoTime())) throw FalhaDeAutorizacao.UsuarioInexistente();

        var modsDeQuem = CalculoDePermissoes.Calcular(quem, await leitor.Guardadas(quem.Id, ct), await leitor.ModulosDe(quem, ct)).Mods;
        var acima = quem.Papel == Papel.Staff ? await leitor.Acima(alvo, ct) : [];
        return CalculoDePermissoes.PodeConceder(quem, modsDeQuem, alvo, acima)
            ? (quem, modsDeQuem, alvo)
            : throw FalhaDeAutorizacao.AcessoNegado();
    }
}

/// <summary>Conversões para os textos do contrato.</summary>
static class Textos
{
    /// <summary>Módulo → "C", "U" ou "CU".</summary>
    public static IReadOnlyDictionary<string, string> Mods(IReadOnlyDictionary<string, Abas> mods) =>
        mods.OrderBy(m => m.Key, StringComparer.Ordinal).ToDictionary(m => m.Key, m => m.Value.ToString());

    /// <summary>Módulo → abas ou null.</summary>
    public static IReadOnlyDictionary<string, string?> Mods(IReadOnlyDictionary<string, Abas?> mods) =>
        mods.ToDictionary(m => m.Key, m => m.Value?.ToString());
}
